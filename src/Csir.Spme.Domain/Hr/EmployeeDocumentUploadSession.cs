using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Hr;

public sealed class EmployeeDocumentUploadSession : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public Guid? LinkedChildId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public string DeclaredSha256 { get; private set; } = string.Empty;
    public string Status { get; private set; } = ProfileDocumentConstants.UploadPending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? FileId { get; private set; }
    public Guid? EmployeeDocumentId { get; private set; }

    private EmployeeDocumentUploadSession() { }

    public EmployeeDocumentUploadSession(
        Guid employeeId,
        Guid instituteId,
        Guid userId,
        string documentType,
        Guid? linkedChildId,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTimeOffset expiresAt)
    {
        EmployeeId = employeeId;
        InstituteId = instituteId;
        InitiatedByUserId = userId;
        DocumentType = documentType.Trim().ToLowerInvariant();
        LinkedChildId = linkedChildId;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        DeclaredSizeBytes = sizeBytes;
        DeclaredSha256 = sha256;
        ExpiresAt = expiresAt;
    }

    public Result<bool> Complete(Guid fileId, Guid employeeDocumentId, DateTimeOffset now)
    {
        if (Status != ProfileDocumentConstants.UploadPending)
            return Result.Failure(Error.Conflict("The upload session is no longer pending."));
        if (ExpiresAt <= now)
        {
            Status = ProfileDocumentConstants.UploadExpired;
            return Result.Failure(Error.Conflict("The upload session has expired."));
        }

        FileId = fileId;
        EmployeeDocumentId = employeeDocumentId;
        Status = ProfileDocumentConstants.UploadCompleted;
        return Result.Success();
    }
}
