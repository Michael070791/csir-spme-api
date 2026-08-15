using Microsoft.AspNetCore.Identity;

namespace Csir.Spme.Domain.Iam;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = string.Empty;
    public string? PendingEmail { get; private set; }
    public string AccountStatus { get; private set; } = "active";
    public string IdentityType { get; private set; } = "Employee";
    public DateTimeOffset? LastLoginAt { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? InstituteId { get; private set; }

    private User() { }

    public User(string userName, string identityType)
    {
        Id = Guid.NewGuid();
        UserName = userName;
        NormalizedUserName = userName.ToUpperInvariant();
        IdentityType = identityType;
        SecurityStamp = Guid.NewGuid().ToString("N");
        DisplayName = userName;
    }

    public void RecordLogin(DateTimeOffset loggedInAt) => LastLoginAt = loggedInAt;

    public void UpdateDisplayName(string displayName) => DisplayName = displayName.Trim();

    public void SetIdentityType(string identityType) => IdentityType = identityType.Trim();

    public void RequestEmailChange(string email) => PendingEmail = email.Trim();

    public void CompleteEmailChange() => PendingEmail = null;

    public void LinkEmployee(Guid employeeId, Guid instituteId, string identityType = "Employee")
    {
        EmployeeId = employeeId;
        InstituteId = instituteId;
        IdentityType = identityType;
    }

    /// <summary>
    /// Clears the employee link without changing <see cref="IdentityType"/> or institute scope.
    /// Used to repair duplicate Employee-identity accounts for the same employee.
    /// </summary>
    public void UnlinkEmployee() => EmployeeId = null;

    /// <summary>
    /// Assigns institute scope. Does not change <see cref="IdentityType"/> unless
    /// <paramref name="identityType"/> is explicitly provided (e.g. seed creating HrAdmin).
    /// </summary>
    public void AssignInstitute(Guid instituteId, string? identityType = null)
    {
        InstituteId = instituteId;
        if (!string.IsNullOrWhiteSpace(identityType))
            IdentityType = identityType.Trim();
    }

    /// <summary>Clears institute scope. Intended for PlatformAdmin identity accounts only.</summary>
    public void ClearInstitute() => InstituteId = null;

    public void MarkPasswordResetRequired() => AccountStatus = "password-reset-required";

    public void CompletePasswordReset() => AccountStatus = "active";

    public bool ImportCompatibleLegacyCredentials(
        string? passwordHash,
        bool emailConfirmed,
        bool phoneNumberConfirmed,
        bool lockoutEnabled,
        DateTimeOffset? lockoutEnd,
        int accessFailedCount)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) ||
            !passwordHash.StartsWith("AQAAAA", StringComparison.Ordinal) ||
            passwordHash.Length != 84)
        {
            MarkPasswordResetRequired();
            return false;
        }

        PasswordHash = passwordHash;
        EmailConfirmed = emailConfirmed;
        PhoneNumberConfirmed = phoneNumberConfirmed;
        LockoutEnabled = lockoutEnabled;
        LockoutEnd = lockoutEnd;
        AccessFailedCount = Math.Max(0, accessFailedCount);
        AccountStatus = "active";
        return true;
    }
}
