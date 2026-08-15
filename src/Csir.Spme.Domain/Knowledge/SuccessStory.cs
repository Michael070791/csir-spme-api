using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Knowledge;

public class SuccessStory : InstituteScopedEntity
{
    public Guid? ProjectId { get; private set; }
    public Guid? ReportId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = "draft";
    public DateTimeOffset? PublishedAt { get; private set; }

    private SuccessStory() { }

    public static SuccessStory Create(
        Guid instituteId,
        Guid? projectId,
        Guid? reportId,
        string title,
        string description,
        DateTimeOffset? publishedAt)
    {
        return new SuccessStory
        {
            InstituteId = instituteId,
            ProjectId = projectId,
            ReportId = reportId,
            Title = title.Trim(),
            Description = description.Trim(),
            Status = publishedAt.HasValue ? "published" : "draft",
            PublishedAt = publishedAt
        };
    }
}
