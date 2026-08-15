using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Leave;

public class HolidayPeriod : BaseEntity
{
    public string ScopeType { get; private set; } = "institute";
    public Guid? InstituteId { get; private set; }
    public short LeaveYear { get; private set; }
    public DateTime ChristmasStartDate { get; private set; }
    public DateTime ChristmasEndDate { get; private set; }
    public DateTime NewYearStartDate { get; private set; }
    public DateTime NewYearEndDate { get; private set; }
    public DateTime AvailabilityStartDate { get; private set; }
    public DateTime AvailabilityEndDate { get; private set; }
    public short DeductionDays { get; private set; }
    public string Status { get; private set; } = "draft";
    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid? FinalizedByUserId { get; private set; }
    public string? Notes { get; private set; }

    private HolidayPeriod() { }

    public static Result<HolidayPeriod> Create(
        string scopeType,
        Guid? instituteId,
        short leaveYear,
        DateTime christmasStartDate,
        DateTime christmasEndDate,
        DateTime newYearStartDate,
        DateTime newYearEndDate,
        DateTime availabilityStartDate,
        DateTime availabilityEndDate,
        short deductionDays,
        string status,
        string? notes)
    {
        var period = new HolidayPeriod();
        var update = period.Update(
            christmasStartDate, christmasEndDate, newYearStartDate, newYearEndDate,
            availabilityStartDate, availabilityEndDate, deductionDays, status, notes);
        if (update.IsFailure)
        {
            return Result<HolidayPeriod>.Failure(update.Error!);
        }

        if (!DomainValues.Contains(ScopeTypes.All, scopeType) || scopeType is ScopeTypes.Self or ScopeTypes.InstituteHierarchy)
        {
            return Result<HolidayPeriod>.Failure(Error.Validation("Holiday period scope must be csir-wide or institute."));
        }

        if ((scopeType == ScopeTypes.CsirWide && instituteId.HasValue) ||
            (scopeType == ScopeTypes.Institute && !instituteId.HasValue))
        {
            return Result<HolidayPeriod>.Failure(Error.Validation("Holiday period scope and institute are inconsistent."));
        }

        period.ScopeType = scopeType;
        period.InstituteId = instituteId;
        period.LeaveYear = leaveYear;
        return Result<HolidayPeriod>.Success(period);
    }

    public Result<bool> Update(
        DateTime christmasStartDate,
        DateTime christmasEndDate,
        DateTime newYearStartDate,
        DateTime newYearEndDate,
        DateTime availabilityStartDate,
        DateTime availabilityEndDate,
        short deductionDays,
        string status,
        string? notes)
    {
        if (christmasEndDate.Date < christmasStartDate.Date ||
            newYearEndDate.Date < newYearStartDate.Date ||
            availabilityEndDate.Date < availabilityStartDate.Date)
        {
            return Result.Failure(Error.Validation("Holiday period end dates cannot precede start dates."));
        }

        if (deductionDays < 0 || !DomainValues.Contains(HolidayPeriodStatuses.All, status))
        {
            return Result.Failure(Error.Validation("Holiday period status or deduction days are invalid."));
        }

        ChristmasStartDate = christmasStartDate.Date;
        ChristmasEndDate = christmasEndDate.Date;
        NewYearStartDate = newYearStartDate.Date;
        NewYearEndDate = newYearEndDate.Date;
        AvailabilityStartDate = availabilityStartDate.Date;
        AvailabilityEndDate = availabilityEndDate.Date;
        DeductionDays = deductionDays;
        Status = status;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return Result.Success();
    }

    public static HolidayPeriod CreateImported(
        string scopeType,
        Guid? instituteId,
        short leaveYear,
        DateTime christmasStartDate,
        DateTime christmasEndDate,
        DateTime newYearStartDate,
        DateTime newYearEndDate,
        DateTime availabilityStartDate,
        DateTime availabilityEndDate,
        short deductionDays,
        string status,
        DateTimeOffset? finalizedAt,
        Guid? finalizedByUserId,
        string? notes)
    {
        return new HolidayPeriod
        {
            ScopeType = scopeType,
            InstituteId = instituteId,
            LeaveYear = leaveYear,
            ChristmasStartDate = christmasStartDate.Date,
            ChristmasEndDate = christmasEndDate.Date,
            NewYearStartDate = newYearStartDate.Date,
            NewYearEndDate = newYearEndDate.Date,
            AvailabilityStartDate = availabilityStartDate.Date,
            AvailabilityEndDate = availabilityEndDate.Date,
            DeductionDays = deductionDays,
            Status = status,
            FinalizedAt = finalizedAt,
            FinalizedByUserId = finalizedByUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }
}
