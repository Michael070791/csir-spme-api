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
        DateTime? selfReportedPromotionDate = null;
        if (sourceGradeId is Guid gradeId)
        {
            selfReportedPromotionDate = await db.EmployeeGradePromotionDates.AsNoTracking()
                .Where(item => item.EmployeeId == employeeId && item.GradeId == gradeId)
                .Select(item => (DateTime?)item.PromotionDate)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            selfReportedPromotionDate = await db.EmployeeGradePromotionDates.AsNoTracking()
                .Where(item => item.EmployeeId == employeeId)
                .OrderByDescending(item => item.PromotionDate)
                .Select(item => (DateTime?)item.PromotionDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var presentGradeStart = PromotionPresentGradeStart.Resolve(
            appointmentDate,
            employmentPromotionDate,
            employmentEffectiveFrom,
            selfReportedPromotionDate);

        var requiredLevel = PromotionStaffCheck.RequiredQualificationFor(
            staffCategory,
            path?.RequiredQualificationLevel);
        var educationLevels = await db.EducationRecords.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => item.QualificationLevel)
            .ToListAsync(cancellationToken);
        var hasQualifyingEducation = PromotionStaffCheck.HasQualifyingEducation(educationLevels, requiredLevel);
        var hasActivePath = path is not null && path.Status == PromotionConstants.PathActive;
        var years = PromotionStaffCheck.InferredMinimumYears(staffCategory, path?.MinimumYearsInSourceGrade);
        var window = PromotionApplicationWindow.Calculate(
            presentGradeStart,
            years,
            DateTime.UtcNow.Date,
            hasQualifyingEducation,
            PromotionStaffCheck.AllowsApplicationDraft(staffCategory, hasActivePath),
            path?.Status == PromotionConstants.PathRequiresPolicyConfirmation);

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

    public static async Task<DateTime?> ResolveSelfReportedPromotionDateAsync(
        SpmeDbContext db,
        Guid employeeId,
        Guid? sourceGradeId,
        CancellationToken cancellationToken)
    {
        if (sourceGradeId is Guid gradeId)
        {
            return await db.EmployeeGradePromotionDates.AsNoTracking()
                .Where(item => item.EmployeeId == employeeId && item.GradeId == gradeId)
                .Select(item => (DateTime?)item.PromotionDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await db.EmployeeGradePromotionDates.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.PromotionDate)
            .Select(item => (DateTime?)item.PromotionDate)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
