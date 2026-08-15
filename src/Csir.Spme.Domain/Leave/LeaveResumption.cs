using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Leave;

/// <summary>Resumption-of-duty declaration for an approved leave request.</summary>
public class LeaveResumption : BaseEntity
{
    public Guid LeaveRequestId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTime ResumptionDate { get; private set; }
    public string Status { get; private set; } = LeaveResumptionStatuses.Submitted;
    public string? EmployeeSignatureName { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private LeaveResumption() { }

    public static Result<LeaveResumption> Create(
        Guid leaveRequestId,
        Guid employeeId,
        DateTime resumptionDate,
        DateTime leaveEndDate,
        string? employeeSignatureName)
    {
        if (resumptionDate.Date < leaveEndDate.Date)
        {
            return Result<LeaveResumption>.Failure(Error.Validation(
                "Resumption cannot be submitted before the leave ends."));
        }

        return Result<LeaveResumption>.Success(new LeaveResumption
        {
            LeaveRequestId = leaveRequestId,
            EmployeeId = employeeId,
            ResumptionDate = resumptionDate.Date,
            Status = LeaveResumptionStatuses.Submitted,
            EmployeeSignatureName = employeeSignatureName,
            SubmittedAt = DateTimeOffset.UtcNow
        });
    }

    public Result<bool> Approve(DateTimeOffset completedAtUtc)
    {
        if (Status is not LeaveResumptionStatuses.Submitted)
        {
            return Result.Failure(Error.StateTransition(
                $"A resumption in status '{Status}' cannot be approved."));
        }

        Status = LeaveResumptionStatuses.Approved;
        CompletedAt = completedAtUtc;
        return Result.Success();
    }

    public Result<bool> Reject(string rejectionReason, DateTimeOffset completedAtUtc)
    {
        if (Status is not LeaveResumptionStatuses.Submitted)
        {
            return Result.Failure(Error.StateTransition(
                $"A resumption in status '{Status}' cannot be rejected."));
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return Result.Failure(Error.Validation("A rejection reason is required."));
        }

        Status = LeaveResumptionStatuses.Rejected;
        RejectionReason = rejectionReason.Trim();
        CompletedAt = completedAtUtc;
        return Result.Success();
    }
}
