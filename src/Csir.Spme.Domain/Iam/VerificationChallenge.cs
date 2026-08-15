using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Iam;

public sealed class VerificationChallenge : BaseEntity
{
    public Guid? UserId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string DestinationHash { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public short AttemptCount { get; private set; }
    public short ResendCount { get; private set; }
    public DateTimeOffset LastSentAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private VerificationChallenge() { }

    public VerificationChallenge(
        Guid userId,
        Guid? employeeId,
        string purpose,
        string channel,
        string destinationHash,
        string codeHash,
        DateTimeOffset expiresAt,
        DateTimeOffset sentAt)
    {
        UserId = userId;
        EmployeeId = employeeId;
        Purpose = purpose;
        Channel = channel;
        DestinationHash = destinationHash;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        LastSentAt = sentAt;
    }

    public bool IsActive(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    public void RecordFailedAttempt(DateTimeOffset now, short maximumAttempts)
    {
        if (AttemptCount < short.MaxValue)
            AttemptCount++;
        if (AttemptCount >= maximumAttempts)
            Consume(now);
    }

    public void Consume(DateTimeOffset consumedAt)
    {
        if (ConsumedAt is null)
            ConsumedAt = consumedAt;
    }

    public void VerifyAndConsume(DateTimeOffset consumedAt)
    {
        VerifiedAt ??= consumedAt;
        Consume(consumedAt);
    }
}
