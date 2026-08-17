using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Csir.Spme.Api.Endpoints.V2;

public sealed record CollectionResponse<T>(IReadOnlyList<T> Items, int Total);

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record EmployeePageResponse(
    IReadOnlyList<EmployeeListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int ApprovedTotal,
    int UnapprovedTotal,
    int HodTotal = 0);

public sealed record BulkApproveEmployeesRequest(IReadOnlyList<Guid>? EmployeeIds);

public sealed record BulkApproveEmployeeResult(
    Guid EmployeeId,
    string Outcome,
    string? Code,
    string? Message);

public sealed record BulkApproveEmployeesResponse(
    int Approved,
    int Skipped,
    int Failed,
    IReadOnlyList<BulkApproveEmployeeResult> Results);

public sealed record ResponseMeta(string RequestId);

public sealed record CursorPage(string? NextCursor);

public sealed record ListResponse<T>(
    IReadOnlyList<T> Data,
    CursorPage Page,
    ResponseMeta Meta);

public sealed record DataResponse<T>(T Data, ResponseMeta Meta);

public sealed class CursorPageResponse<T>
{
    [System.Text.Json.Serialization.JsonConstructor]
    public CursorPageResponse(IReadOnlyList<T> data, CursorPage page, ResponseMeta meta)
    {
        Data = data;
        Page = page;
        Meta = meta;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<T> Items => Data;

    [System.Text.Json.Serialization.JsonIgnore]
    public string? NextCursor => Page.NextCursor;

    public CursorPageResponse(IReadOnlyList<T> data, string? nextCursor)
        : this(data, new CursorPage(nextCursor),
            new ResponseMeta(System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty))
    {
    }

    public IReadOnlyList<T> Data { get; }
    public CursorPage Page { get; }
    public ResponseMeta Meta { get; }
}

internal static class ResponseEnvelope
{
    public static ListResponse<T> List<T>(
        HttpContext context,
        IReadOnlyList<T> data,
        string? nextCursor) =>
        new(data, new CursorPage(nextCursor), new ResponseMeta(context.TraceIdentifier));

    public static DataResponse<T> Data<T>(HttpContext context, T data) =>
        new(data, new ResponseMeta(context.TraceIdentifier));
}

public sealed record ReportingPeriodResponse(
    Guid Id,
    string ScopeType,
    Guid? InstituteId,
    string Code,
    string Name,
    string PeriodType,
    DateTime StartDate,
    DateTime EndDate,
    DateTime? DueDate,
    string Status,
    string Etag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateReportingPeriodRequest(
    [property: Required, StringLength(16)] string ScopeType,
    Guid? InstituteId,
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(256)] string Name,
    [property: Required, StringLength(32)] string PeriodType,
    DateTime StartDate,
    DateTime EndDate,
    DateTime? DueDate);

public sealed record HealthResponse(string Status, string? Database = null, string? Version = null);

public sealed record LoginRequest(
    [property: Required, StringLength(256)] string Username,
    [property: Required, StringLength(256)] string Password,
    [property: StringLength(128)] string? DeviceName = null,
    [property: StringLength(32)] string? Platform = null);

public sealed record RequestPasswordResetRequest(
    [property: Required, EmailAddress, StringLength(320)] string Email);

public sealed record ConfirmPasswordResetRequest(
    [property: Required] Guid RequestId,
    [property: Required, StringLength(4096)] string Token,
    [property: Required, StringLength(256, MinimumLength = 12)] string NewPassword,
    [property: Required, Compare(nameof(ConfirmPasswordResetRequest.NewPassword))] string ConfirmNewPassword);

public sealed record AuthenticatedUserResponse(
    Guid Id, string? UserName, string? Email, string IdentityType, string AccountStatus,
    Guid? InstituteId, Guid? EmployeeId);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    AuthenticatedUserResponse User,
    IReadOnlyList<string> Roles,
    Guid SessionId,
    DateTimeOffset RefreshExpiresAt);

public sealed record UserSessionResponse(
    Guid Id, string? DeviceName, string? Platform, DateTimeOffset StartedAt,
    DateTimeOffset LastSeenAt, bool IsCurrent);

public sealed record CreateAccountActivationChallengeRequest(
    [property: Required, StringLength(320)] string Identifier,
    [property: StringLength(320)] string? Contact = null);

public sealed record AccountActivationChallengeResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    string Outcome,
    string DeliveryChannel,
    string MaskedDestination,
    string Message);

public sealed record VerifyAccountActivationChallengeRequest(
    [property: Required, StringLength(12, MinimumLength = 4)] string Code);

public sealed record VerifyAccountActivationChallengeResponse(
    string VerificationToken,
    DateTimeOffset ExpiresAt,
    string Message);

public sealed record CompleteAccountActivationRequest(
    Guid ChallengeId,
    [property: Required, StringLength(128)] string VerificationToken,
    [property: Required, StringLength(256, MinimumLength = 12)] string Password,
    [property: Required, Compare(nameof(CompleteAccountActivationRequest.Password))] string ConfirmPassword);

