using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Reporting;

public class Report : InstituteScopedEntity
{
    public string ReportScope { get; private set; } = ReportScopes.Institute;
    public Guid? OwnerEmployeeId { get; private set; }
    public Guid? ReviewerEmployeeId { get; private set; }
    public Guid? ReviewerUserId { get; private set; }
    public Guid ReportingPeriodId { get; private set; }
    public string ReportType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? Abstract { get; private set; }
    public string? KeyResults { get; private set; }
    public string? Conclusion { get; private set; }
    public string Status { get; private set; } = ReportStatuses.Draft;
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ReturnReason { get; private set; }

    private Report() { }

    public static Report Create(
        Guid instituteId,
        Guid reportingPeriodId,
        string reportType,
        string title,
        string summary,
        string? @abstract,
        string? keyResults,
        string? conclusion)
    {
        return new Report
        {
            InstituteId = instituteId,
            ReportingPeriodId = reportingPeriodId,
            ReportType = reportType,
            Title = title,
            Summary = summary,
            Abstract = @abstract,
            KeyResults = keyResults,
            Conclusion = conclusion,
            Status = ReportStatuses.Draft
        };
    }

    public static Report CreateStaffQuarterly(
        Guid instituteId,
        Guid ownerEmployeeId,
        Guid reviewerEmployeeId,
        Guid reviewerUserId,
        Guid reportingPeriodId,
        string title,
        string workSummary,
        string? @abstract,
        string? keyResults,
        string? conclusion)
    {
        return new Report
        {
            InstituteId = instituteId,
            ReportScope = ReportScopes.EmployeeQuarterly,
            OwnerEmployeeId = ownerEmployeeId,
            ReviewerEmployeeId = reviewerEmployeeId,
            ReviewerUserId = reviewerUserId,
            ReportingPeriodId = reportingPeriodId,
            ReportType = ReportTypes.StaffQuarterly,
            Title = title,
            Summary = workSummary,
            Abstract = @abstract,
            KeyResults = keyResults,
            Conclusion = conclusion,
            Status = ReportStatuses.Draft
        };
    }

    public Result<bool> UpdateStaffQuarterly(
        Guid reportingPeriodId,
        Guid reviewerEmployeeId,
        Guid reviewerUserId,
        string title,
        string workSummary,
        string? @abstract,
        string? keyResults,
        string? conclusion)
    {
        var updated = Update(title, workSummary, @abstract, keyResults, conclusion);
        if (updated.IsFailure)
            return updated;

        ReportingPeriodId = reportingPeriodId;
        ReviewerEmployeeId = reviewerEmployeeId;
        ReviewerUserId = reviewerUserId;
        return Result.Success();
    }

    public bool IsEditable => Status is ReportStatuses.Draft or ReportStatuses.Returned;

    /// <summary>Updates report content. Only draft or returned reports are editable.</summary>
    public Result<bool> Update(
        string title,
        string summary,
        string? @abstract,
        string? keyResults,
        string? conclusion)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition(
                $"Only draft or returned reports can be edited. Current status is '{Status}'."));
        }

        Title = title;
        Summary = summary;
        Abstract = @abstract;
        KeyResults = keyResults;
        Conclusion = conclusion;
        return Result.Success();
    }

    /// <summary>draft | returned -> submitted.</summary>
    public Result<bool> Submit(Guid actorUserId, DateTimeOffset submittedAtUtc)
    {
        if (Status is not (ReportStatuses.Draft or ReportStatuses.Returned))
        {
            return Result.Failure(Error.StateTransition(
                $"A report in status '{Status}' cannot be submitted."));
        }

        Status = ReportStatuses.Submitted;
        SubmittedByUserId = actorUserId;
        SubmittedAt = submittedAtUtc;
        ReturnReason = null;
        return Result.Success();
    }

    /// <summary>submitted -> approved. Approved reports are immutable.</summary>
    public Result<bool> Approve(Guid actorUserId, DateTimeOffset approvedAtUtc)
    {
        if (Status is not ReportStatuses.Submitted)
        {
            return Result.Failure(Error.StateTransition(
                $"A report in status '{Status}' cannot be approved."));
        }

        Status = ReportStatuses.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAt = approvedAtUtc;
        return Result.Success();
    }

    /// <summary>submitted -> returned with an employee-visible reason.</summary>
    public Result<bool> Return(string returnReason)
    {
        if (Status is not ReportStatuses.Submitted)
        {
            return Result.Failure(Error.StateTransition(
                $"A report in status '{Status}' cannot be returned."));
        }

        if (string.IsNullOrWhiteSpace(returnReason))
        {
            return Result.Failure(Error.Validation("A return reason is required."));
        }

        Status = ReportStatuses.Returned;
        ReturnReason = returnReason.Trim();
        return Result.Success();
    }
}
