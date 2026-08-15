using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Iam;

public sealed class PasswordResetRequest : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid VerificationChallengeId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }

    private PasswordResetRequest() { }

    public PasswordResetRequest(
        Guid id,
        Guid userId,
        Guid verificationChallengeId,
        DateTimeOffset requestedAt)
    {
        Id = id;
        UserId = userId;
        VerificationChallengeId = verificationChallengeId;
        RequestedAt = requestedAt;
    }

    public bool IsActive => CompletedAt is null && SupersededAt is null;

    public void Complete(DateTimeOffset completedAt)
    {
        if (!IsActive)
            throw new InvalidOperationException("Only an active password reset request can be completed.");
        CompletedAt = completedAt;
    }

    public void Supersede(DateTimeOffset supersededAt)
    {
        if (IsActive)
            SupersededAt = supersededAt;
    }
}
