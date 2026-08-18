namespace Csir.Spme.Domain.Promotions;

public static class PromotionPresentGradeStart
{
    public const string SourceLastPromotion = "last-promotion";
    public const string SourceFirstAppointment = "first-appointment";

    public static DateTime? RecordedPromotionDate(
        DateTime? promotionDate,
        DateTime effectiveFrom,
        DateTime? appointmentDate)
    {
        if (promotionDate is not { } promotion)
            return null;

        var date = promotion.Date;
        if (date == effectiveFrom.Date && appointmentDate is { } appointment && appointment.Date < date)
            return null;

        return date;
    }

    public static PresentGradeStartResult Resolve(
        DateTime? appointmentDate,
        DateTime? employmentPromotionDate,
        DateTime employmentEffectiveFrom,
        DateTime? selfReportedPromotionDate)
    {
        var recorded = RecordedPromotionDate(employmentPromotionDate, employmentEffectiveFrom, appointmentDate)
            ?? RecordedPromotionDate(selfReportedPromotionDate, employmentEffectiveFrom, appointmentDate);

        if (recorded.HasValue)
        {
            return new PresentGradeStartResult(recorded.Value.Date, SourceLastPromotion);
        }

        if (appointmentDate is { } appointment)
        {
            return new PresentGradeStartResult(appointment.Date, SourceFirstAppointment);
        }

        return new PresentGradeStartResult(null, null);
    }
}

public sealed record PresentGradeStartResult(DateTime? StartDate, string? Source);
