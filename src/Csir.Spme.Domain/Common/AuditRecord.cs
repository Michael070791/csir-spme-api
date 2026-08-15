namespace Csir.Spme.Domain.Common;

public class AuditRecord
{
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string ActorScope { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string? TargetId { get; set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? ClientIp { get; set; }
    public string? BeforeSummary { get; set; }
    public string? AfterSummary { get; set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditRecord() { }
    public AuditRecord(Guid actorUserId, string action, string targetType, string correlationId)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action;
        TargetType = targetType;
        CorrelationId = correlationId;
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
