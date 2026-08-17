using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Reporting;

public sealed class ReportProject : BaseEntity
{
    public Guid ReportId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? ProjectCodeSnapshot { get; private set; }
    public string? ProjectNameSnapshot { get; private set; }
    public string? ProgressSummary { get; private set; }
    public string? ProgressKeyResults { get; private set; }
    public string? Challenges { get; private set; }
    public string? NextQuarterActivities { get; private set; }
    public string? WayForward { get; private set; }
    public int ConferencePapersProduced { get; private set; }
    public int IpTechnologiesProtected { get; private set; }
    public string? SnapshotLeadName { get; private set; }
    public string? SnapshotEstimatedDuration { get; private set; }
    public string? SnapshotSponsorName { get; private set; }
    public string? SnapshotLocation { get; private set; }
    public string? SnapshotCollaboratingInstitute { get; private set; }
    public string? SnapshotParticipatingScientists { get; private set; }
    public string? SnapshotObjective { get; private set; }
    public string? SnapshotMethod { get; private set; }
    public string? SnapshotJustification { get; private set; }
    public string? SnapshotExpectedBeneficiaries { get; private set; }
    public string? SnapshotPotentialTechnology { get; private set; }
    public string? SnapshotCommercialization { get; private set; }
    public string? SnapshotContributionToKnowledge { get; private set; }
    public string? SnapshotPin { get; private set; }

    private ReportProject() { }

    public ReportProject(Guid reportId, Guid projectId)
    {
        ReportId = reportId;
        ProjectId = projectId;
    }

    public void UpdateProgress(
        string progressSummary,
        string? progressKeyResults,
        string? challenges,
        string? nextQuarterActivities,
        string? wayForward,
        int conferencePapersProduced,
        int ipTechnologiesProtected)
    {
        ProgressSummary = progressSummary.Trim();
        ProgressKeyResults = NormalizeOptional(progressKeyResults);
        Challenges = NormalizeOptional(challenges);
        NextQuarterActivities = NormalizeOptional(nextQuarterActivities);
        WayForward = NormalizeOptional(wayForward);
        ConferencePapersProduced = Math.Max(0, conferencePapersProduced);
        IpTechnologiesProtected = Math.Max(0, ipTechnologiesProtected);
    }

    public void CaptureSnapshot(string code, string name)
    {
        ProjectCodeSnapshot ??= code;
        ProjectNameSnapshot ??= name;
    }

    public void CaptureForm1Snapshot(
        string code,
        string name,
        string leadName,
        string estimatedDuration,
        string sponsorName,
        string location,
        string? collaboratingInstitute,
        string? participatingScientists,
        string objective,
        string? method,
        string? justification,
        string? expectedBeneficiaries,
        string? potentialTechnology,
        string? commercialization,
        string? contributionToKnowledge,
        string? pin = null)
    {
        CaptureSnapshot(code, name);
        SnapshotLeadName = leadName;
        SnapshotEstimatedDuration = estimatedDuration;
        SnapshotSponsorName = sponsorName;
        SnapshotLocation = location;
        SnapshotCollaboratingInstitute = collaboratingInstitute;
        SnapshotParticipatingScientists = participatingScientists;
        SnapshotObjective = objective;
        SnapshotMethod = method;
        SnapshotJustification = justification;
        SnapshotExpectedBeneficiaries = expectedBeneficiaries;
        SnapshotPotentialTechnology = potentialTechnology;
        SnapshotCommercialization = commercialization;
        SnapshotContributionToKnowledge = contributionToKnowledge;
        SnapshotPin = pin;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
