using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public sealed class AppraisalCycle : InstituteScopedEntity
{
    public string Name { get; private set; } = string.Empty;
    public short Year { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public DateTime PlanningStart { get; private set; }
    public DateTime PlanningEnd { get; private set; }
    public DateTime MidyearStart { get; private set; }
    public DateTime MidyearEnd { get; private set; }
    public DateTime YearEndStart { get; private set; }
    public DateTime YearEndEnd { get; private set; }
    public string Status { get; private set; } = AppraisalCycleStatuses.Draft;
    public string? ReopenReason { get; private set; }
    public string FormTemplateVersion { get; private set; } = AppraisalFormTemplate.Version;
    public string FormTemplateChecksum { get; private set; } = AppraisalFormTemplate.CanonicalContentChecksum;
    private AppraisalCycle() { }

    public static Result<AppraisalCycle> Create(Guid instituteId, string name, short year,
        DateTime startDate, DateTime endDate, DateTime planningStart, DateTime planningEnd,
        DateTime midyearStart, DateTime midyearEnd, DateTime yearEndStart, DateTime yearEndEnd)
    {
        var error = Validate(instituteId, name, year, startDate, endDate, planningStart, planningEnd,
            midyearStart, midyearEnd, yearEndStart, yearEndEnd);
        if (error is not null) return Result<AppraisalCycle>.Failure(error);
        return Result<AppraisalCycle>.Success(new AppraisalCycle
        {
            InstituteId = instituteId, Name = name.Trim(), Year = year,
            StartDate = startDate.Date, EndDate = endDate.Date,
            PlanningStart = planningStart.Date, PlanningEnd = planningEnd.Date,
            MidyearStart = midyearStart.Date, MidyearEnd = midyearEnd.Date,
            YearEndStart = yearEndStart.Date, YearEndEnd = yearEndEnd.Date,
            FormTemplateVersion = AppraisalFormTemplate.Version,
            FormTemplateChecksum = AppraisalFormTemplate.CanonicalContentChecksum
        });
    }

    public Result<bool> Update(string name, DateTime startDate, DateTime endDate, DateTime planningStart,
        DateTime planningEnd, DateTime midyearStart, DateTime midyearEnd, DateTime yearEndStart, DateTime yearEndEnd)
    {
        if (Status != AppraisalCycleStatuses.Draft)
            return Result.Failure(Error.StateTransition("Only a draft appraisal cycle can be edited."));
        var error = Validate(InstituteId, name, Year, startDate, endDate, planningStart, planningEnd,
            midyearStart, midyearEnd, yearEndStart, yearEndEnd);
        if (error is not null) return Result.Failure(error);
        Name = name.Trim(); StartDate = startDate.Date; EndDate = endDate.Date;
        PlanningStart = planningStart.Date; PlanningEnd = planningEnd.Date;
        MidyearStart = midyearStart.Date; MidyearEnd = midyearEnd.Date;
        YearEndStart = yearEndStart.Date; YearEndEnd = yearEndEnd.Date;
        return Result.Success();
    }

    public Result<bool> Open() => Transition(AppraisalCycleStatuses.Draft, AppraisalCycleStatuses.Open);
    public Result<bool> Close() => Transition(AppraisalCycleStatuses.Open, AppraisalCycleStatuses.Closed);
    public Result<bool> Reopen(string reason)
    {
        if (Status != AppraisalCycleStatuses.Closed || string.IsNullOrWhiteSpace(reason))
            return Result.Failure(Error.StateTransition("A closed cycle and a reopen reason are required."));
        Status = AppraisalCycleStatuses.Open; ReopenReason = reason.Trim(); return Result.Success();
    }

    public DateTime DeadlineFor(string appraisalStatus) => appraisalStatus switch
    {
        AppraisalStatuses.Planning or AppraisalStatuses.PlanningReview => PlanningEnd,
        AppraisalStatuses.Midyear or AppraisalStatuses.MidyearReview or AppraisalStatuses.MidyearStaffSignature
            or AppraisalStatuses.MidyearDirectorReview => MidyearEnd,
        _ => YearEndEnd
    };

    public bool IsStageWindowOpen(string appraisalStatus, DateTime date)
    {
        if (Status != AppraisalCycleStatuses.Open) return false;
        if (!string.IsNullOrWhiteSpace(ReopenReason)) return true;

        var (start, end) = appraisalStatus switch
        {
            AppraisalStatuses.Planning or AppraisalStatuses.PlanningReview => (PlanningStart, PlanningEnd),
            AppraisalStatuses.Midyear or AppraisalStatuses.MidyearReview or AppraisalStatuses.MidyearStaffSignature
                or AppraisalStatuses.MidyearDirectorReview => (MidyearStart, MidyearEnd),
            _ => (YearEndStart, YearEndEnd)
        };

        return date.Date >= start && date.Date <= end;
    }

    private Result<bool> Transition(string expected, string next)
    {
        if (Status != expected) return Result.Failure(Error.StateTransition($"The cycle cannot move from '{Status}' to '{next}'."));
        Status = next; return Result.Success();
    }

    private static Error? Validate(Guid instituteId, string name, short year, DateTime startDate,
        DateTime endDate, DateTime planningStart, DateTime planningEnd, DateTime midyearStart,
        DateTime midyearEnd, DateTime yearEndStart, DateTime yearEndEnd)
    {
        if (instituteId == Guid.Empty || string.IsNullOrWhiteSpace(name) || year < 2000)
            return Error.Validation("Institute, name, and year are required.");
        if (endDate.Date < startDate.Date || planningEnd.Date < planningStart.Date || midyearEnd.Date < midyearStart.Date ||
            yearEndEnd.Date < yearEndStart.Date || planningStart.Date < startDate.Date || yearEndEnd.Date > endDate.Date ||
            planningEnd.Date >= midyearStart.Date || midyearEnd.Date >= yearEndStart.Date)
            return Error.Validation("Appraisal stage windows must be ordered and contained within the cycle.");
        return null;
    }
}
