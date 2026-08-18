using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionStatusWindowBuilder
{
    public static async Task<PromotionApplicationWindowResponse?> BuildAsync(
        SpmeDbContext db,
        Guid employeeId,
        string? staffCategory,
        Guid? sourceGradeId,
        DateTime? appointmentDate,
        DateTime? employmentPromotionDate,
        DateTime employmentEffectiveFrom,
        PromotionPath? path,
        CancellationToken cancellationToken)
    {
        if (path is null || sourceGradeId is null)
            return null;

        var selfReportedPromotionDate = await db.EmployeeGradePromotionDates.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.GradeId == sourceGradeId.Value)
            .Select(item => (DateTime?)item.PromotionDate)
            .FirstOrDefaultAsync(cancellationToken);

        var presentGradeStart = PromotionPresentGradeStart.Resolve(
            appointmentDate,
            employmentPromotionDate,
            employmentEffectiveFrom,
            selfReportedPromotionDate);

        var hasQualifyingEducation = await db.EducationRecords.AsNoTracking()
            .AnyAsync(item =>
                item.EmployeeId == employeeId &&
                item.QualificationLevel == path.RequiredQualificationLevel,
                cancellationToken);

        var window = PromotionApplicationWindow.Calculate(
            presentGradeStart,
            path.MinimumYearsInSourceGrade,
            DateTime.UtcNow.Date,
            hasQualifyingEducation,
            string.Equals(staffCategory, PromotionConstants.SeniorStaff, StringComparison.OrdinalIgnoreCase),
            path.Status == PromotionConstants.PathActive,
            path.Status == PromotionConstants.PathRequiresPolicyConfirmation);

        if (window.OpensOn is null)
            return null;

        return new PromotionApplicationWindowResponse(
            window.OpensOn.Value,
            window.ServiceDueOn!.Value,
            window.PresentGradeStart!.Value,
            window.PresentGradeStartSource!,
            window.IsOpen,
            window.CanPrepareDraft);
    }

    public static DateTime ResolvePresentGradeStartDate(
        DateTime? appointmentDate,
        DateTime? employmentPromotionDate,
        DateTime employmentEffectiveFrom,
        DateTime? selfReportedPromotionDate)
    {
        var present = PromotionPresentGradeStart.Resolve(
            appointmentDate,
            employmentPromotionDate,
            employmentEffectiveFrom,
            selfReportedPromotionDate);
        return present.StartDate ?? employmentEffectiveFrom.Date;
    }

    public static DateTime? ResolveRecordedLastPromotionDate(
        DateTime? appointmentDate,
        DateTime? employmentPromotionDate,
        DateTime employmentEffectiveFrom,
        DateTime? selfReportedPromotionDate)
    {
        return PromotionPresentGradeStart.Resolve(
            appointmentDate,
            employmentPromotionDate,
            employmentEffectiveFrom,
            selfReportedPromotionDate).Source == PromotionPresentGradeStart.SourceLastPromotion
            ? PromotionPresentGradeStart.RecordedPromotionDate(employmentPromotionDate, employmentEffectiveFrom, appointmentDate)
              ?? PromotionPresentGradeStart.RecordedPromotionDate(selfReportedPromotionDate, employmentEffectiveFrom, appointmentDate)
            : null;
    }
}
