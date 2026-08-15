using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Projects;

public class ProjectFunding : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string FundingType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "GHS";
    public DateTime? ReceivedDate { get; private set; }
    public string? Reference { get; private set; }

    private ProjectFunding() { }
}
