using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Projects;

public class ProjectUpdate : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public Guid? ReportingPeriodId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string Status { get; private set; } = "draft";
    public decimal ProgressPercent { get; private set; }
    public string? Risks { get; private set; }
    public string? NextSteps { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    private ProjectUpdate() { }
}
