namespace Csir.Spme.Domain.Leave;

public class WorkflowApprovalToken
{
    public Guid Id { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid ApproverUserId { get; private set; }
    public string Stage { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WorkflowApprovalToken() { }

    public static WorkflowApprovalToken Create(
        string purpose,
        Guid resourceId,
        Guid approverUserId,
        string stage,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new WorkflowApprovalToken
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            ResourceId = resourceId,
            ApproverUserId = approverUserId,
            Stage = stage,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt
        };
    }

    public bool IsActive(DateTimeOffset now) =>
        ConsumedAt is null && RevokedAt is null && ExpiresAt > now;

    public void Consume(DateTimeOffset consumedAt) => ConsumedAt = consumedAt;

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt = revokedAt;
}
