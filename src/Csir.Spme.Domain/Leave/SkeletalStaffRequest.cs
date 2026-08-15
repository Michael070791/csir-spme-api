using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Leave;

public class SkeletalStaffRequest : InstituteScopedEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid HolidayPeriodId { get; private set; }
    public string SelectedDatesJson { get; private set; } = string.Empty;
    public DateTime? SelectedStartDate { get; private set; }
    public DateTime? SelectedEndDate { get; private set; }
    public string Status { get; private set; } = "draft";
    public string CurrentApprovalStage { get; private set; } = string.Empty;
    public string? SignatureName { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public short? LeaveCreditYear { get; private set; }
    public DateTimeOffset? LeaveCreditedAt { get; private set; }
    public string? Comment { get; private set; }
    public string? RejectionReason { get; private set; }

    private SkeletalStaffRequest() { }

    public static Result<SkeletalStaffRequest> CreateDraft(
        Guid employeeId,
        Guid instituteId,
        Guid holidayPeriodId,
        string selectedDatesJson,
        DateTime selectedStartDate,
        DateTime selectedEndDate,
        string signatureName,
        string? comment)
    {
        if (employeeId == Guid.Empty || instituteId == Guid.Empty || holidayPeriodId == Guid.Empty ||
            string.IsNullOrWhiteSpace(selectedDatesJson) || string.IsNullOrWhiteSpace(signatureName))
        {
            return Result<SkeletalStaffRequest>.Failure(Error.Validation("Employee, holiday period, selected dates, and signature are required."));
        }

        return Result<SkeletalStaffRequest>.Success(new SkeletalStaffRequest
        {
            EmployeeId = employeeId,
            InstituteId = instituteId,
            HolidayPeriodId = holidayPeriodId,
            SelectedDatesJson = selectedDatesJson,
            SelectedStartDate = selectedStartDate.Date,
            SelectedEndDate = selectedEndDate.Date,
            SignatureName = signatureName.Trim(),
            Comment = NormalizeOptional(comment),
            Status = SkeletalStaffRequestStatuses.Draft,
            CurrentApprovalStage = string.Empty
        });
    }

    public bool IsEditable => Status == SkeletalStaffRequestStatuses.Draft;

    public Result<bool> UpdateDraft(string selectedDatesJson, DateTime selectedStartDate, DateTime selectedEndDate, string signatureName, string? comment)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition("Only draft skeletal staff requests can be edited."));
        }

        if (string.IsNullOrWhiteSpace(selectedDatesJson) || string.IsNullOrWhiteSpace(signatureName))
        {
            return Result.Failure(Error.Validation("Selected dates and signature are required."));
        }

        SelectedDatesJson = selectedDatesJson;
        SelectedStartDate = selectedStartDate.Date;
        SelectedEndDate = selectedEndDate.Date;
        SignatureName = signatureName.Trim();
        Comment = NormalizeOptional(comment);
        return Result.Success();
    }

    public Result<bool> Submit(string firstApprovalStage, DateTimeOffset submittedAt)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition($"A skeletal staff request in status '{Status}' cannot be submitted."));
        }

        Status = SkeletalStaffRequestStatuses.Submitted;
        CurrentApprovalStage = firstApprovalStage;
        SubmittedAt = submittedAt;
        return Result.Success();
    }

    public Result<bool> Approve(string expectedStage, string? nextStage)
    {
        if (Status is not (SkeletalStaffRequestStatuses.Submitted or SkeletalStaffRequestStatuses.UnderReview) ||
            !string.Equals(CurrentApprovalStage, expectedStage, StringComparison.Ordinal))
        {
            return Result.Failure(Error.StateTransition("The skeletal staff request is not awaiting this approval decision."));
        }

        CurrentApprovalStage = nextStage ?? string.Empty;
        Status = nextStage is null ? SkeletalStaffRequestStatuses.Approved : SkeletalStaffRequestStatuses.UnderReview;
        return Result.Success();
    }

    public Result<bool> Reject(string expectedStage, string reason)
    {
        if (Status is not (SkeletalStaffRequestStatuses.Submitted or SkeletalStaffRequestStatuses.UnderReview) ||
            !string.Equals(CurrentApprovalStage, expectedStage, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.StateTransition("The skeletal staff request cannot be rejected in its current state."));
        }

        Status = SkeletalStaffRequestStatuses.Rejected;
        CurrentApprovalStage = string.Empty;
        RejectionReason = reason.Trim();
        return Result.Success();
    }

    public Result<bool> Cancel()
    {
        if (Status is not (SkeletalStaffRequestStatuses.Draft or SkeletalStaffRequestStatuses.Submitted or SkeletalStaffRequestStatuses.UnderReview))
        {
            return Result.Failure(Error.StateTransition("The skeletal staff request cannot be cancelled in its current state."));
        }

        Status = SkeletalStaffRequestStatuses.Cancelled;
        CurrentApprovalStage = string.Empty;
        return Result.Success();
    }

    public Result<bool> Complete(DateTimeOffset completedAt)
    {
        if (Status != SkeletalStaffRequestStatuses.Approved)
        {
            return Result.Failure(Error.StateTransition("Only approved skeletal staff requests can be completed."));
        }

        Status = SkeletalStaffRequestStatuses.Completed;
        CompletedAt = completedAt;
        return Result.Success();
    }

    public Result<bool> CreditLeave(short leaveYear, DateTimeOffset creditedAt)
    {
        if (Status != SkeletalStaffRequestStatuses.Completed || LeaveCreditedAt.HasValue)
        {
            return Result.Failure(Error.StateTransition("Leave credit can be applied once after the skeletal staff request is completed."));
        }

        LeaveCreditYear = leaveYear;
        LeaveCreditedAt = creditedAt;
        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
