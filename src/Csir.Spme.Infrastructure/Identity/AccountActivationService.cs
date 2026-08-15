using System.Security.Cryptography;
using System.Text;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Iam;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Csir.Spme.Infrastructure.Identity;

public sealed class AccountActivationService : IAccountActivationService
{
    private const string EmployeeRole = "Employee";
    private readonly SpmeDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ICommunicationOutbox _outbox;
    private readonly IAuditService _audit;
    private readonly IConfiguration _configuration;

    public AccountActivationService(
        SpmeDbContext db,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ICommunicationOutbox outbox,
        IAuditService audit,
        IConfiguration configuration)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _outbox = outbox;
        _audit = audit;
        _configuration = configuration;
    }

    public async Task<AccountActivationResult<AccountActivationChallengeData>> CreateChallengeAsync(
        string identifier,
        string? contact,
        CancellationToken cancellationToken)
    {
        var normalized = LoginIdentifierNormalizer.Normalize(identifier);
        if (normalized is null)
            return NotFound();

        var employeeIds = await FindEmployeeIdsAsync(normalized.Value, cancellationToken);
        if (employeeIds.Count != 1)
            return NotFound();

        var employee = await _db.Employees.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == employeeIds[0], cancellationToken);
        var linkedUserIds = await _db.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employee.Id)
            .Select(user => user.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (linkedUserIds.Count != 1)
            return NotFound();

        var user = await _userManager.FindByIdAsync(linkedUserIds[0].ToString());
        if (user is null)
            return NotFound();
        if (!employee.IsHrApproved || !string.Equals(employee.ProfileStatus, "active", StringComparison.OrdinalIgnoreCase))
            return Conflict("This staff record cannot be activated yet. Contact your institute HR office for assistance.");
        if (user.AccountStatus != "password-reset-required" ||
            await _userManager.HasPasswordAsync(user) && !string.IsNullOrWhiteSpace(user.PasswordHash))
            return Conflict("This account is already active. Sign in, or use Forgot password if you need to reset it.");

        var destinationResult = ResolveDestination(normalized.Value, contact, employee.PrimaryEmail, employee.Phone);
        if (!destinationResult.Succeeded)
            return AccountActivationResult<AccountActivationChallengeData>.Failure(
                422, "validation_failed", destinationResult.Detail!, ContactErrors(destinationResult.Detail!));

        var destination = destinationResult.Destination!;
        var hashKey = GetHashKey();
        var identifierHash = ComputeKeyedHash(hashKey, normalized.Value.Value);
        var destinationHash = ComputeKeyedHash(hashKey, destination.Value);
        var now = DateTimeOffset.UtcNow;
        var resendLimit = Math.Clamp(_configuration.GetValue("AccountActivation:ResendLimit", 3), 1, 10);
        var resendCutoff = now.AddMinutes(-Math.Clamp(
            _configuration.GetValue("AccountActivation:ResendWindowMinutes", 60), 10, 1440));

        if (await CountRecentChallengesAsync(identifierHash, destinationHash, resendCutoff, cancellationToken) >= resendLimit)
        {
            return AccountActivationResult<AccountActivationChallengeData>.Failure(
                429,
                "verification_rate_limited",
                "Too many activation codes have been requested for this staff identifier or destination. Try again later.");
        }

        var expiryMinutes = Math.Clamp(_configuration.GetValue("AccountActivation:OtpExpiryMinutes", 10), 5, 30);
        var maximumAttempts = Math.Clamp(_configuration.GetValue("AccountActivation:MaximumAttempts", 5), 3, 10);
        var otpLength = Math.Clamp(_configuration.GetValue("AccountActivation:OtpLength", 6), 6, 8);
        var otp = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, otpLength)).ToString($"D{otpLength}");
        var challenge = new AccountActivationChallenge(
            user.Id,
            identifierHash,
            destination.Channel,
            destinationHash,
            ComputeKeyedHash(hashKey, otp),
            now.AddMinutes(expiryMinutes),
            maximumAttempts);
        _db.AccountActivationChallenges.Add(challenge);

        if (destination.Channel == "email")
        {
            await _outbox.EnqueueEmailAsync(
                destination.Original,
                "Activate your CSIR SPME account",
                $"Your CSIR verification code is {otp}. It expires in {expiryMinutes} minutes.",
                false,
                "authentication",
                $"account-activation:{challenge.Id}:email",
                cancellationToken);
        }
        else
        {
            var template = _configuration.GetValue<string>("MNotify:OtpMessageTemplate")
                ?? "Your CSIR verification code is %otp_code%. It expires in %expiry% minutes.";
            await _outbox.EnqueueSmsAsync(
                destination.Original,
                template.Replace("%otp_code%", otp, StringComparison.Ordinal)
                    .Replace("%expiry%", expiryMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                "authentication",
                $"account-activation:{challenge.Id}:sms",
                cancellationToken);
        }

        await _audit.RecordAndSaveAsync(
            "auth.account-activation-challenge-created",
            "AccountActivationChallenge",
            challenge.Id.ToString(),
            null,
            $"queued-{destination.Channel}",
            cancellationToken);

        return AccountActivationResult<AccountActivationChallengeData>.Success(
            202,
            new(challenge.Id, challenge.ExpiresAt, destination.Channel, Mask(destination)));
    }

    public async Task<AccountActivationResult<AccountActivationVerificationData>> VerifyChallengeAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken)
    {
        var challenge = await _db.AccountActivationChallenges
            .SingleOrDefaultAsync(candidate => candidate.Id == challengeId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.VerifiedAt.HasValue || now >= challenge.ExpiresAt)
            return VerificationFailure("verification_expired", "The activation challenge is missing, expired, or has already been used.");
        if (challenge.AttemptCount >= challenge.MaximumAttempts)
            return VerificationFailure("verification_failed", "The activation challenge has exhausted its allowed verification attempts.");

        var suppliedHash = ComputeKeyedHash(GetHashKey(), code.Trim());
        if (!FixedTimeEquals(challenge.OtpHash, suppliedHash))
        {
            challenge.RecordFailedAttempt();
            await _db.SaveChangesAsync(cancellationToken);
            var detail = challenge.AttemptCount >= challenge.MaximumAttempts
                ? "The verification code is incorrect and the activation challenge has exhausted its allowed attempts."
                : "The verification code is incorrect.";
            return VerificationFailure("verification_failed", detail);
        }

        var verificationToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        challenge.Verify(HashToken(verificationToken), now);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAndSaveAsync(
            "auth.account-activation-challenge-verified",
            "AccountActivationChallenge",
            challenge.Id.ToString(),
            null,
            "verified",
            cancellationToken);
        return AccountActivationResult<AccountActivationVerificationData>.Success(
            200,
            new(verificationToken, challenge.ExpiresAt));
    }

    public async Task<AccountActivationResult> CompleteAsync(
        Guid challengeId,
        string verificationToken,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            return Validation("Password and confirmation password must match.", "confirmPassword");

        var challenge = await _db.AccountActivationChallenges
            .SingleOrDefaultAsync(candidate => candidate.Id == challengeId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (challenge is null || !challenge.CanComplete(HashToken(verificationToken), now))
            return Expired("The verified activation challenge or token is invalid, expired, or already consumed.");

        var user = await _userManager.FindByIdAsync(challenge.UserId!.Value.ToString());
        if (user is null || !user.EmployeeId.HasValue)
            return Validation("The staff account linked to this activation challenge is no longer available.");
        if (user.AccountStatus != "password-reset-required")
            return Validation("This staff account can no longer be activated. Sign in, or contact your institute HR office.");

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, password);
        if (!passwordResult.Succeeded)
        {
            var detail = string.Join(" ", passwordResult.Errors.Select(error => error.Description).Distinct());
            return Validation(
                string.IsNullOrWhiteSpace(detail) ? "The password does not satisfy the account security policy." : detail,
                "password");
        }

        user.CompletePasswordReset();
        if (challenge.DeliveryChannel == "email")
            user.EmailConfirmed = true;
        if (challenge.DeliveryChannel == "sms")
            user.PhoneNumberConfirmed = true;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return Validation("The staff account status could not be updated after setting the password.");

        if (!await _roleManager.RoleExistsAsync(EmployeeRole))
        {
            var roleResult = await _roleManager.CreateAsync(
                new Role(EmployeeRole, EmployeeRole, "Employee self-service role.", true));
            if (!roleResult.Succeeded)
                return Validation("The employee self-service role could not be prepared.");
        }
        if (!await _userManager.IsInRoleAsync(user, EmployeeRole))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, EmployeeRole);
            if (!roleResult.Succeeded)
                return Validation("The employee self-service role could not be assigned.");
        }

        await ProvisionSafeLoginIdentifiersAsync(user, challenge.DeliveryChannel, now, cancellationToken);
        challenge.Consume(now);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAndSaveAsync(
            "auth.account-activated", "User", user.Id.ToString(), null, "identity-password-set", cancellationToken);
        return AccountActivationResult.Success();
    }

    private async Task<List<Guid>> FindEmployeeIdsAsync(
        (string Type, string Value) normalized,
        CancellationToken cancellationToken)
    {
        if (normalized.Type == "staff-id")
            return await _db.Employees.AsNoTracking()
                .Where(employee => employee.NormalizedStaffId == normalized.Value)
                .Select(employee => employee.Id).Take(2).ToListAsync(cancellationToken);
        if (normalized.Type == "email")
            return await _db.Employees.AsNoTracking()
                .Where(employee => employee.NormalizedPrimaryEmail == normalized.Value)
                .Select(employee => employee.Id).Take(2).ToListAsync(cancellationToken);
        return (await _db.Employees.AsNoTracking()
                .Where(employee => employee.Phone != null)
                .Select(employee => new { employee.Id, employee.Phone })
                .ToListAsync(cancellationToken))
            .Where(employee => LoginIdentifierNormalizer.NormalizeGhanaPhone(employee.Phone) == normalized.Value)
            .Select(employee => employee.Id).Take(2).ToList();
    }

    private async Task<int> CountRecentChallengesAsync(
        string identifierHash,
        string destinationHash,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var query = _db.AccountActivationChallenges.AsNoTracking()
            .Where(challenge =>
                challenge.RequestedIdentifierHash == identifierHash ||
                challenge.DestinationHash == destinationHash)
            .Select(challenge => challenge.CreatedAt);
        return _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await query.ToListAsync(cancellationToken)).Count(createdAt => createdAt >= cutoff)
            : await query.CountAsync(createdAt => createdAt >= cutoff, cancellationToken);
    }

    private async Task ProvisionSafeLoginIdentifiersAsync(
        User user,
        string verifiedDeliveryChannel,
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken)
    {
        var employee = await _db.Employees.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == user.EmployeeId, cancellationToken);
        var candidates = new[]
        {
            (Type: "staff-id", Value: LoginIdentifierNormalizer.Normalize(employee.StaffId)),
            verifiedDeliveryChannel == "email"
                ? (Type: "email", Value: LoginIdentifierNormalizer.Normalize(employee.PrimaryEmail))
                : (Type: "phone", Value: LoginIdentifierNormalizer.Normalize(employee.Phone))
        };
        foreach (var candidate in candidates)
        {
            if (candidate.Value is null || candidate.Value.Value.Type != candidate.Type)
                continue;
            var normalized = candidate.Value.Value.Value;
            var unique = (await FindEmployeeIdsAsync((candidate.Type, normalized), cancellationToken)).Count == 1;
            var exists = await _db.UserLoginIdentifiers.AnyAsync(identifier =>
                identifier.IdentifierType == candidate.Type &&
                identifier.NormalizedValue == normalized &&
                identifier.IsActive, cancellationToken);
            if (unique && !exists)
            {
                _db.UserLoginIdentifiers.Add(new UserLoginIdentifier(
                    user.Id, employee.Id, candidate.Type, normalized, "account-activation", verifiedAt));
            }
        }
    }

    private static DestinationResult ResolveDestination(
        (string Type, string Value) identifier,
        string? contact,
        string? employeeEmail,
        string? employeePhone)
    {
        var supplied = identifier.Type switch
        {
            "email" => employeeEmail,
            "phone" => employeePhone,
            _ => contact
        };
        if (string.IsNullOrWhiteSpace(supplied))
            return DestinationResult.Fail("A matching email address or phone number is required.");

        var normalized = LoginIdentifierNormalizer.Normalize(supplied);
        if (normalized is null || normalized.Value.Type is not ("email" or "phone"))
            return DestinationResult.Fail("The contact is not a valid email address or Ghanaian phone number.");

        if (identifier.Type == "staff-id")
        {
            var stored = normalized.Value.Type == "email"
                ? LoginIdentifierNormalizer.Normalize(employeeEmail)
                : LoginIdentifierNormalizer.Normalize(employeePhone);
            if (stored is null || stored.Value != normalized.Value)
                return DestinationResult.Fail("The supplied contact does not match the contact held on the staff record.");
        }

        if (!IsDeliverable(normalized.Value.Type, supplied))
            return DestinationResult.Fail("The contact held on the staff record is not deliverable. Contact your institute HR office to correct it.");
        return DestinationResult.Ok(normalized.Value.Type, normalized.Value.Value, supplied.Trim());
    }

    private static bool IsDeliverable(string channel, string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (channel == "email")
        {
            return !normalized.EndsWith("@invalid", StringComparison.Ordinal) &&
                !normalized.EndsWith("@example.invalid", StringComparison.Ordinal) &&
                !normalized.Contains("placeholder", StringComparison.Ordinal);
        }
        return !normalized.Contains("000000", StringComparison.Ordinal);
    }

    private static string Mask(Destination destination)
    {
        if (destination.Channel == "email")
        {
            var separator = destination.Original.IndexOf('@');
            return separator <= 0
                ? "***"
                : $"{destination.Original[0]}***{destination.Original[separator..]}";
        }
        return $"********{destination.Value[^4..]}";
    }

    private string GetHashKey()
    {
        var key = _configuration["AccountActivation:HashKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || key.Length < 32)
            throw new InvalidOperationException("AccountActivation:HashKey must be a non-placeholder secret of at least 32 characters.");
        return key;
    }

    private static string ComputeKeyedHash(string key, string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));

    private static IReadOnlyDictionary<string, string[]> ContactErrors(string detail) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["contact"] = [detail] };

    private static AccountActivationResult<AccountActivationChallengeData> NotFound() =>
        AccountActivationResult<AccountActivationChallengeData>.Failure(
            404, "not_found", "No matching staff account was found for the details you entered.");

    private static AccountActivationResult<AccountActivationChallengeData> Conflict(string detail) =>
        AccountActivationResult<AccountActivationChallengeData>.Failure(409, "conflict", detail);

    private static AccountActivationResult<AccountActivationVerificationData> VerificationFailure(
        string code,
        string detail) =>
        AccountActivationResult<AccountActivationVerificationData>.Failure(422, code, detail);

    private static AccountActivationResult Validation(string detail, string? field = null) =>
        AccountActivationResult.Failure(
            422,
            "validation_failed",
            detail,
            field is null ? null : new Dictionary<string, string[]> { [field] = [detail] });

    private static AccountActivationResult Expired(string detail) =>
        AccountActivationResult.Failure(422, "verification_expired", detail);

    private sealed record Destination(string Channel, string Value, string Original);

    private sealed record DestinationResult(bool Succeeded, Destination? Destination, string? Detail)
    {
        public static DestinationResult Ok(string channel, string value, string original) =>
            new(true, new(channel, value, original), null);

        public static DestinationResult Fail(string detail) => new(false, null, detail);
    }
}
