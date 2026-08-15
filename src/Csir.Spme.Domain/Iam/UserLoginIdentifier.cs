using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Iam;

public sealed class UserLoginIdentifier : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string IdentifierType { get; private set; } = string.Empty;
    public string NormalizedValue { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public bool IsActive { get; private set; }
    public string VerificationSource { get; private set; } = string.Empty;
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private UserLoginIdentifier() { }

    public UserLoginIdentifier(
        Guid userId,
        Guid employeeId,
        string identifierType,
        string normalizedValue,
        string verificationSource,
        DateTimeOffset verifiedAt)
    {
        UserId = userId;
        EmployeeId = employeeId;
        IdentifierType = identifierType;
        NormalizedValue = normalizedValue;
        VerificationSource = verificationSource;
        VerifiedAt = verifiedAt;
        IsVerified = true;
        IsActive = true;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        IsActive = false;
        RevokedAt = revokedAt;
    }
}