public sealed record CurrentUserResponse(
    Guid Id, string? UserName, string? Email, string DisplayName, string? PendingEmail, string IdentityType, string AccountStatus,
    Guid? InstituteId, Guid? EmployeeId, IReadOnlyList<string> Roles);

/// <summary>
/// Minimal, authenticated employee-portal projection. This deliberately excludes
/// personal-record fields that the portal does not need, such as phone numbers,
/// dates of birth, addresses, and family information.
/// </summary>
public sealed record PortalProfileResponse(
    Guid UserId,
    Guid? EmployeeId,
    string? StaffId,
    string DisplayName,
    string? PreferredName,
    string? JobTitle,
    string? StaffCategory,
    string? GradeCode,
    string? GradeName,
    PortalInstituteResponse? Institute,
    PortalContactStatusResponse Contact,
    string IdentityType,
    string AccountStatus,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> LeadershipRoles,
    bool IsHod,
    bool IsDirector,
    string? DivisionName = null,
    string? SectionName = null,
    int? ProfileCompletion = null);

public sealed record PortalInstituteResponse(Guid Id, string Code, string Name);

public sealed record PortalContactStatusResponse(
    string? Email,
    bool EmailConfirmed,
    bool PhoneConfirmed,
    bool EmployeeContactVerified);

public sealed record UpdateMyProfileRequest(
    [property: Required, StringLength(256, MinimumLength = 1)] string DisplayName,
    [property: EmailAddress, StringLength(320)] string? Email = null);

public sealed record ConfirmMyEmailRequest([property: Required, StringLength(4096)] string Token);

public sealed record ChangeMyPasswordRequest(
    [property: Required, StringLength(256)] string CurrentPassword,
    [property: Required, StringLength(256, MinimumLength = 12)] string NewPassword,
    [property: Required, Compare(nameof(ChangeMyPasswordRequest.NewPassword))] string ConfirmNewPassword);

public sealed record NotificationPreferenceResponse(bool EmailAlerts, bool LeaveReminders, bool PromotionUpdates, bool SystemAnnouncements);

public sealed record UpdateNotificationPreferenceRequest(bool EmailAlerts, bool LeaveReminders, bool PromotionUpdates, bool SystemAnnouncements);

public sealed record RoleResponse(Guid Id, string Code, string? Name, string Description, bool IsSystemRole);

public sealed record UserResponse(Guid Id, string? UserName, string? Email, string AccountStatus, string IdentityType, DateTimeOffset? LastLoginAt);

public sealed record SystemUserEmployeeSummary(
    Guid Id, string StaffId, string? Prefix, string Surname, string? OtherNames, string ProfileStatus);

public sealed record SystemUserResponse(
    Guid Id,
    string? UserName,
    string? Email,
    string AccountStatus,
    string IdentityType,
    Guid? InstituteId,
    Guid? EmployeeId,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt,
    SystemUserEmployeeSummary? Employee,
    EmployeeInstituteSummary? Institute);

public sealed record UpdateSystemUserRolesRequest(
    [property: Required] IReadOnlyList<string> Roles);

public sealed record UpdateSystemUserInstituteRequest(Guid? InstituteId);

public sealed record HrDashboardResponse(
    int TotalEmployees,
    int OnLeaveToday,
    int PendingPromotions,
    int OpenLeaveRequests);

public sealed record EmailRecipientsRequest(
    IReadOnlyList<Guid>? UserIds,
    IReadOnlyList<string>? Roles,
    string? Status);

public sealed record EmailRecipientResponse(
    Guid UserId,
    string? UserName,
    string Email,
    IReadOnlyList<string> Roles,
    string AccountStatus,
    EmployeeInstituteSummary? Institute);

public sealed record EmailRecipientsResponse(
    IReadOnlyList<EmailRecipientResponse> Items,
    int Total);

public sealed record BulkEmailRequest(
    IReadOnlyList<Guid>? UserIds,
    IReadOnlyList<string>? Roles,
    string? Status,
    [property: Required, StringLength(256)] string Subject,
    [property: Required, StringLength(8000)] string Body,
    bool IsHtml);

public sealed record BulkEmailResponse(int Sent, int Skipped);

public sealed record SendHrPortalLinksRequest(
    IReadOnlyList<Guid>? UserIds,
    IReadOnlyList<string>? Roles,
    string? Status);

public sealed record SendHrPortalLinksResponse(int Sent, int Skipped);

public sealed record LoginLockResponse(bool IsLocked);

public sealed record UpdateLoginLockRequest(bool IsLocked);

public sealed record EmployeeInstituteSummary(Guid Id, string Code, string Name, string Kind);

public sealed record EmployeeCurrentEmploymentSummary(
    Guid? DivisionId, string? DivisionName, Guid? SectionId, string? SectionName, string? JobTitle,
    IReadOnlyList<string> LeadershipRoles, string? StaffCategory, Guid? GradeId, string? GradeCode, string? GradeName,
    string? GradeStep, string? AreaOfSpecialization, string ServiceStatus, string? Organization, string? Location,
    string? Region, string? District, string? PensionType, string? PensionId, DateTime? AppointmentDate,
    DateTime? PromotionDate);

public sealed record ProfileImageReferenceResponse(
    Guid Id, string Url, string AccessUrl, string ContentType, string Etag);

