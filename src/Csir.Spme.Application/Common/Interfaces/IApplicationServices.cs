namespace Csir.Spme.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(Stream content, string storageKey, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    Task<FileReadAccessResult?> CreateReadAccessAsync(string storageKey, CancellationToken ct = default);
}

public interface IDirectFileUploadService
{
    Task<DirectFileUploadAccess?> CreateWriteAccessAsync(
        string storageKey, string contentType, long sizeBytes, string sha256, DateTimeOffset expiresAt, CancellationToken ct = default);
    Task<FileUploadInspection?> InspectAsync(string storageKey, CancellationToken ct = default);
}

public sealed record DirectFileUploadAccess(Uri UploadUri, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> RequiredHeaders);
public sealed record FileUploadInspection(long SizeBytes, string? ContentType, string? Sha256);

public interface IPromotionMalwareScanner
{
    Task<string> ScanAsync(string storageKey, CancellationToken ct = default);
}

public record FileUploadResult(string StorageKey, long SizeBytes, string Checksum);

public sealed record FileReadAccessResult(Uri Uri, DateTimeOffset ExpiresAt);

public interface IProfileImageProcessor
{
    Task<ProfileImageProcessingResult> ProcessAsync(Stream content, CancellationToken ct = default);
}

public sealed record ProfileImageProcessingResult(
    Stream Content,
    string ContentType,
    long SizeBytes,
    string SourceContentType);

public sealed class FileStorageUnavailableException : Exception
{
    public FileStorageUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? InstituteId { get; }
    Guid? EmployeeId { get; }
    string? IdentityType => null;
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionCode);
    bool IsInRole(string role) => false;
}

public interface IInstituteAccessContext
{
    Guid InstituteId { get; }
    void AssertInstitute(Guid instituteId);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default);
}

public interface ISmsService
{
    Task SendAsync(string to, string body, CancellationToken ct = default);
}

public interface ICommunicationOutbox
{
    Task EnqueueEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        string category,
        string idempotencyKey,
        CancellationToken ct = default);

    Task EnqueueSmsAsync(
        string to,
        string body,
        string category,
        string idempotencyKey,
        CancellationToken ct = default);
}

/// <summary>Stages workflow notifications in the same unit of work as the owning mutation.</summary>
public interface IWorkflowNotificationOutbox
{
    Task StageStaffQuarterlyReportSubmittedAsync(
        StaffQuarterlyReportNotification notification,
        CancellationToken ct = default);

    Task StageReportSubmittedAsync(
        Guid reportId,
        Guid instituteId,
        Guid submittedByUserId,
        DateTimeOffset submittedAt,
        string title,
        CancellationToken ct = default);

    Task StageLeaveDecisionAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        Guid decidedByUserId,
        string decision,
        CancellationToken ct = default);

    Task StageLeaveAwaitingApprovalAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        CancellationToken ct = default);

    Task StageLeaveOwnerNoticeAsync(
        Guid leaveRequestId,
        Guid instituteId,
        Guid employeeId,
        string eventName,
        string title,
        string body,
        CancellationToken ct = default);

    Task StageStaffQuarterlyReportReviewedAsync(
        Guid reportId,
        Guid instituteId,
        Guid ownerEmployeeId,
        string periodName,
        string title,
        string outcome,
        string? returnReason,
        CancellationToken ct = default);

    Task StageMemoPublishedAsync(
        Guid memoId,
        Guid instituteId,
        string title,
        string emailBody,
        string smsSynopsis,
        IReadOnlyList<MemoChannelRecipient> recipients,
        CancellationToken ct = default);

    Task StageHrApprovalAccessAsync(
        Guid employeeId,
        Guid instituteId,
        CancellationToken ct = default);

    Task StageSkeletalStaffAwaitingApprovalAsync(
        Guid requestId,
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct = default);

    Task StageSkeletalStaffDecisionAsync(
        Guid requestId,
        Guid instituteId,
        Guid employeeId,
        string decision,
        string? reason,
        CancellationToken ct = default);

    Task StageSkeletalStaffServiceReportAsync(
        SkeletalStaffServiceReportNotification notification,
        CancellationToken ct = default);
}

public sealed record SkeletalStaffApprovalTrailEntry(
    string Stage,
    string Decision,
    DateTimeOffset DecidedAt,
    string? Comments);

public sealed record SkeletalStaffServiceReportNotification(
    Guid RequestId,
    Guid InstituteId,
    Guid OwnerEmployeeId,
    Guid RecipientUserId,
    string RecipientDisplayName,
    string RecipientEmail,
    string? RecipientPhone,
    string StaffDisplayName,
    string PeriodName,
    DateTime AvailabilityStart,
    DateTime AvailabilityEnd,
    IReadOnlyList<SkeletalStaffApprovalTrailEntry> Approvals,
    byte[] PdfContent,
    bool AttachPdf);

public sealed record StaffQuarterlyReportNotification(
    Guid ReportId,
    Guid InstituteId,
    Guid OwnerEmployeeId,
    Guid ReviewerUserId,
    string ReviewerDisplayName,
    string ReviewerEmail,
    string ReviewerPhone,
    string StaffDisplayName,
    string PeriodName,
    string Title,
    string? Abstract,
    string WorkSummary,
    string? KeyResults,
    string? ConclusionNextSteps,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<StaffQuarterlyProjectReportContent> ProjectReports,
    IReadOnlyList<string> ImageFileNames,
    DateTimeOffset SubmittedAt);

public sealed record StaffQuarterlyProjectReportContent(
    string Code,
    string Title,
    string? Pin,
    string LeadName,
    string EstimatedDuration,
    string SponsorName,
    string Location,
    string Objective,
    string? Method,
    string? Justification,
    string? ExpectedBeneficiaries,
    string? PotentialTechnology,
    string? Commercialization,
    string? ContributionToKnowledge,
    string ProgressSummary,
    string? ProgressKeyResults,
    string? Challenges,
    string? NextQuarterActivities,
    string? WayForward,
    int ConferencePapersProduced,
    int IpTechnologiesProtected);

public sealed record MemoChannelRecipient(
    Guid? UserId,
    string? Email,
    string? Phone,
    bool SendEmail,
    bool SendSms);

public sealed record EmailAttachment(string FileName, string ContentType, string ContentBase64);

public interface IEmailTransport
{
    Task<CommunicationTransportResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        string? textBody,
        string category,
        CancellationToken ct = default,
        IReadOnlyList<EmailAttachment>? attachments = null);
}

public interface ISmsTransport
{
    Task<CommunicationTransportResult> SendAsync(
        string to,
        string body,
        CancellationToken ct = default);
}

public sealed record CommunicationTransportResult(
    bool Accepted,
    string Provider,
    string? ProviderMessageId,
    string? ErrorCode,
    int? HttpStatusCode,
    bool IsTransient);

public interface IAuditService
{
    /// <summary>
    /// Stages an audit record in the current unit of work without committing it.
    /// The caller that owns the business operation must save the shared context.
    /// </summary>
    Task RecordAsync(string action, string targetType, string? targetId = null, string? before = null, string? after = null, CancellationToken ct = default);

    /// <summary>
    /// Records and immediately commits a standalone audit operation.
    /// Do not use this when an aggregate mutation must commit with its audit record.
    /// </summary>
    Task RecordAndSaveAsync(string action, string targetType, string? targetId = null, string? before = null, string? after = null, CancellationToken ct = default);
}
