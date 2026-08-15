using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Reporting;

public sealed class ReportAttachment : BaseEntity
{
    public Guid ReportId { get; private set; }
    public Guid FileId { get; private set; }
    public string AttachmentType { get; private set; } = string.Empty;

    private ReportAttachment() { }

    public ReportAttachment(Guid reportId, Guid fileId, string attachmentType)
    {
        ReportId = reportId;
        FileId = fileId;
        AttachmentType = attachmentType;
    }
}
