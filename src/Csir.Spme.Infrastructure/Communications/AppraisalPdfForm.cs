namespace Csir.Spme.Infrastructure.Communications;

public sealed record AppraisalPdfForm(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    AppraisalPdfEmployee Employee,
    AppraisalPdfAppraiser Appraiser,
    IReadOnlyList<AppraisalPdfTraining> TrainingReceived,
    IReadOnlyList<AppraisalPdfTarget> Targets,
    IReadOnlyList<string> KeyCompetencies,
    IReadOnlyList<AppraisalPdfCompetencyProgress> CompetencyProgress,
    string? TrainingNeed,
    IReadOnlyList<AppraisalPdfCompetencyRating> CompetencyRatings,
    decimal? BehavioralScore,
    decimal? CoreScore,
    decimal? TotalScore,
    string? SupervisorComments,
    string? EmployeeComments,
    AppraisalPdfDirectorAssessment DirectorAssessment,
    AppraisalPdfSignature PlanningEmployeeSignature,
    AppraisalPdfSignature PlanningSupervisorSignature,
    AppraisalPdfSignature MidyearEmployeeSignature,
    AppraisalPdfSignature MidyearSupervisorSignature,
    AppraisalPdfSignature YearEndEmployeeSubmissionSignature,
    AppraisalPdfSignature YearEndSupervisorSignature,
    AppraisalPdfSignature YearEndEmployeeSignature,
    AppraisalPdfSignature DirectorSignature);

public sealed record AppraisalPdfEmployee(
    string? Title,
    string Surname,
    string? FirstName,
    string? OtherNames,
    string? PresentGrade,
    string? SalaryGradeStep,
    DateTime? DateOfPresentGrade,
    string Institute,
    string? DivisionUnit,
    DateTime? DateOfFirstAppointment);

public sealed record AppraisalPdfAppraiser(
    string? Title,
    string Surname,
    string? FirstName,
    string? OtherNames,
    string? Position);

public sealed record AppraisalPdfTraining(string Institution, DateTime? Date, string Programme);

public sealed record AppraisalPdfTarget(
    Guid Id,
    short DisplayOrder,
    string CoreArea,
    string Target,
    string ResourcesRequired,
    string? Timeline,
    string? MidyearProgress,
    string? MidyearRemarks,
    string? WorkAccomplished,
    short? WorkCompletedPercentage,
    string? ExtentAndConstraints,
    short? PerformanceAssessment,
    string? PerformanceComments);

public sealed record AppraisalPdfCompetencyProgress(
    short DisplayOrder,
    string Competency,
    string? ProgressReview,
    string? Remarks);

public sealed record AppraisalPdfCompetencyRating(string Code, short? Rating);

public sealed record AppraisalPdfDirectorAssessment(
    string CommentsOnWork,
    string? ConsiderPromotionTo,
    string? PerformanceBonus,
    string? Training,
    string? Reassignment,
    string? ReprimandOrCaution,
    string? TerminationOfAppointment);

public sealed record AppraisalPdfSignature(string Name, DateTimeOffset? SignedAt);
