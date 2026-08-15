using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Knowledge;

public class Publication : InstituteScopedEntity
{
    public Guid? TechnologyId { get; private set; }
    public Guid? ReportId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Abstract { get; private set; }
    public DateTime? PublishedOn { get; private set; }
    public string PublicationType { get; private set; } = string.Empty;
    public Guid? LeadEmployeeId { get; private set; }
    public string? Citation { get; private set; }
    public string Status { get; private set; } = "draft";

    private Publication() { }

    public static Publication Create(
        Guid instituteId,
        Guid? technologyId,
        Guid? reportId,
        string title,
        string? abstractText,
        DateTime? publishedOn,
        string publicationType,
        Guid? leadEmployeeId,
        string? citation)
    {
        return new Publication
        {
            InstituteId = instituteId,
            TechnologyId = technologyId,
            ReportId = reportId,
            Title = title.Trim(),
            Abstract = string.IsNullOrWhiteSpace(abstractText) ? null : abstractText.Trim(),
            PublishedOn = publishedOn?.Date,
            PublicationType = publicationType,
            LeadEmployeeId = leadEmployeeId,
            Citation = string.IsNullOrWhiteSpace(citation) ? null : citation.Trim(),
            Status = publishedOn.HasValue ? "published" : "draft"
        };
    }
}
