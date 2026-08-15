using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Projects;

public class ProjectMilestone : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public string Status { get; private set; } = "pending";
    public short DisplayOrder { get; private set; }

    private ProjectMilestone() { }
}
