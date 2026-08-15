using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class PerformanceAppraisal : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public DateTime AppraisalPeriodStart { get; private set; }
    public DateTime AppraisalPeriodEnd { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? SourceFileId { get; private set; }
    public string? Comments { get; private set; }

    private PerformanceAppraisal() { }
}
