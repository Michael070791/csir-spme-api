namespace Csir.Spme.Api.Endpoints.V2;

public sealed record CreateAppraisalCycleRequest(
    string Name, short Year, DateTime StartDate, DateTime EndDate,
    DateTime PlanningStart, DateTime PlanningEnd, DateTime MidyearStart, DateTime MidyearEnd,
    DateTime YearEndStart, DateTime YearEndEnd);
public sealed record UpdateAppraisalCycleRequest(
    string Name, DateTime StartDate, DateTime EndDate,
    DateTime PlanningStart, DateTime PlanningEnd, DateTime MidyearStart, DateTime MidyearEnd,
    DateTime YearEndStart, DateTime YearEndEnd);
public sealed record ReopenAppraisalCycleRequest(string Reason);
public sealed record AppraisalCycleResponse(
    Guid Id, Guid InstituteId, string Name, short Year, DateTime StartDate, DateTime EndDate,
    DateTime PlanningStart, DateTime PlanningEnd, DateTime MidyearStart, DateTime MidyearEnd,
    DateTime YearEndStart, DateTime YearEndEnd, string Status, string? ReopenReason,
    string FormTemplateVersion, string FormTemplateChecksum, string Etag,
    IReadOnlyList<string> AvailableActions, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AppraisalAssignmentCandidateResponse(
    Guid UserId, Guid? EmployeeId, string DisplayName, IReadOnlyList<string> EligibleRoles);

public sealed record AssignAppraisalRequest(
    Guid EmployeeId, Guid? HodUserId, Guid? DirectorUserId, string? RoutingExceptionReason);
public sealed record UpdateAppraisalAssignmentRequest(
    Guid? HodUserId, Guid? DirectorUserId, string? RoutingExceptionReason);

public sealed record AppraisalEmployeeSnapshot(
    string? Title, string Surname, string? FirstName, string? OtherNames, string? PresentGrade,
    string? SalaryGradeStep, DateTime? DateOfPresentGrade, string Institute, string? DivisionUnit,
    DateTime? DateOfFirstAppointment);
public sealed record AppraisalAppraiserSnapshot(
    string? Title, string Surname, string? FirstName, string? OtherNames, string? PositionOfAppraiser);
public sealed record AppraisalTrainingEntry(string Institution, DateTime? Date, string Programme);
public sealed record AppraisalTargetInput(
    Guid? Id, string CoreArea, string Target, string ResourcesRequired, string? Timeline);
public sealed record SaveAppraisalPlanningRequest(
    IReadOnlyList<AppraisalTrainingEntry> TrainingReceived,
    IReadOnlyList<AppraisalTargetInput> Targets,
    IReadOnlyList<string> KeyCompetencies);
public sealed record AppraisalTargetReview(Guid TargetId, string ProgressReview);
public sealed record AppraisalCompetencyReview(string Competency, string ProgressReview);
public sealed record SaveAppraisalMidyearRequest(
    IReadOnlyList<AppraisalTargetReview> TargetReviews,
    IReadOnlyList<AppraisalCompetencyReview> CompetencyReviews,
    string? TrainingNeed);
public sealed record AppraisalTargetRemark(Guid TargetId, string? Remarks);
public sealed record AppraisalCompetencyRemark(string Competency, string? Remarks);
public sealed record AppraisalTargetAmendmentInput(
    Guid TargetId, string RevisedTarget, string RevisedResourcesRequired, string? RevisedTimeline, string Reason);
public sealed record SaveHodMidyearReviewRequest(
    IReadOnlyList<AppraisalTargetRemark> TargetRemarks,
    IReadOnlyList<AppraisalCompetencyRemark> CompetencyRemarks,
    string? TrainingNeedComment,
    IReadOnlyList<AppraisalTargetAmendmentInput> TargetAmendments,
    string? ResponseToDecline);
public sealed record AppraisalTargetResult(
    Guid TargetId, string WorkAccomplished, short WorkCompletedPercentage, string ExtentAndConstraints);
public sealed record SaveAppraisalYearEndRequest(IReadOnlyList<AppraisalTargetResult> TargetResults);
public sealed record AppraisalTargetAssessment(Guid TargetId, short Rating, string? Comments);
public sealed record AppraisalCompetencyRating(string Code, short? Rating);
public sealed record SaveHodAppraisalAssessmentRequest(
    IReadOnlyList<AppraisalTargetAssessment> TargetAssessments,
    IReadOnlyList<AppraisalCompetencyRating> CompetencyRatings,
    string SupervisorComments);
public sealed record AppraisalStaffSignatureRequest(bool Accepted, string? Comments, string? DeclineReason);
public sealed record AppraisalAttestationRequest(bool Attested);
public sealed record AppraisalHodSubmissionRequest(string? ResponseToDecline);
public sealed record AppraisalDirectorReturnRequest(string Reason);
public sealed record AppraisalMidyearDirectorApprovalRequest(string CommentsOnProgress);
public sealed record AppraisalDirectorApprovalRequest(
    string CommentsOnWork, string? ConsiderPromotionTo, string? PerformanceBonus, string? Training,
    string? Reassignment, string? ReprimandOrCaution, string? TerminationOfAppointment);

public sealed record AppraisalScoreResponse(
    decimal? BehavioralScore, decimal? CoreScore, decimal? TotalPercentage, string? OverallBand);
public sealed record AppraisalSignatureAttemptResponse(
    string Phase, short Attempt, bool Accepted, string? Comments, string? DeclineReason,
    DateTimeOffset RecordedAt);
public sealed record AppraisalCompletenessResponse(bool IsComplete, IReadOnlyList<string> MissingFields);
public sealed record AppraisalHistoryResponse(
    string Action, string Stage, DateTimeOffset OccurredAt, string? Detail);
public sealed record AppraisalMidyearDirectorDecisionResponse(
    string Decision, string CommentsOnProgress, string? ReturnReason, DateTimeOffset DecidedAt);
public sealed record AppraisalSummaryResponse(
    Guid Id, Guid CycleId, string CycleName, short CycleYear, DateTime CycleStartDate, DateTime CycleEndDate,
    Guid EmployeeId, string EmployeeDisplayName, string StaffId,
    Guid InstituteId, Guid? HodUserId, string? HodDisplayName, Guid? DirectorUserId, string? DirectorDisplayName,
    string Status, string CurrentStage, bool IsRoutingException, string? RoutingExceptionReason,
    bool HasSignatureDisagreement, int SignatureAttemptCount, bool FinalDocumentAvailable,
    decimal? TotalScore, string? OverallBand, IReadOnlyList<string> AvailableActions, string Etag,
    DateTime CurrentStageDeadline, DateTimeOffset UpdatedAt);
public sealed record PerformanceAppraisalResponse(
    AppraisalSummaryResponse Summary,
    AppraisalEmployeeSnapshot Employee,
    AppraisalAppraiserSnapshot Appraiser,
    AppraisalAppraiserSnapshot Approver,
    SaveAppraisalPlanningRequest? Planning,
    SaveAppraisalMidyearRequest? Midyear,
    SaveHodMidyearReviewRequest? HodMidyearReview,
    SaveAppraisalYearEndRequest? YearEnd,
    SaveHodAppraisalAssessmentRequest? HodAssessment,
    IReadOnlyList<AppraisalSignatureAttemptResponse> StaffSignatureAttempts,
    AppraisalMidyearDirectorDecisionResponse? MidyearDirectorDecision,
    AppraisalDirectorApprovalRequest? DirectorAssessment,
    AppraisalScoreResponse Scores,
    IReadOnlyList<AppraisalFactorResponse> BehavioralFactors,
    IReadOnlyList<AppraisalFactorResponse> CoreFactors,
    IReadOnlyList<AppraisalRatingGuidanceResponse> BehavioralRatingGuidance,
    IReadOnlyList<AppraisalRatingGuidanceResponse> CoreRatingGuidance,
    AppraisalCompletenessResponse Completeness,
    IReadOnlyList<AppraisalHistoryResponse> History);
public sealed record AppraisalFactorResponse(string Code, string Label);
public sealed record AppraisalRatingGuidanceResponse(short Rating, string Label, string Explanation);
public sealed record AppraisalCycleMetricsResponse(
    Guid CycleId, int Total, IReadOnlyDictionary<string, int> CountsByStatus, int Overdue,
    int SignatureDisagreements, int Approved, decimal CompletionPercentage);
public sealed record AppraisalReminderRunResponse(Guid CycleId, int Processed, int Staged);
