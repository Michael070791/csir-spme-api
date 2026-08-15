using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Reporting;

public sealed class StaffQuarterlyReportUploadSession : BaseEntity
{
    public Guid InstituteId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public Guid? ReportId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string UploadKind { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public string DeclaredSha256 { get; private set; } = string.Empty;
    public string Status { get; private set; } = StaffReportUploadStatuses.Pending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? FileId { get; private set; }

    private StaffQuarterlyReportUploadSession() { }

    public StaffQuarterlyReportUploadSession(
        Guid instituteId,
        Guid employeeId,
        Guid userId,
        string uploadKind,
        Guid? reportId,
        Guid? projectId,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DateTimeOffset expiresAt)
    {
        InstituteId = instituteId;
        EmployeeId = employeeId;
        InitiatedByUserId = userId;
        UploadKind = uploadKind;
        ReportId = reportId;
        ProjectId = projectId;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        DeclaredSizeBytes = sizeBytes;
        DeclaredSha256 = sha256;
        ExpiresAt = expiresAt;
    }

    public Result<bool> Complete(Guid fileId, DateTimeOffset now)
    {
        if (Status != StaffReportUploadStatuses.Pending)
            return Result.Failure(Error.Conflict("The upload session is no longer pending."));
        if (ExpiresAt <= now)
        {
            Status = StaffReportUploadStatuses.Expired;
            return Result.Failure(Error.Conflict("The upload session has expired."));
        }

        FileId = fileId;
        Status = StaffReportUploadStatuses.Completed;
        return Result.Success();
    }
}
