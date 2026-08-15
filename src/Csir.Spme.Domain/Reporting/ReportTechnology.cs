using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Reporting;

public sealed class ReportTechnology : BaseEntity
{
    public Guid ReportId { get; private set; }
    public Guid TechnologyId { get; private set; }
    public string? TechnologyCodeSnapshot { get; private set; }
    public string? TechnologyNameSnapshot { get; private set; }

    private ReportTechnology() { }

    public ReportTechnology(Guid reportId, Guid technologyId)
    {
        ReportId = reportId;
        TechnologyId = technologyId;
    }

    public void CaptureSnapshot(string code, string name)
    {
        TechnologyCodeSnapshot ??= code;
        TechnologyNameSnapshot ??= name;
    }
}
