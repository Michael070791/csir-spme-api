namespace Csir.Spme.Application.Reporting;

public sealed record StaffQuarterlyPeriodOption(
    Guid Id, string Code, string Name, DateTime StartDate, DateTime EndDate, DateTime? DueDate);

public sealed record StaffQuarterlyCatalogOption(
    Guid Id,
    string Code,
    string Name,
    string Status,
    bool HasInception = false,
    bool AlreadyExisted = false,
    string? Pin = null,
    string PinStatus = "pending");

public sealed record StaffQuarterlyFormOneSummary(
    Guid Id,
    string Name,
    bool HasInception,
    bool IsComplete,
    string? Pin,
    string PinStatus,
    DateTimeOffset? PinAssignedAt);

public sealed record StaffQuarterlyReviewerOption(
    Guid UserId, Guid EmployeeId, string DisplayName, string Role, string Email, string Phone);

public sealed record StaffQuarterlyReportOptions(
    IReadOnlyList<StaffQuarterlyPeriodOption> ReportingPeriods,
    IReadOnlyList<StaffQuarterlyCatalogOption> Projects,
    IReadOnlyList<StaffQuarterlyCatalogOption> Technologies,
    IReadOnlyList<StaffQuarterlyReviewerOption> Reviewers);

public sealed record StaffQuarterlyReportReference(Guid Id, string Code, string Name);

public sealed record StaffQuarterlyReportPeriod(
    Guid Id, string Code, string Name, DateTime StartDate, DateTime EndDate, DateTime? DueDate);

public sealed record StaffQuarterlyReportOwner(Guid EmployeeId, string StaffId, string DisplayName);

public sealed record StaffQuarterlyReportReviewer(
    Guid UserId, Guid EmployeeId, string DisplayName, string Role, string? Email = null, string? Phone = null);

public sealed record StaffQuarterlyFileMetadata(
    Guid FileId, string FileName, string ContentType, long SizeBytes, string ScanStatus);

public sealed record StaffQuarterlyProjectInceptionResponse(
    Guid ProjectId,
    string Code,
    string Name,
    string Objective,
    string? Justification,
    string? Method,
    string Nature,
    DateTime StartDate,
    DateTime? EndDate,
    string Currency,
    decimal? BudgetAmount,
    Guid LeadEmployeeId,
    string LeadDisplayName,
    string EstimatedDuration,
    string SponsorName,
    string Location,
    string? CollaboratingInstitute,
    string? ParticipatingScientists,
    string? ExpectedBeneficiaries,
    string? PotentialTechnology,
    string? Commercialization,
    string? ContributionToKnowledge,
    string? Pin,
    string PinStatus,
    DateTimeOffset? PinAssignedAt,
    string? InstituteName,
    bool HasInception,
    StaffQuarterlyFileMetadata? ConceptNote,
    string Etag);

public sealed record StaffQuarterlyProjectProgressResponse(
    Guid ProjectId,
    string Code,
    string Name,
    bool HasInception,
    StaffQuarterlyProjectInceptionResponse? Inception,
    string? ProgressSummary,
    string? ProgressKeyResults,
    string? Challenges,
    string? NextQuarterActivities,
    string? WayForward,
    int ConferencePapersProduced,
    int IpTechnologiesProtected);

public sealed record StaffQuarterlyReportResponse(
    Guid Id,
    StaffQuarterlyReportPeriod ReportingPeriod,
    StaffQuarterlyReportOwner Owner,
    StaffQuarterlyReportReviewer Reviewer,
    string Title,
    string? Abstract,
    string WorkSummary,
    string? KeyResults,
    string? ConclusionNextSteps,
    string Status,
    IReadOnlyList<StaffQuarterlyReportReference> Projects,
    IReadOnlyList<StaffQuarterlyReportReference> Technologies,
    IReadOnlyList<StaffQuarterlyProjectProgressResponse> ProjectProgress,
    IReadOnlyList<StaffQuarterlyFileMetadata> Images,
    IReadOnlyList<string> AvailableActions,
    string? ReturnReason,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Etag);

public sealed record SaveStaffQuarterlyProjectProgressCommand(
    Guid ProjectId,
    string ProgressSummary,
    string? ProgressKeyResults,
    string? Challenges,
    string? NextQuarterActivities,
    string? WayForward,
    int ConferencePapersProduced,
    int IpTechnologiesProtected);

public sealed record SaveStaffQuarterlyReportCommand(
    Guid ReportingPeriodId,
    Guid ReviewerUserId,
    string Title,
    string? Abstract,
    string WorkSummary,
    string? KeyResults,
    string? ConclusionNextSteps,
    IReadOnlyList<Guid> ProjectIds,
    IReadOnlyList<Guid> TechnologyIds,
    IReadOnlyList<SaveStaffQuarterlyProjectProgressCommand> ProjectProgress);

public sealed record AssignProjectPinCommand(string Pin);

public sealed record StaffQuarterlyCollationProjectRow(
    Guid ProjectId,
    string Code,
    string Name,
    string? Pin,
    string PinStatus,
    string? ProgressSummary,
    string? ProgressKeyResults,
    string? Challenges,
    string? NextQuarterActivities,
    string? WayForward,
    int ConferencePapersProduced,
    int IpTechnologiesProtected);

public sealed record StaffQuarterlyCollationEntry(
    Guid ReportId,
    StaffQuarterlyReportPeriod ReportingPeriod,
    StaffQuarterlyReportOwner Owner,
    string Title,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    IReadOnlyList<StaffQuarterlyCollationProjectRow> Projects);

public sealed record SaveStaffQuarterlyProjectInceptionCommand(
    string Name,
    string Objective,
    string Justification,
    string Method,
    string Nature,
    DateTime StartDate,
    DateTime? EndDate,
    decimal BudgetAmount,
    string Currency,
    Guid LeadEmployeeId,
    string EstimatedDuration,
    string SponsorName,
    string Location,
    string? CollaboratingInstitute,
    string? ParticipatingScientists,
    string? ExpectedBeneficiaries,
    string? PotentialTechnology,
    string? Commercialization,
    string? ContributionToKnowledge,
    bool CompleteInception);

public sealed record CreateStaffQuarterlyProjectDraftCommand(
    SaveStaffQuarterlyProjectInceptionCommand Inception);

public sealed record CreateStaffQuarterlyTechnologyDraftCommand(
    string Code, string Name, string Description, string ApplicationArea, string TechnologyType,
    short? YearIntroduced, bool HasIntellectualProperty);

public sealed record CreateStaffQuarterlyUploadSessionCommand(
    string FileName,
    string ContentType,
    long ByteLength,
    string Sha256Checksum);

public sealed record StaffQuarterlyUploadSessionResponse(
    Guid Id, Uri UploadUrl, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string>? RequiredHeaders);
