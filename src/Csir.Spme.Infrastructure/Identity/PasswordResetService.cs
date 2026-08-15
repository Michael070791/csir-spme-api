using System.Security.Cryptography;
using System.Text;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Iam;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Identity;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";
    public string HashKey { get; set; } = string.Empty;
    public TimeSpan TokenLifespan { get; set; } = TimeSpan.FromHours(24);
}

public sealed class PasswordResetService : IPasswordResetService
{
    private const string Purpose = "password-reset";
    private readonly SpmeDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IAuditService _audit;
    private readonly BrandedEmailRenderer _renderer;
    private readonly PasswordResetOptions _options;
    private readonly PortalUrlOptions _portalUrls;
    private readonly TimeProvider _timeProvider;

    public PasswordResetService(
        SpmeDbContext db,
        UserManager<User> userManager,
        IAuditService audit,
        BrandedEmailRenderer renderer,
        IOptions<PasswordResetOptions> options,
        IOptions<PortalUrlOptions> portalUrls,
        TimeProvider timeProvider)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
        _renderer = renderer;
        _options = options.Value;
        _portalUrls = portalUrls.Value;
        _timeProvider = timeProvider;
    }

    public async Task RequestAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !IsEligible(user) || string.IsNullOrWhiteSpace(user.Email))
            return;

        var now = _timeProvider.GetUtcNow();
        var requestId = Guid.NewGuid();
        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken));
        var challenge = new VerificationChallenge(
            user.Id,
            user.EmployeeId,
            Purpose,
            "email",
            ComputeHmac(user.Email.Trim().ToUpperInvariant()),
            ComputeHmac($"{requestId:N}.{encodedToken}"),
            now.Add(_options.TokenLifespan),
            now);
        var resetRequest = new PasswordResetRequest(requestId, user.Id, challenge.Id, now);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var activeRequests = await _db.PasswordResetRequests
            .Where(candidate => candidate.UserId == user.Id &&
                candidate.CompletedAt == null && candidate.SupersededAt == null)
            .ToListAsync(ct);
        if (activeRequests.Count > 0)
        {
            var challengeIds = activeRequests.Select(candidate => candidate.VerificationChallengeId).ToArray();
            var activeChallenges = await _db.VerificationChallenges
                .Where(candidate => challengeIds.Contains(candidate.Id) && candidate.ConsumedAt == null)
                .ToListAsync(ct);
            foreach (var prior in activeRequests)
                prior.Supersede(now);
            foreach (var prior in activeChallenges)
                prior.Consume(now);
        }

        // Employee and legacy StaffUser accounts use the staff portal. HR/admin identity
        // types (HrAdmin, PlatformAdmin, InstituteAdmin, ...) use the HR portal.
        var baseUrl = UsesStaffPortal(user.IdentityType)
            ? _portalUrls.StaffPasswordResetUrl
            : _portalUrls.HrPasswordResetUrl;
        var resetUrl = QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["requestId"] = requestId.ToString("D"),
            ["token"] = encodedToken
        });
        var emailMessage = _renderer.PasswordReset(user.DisplayName, resetUrl);

        _db.VerificationChallenges.Add(challenge);
        _db.PasswordResetRequests.Add(resetRequest);
        _db.CommunicationOutboxMessages.Add(new CommunicationOutboxMessage(
            "email",
            user.Email,
            emailMessage.Subject,
            emailMessage.HtmlBody,
            true,
            "authentication",
            $"password-reset:{requestId:N}",
            emailMessage.TextBody));
        await _audit.RecordAsync(
            "auth.password-reset-requested",
            "PasswordResetRequest",
            requestId.ToString(),
            null,
            "reset-email-queued",
            ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PasswordResetConfirmationResult> ConfirmAsync(
        Guid requestId,
        string token,
        string newPassword,
        string confirmNewPassword,
        CancellationToken ct = default)
    {
        if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
        {
            return PasswordResetConfirmationResult.Failure(
                "validation_failed",
                "The new password confirmation does not match.",
                new Dictionary<string, string[]> { ["confirmNewPassword"] = ["The passwords do not match."] });
        }

        var resetRequest = await _db.PasswordResetRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, ct);
        if (resetRequest is null || !resetRequest.IsActive)
            return InvalidOrExpired("verification_expired");

        var challenge = await _db.VerificationChallenges
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == resetRequest.VerificationChallengeId &&
                candidate.UserId == resetRequest.UserId &&
                candidate.Purpose == Purpose, ct);
        var now = _timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsActive(now))
            return InvalidOrExpired("verification_expired");

        var suppliedHash = ComputeHmac($"{requestId:N}.{token}");
        if (!FixedTimeEqualsHex(challenge.CodeHash, suppliedHash) ||
            !TryDecodeToken(token, out var identityToken))
        {
            challenge.RecordFailedAttempt(now, 5);
            await _db.SaveChangesAsync(ct);
            return InvalidOrExpired("verification_failed");
        }

        var user = await _userManager.FindByIdAsync(resetRequest.UserId.ToString());
        if (user is null || !IsEligible(user))
            return InvalidOrExpired("verification_expired");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var result = await _userManager.ResetPasswordAsync(user, identityToken, newPassword);
        if (!result.Succeeded)
        {
            var passwordErrors = result.Errors
                .Where(error => error.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase))
                .Select(error => error.Description)
                .Distinct()
                .ToArray();
            if (passwordErrors.Length > 0)
            {
                return PasswordResetConfirmationResult.Failure(
                    "validation_failed",
                    "The new password does not satisfy the password policy.",
                    new Dictionary<string, string[]> { ["newPassword"] = passwordErrors });
            }
            return InvalidOrExpired("verification_failed");
        }

        user.CompletePasswordReset();
        var statusResult = await _userManager.UpdateAsync(user);
        if (!statusResult.Succeeded)
            return PasswordResetConfirmationResult.Failure(
                "validation_failed", "The password reset request could not be completed.");
        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            return PasswordResetConfirmationResult.Failure(
                "validation_failed", "The password reset request could not be completed.");

        var refreshTokens = await _db.RefreshTokens
            .Where(candidate => candidate.UserId == user.Id && candidate.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var refreshToken in refreshTokens)
            refreshToken.Revoke("password-reset", now);
        var sessions = await _db.UserSessions
            .Where(candidate => candidate.UserId == user.Id && candidate.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var session in sessions)
            session.Revoke(now);

        challenge.VerifyAndConsume(now);
        resetRequest.Complete(now);
        await _audit.RecordAsync(
            "auth.password-reset-completed",
            "User",
            user.Id.ToString(),
            null,
            "security-stamp-rotated;refresh-tokens-and-sessions-revoked",
            ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return PasswordResetConfirmationResult.Success();
    }

    private string ComputeHmac(string value)
    {
        var key = Encoding.UTF8.GetBytes(_options.HashKey);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
    }

    private static bool FixedTimeEqualsHex(string expected, string actual)
    {
        if (expected.Length != 64 || actual.Length != 64)
            return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(actual));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryDecodeToken(string encodedToken, out string token)
    {
        token = string.Empty;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool UsesStaffPortal(string identityType) =>
        string.Equals(identityType, "Employee", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(identityType, "StaffUser", StringComparison.OrdinalIgnoreCase);

    private static bool IsEligible(User user) =>
        user.AccountStatus is "active" or "password-reset-required";

    private static PasswordResetConfirmationResult InvalidOrExpired(string code) =>
        PasswordResetConfirmationResult.Failure(
            code,
            "The password reset request is invalid or has expired.");
}
