using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Reporting;

public class ReportingPeriod : BaseEntity
{
    public string ScopeType { get; private set; } = ScopeTypes.Institute;
    public Guid? InstituteId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string PeriodType { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public string Status { get; private set; } = ReportingPeriodStatuses.Draft;

    private ReportingPeriod() { }

    public static Result<ReportingPeriod> Create(
        string scopeType,
        Guid? instituteId,
        string code,
        string name,
        string periodType,
        DateTime startDate,
        DateTime endDate,
        DateTime? dueDate)
    {
        if (scopeType is not ScopeTypes.Institute and not ScopeTypes.CsirWide)
        {
            return Result<ReportingPeriod>.Failure(Error.Validation(
                "A reporting period scope must be institute or csir-wide."));
        }

        if (scopeType == ScopeTypes.CsirWide && instituteId.HasValue)
        {
            return Result<ReportingPeriod>.Failure(Error.Validation(
                "A csir-wide reporting period cannot be assigned to an institute."));
        }

        if (scopeType != ScopeTypes.CsirWide && !instituteId.HasValue)
        {
            return Result<ReportingPeriod>.Failure(Error.Validation(
                "An institute-scoped reporting period requires an institute."));
        }

        if (endDate < startDate)
        {
            return Result<ReportingPeriod>.Failure(Error.Validation(
                "The reporting period end date cannot precede the start date."));
        }

        return Result<ReportingPeriod>.Success(new ReportingPeriod
        {
            ScopeType = scopeType,
            InstituteId = instituteId,
            Code = code,
            Name = name,
            PeriodType = periodType,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            DueDate = dueDate?.Date,
            Status = ReportingPeriodStatuses.Draft
        });
    }

    /// <summary>Measurements may change only while the period is draft or open.</summary>
    public bool AllowsMeasurementChanges =>
        Status is ReportingPeriodStatuses.Draft or ReportingPeriodStatuses.Open;

    /// <summary>draft -> open.</summary>
    public Result<bool> Open()
    {
        if (Status is not ReportingPeriodStatuses.Draft)
        {
            return Result.Failure(Error.StateTransition(
                $"A reporting period in status '{Status}' cannot be opened."));
        }

        Status = ReportingPeriodStatuses.Open;
        return Result.Success();
    }

    /// <summary>open -> closed.</summary>
    public Result<bool> Close()
    {
        if (Status is not ReportingPeriodStatuses.Open)
        {
            return Result.Failure(Error.StateTransition(
                $"A reporting period in status '{Status}' cannot be closed."));
        }

        Status = ReportingPeriodStatuses.Closed;
        return Result.Success();
    }

    /// <summary>closed -> finalized.</summary>
    public Result<bool> Finalize()
    {
        if (Status is not ReportingPeriodStatuses.Closed)
        {
            return Result.Failure(Error.StateTransition(
                $"A reporting period in status '{Status}' cannot be finalized."));
        }

        Status = ReportingPeriodStatuses.Finalized;
        return Result.Success();
    }
}
