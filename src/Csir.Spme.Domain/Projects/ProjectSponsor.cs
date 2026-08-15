using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Projects;

public class ProjectSponsor : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? ContactDetails { get; private set; }
    public decimal? CommittedAmount { get; private set; }
    public string? Currency { get; private set; }

    private ProjectSponsor() { }

    public ProjectSponsor(
        Guid projectId,
        string name,
        decimal? committedAmount,
        string? currency)
    {
        ProjectId = projectId;
        Name = name.Trim();
        CommittedAmount = committedAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim();
    }
}
