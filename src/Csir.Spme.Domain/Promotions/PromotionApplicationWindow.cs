namespace Csir.Spme.Domain.Promotions;

public static class PromotionApplicationWindow
{
    public const int MonthsBeforeServiceDue = 5;

    public static PromotionApplicationWindowResult Calculate(
        PresentGradeStartResult presentGradeStart,
        short minimumYearsInSourceGrade,
        DateTime evaluationDate,
        bool hasQualifyingEducationRecord,
        bool isSeniorStaff,
        bool hasActivePath,
        bool pathRequiresPolicyConfirmation)
    {
        if (!isSeniorStaff || !hasActivePath || pathRequiresPolicyConfirmation ||
            presentGradeStart.StartDate is not { } start)
        {
            return new PromotionApplicationWindowResult(
                null, null, presentGradeStart.StartDate, presentGradeStart.Source,
                false, false, false);
        }

        var serviceDueOn = start.AddYears(minimumYearsInSourceGrade);
        var opensOn = serviceDueOn.AddMonths(-MonthsBeforeServiceDue);
        var isOpen = evaluationDate.Date >= opensOn.Date;
        var canPrepareDraft = isOpen && hasQualifyingEducationRecord;

        return new PromotionApplicationWindowResult(
            opensOn.Date,
            serviceDueOn.Date,
            start,
            presentGradeStart.Source,
            isOpen,
            canPrepareDraft,
            hasQualifyingEducationRecord);
    }
}

public sealed record PromotionApplicationWindowResult(
    DateTime? OpensOn,
    DateTime? ServiceDueOn,
    DateTime? PresentGradeStart,
    string? PresentGradeStartSource,
    bool IsOpen,
    bool CanPrepareDraft,
    bool HasQualifyingEducation);
