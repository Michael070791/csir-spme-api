using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Leave;

public class LeaveRequest : InstituteScopedEntity
{
    public Guid EmployeeId { get; private set; }
    public string LeaveType { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public decimal WorkingDays { get; private set; }
    public string Status { get; private set; } = LeaveRequestStatuses.Draft;
    public string CurrentApprovalStage { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? HandoverNotes { get; private set; }
    public Guid? DelegateEmployeeId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? MedicalDocumentFileId { get; private set; }
    public Guid? AdmissionLetterFileId { get; private set; }
    public Guid? HandoverDocumentFileId { get; private set; }

    private LeaveRequest() { }

    public static LeaveRequest CreateDraft(
        Guid employeeId,
        Guid instituteId,
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        string? reason,
        string? handoverNotes,
        Guid? delegateEmployeeId,
        Guid? medicalDocumentFileId,
        Guid? admissionLetterFileId,
        Guid? handoverDocumentFileId)
    {
        return new LeaveRequest
        {
            EmployeeId = employeeId,
            InstituteId = instituteId,
            LeaveType = leaveType,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            WorkingDays = workingDays,
            Reason = reason,
            HandoverNotes = handoverNotes,
            DelegateEmployeeId = delegateEmployeeId,
            MedicalDocumentFileId = medicalDocumentFileId,
            AdmissionLetterFileId = admissionLetterFileId,
            HandoverDocumentFileId = handoverDocumentFileId,
            Status = LeaveRequestStatuses.Draft,
            CurrentApprovalStage = string.Empty
        };
    }

    public bool IsEditable => Status is LeaveRequestStatuses.Draft;

    /// <summary>Updates draft content. Only draft requests are editable.</summary>
    public Result<bool> UpdateDraft(
        string leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal workingDays,
        string? reason,
        string? handoverNotes,
        Guid? delegateEmployeeId,
        Guid? medicalDocumentFileId,
        Guid? admissionLetterFileId,
        Guid? handoverDocumentFileId)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot be edited."));
        }

        LeaveType = leaveType;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        WorkingDays = workingDays;
        Reason = reason;
        HandoverNotes = handoverNotes;
        DelegateEmployeeId = delegateEmployeeId;
        MedicalDocumentFileId = medicalDocumentFileId;
        AdmissionLetterFileId = admissionLetterFileId;
        HandoverDocumentFileId = handoverDocumentFileId;
        return Result.Success();
    }

    /// <summary>draft -> submitted at the first approval stage.</summary>
    public Result<bool> Submit(string firstApprovalStage, DateTimeOffset submittedAtUtc)
    {
        if (Status is not LeaveRequestStatuses.Draft)
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot be submitted."));
        }

        Status = LeaveRequestStatuses.Submitted;
        CurrentApprovalStage = firstApprovalStage;
        SubmittedAt = submittedAtUtc;
        return Result.Success();
    }

    /// <summary>
    /// Records an approval at the current stage. Moves to <paramref name="nextStage"/>
    /// (under-review) or to approved when the chain is complete.
    /// </summary>
    public Result<bool> Approve(string expectedStage, string? nextStage)
    {
        if (Status is not (LeaveRequestStatuses.Submitted or LeaveRequestStatuses.UnderReview))
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot be approved."));
        }

        if (!string.Equals(CurrentApprovalStage, expectedStage, StringComparison.Ordinal))
        {
            return Result.Failure(Error.StateTransition(
                $"The leave request is awaiting stage '{CurrentApprovalStage}', not '{expectedStage}'."));
        }

        if (nextStage is null)
        {
            Status = LeaveRequestStatuses.Approved;
            CompletedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            Status = LeaveRequestStatuses.UnderReview;
            CurrentApprovalStage = nextStage;
        }

        return Result.Success();
    }

    /// <summary>submitted | under-review -> rejected with an approver-visible reason.</summary>
    public Result<bool> Reject(string expectedStage, string rejectionReason)
    {
        if (Status is not (LeaveRequestStatuses.Submitted or LeaveRequestStatuses.UnderReview))
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot be rejected."));
        }

        if (!string.Equals(CurrentApprovalStage, expectedStage, StringComparison.Ordinal))
        {
            return Result.Failure(Error.StateTransition(
                $"The leave request is awaiting stage '{CurrentApprovalStage}', not '{expectedStage}'."));
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return Result.Failure(Error.Validation("A rejection reason is required."));
        }

        Status = LeaveRequestStatuses.Rejected;
        RejectionReason = rejectionReason.Trim();
        CompletedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>draft | submitted | under-review | approved -> cancelled.</summary>
    public Result<bool> Cancel(DateTimeOffset cancelledAtUtc)
    {
        if (Status is not (LeaveRequestStatuses.Draft or LeaveRequestStatuses.Submitted
            or LeaveRequestStatuses.UnderReview or LeaveRequestStatuses.Approved))
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot be cancelled."));
        }

        Status = LeaveRequestStatuses.Cancelled;
        CancelledAt = cancelledAtUtc;
        CompletedAt ??= cancelledAtUtc;
        return Result.Success();
    }

    /// <summary>approved -> resumption-pending when the employee initiates resumption.</summary>
    public Result<bool> BeginResumption()
    {
        if (Status is not LeaveRequestStatuses.Approved)
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot begin resumption."));
        }

        Status = LeaveRequestStatuses.ResumptionPending;
        return Result.Success();
    }

    /// <summary>resumption-pending -> resumed once the resumption is approved.</summary>
    public Result<bool> CompleteResumption(DateTimeOffset completedAtUtc)
    {
        if (Status is not LeaveRequestStatuses.ResumptionPending)
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot complete resumption."));
        }

        Status = LeaveRequestStatuses.Resumed;
        CompletedAt = completedAtUtc;
        return Result.Success();
    }

    /// <summary>resumption-pending -> approved when a resumption is rejected.</summary>
    public Result<bool> RejectResumption()
    {
        if (Status is not LeaveRequestStatuses.ResumptionPending)
        {
            return Result.Failure(Error.StateTransition(
                $"A leave request in status '{Status}' cannot reject resumption."));
        }

        Status = LeaveRequestStatuses.Approved;
        return Result.Success();
    }
}
