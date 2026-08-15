using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Iam;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public Guid SessionId { get; private set; }
    public string SecurityStamp { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private RefreshToken() { }
    public RefreshToken(
        Guid userId,
        string tokenHash,
        Guid familyId,
        Guid sessionId,
        string securityStamp,
        DateTimeOffset expiresAt,
        string? ipAddress = null,
        string? userAgent = null)
    {
        Id = Guid.NewGuid();
        UserId = userId; TokenHash = tokenHash; FamilyId = familyId;
        SessionId = sessionId; SecurityStamp = securityStamp; ExpiresAt = expiresAt;
        IssuedAt = DateTimeOffset.UtcNow;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Rotate(Guid replacementTokenId, DateTimeOffset revokedAt)
    {
        RevokedAt = revokedAt;
        ReplacedByTokenId = replacementTokenId;
        RevocationReason = "rotated";
    }

    public void Revoke(string reason, DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
        RevocationReason ??= reason;
    }
}