public sealed record ProfileImageAccessResponse(
    Guid Id, string Url, DateTimeOffset ExpiresAt, string ContentType, string Etag);

public sealed record EmployeeListItem(
    Guid Id, string StaffId, string? Prefix, string Surname, string? OtherNames, string Gender, string? Religion,
    string? PrimaryEmail, string? Phone, string ProfileStatus, bool IsHrApproved, DateTimeOffset CreatedAt,
    Guid? ProfileImageFileId, ProfileImageReferenceResponse? ProfileImage,
    EmployeeInstituteSummary? Institute, EmployeeCurrentEmploymentSummary? CurrentEmployment,
    DateTime? OnLeaveUntil = null, decimal RemainingAnnualLeaveDays = 0m);

public sealed record EmployeeDetailResponse(
    Guid Id, string StaffId, string? Prefix, string Surname, string? OtherNames, string? PreferredName,
    string Gender, DateTime? DateOfBirth, string? Nationality, string? Religion, string? MaritalStatus, string? PrimaryEmail,
    string? Phone, string ProfileStatus, bool IsHrApproved, bool IsContactVerified, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, Guid? ProfileImageFileId, ProfileImageReferenceResponse? ProfileImage,
    EmployeeInstituteSummary? Institute, EmployeeCurrentEmploymentSummary? CurrentEmployment);

public sealed record EmploymentRecordResponse(
    Guid Id, Guid? DivisionId, string? DivisionName, Guid? SectionId, string? SectionName, string? JobTitle,
    IReadOnlyList<string> LeadershipRoles, string? StaffCategory, Guid? GradeId, string? GradeCode, string? GradeName,
    string? GradeStep, string? AreaOfSpecialization, string ServiceStatus, string? Organization, string? Location,
    string? Region, string? District, string? PensionType, string? PensionId, DateTime? AppointmentDate,
    DateTime? PromotionDate, DateTime EffectiveFrom, DateTime? EffectiveTo, bool IsCurrent);

public sealed record UpsertEmployeeRequest(
    Guid? InstituteId,
    [property: Required, StringLength(64)] string StaffId,
    [property: StringLength(32)] string? Prefix,
    [property: Required, StringLength(128)] string Surname,
    [property: StringLength(256)] string? OtherNames,
    [property: Required, StringLength(32)] string Gender,
    DateTime? DateOfBirth,
    [property: StringLength(96)] string? Nationality,
    [property: StringLength(96)] string? Religion,
    [property: StringLength(32)] string? MaritalStatus,
    [property: EmailAddress, StringLength(320)] string? PrimaryEmail,
    [property: StringLength(32)] string? Phone,
    [property: StringLength(32)] string? ProfileStatus,
    bool? IsHrApproved,
    Guid? DivisionId,
    Guid? SectionId,
    Guid? GradeId,
    [property: StringLength(256)] string? JobTitle,
    IReadOnlyList<string>? LeadershipRoles,
    [property: StringLength(64)] string? StaffCategory,
    [property: StringLength(32)] string? GradeStep,
    [property: StringLength(256)] string? AreaOfSpecialization,
    [property: StringLength(32)] string? ServiceStatus,
    [property: StringLength(256)] string? Organization,
    [property: StringLength(128)] string? Location,
    [property: StringLength(128)] string? Region,
    [property: StringLength(128)] string? District,
    [property: StringLength(32)] string? PensionType,
    [property: StringLength(128)] string? PensionId,
    DateTime? AppointmentDate,
    DateTime? PromotionDate);

public sealed record UpsertEmployeeSpouseRequest(
    [property: Required, StringLength(256)] string Name,
    DateTime? DateOfBirth,
    [property: StringLength(32)] string? Phone,
    [property: EmailAddress, StringLength(320)] string? Email,
    [property: StringLength(256)] string? Occupation,
    [property: StringLength(256)] string? Employer);

