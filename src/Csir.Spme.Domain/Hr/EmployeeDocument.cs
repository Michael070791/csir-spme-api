using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Hr;

public sealed class EmployeeDocument : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid InstituteId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public Guid FileId { get; private set; }
    public Guid? LinkedChildId { get; private set; }
    public string Status { get; private set; } = ProfileDocumentConstants.StatusActive;
    public Guid UploadedByUserId { get; private set; }

    private EmployeeDocument() { }

    public EmployeeDocument(
        Guid employeeId,
        Guid instituteId,
        string documentType,
        Guid fileId,
        Guid uploadedByUserId,
        Guid? linkedChildId = null)
    {
        EmployeeId = employeeId;
        InstituteId = instituteId;
        DocumentType = documentType.Trim().ToLowerInvariant();
        FileId = fileId;
        UploadedByUserId = uploadedByUserId;
        LinkedChildId = linkedChildId;
    }

    public void MarkSuperseded() => Status = ProfileDocumentConstants.StatusSuperseded;
}
