namespace Csir.Spme.Application.Reporting;

public sealed record StaffQuarterlyPeriodOption(
    Guid Id, string Code, string Name, DateTime StartDate, DateTime EndDate, DateTime? DueDate);

public sealed record StaffQuarterlyCatalogOption(
    Guid Id, string Code, string Name, string Status, bool HasInception = false, bool AlreadyExisted = false);

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
    string? ContributionToKnowledge,
    bool HasInception,
    StaffQuarterlyFileMetadata? ConceptNote);

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

public sealed record SaveStaffQuarterlyProjectInceptionCommand(
    string Code,
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
