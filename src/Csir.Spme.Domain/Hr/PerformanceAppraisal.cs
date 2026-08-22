using System.Text.Json;
using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public sealed class PerformanceAppraisal : InstituteScopedEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public Guid EmployeeId { get; private set; }
    public Guid AppraisalCycleId { get; private set; }
    public Guid? HodUserId { get; private set; }
    public Guid? DirectorUserId { get; private set; }
    public DateTime AppraisalPeriodStart { get; private set; }
    public DateTime AppraisalPeriodEnd { get; private set; }
    public string Status { get; private set; } = AppraisalStatuses.Planning;
    public string Outcome { get; private set; } = string.Empty;
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? SourceFileId { get; private set; }
    public Guid? FinalDocumentFileId { get; private set; }
    public string? Comments { get; private set; }
    public string? RoutingExceptionReason { get; private set; }
    public string EmployeeSnapshotJson { get; private set; } = "{}";
    public string AppraiserSnapshotJson { get; private set; } = "{}";
    public string ApproverSnapshotJson { get; private set; } = "{}";
    public string PlanningJson { get; private set; } = "{}";
    public string MidyearJson { get; private set; } = "{}";
    public string HodMidyearReviewJson { get; private set; } = "{}";
    public string YearEndJson { get; private set; } = "{}";
    public string HodAssessmentJson { get; private set; } = "{}";
    public string StaffSignatureAttemptsJson { get; private set; } = "[]";
    public string DirectorAssessmentJson { get; private set; } = "{}";
    public decimal? BehavioralScore { get; private set; }
    public decimal? CoreScore { get; private set; }
    public decimal? TotalScore { get; private set; }
    private PerformanceAppraisal() { }

    public static PerformanceAppraisal Assign(Guid instituteId, Guid employeeId, AppraisalCycle cycle,
        Guid? hodUserId, Guid? directorUserId, object employeeSnapshot, object appraiserSnapshot, object approverSnapshot,
        string? routingExceptionReason) => new()
    {
        InstituteId = instituteId, EmployeeId = employeeId, AppraisalCycleId = cycle.Id,
        HodUserId = hodUserId, DirectorUserId = directorUserId,
        AppraisalPeriodStart = cycle.StartDate, AppraisalPeriodEnd = cycle.EndDate,
        EmployeeSnapshotJson = JsonSerializer.Serialize(employeeSnapshot, JsonOptions),
        AppraiserSnapshotJson = JsonSerializer.Serialize(appraiserSnapshot, JsonOptions),
        ApproverSnapshotJson = JsonSerializer.Serialize(approverSnapshot, JsonOptions),
        RoutingExceptionReason = Normalize(routingExceptionReason)
    };

    public Result<bool> UpdateRouting(Guid? hodUserId, Guid? directorUserId, object appraiserSnapshot,
        object approverSnapshot, string? reason)
    {
        if (Status == AppraisalStatuses.Approved)
            return Result.Failure(Error.StateTransition("An approved appraisal cannot be rerouted."));
        if (hodUserId.HasValue && hodUserId == directorUserId)
            return Result.Failure(Error.Validation("The HOD and Director must be distinct users."));
        if ((!hodUserId.HasValue || !directorUserId.HasValue) && string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.Validation("An unresolved routing exception requires a reason."));
        HodUserId = hodUserId; DirectorUserId = directorUserId;
        AppraiserSnapshotJson = JsonSerializer.Serialize(appraiserSnapshot, JsonOptions);
        ApproverSnapshotJson = JsonSerializer.Serialize(approverSnapshot, JsonOptions);
        RoutingExceptionReason = Normalize(reason);
        return Result.Success();
    }

    public Result<bool> SavePlanning(object value) => SaveAt(AppraisalStatuses.Planning, value, json => PlanningJson = json);
    public Result<bool> SaveMidyear(object value) => SaveAt(AppraisalStatuses.Midyear, value, json => MidyearJson = json);
    public Result<bool> SaveHodMidyearReview(object value) => SaveAt(AppraisalStatuses.MidyearReview, value, json => HodMidyearReviewJson = json);
    public Result<bool> SaveYearEnd(object value) => SaveAt(AppraisalStatuses.YearEnd, value, json => YearEndJson = json);
    public Result<bool> SaveHodAssessment(object value, decimal? behavioral, decimal? core)
    {
        var saved = SaveAt(AppraisalStatuses.HodAssessment, value, json => HodAssessmentJson = json);
        if (saved.IsFailure) return saved;
        BehavioralScore = behavioral; CoreScore = core;
        TotalScore = behavioral.HasValue && core.HasValue ? behavioral.Value + core.Value : null;
        return Result.Success();
    }

    public bool RoutingResolved => HodUserId.HasValue && DirectorUserId.HasValue && HodUserId != DirectorUserId;
    public Result<bool> SubmitPlanning() => RoutingResolved
        ? Move(AppraisalStatuses.Planning, AppraisalStatuses.PlanningReview)
        : Result.Failure(Error.Conflict("The appraisal assignment routing must be resolved before submission."));
    public Result<bool> ReturnPlanning() => Move(AppraisalStatuses.PlanningReview, AppraisalStatuses.Planning);
    public Result<bool> ConfirmPlanning() => Move(AppraisalStatuses.PlanningReview, AppraisalStatuses.Midyear);
    public Result<bool> SubmitMidyear() => Move(AppraisalStatuses.Midyear, AppraisalStatuses.MidyearReview);
    public Result<bool> SubmitMidyearReview(string? responseToDecline)
    {
        if (HasDeclinedSignature(AppraisalPhases.Midyear) && string.IsNullOrWhiteSpace(responseToDecline))
            return Result.Failure(Error.Validation("The HOD response to the employee's declined signature is required."));
        return Move(AppraisalStatuses.MidyearReview, AppraisalStatuses.MidyearStaffSignature);
    }
    public Result<bool> ApproveMidyearByDirector() => Move(AppraisalStatuses.MidyearDirectorReview, AppraisalStatuses.YearEnd);
    public Result<bool> ReturnMidyearByDirector() => Move(AppraisalStatuses.MidyearDirectorReview, AppraisalStatuses.MidyearReview);
    public Result<bool> SubmitYearEnd() => Move(AppraisalStatuses.YearEnd, AppraisalStatuses.HodAssessment);
    public Result<bool> SubmitHodAssessment(string? responseToDecline)
    {
        if (HasDeclinedSignature(AppraisalPhases.YearEnd) && string.IsNullOrWhiteSpace(responseToDecline))
            return Result.Failure(Error.Validation("The HOD response to the employee's declined signature is required."));
        return Move(AppraisalStatuses.HodAssessment, AppraisalStatuses.StaffSignature);
    }

    public Result<bool> RecordStaffSignature(string phase, bool accepted, string? comments, string? declineReason, DateTimeOffset now)
    {
        var expected = phase == AppraisalPhases.Midyear ? AppraisalStatuses.MidyearStaffSignature : AppraisalStatuses.StaffSignature;
        if (Status != expected)
            return Result.Failure(Error.StateTransition("The appraisal is not awaiting the employee signature."));
        if (!accepted && string.IsNullOrWhiteSpace(declineReason))
            return Result.Failure(Error.Validation("A decline reason is required when the employee does not sign."));
        if (phase == AppraisalPhases.YearEnd && accepted && string.IsNullOrWhiteSpace(comments))
            return Result.Failure(Error.Validation("Enter comments or 'No comment' before signing the supervisor's assessment."));
        var attempts = Read<List<AppraisalSignatureAttempt>>(StaffSignatureAttemptsJson) ?? [];
        attempts.Add(new(phase, accepted, Normalize(comments), Normalize(declineReason), now));
        StaffSignatureAttemptsJson = JsonSerializer.Serialize(attempts, JsonOptions);
        Status = phase == AppraisalPhases.Midyear
            ? accepted ? AppraisalStatuses.MidyearDirectorReview : AppraisalStatuses.MidyearReview
            : accepted ? AppraisalStatuses.DirectorReview : AppraisalStatuses.HodAssessment;
        return Result.Success();
    }

    public Result<bool> ReturnByDirector() => Move(AppraisalStatuses.DirectorReview, AppraisalStatuses.HodAssessment);

    public Result<bool> ApproveByDirector(object assessment, Guid directorUserId, Guid finalDocumentFileId, DateTimeOffset now)
    {
        if (Status != AppraisalStatuses.DirectorReview || directorUserId != DirectorUserId || !TotalScore.HasValue)
            return Result.Failure(Error.StateTransition("The appraisal is not ready for its assigned Director's approval."));
        DirectorAssessmentJson = JsonSerializer.Serialize(assessment, JsonOptions);
        FinalDocumentFileId = finalDocumentFileId;
        Outcome = TotalScore.Value >= 50m ? "satisfactory" : "unsatisfactory";
        Status = AppraisalStatuses.Approved; ApprovedByUserId = directorUserId;
        ApprovedAt = now; CompletedAt = now;
        return Result.Success();
    }

    public T? Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
    private Result<bool> SaveAt(string expected, object value, Action<string> save)
    {
        if (Status != expected) return Result.Failure(Error.StateTransition($"The appraisal is not editable at the {expected} stage."));
        save(JsonSerializer.Serialize(value, JsonOptions)); return Result.Success();
    }
    private Result<bool> Move(string expected, string next)
    {
        if (Status != expected) return Result.Failure(Error.StateTransition($"The appraisal cannot move from '{Status}' to '{next}'."));
        Status = next; return Result.Success();
    }
    private bool HasDeclinedSignature(string phase) =>
        (Read<List<AppraisalSignatureAttempt>>(StaffSignatureAttemptsJson) ?? [])
        .Where(attempt => attempt.Phase == phase)
        .LastOrDefault() is { Accepted: false };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class AppraisalPhases
{
    public const string PlanningEmployee = "planning-employee";
    public const string PlanningHod = "planning-hod";
    public const string MidyearEmployeeSubmission = "midyear-employee-submission";
    public const string MidyearHod = "midyear-hod";
    public const string Midyear = "midyear";
    public const string YearEndEmployeeSubmission = "year-end-employee-submission";
    public const string YearEndHod = "year-end-hod";
    public const string YearEnd = "year-end";
}

public sealed record AppraisalSignatureAttempt(
    string Phase, bool Accepted, string? Comments, string? DeclineReason, DateTimeOffset RecordedAt);