public sealed record EmployeeSpouseResponse(
    Guid Id, Guid EmployeeId, string Name, DateTime? DateOfBirth, string? Phone, string? Email,
    string? Occupation, string? Employer, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record UpsertEmployeeChildRequest(
    [property: Required, StringLength(256)] string Name,
    DateTime DateOfBirth,
    [property: Required, StringLength(32)] string Gender,
    [property: StringLength(128)] string? BirthCertificateNumber,
    Guid? BirthCertificateFileId);

public sealed record EmployeeChildResponse(
    Guid Id, Guid EmployeeId, string Name, DateTime DateOfBirth, string Gender,
    string? BirthCertificateNumber, Guid? BirthCertificateFileId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record UpsertEducationRecordRequest(
    [property: Required, StringLength(256)] string InstitutionName,
    [property: Required, StringLength(256)] string CourseStudied,
    [property: Required, StringLength(256)] string CertificateAwarded,
    [property: Required, StringLength(64)] string QualificationLevel,
    [property: StringLength(64)] string? Grade = null,
    [property: StringLength(256)] string? Specialization = null,
    [property: StringLength(512)] string? ProfessionalQualifications = null,
    [property: StringLength(512)] string? Affiliations = null,
    [property: StringLength(128)] string? CertificateNumber = null,
    DateTime? DateCommenced = null,
    DateTime? DateCompleted = null);

public sealed record EducationRecordResponse(
    Guid Id, Guid EmployeeId, string InstitutionName, string CourseStudied, string CertificateAwarded,
    string QualificationLevel, string? Grade, string? Specialization, string? ProfessionalQualifications,
    string? Affiliations, string? CertificateNumber, DateTime? DateCommenced, DateTime? DateCompleted,
    string InstitutionRecognitionStatus, Guid? InstitutionRecognitionEvidenceFileId,
    string RelevantFieldStatus, Guid? CertificateFileId, string Etag,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record EducationCertificateTypeResponse(
    string Code,
    string Label,
    string Name,
    string QualificationLevel,
    bool IsOpenAward);

public sealed record ReviewEducationRecordRequest(
    [property: StringLength(32)] string? InstitutionRecognitionStatus,
    [property: StringLength(32)] string? RelevantFieldStatus);

public sealed record EmployeeSelfContactResponse(
    Guid EmployeeId,
    string? PrimaryEmail,
    string? PendingEmail,
    string? Phone,
    string? ResidentialAddress,
    DateTimeOffset UpdatedAt);

public sealed record UpdateEmployeeSelfContactRequest(
    [property: EmailAddress, StringLength(320)] string? PrimaryEmail,
    [property: StringLength(32)] string? Phone,
    [property: StringLength(512)] string? ResidentialAddress);

public sealed record EmployeeProfileDocumentResponse(
    Guid Id,
    string DocumentType,
    string Label,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    bool IsComplete,
    Guid? LinkedChildId,
    DateTimeOffset UploadedAt);

public sealed record CreateEmployeeProfileDocumentUploadRequest(
    [property: Required, StringLength(64)] string DocumentType,
    [property: Required, StringLength(512)] string FileName,
    [property: Required, StringLength(128)] string ContentType,
    long ByteLength,
    [property: Required, StringLength(64, MinimumLength = 64)] string Sha256Checksum,
    Guid? LinkedChildId);

public sealed record EmployeeProfileDocumentUploadSessionResponse(
    Guid Id, Uri UploadUrl, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record InstituteResponse(Guid Id, string Code, string Name, string Kind, string? EmailDomain);

public sealed record InstituteDetailResponse(Guid Id, string Code, string Name, string Kind, string? EmailDomain, string? Address, bool IsActive);

public sealed record DivisionResponse(Guid Id, Guid InstituteId, string? Code, string Name);

public sealed record SectionResponse(Guid Id, Guid DivisionId, string? Code, string Name);

public sealed record CreateDivisionRequest(
    [property: Required, StringLength(256)] string Name,
    [property: StringLength(32)] string? Code,
    Guid? InstituteId);

public sealed record UpdateDivisionRequest(
    [property: Required, StringLength(256)] string Name,
    [property: StringLength(32)] string? Code,
    bool? IsActive);

public sealed record CreateSectionRequest(
    [property: Required] Guid DivisionId,
    [property: Required, StringLength(256)] string Name,
    [property: StringLength(32)] string? Code);

public sealed record UpdateSectionRequest(
    [property: Required, StringLength(256)] string Name,
    [property: StringLength(32)] string? Code,
    bool? IsActive);

public sealed record GradeResponse(
    Guid Id, string Code, string Name, string? StaffCategory, string? PromotionStream, short? PromotionLevel,
    bool IsPromotionGrade);

public sealed record StrategicPlanResponse(
    Guid Id, Guid InstituteId, string Code, string Name, string Definition, string Objective,
    short StartYear, short EndYear, string Status, string Etag,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateStrategicPlanRequest(
    Guid? InstituteId,
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(256)] string Name,
    [property: Required] string Definition,
    [property: Required] string Objective,
    [property: Range(2000, 3000)] short StartYear,
    [property: Range(2000, 3000)] short EndYear);

public sealed record UpdateStrategicPlanRequest(
    [property: Required, StringLength(256)] string Name,
    [property: Required] string Definition,
    [property: Required] string Objective,
    [property: Range(2000, 3000)] short StartYear,
    [property: Range(2000, 3000)] short EndYear);

public sealed record ThrustResponse(
    Guid Id, Guid StrategicPlanId, Guid InstituteId, string Code, string Title, string Description,
    string Objective, short DisplayOrder, string Status, string Etag);

public sealed record CreateThrustRequest(
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Description,
    [property: Required] string Objective,
    short DisplayOrder);

public sealed record UpdateThrustRequest(
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Description,
    [property: Required] string Objective,
    short DisplayOrder,
    [property: Required, StringLength(32)] string Status);

public sealed record OutputResponse(
    Guid Id, Guid ThrustId, string Code, string Description, Guid? OwnerUserId, DateTime? DueDate,
    string Status, short DisplayOrder, string Etag);

public sealed record CreateOutputRequest(
    [property: Required, StringLength(32)] string Code,
    [property: Required] string Description,
    Guid? OwnerUserId,
    DateTime? DueDate,
    short DisplayOrder);

public sealed record CreateRootOutputRequest(
    Guid ThrustId,
    [property: Required, StringLength(32)] string Code,
    [property: Required] string Description,
    Guid? OwnerUserId,
    DateTime? DueDate,
    short DisplayOrder);

public sealed record UpdateOutputRequest(
    [property: Required] string Description,
    Guid? OwnerUserId,
    DateTime? DueDate,
    short DisplayOrder,
    [property: Required, StringLength(32)] string Status);

public sealed record IndicatorResponse(
    Guid Id, Guid OutputId, string Code, string Description, string UnitOfMeasure,
    decimal? BaselineValue, decimal? TargetValue, string? VerificationMethod, DateTime? DueDate,
    string Status, string Etag);

public sealed record CreateIndicatorRequest(
    [property: Required, StringLength(32)] string Code,
    [property: Required] string Description,
    [property: Required, StringLength(64)] string UnitOfMeasure,
    decimal? BaselineValue,
    decimal? TargetValue,
    [property: StringLength(1000)] string? VerificationMethod,
    DateTime? DueDate);

public sealed record UpdateIndicatorRequest(
    [property: Required] string Description,
    [property: Required, StringLength(64)] string UnitOfMeasure,
    decimal? BaselineValue,
    decimal? TargetValue,
    [property: StringLength(1000)] string? VerificationMethod,
    DateTime? DueDate,
    [property: Required, StringLength(32)] string Status);

public sealed record IndicatorDataResponse(
    Guid Id, Guid IndicatorId, Guid ReportingPeriodId, decimal Value, decimal? Variance,
    string? Remarks, Guid? EvidenceFileId, Guid RecordedByUserId, string Etag);

public sealed record CreateIndicatorDataRequest(
    [property: Required] Guid ReportingPeriodId,
    decimal Value,
    [property: StringLength(2000)] string? Remarks,
    Guid? EvidenceFileId);

public sealed record UpdateIndicatorDataRequest(
    decimal Value,
    [property: StringLength(2000)] string? Remarks,
    Guid? EvidenceFileId);

public sealed record ProjectResponse(
    Guid Id, Guid InstituteId, string Code, string Name, string Objective, string? Justification,
    string? ExpectedResult, string? ActualResult, string Status, string? Nature,
    DateTime StartDate, DateTime? EndDate, string Currency, decimal? BudgetAmount,
    string? Innovation, string? Impact, Guid? LeadEmployeeId, Guid? ThrustId,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateProjectRequest(
    Guid? InstituteId,
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(256)] string Name,
    [property: Required] string Objective,
    [property: StringLength(4000)] string? Justification,
    [property: StringLength(4000)] string? ExpectedResult,
    [property: StringLength(64)] string? Nature,
    DateTime StartDate,
    DateTime? EndDate,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    decimal? BudgetAmount,
    [property: StringLength(4000)] string? Innovation,
    [property: StringLength(4000)] string? Impact,
    Guid? LeadEmployeeId,
    Guid? ThrustId);

public sealed record UpdateProjectRequest(
    [property: Required, StringLength(256)] string Name,
    [property: Required] string Objective,
    [property: StringLength(4000)] string? Justification,
    [property: StringLength(4000)] string? ExpectedResult,
    [property: StringLength(4000)] string? ActualResult,
    [property: StringLength(64)] string? Nature,
    DateTime StartDate,
    DateTime? EndDate,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    decimal? BudgetAmount,
    [property: StringLength(4000)] string? Innovation,
    [property: StringLength(4000)] string? Impact,
    Guid? LeadEmployeeId,
    Guid? ThrustId,
    [property: Required, StringLength(32)] string Status);

public sealed record ReportResponse(
    Guid Id, Guid InstituteId, Guid ReportingPeriodId, string ReportType, string Title,
    string Summary, string? Abstract, string? KeyResults, string? Conclusion, string Status,
    DateTimeOffset? SubmittedAt, DateTimeOffset? ApprovedAt, string? ReturnReason,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateReportRequest(
    Guid? InstituteId,
    [property: Required] Guid ReportingPeriodId,
    [property: Required, StringLength(64)] string ReportType,
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Summary,
    [property: StringLength(4000)] string? Abstract,
    string? KeyResults,
    [property: StringLength(4000)] string? Conclusion);

public sealed record UpdateReportRequest(
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Summary,
    [property: StringLength(4000)] string? Abstract,
    string? KeyResults,
    [property: StringLength(4000)] string? Conclusion);

public sealed record ReturnReportRequest(
    [property: Required, StringLength(2000)] string ReturnReason);

public sealed record PromotionReportSectionRequest(
    [property: Required, StringLength(64)] string Code,
    [property: StringLength(256)] string? Heading,
    JsonElement Content);

public sealed record PromotionReportContentRequest(
    [property: Range(1, int.MaxValue)] int SchemaVersion,
    [property: Required, MaxLength(100)] IReadOnlyList<PromotionReportSectionRequest> Sections);

public sealed record ReplacePromotionReportRequest(
    [property: Required, StringLength(512)] string Title,
    [property: Required] PromotionReportContentRequest Content);

public sealed record PromotionReportSectionResponse(
    string Code,
    string? Heading,
    JsonElement Content);

public sealed record PromotionReportContentResponse(
    int SchemaVersion,
    IReadOnlyList<PromotionReportSectionResponse> Sections);

public sealed record PromotionReportResponse(
    Guid Id,
    Guid PromotionSubmissionId,
    Guid RequirementSnapshotId,
    string ReportType,
    string Title,
    PromotionReportContentResponse Content,
    string Status,
    Guid? RenderedFileId,
    DateTimeOffset LastSavedAt,
    DateTimeOffset? FinalizedAt,
    string Etag,
    DateTimeOffset UpdatedAt);

public sealed record LeaveRequestResponse(
    Guid Id, Guid EmployeeId, string LeaveType, DateTime StartDate, DateTime EndDate, decimal WorkingDays,
    string Status, string CurrentApprovalStage, string? Reason, DateTimeOffset? SubmittedAt, DateTimeOffset? CompletedAt);

public sealed record CreateLeaveRequestRequest(
    Guid? EmployeeId,
    [property: Required, StringLength(32)] string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    [property: StringLength(2000)] string? Reason,
    [property: StringLength(4000)] string? HandoverNotes = null,
    Guid? DelegateEmployeeId = null,
    Guid? MedicalDocumentFileId = null,
    Guid? AdmissionLetterFileId = null,
    Guid? HandoverDocumentFileId = null);

public sealed record UpdateLeaveRequestRequest(
    [property: Required, StringLength(32)] string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    [property: StringLength(2000)] string? Reason,
    [property: StringLength(4000)] string? HandoverNotes = null,
    Guid? DelegateEmployeeId = null,
    Guid? MedicalDocumentFileId = null,
    Guid? AdmissionLetterFileId = null,
    Guid? HandoverDocumentFileId = null);

public sealed record CalculateWorkingDaysRequest(
    [property: Required, StringLength(32)] string LeaveType,
    DateTime StartDate,
    DateTime EndDate);

public sealed record WorkingDaysResponse(
    decimal WorkingDays,
    DateTime StartDate,
    DateTime EndDate,
    DateTime ExpectedReturnDate);

public sealed record LeaveDecisionRequest(
    [property: StringLength(2000)] string? Comments = null,
    [property: StringLength(256)] string? SignatureName = null);

public sealed record RejectLeaveRequest(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Reason,
    [property: StringLength(2000)] string? Comments = null,
    [property: StringLength(256)] string? SignatureName = null);

public sealed record ResumeLeaveRequest(
    DateTime? ResumptionDate = null,
    [property: StringLength(256)] string? EmployeeSignatureName = null);

public sealed record AssignAnnualLeaveRequest(
    Guid EmployeeId,
    decimal TotalDays,
    short? LeaveYear = null);

public sealed record BulkAssignAnnualLeaveRequest(
    IReadOnlyList<Guid>? EmployeeIds,
    decimal TotalDays,
    short? LeaveYear = null,
    [property: StringLength(64)] string? StaffCategory = null);

public sealed record HolidayPeriodResponse(
    Guid Id,
    string ScopeType,
    Guid? InstituteId,
    short LeaveYear,
    DateTime ChristmasStartDate,
    DateTime ChristmasEndDate,
    DateTime NewYearStartDate,
    DateTime NewYearEndDate,
    DateTime AvailabilityStartDate,
    DateTime AvailabilityEndDate,
    short DeductionDays,
    string Status,
    string? Notes,
    string Etag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateHolidayPeriodRequest(
    [property: Required, StringLength(16)] string ScopeType,
    Guid? InstituteId,
    [property: Range(2000, 3000)] short LeaveYear,
    DateTime ChristmasStartDate,
    DateTime ChristmasEndDate,
    DateTime NewYearStartDate,
    DateTime NewYearEndDate,
    DateTime AvailabilityStartDate,
    DateTime AvailabilityEndDate,
    [property: Range(0, 365)] short DeductionDays,
    [property: Required, StringLength(32)] string Status,
    [property: StringLength(2000)] string? Notes);

public sealed record UpdateHolidayPeriodRequest(
    DateTime ChristmasStartDate,
    DateTime ChristmasEndDate,
    DateTime NewYearStartDate,
    DateTime NewYearEndDate,
    DateTime AvailabilityStartDate,
    DateTime AvailabilityEndDate,
    [property: Range(0, 365)] short DeductionDays,
    [property: Required, StringLength(32)] string Status,
    [property: StringLength(2000)] string? Notes);

public sealed record SkeletalStaffApprovalResponse(
    Guid Id,
    string ApprovalStage,
    string Decision,
    string? Comments,
    DateTimeOffset DecidedAt,
    short Sequence);

public sealed record SkeletalStaffRequestResponse(
    Guid Id,
    Guid EmployeeId,
    Guid HolidayPeriodId,
    IReadOnlyList<DateTime> SelectedDates,
    DateTime SelectedStartDate,
    DateTime SelectedEndDate,
    string Status,
    string CurrentApprovalStage,
    string? SignatureName,
    string? Comment,
    string? RejectionReason,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    short? LeaveCreditYear,
    DateTimeOffset? LeaveCreditedAt,
    IReadOnlyList<SkeletalStaffApprovalResponse> Approvals,
    string Etag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateSkeletalStaffRequest(
    Guid HolidayPeriodId,
    [property: Required, MinLength(1)] IReadOnlyList<DateTime> SelectedDates,
    [property: Required, StringLength(256, MinimumLength = 1)] string SignatureName,
    [property: StringLength(2000)] string? Comment,
    bool ConfirmAvailability);

public sealed record UpdateSkeletalStaffRequest(
    [property: Required, MinLength(1)] IReadOnlyList<DateTime> SelectedDates,
    [property: Required, StringLength(256, MinimumLength = 1)] string SignatureName,
    [property: StringLength(2000)] string? Comment,
    bool ConfirmAvailability);

public sealed record SkeletalStaffDecisionRequest(
    [property: StringLength(2000)] string? Comments);

public sealed record WorkflowApprovalPreviewRequest(
    [property: Required, StringLength(512, MinimumLength = 16)] string Token);

public sealed record WorkflowApprovalPreviewResponse(
    string Purpose,
    Guid ResourceId,
    string Stage,
    string Title,
    string Summary,
    string ActionPath,
    string? Etag);

public sealed record WorkflowApprovalDecideRequest(
    [property: Required, StringLength(512, MinimumLength = 16)] string Token,
    [property: Required, StringLength(16)] string Decision,
    [property: StringLength(2000)] string? Reason,
    [property: StringLength(2000)] string? Comments,
    string? Etag);

public sealed record RejectSkeletalStaffRequest(
    [property: Required, StringLength(2000, MinimumLength = 1)] string Reason,
    [property: StringLength(2000)] string? Comments);

public sealed record CreditSkeletalStaffLeaveRequest(
    [property: Range(2000, 3000)] short LeaveYear);

public sealed record SkeletalStaffAllowanceEligibilityResponse(
    string AllowanceType,
    string Status,
    decimal? MonetaryAmount,
    string? Currency,
    decimal LeaveCreditDays,
    short? LeaveCreditYear,
    DateTimeOffset? LeaveCreditedAt,
    string Notes);

public sealed record SkeletalStaffEmployeeSummaryResponse(
    Guid Id,
    string StaffId,
    string Surname,
    string? OtherNames);

public sealed record SkeletalStaffInstituteSummaryResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record SkeletalStaffAllowanceReportResponse(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    SkeletalStaffRequestResponse Request,
    SkeletalStaffEmployeeSummaryResponse Employee,
    SkeletalStaffInstituteSummaryResponse Institute,
    HolidayPeriodResponse HolidayPeriod,
    SkeletalStaffAllowanceEligibilityResponse AllowanceEligibility,
    string MonetaryAllowanceStatus);

public sealed record LeaveTypeMetadataResponse(
    string Code,
    string Name,
    string Description,
    string Category,
    string Unit,
    LeaveEntitlementResponse Entitlement,
    LeaveDeductionResponse Deduction,
    LeaveEligibilityResponse Eligibility,
    LeaveRequestWindowResponse RequestWindow,
    IReadOnlyList<string> RequiredDocuments,
    string Approval,
    LeaveSourceClauseResponse Source,
    bool IsRequestable,
    string PolicyStatus);

public sealed record LeaveEntitlementResponse(
    decimal? MinimumDuration,
    decimal? MaximumDuration,
    string Unit,
    string? CalendarYearRule,
    string? RenewalRule,
    string? ExtensionRule,
    string? Notes);

public sealed record LeaveDeductionResponse(
    bool DeductsFromAnnualLeave,
    bool DeductsFromFutureAnnualLeave,
    bool AffectsSalary,
    string? Notes);

public sealed record LeaveEligibilityResponse(
    IReadOnlyList<string> AllowedGenders,
    int? MinimumServiceMonths,
    string? EmploymentCategoryNotes,
    IReadOnlyList<string> SpecialConstraints);

public sealed record LeaveRequestWindowResponse(
    LeaveAdvanceNoticeResponse AdvanceNotice,
    string? EarliestRequestTiming,
    string? LatestRequestTiming,
    bool CalendarYearRestricted,
    IReadOnlyList<string> SpecialTimingClauses);

public sealed record LeaveAdvanceNoticeResponse(
    decimal? MinimumDuration,
    string Unit,
    string Status,
    string Requirement,
    string? Exception);

public sealed record LeaveSourceClauseResponse(
    string DocumentTitle,
    string Chapter,
    string Section,
    IReadOnlyList<string> Clauses);

public sealed record TechnologyResponse(
    Guid Id, Guid InstituteId, string Code, string Name, string Description, string ApplicationArea,
    Guid? LeadEmployeeId, string TechnologyType, short? YearIntroduced, bool HasIntellectualProperty,
    string Status, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateTechnologyRequest(
    Guid? InstituteId,
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(512)] string Name,
    [property: Required] string Description,
    [property: Required, StringLength(256)] string ApplicationArea,
    Guid? LeadEmployeeId,
    [property: Required, StringLength(64)] string TechnologyType,
    short? YearIntroduced,
    bool HasIntellectualProperty);

public sealed record UpdateTechnologyRequest(
    [property: Required, StringLength(512)] string Name,
    [property: Required] string Description,
    [property: Required, StringLength(256)] string ApplicationArea,
    Guid? LeadEmployeeId,
    [property: Required, StringLength(64)] string TechnologyType,
    short? YearIntroduced,
    bool HasIntellectualProperty,
    [property: Required, StringLength(32)] string Status);

public sealed record MemoAudienceResponse(string AudienceType, Guid? InstituteId, Guid? DivisionId, Guid? SectionId, Guid? EmployeeId, string? RoleCode);

public sealed record MemoResponse(
    Guid Id,
    Guid InstituteId,
    string Title,
    string Body,
    string SmsSynopsis,
    string Status,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<MemoAudienceResponse> Audiences,
    string Etag);

public sealed record CreateMemoRequest(
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Body,
    IReadOnlyList<MemoAudienceInput>? Audiences);

public sealed record UpdateMemoRequest(
    [property: Required, StringLength(512)] string Title,
    [property: Required] string Body,
    IReadOnlyList<MemoAudienceInput>? Audiences);

public sealed record MemoAudienceInput(
    [property: Required, StringLength(32)] string AudienceType,
    Guid? InstituteId,
    Guid? DivisionId,
    Guid? SectionId,
    Guid? EmployeeId,
    [property: StringLength(64)] string? RoleCode);

public sealed record MemoPreviewRecipientResponse(
    Guid EmployeeId,
    string StaffId,
    string DisplayName,
    string InstituteName,
    bool InApp,
    bool Email,
    bool Sms);

public sealed record MemoPreviewResponse(
    string Title,
    string Body,
    string SmsSynopsis,
    IReadOnlyList<MemoAudienceResponse> Audiences,
    int RecipientCount,
    int InAppCount,
    int EmailCount,
    int SmsCount,
    IReadOnlyList<MemoPreviewRecipientResponse> Recipients);

public sealed record HolidayResponse(Guid Id, string ScopeType, Guid? InstituteId, string Name, DateTime HolidayDate, bool IsFullDay, bool IsIslamic, string? Notes, string Etag);

public sealed record CreateHolidayRequest(
    [property: Required, StringLength(32)] string ScopeType,
    Guid? InstituteId,
    [property: Required, StringLength(256)] string Name,
    DateTime HolidayDate,
    bool IsFullDay = true,
    bool IsIslamic = false,
    [property: StringLength(2000)] string? Notes = null);

public sealed record UpdateHolidayRequest(
    [property: Required, StringLength(256)] string Name,
    DateTime HolidayDate,
    bool IsFullDay = true,
    bool IsIslamic = false,
    [property: StringLength(2000)] string? Notes = null);

public sealed record NotificationResponse(
    Guid Id, string Title, string Body, string? ActionLink, bool IsRead, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public sealed record PromotionCycleResponse(short CycleYear, DateTime EffectivePromotionDate, string Status);

public sealed record PromotionPathResponse(
    Guid Id, string Code, string SectionReference, string StaffCategory, string PromotionStream,
    Guid SourceGradeId, Guid? TargetGradeId, short MinimumYearsInSourceGrade, string RequiredQualificationLevel,
    string Status, DateTime EffectiveFrom, DateTime? EffectiveTo);

public sealed record PromotionStatusCriterion(string Code, string Status, string? Required = null);

public sealed record PromotionGradeRef(string Code, string Name);

public sealed record PromotionNextPromotion(
    string PathCode, string PolicySection, PromotionGradeRef TargetGrade, short MinimumYearsInSourceGrade,
    DateTime? ServiceRequirementMetOn);

public sealed record PromotionStatusResponse(
    string StaffId, string StaffCategory, short CycleYear, DateTime EffectivePromotionDate, string AssessmentState,
    string? EligibilityState, string? PromotionSubmissionStatus, Guid? LatestAssessmentId,
    Guid? LatestPromotionSubmissionId, Guid? SourceGradeId, Guid? TargetGradeId, DateTimeOffset CalculatedAt,
    IReadOnlyList<PromotionStatusCriterion>? Criteria = null,
    IReadOnlyList<string>? AvailableActions = null,
    string? NextAction = null,
    PromotionGradeRef? CurrentGrade = null,
    PromotionNextPromotion? NextPromotion = null,
    string? AffectedPolicySection = null,
    DateTime? AppointmentDate = null,
    DateTime? LastPromotionDate = null,
    DateTime? SourceGradeEffectiveDate = null);

public sealed record PromotionStatusLookupRequest(
    [property: Required, StringLength(64)] string StaffId,
    [property: Required, StringLength(64)] string StaffCategory,
    short? CycleYear);

public sealed record CreatePromotionAssessmentRequest(Guid EmployeeId, Guid PromotionCycleId);

public sealed record PromotionAssessmentResponse(
    Guid Id, Guid EmployeeId, Guid PromotionCycleId, Guid PromotionPathId, Guid SourceGradeId, Guid? TargetGradeId,
    DateTime EffectivePromotionDate, DateTime? ServiceRequirementMetOn, decimal CompletedSourceGradeYears,
    string EligibilityState, DateTimeOffset? AssessedAt);
