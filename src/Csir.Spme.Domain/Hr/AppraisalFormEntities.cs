using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public sealed class AppraisalTrainingRecord : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Institution { get; private set; } = string.Empty;
    public DateTime TrainingDate { get; private set; }
    public string Programme { get; private set; } = string.Empty;
    private AppraisalTrainingRecord() { }
    public AppraisalTrainingRecord(Guid appraisalId, string institution, DateTime date, string programme)
    { PerformanceAppraisalId = appraisalId; Institution = institution.Trim(); TrainingDate = date.Date; Programme = programme.Trim(); }
}

public sealed class AppraisalTarget : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public short DisplayOrder { get; private set; }
    public string CoreArea { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string ResourcesRequired { get; private set; } = string.Empty;
    public string? Timeline { get; private set; }
    private AppraisalTarget() { }
    public AppraisalTarget(Guid appraisalId, short order, string coreArea, string target, string resources, string? timeline)
    { PerformanceAppraisalId = appraisalId; DisplayOrder = order; CoreArea = coreArea.Trim(); Target = target.Trim(); ResourcesRequired = resources.Trim(); Timeline = Normalize(timeline); }
    public void Update(short order, string coreArea, string target, string resources, string? timeline)
    { DisplayOrder = order; CoreArea = coreArea.Trim(); Target = target.Trim(); ResourcesRequired = resources.Trim(); Timeline = Normalize(timeline); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalTargetVersion : BaseEntity
{
    public Guid AppraisalTargetId { get; private set; }
    public short Version { get; private set; }
    public string CoreArea { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string ResourcesRequired { get; private set; } = string.Empty;
    public string? Timeline { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    private AppraisalTargetVersion() { }
    public AppraisalTargetVersion(Guid targetId, short version, string coreArea, string target,
        string resources, string? timeline, DateTimeOffset capturedAt)
    { AppraisalTargetId = targetId; Version = version; CoreArea = coreArea; Target = target;
      ResourcesRequired = resources; Timeline = timeline; CapturedAt = capturedAt; }
}

public sealed class AppraisalKeyCompetency : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public short DisplayOrder { get; private set; }
    public string Competency { get; private set; } = string.Empty;
    private AppraisalKeyCompetency() { }
    public AppraisalKeyCompetency(Guid appraisalId, short order, string competency)
    { PerformanceAppraisalId = appraisalId; DisplayOrder = order; Competency = competency.Trim(); }
}

public sealed class AppraisalMidyearTargetReview : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public Guid AppraisalTargetId { get; private set; }
    public string ProgressReview { get; private set; } = string.Empty;
    public string? Remarks { get; private set; }
    private AppraisalMidyearTargetReview() { }
    public AppraisalMidyearTargetReview(Guid appraisalId, Guid targetId, string review, string? remarks)
    { PerformanceAppraisalId = appraisalId; AppraisalTargetId = targetId; ProgressReview = review.Trim(); Remarks = Normalize(remarks); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalMidyearCompetencyReview : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Competency { get; private set; } = string.Empty;
    public string ProgressReview { get; private set; } = string.Empty;
    public string? Remarks { get; private set; }
    private AppraisalMidyearCompetencyReview() { }
    public AppraisalMidyearCompetencyReview(Guid appraisalId, string competency, string review, string? remarks)
    { PerformanceAppraisalId = appraisalId; Competency = competency.Trim(); ProgressReview = review.Trim(); Remarks = Normalize(remarks); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalYearEndResult : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public Guid AppraisalTargetId { get; private set; }
    public string WorkAccomplished { get; private set; } = string.Empty;
    public short WorkCompletedPercentage { get; private set; }
    public string ExtentAndConstraints { get; private set; } = string.Empty;
    private AppraisalYearEndResult() { }
    public AppraisalYearEndResult(Guid appraisalId, Guid targetId, string work, short workCompletedPercentage, string extent)
    { PerformanceAppraisalId = appraisalId; AppraisalTargetId = targetId; WorkAccomplished = work.Trim(); WorkCompletedPercentage = workCompletedPercentage; ExtentAndConstraints = extent.Trim(); }
}

public sealed class AppraisalHodSubmission : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public short Version { get; private set; }
    public Guid HodUserId { get; private set; }
    public string? ResponseToDecline { get; private set; }
    public string? SupervisorComments { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    private AppraisalHodSubmission() { }
    public AppraisalHodSubmission(Guid appraisalId, string phase, short version, Guid hodUserId,
        string? responseToDecline, string? comments, DateTimeOffset submittedAt)
    { PerformanceAppraisalId = appraisalId; Phase = phase; Version = version; HodUserId = hodUserId;
      ResponseToDecline = Normalize(responseToDecline); SupervisorComments = Normalize(comments); SubmittedAt = submittedAt; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalMidyearTargetRemark : BaseEntity
{
    public Guid HodSubmissionId { get; private set; }
    public Guid AppraisalTargetId { get; private set; }
    public string? Remarks { get; private set; }
    private AppraisalMidyearTargetRemark() { }
    public AppraisalMidyearTargetRemark(Guid submissionId, Guid targetId, string? remarks)
    { HodSubmissionId = submissionId; AppraisalTargetId = targetId; Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(); }
}

public sealed class AppraisalMidyearCompetencyRemark : BaseEntity
{
    public Guid HodSubmissionId { get; private set; }
    public string Competency { get; private set; } = string.Empty;
    public string? Remarks { get; private set; }
    private AppraisalMidyearCompetencyRemark() { }
    public AppraisalMidyearCompetencyRemark(Guid submissionId, string competency, string? remarks)
    { HodSubmissionId = submissionId; Competency = competency.Trim(); Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(); }
}

public sealed class AppraisalTargetAmendment : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public Guid AppraisalTargetId { get; private set; }
    public short Version { get; private set; }
    public string OriginalTarget { get; private set; } = string.Empty;
    public string OriginalResourcesRequired { get; private set; } = string.Empty;
    public string? OriginalTimeline { get; private set; }
    public string RevisedTarget { get; private set; } = string.Empty;
    public string RevisedResourcesRequired { get; private set; } = string.Empty;
    public string? RevisedTimeline { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Status { get; private set; } = "proposed";
    public DateTimeOffset ProposedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    private AppraisalTargetAmendment() { }
    public AppraisalTargetAmendment(Guid appraisalId, AppraisalTarget target, short version,
        string revisedTarget, string revisedResources, string? revisedTimeline, string reason, DateTimeOffset proposedAt)
    { PerformanceAppraisalId = appraisalId; AppraisalTargetId = target.Id; Version = version;
      OriginalTarget = target.Target; OriginalResourcesRequired = target.ResourcesRequired; OriginalTimeline = target.Timeline;
      RevisedTarget = revisedTarget.Trim(); RevisedResourcesRequired = revisedResources.Trim();
      RevisedTimeline = Normalize(revisedTimeline); Reason = reason.Trim(); ProposedAt = proposedAt; }
    public void Accept(AppraisalTarget target, DateTimeOffset acceptedAt)
    { Status = "accepted"; AcceptedAt = acceptedAt; target.Update(target.DisplayOrder, target.CoreArea, RevisedTarget, RevisedResourcesRequired, RevisedTimeline); }
    public void Supersede(DateTimeOffset supersededAt)
    { Status = "superseded"; AcceptedAt = supersededAt; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalTargetAssessmentRecord : BaseEntity
{
    public Guid HodSubmissionId { get; private set; }
    public Guid AppraisalTargetId { get; private set; }
    public short Rating { get; private set; }
    public string? Comments { get; private set; }
    private AppraisalTargetAssessmentRecord() { }
    public AppraisalTargetAssessmentRecord(Guid submissionId, Guid targetId, short rating, string? comments)
    { HodSubmissionId = submissionId; AppraisalTargetId = targetId; Rating = rating; Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(); }
}

public sealed class AppraisalCompetencyRatingRecord : BaseEntity
{
    public Guid HodSubmissionId { get; private set; }
    public string FactorCode { get; private set; } = string.Empty;
    public short? Rating { get; private set; }
    private AppraisalCompetencyRatingRecord() { }
    public AppraisalCompetencyRatingRecord(Guid submissionId, string code, short? rating)
    { HodSubmissionId = submissionId; FactorCode = code; Rating = rating; }
}

public sealed class AppraisalSignatureRecord : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public short Attempt { get; private set; }
    public bool Accepted { get; private set; }
    public string? Comments { get; private set; }
    public string? DeclineReason { get; private set; }
    public Guid EmployeeUserId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    private AppraisalSignatureRecord() { }
    public AppraisalSignatureRecord(Guid appraisalId, string phase, short attempt, bool accepted,
        string? comments, string? declineReason, Guid userId, DateTimeOffset recordedAt)
    { PerformanceAppraisalId = appraisalId; Phase = phase; Attempt = attempt; Accepted = accepted;
      Comments = Normalize(comments); DeclineReason = Normalize(declineReason); EmployeeUserId = userId; RecordedAt = recordedAt; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalDirectorDecision : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public short Version { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public Guid DirectorUserId { get; private set; }
    public string CommentsOnWork { get; private set; } = string.Empty;
    public string? ReturnReason { get; private set; }
    public string? RecommendationsJson { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    private AppraisalDirectorDecision() { }
    public AppraisalDirectorDecision(Guid appraisalId, string phase, short version, string decision,
        Guid directorUserId, string comments, string? reason, string? recommendationsJson, DateTimeOffset decidedAt)
    { PerformanceAppraisalId = appraisalId; Phase = phase; Version = version; Decision = decision;
      DirectorUserId = directorUserId; CommentsOnWork = comments.Trim(); ReturnReason = Normalize(reason);
      RecommendationsJson = recommendationsJson; DecidedAt = decidedAt; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AppraisalReminderRecord : BaseEntity
{
    public Guid PerformanceAppraisalId { get; private set; }
    public string Stage { get; private set; } = string.Empty;
    public string OffsetCode { get; private set; } = string.Empty;
    public DateTimeOffset StagedAt { get; private set; }
    private AppraisalReminderRecord() { }
    public AppraisalReminderRecord(Guid appraisalId, string stage, string offsetCode, DateTimeOffset stagedAt)
    { PerformanceAppraisalId = appraisalId; Stage = stage; OffsetCode = offsetCode; StagedAt = stagedAt; }
}
