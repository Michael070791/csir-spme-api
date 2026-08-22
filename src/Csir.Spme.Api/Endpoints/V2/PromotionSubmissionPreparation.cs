using System.Text.Json;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionSubmissionPreparation
{
    public static async Task<PromotionAssessmentResolution> ResolveForCreateAsync(
        SpmeDbContext db,
        Guid employeeId,
        Guid? promotionAssessmentId,
        CancellationToken cancellationToken)
    {
        if (promotionAssessmentId is Guid assessmentId)
        {
            var requested = await db.PromotionAssessments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == assessmentId && item.EmployeeId == employeeId, cancellationToken);
            if (requested is null)
                return PromotionAssessmentResolution.NotFound();

            var window = await BuildWindowAsync(db, employeeId, requested, cancellationToken);
            var canCreate = CanCreateSubmission(requested, window);
            return new PromotionAssessmentResolution(requested, window, canCreate, canCreate ? null : "not-allowed");
        }

        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
            return PromotionAssessmentResolution.NotFound();

        var employment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.IsCurrent, cancellationToken);
        var resolvedGrade = await PromotionEndpoints.ResolveCurrentGradeAsync(
            db, employment?.GradeId, employment?.JobTitle, employment?.StaffCategory ?? string.Empty, cancellationToken);
        var sourceGradeId = resolvedGrade?.Id ?? employment?.GradeId;
        if (employment is null || sourceGradeId is null)
            return PromotionAssessmentResolution.Conflict("The employee does not have a current canonical grade.");

        var cycle = await db.PromotionCycles.AsNoTracking()
            .Where(item => item.Status == PromotionConstants.CycleOpen)
            .OrderByDescending(item => item.CycleYear)
            .FirstOrDefaultAsync(cancellationToken);
        if (cycle is null)
            return PromotionAssessmentResolution.Conflict("The promotion cycle is not open.");

        var path = await PromotionEndpoints.MatchingPathQuery(
                db,
                employment.StaffCategory ?? string.Empty,
                sourceGradeId.Value,
                cycle.EffectivePromotionDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (path is null)
            return PromotionAssessmentResolution.Conflict("No approved promotion path applies to the employee's current grade.");

        var existingAssessment = await db.PromotionAssessments.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.EmployeeId == employeeId &&
                item.PromotionCycleId == cycle.Id &&
                item.PromotionPathId == path.Id,
                cancellationToken);
        if (existingAssessment is not null)
        {
            var existingWindow = await BuildWindowAsync(db, employeeId, existingAssessment, cancellationToken);
            var canCreateExisting = CanCreateSubmission(existingAssessment, existingWindow);
            return new PromotionAssessmentResolution(
                existingAssessment,
                existingWindow,
                canCreateExisting,
                canCreateExisting ? null : "not-allowed");
        }

        var windowForCreate = await PromotionStatusWindowBuilder.BuildAsync(
            db,
            employeeId,
            employment.StaffCategory,
            sourceGradeId,
            employment.AppointmentDate,
            employment.PromotionDate,
            employment.EffectiveFrom,
            path,
            cancellationToken);
        if (windowForCreate?.CanPrepareDraft != true)
            return PromotionAssessmentResolution.Conflict(
                "Promotion draft preparation is not open for this employee and cycle.");

        var created = await CreateAssessmentAsync(db, employee, employment, cycle, path, sourceGradeId.Value, cancellationToken);
        return new PromotionAssessmentResolution(created, windowForCreate, true, null);
    }

    public static bool CanCreateSubmission(
        PromotionAssessment assessment,
        PromotionApplicationWindowResponse? window) =>
        assessment.TargetGradeId.HasValue &&
        (assessment.EligibilityState == PromotionConstants.EligibilityEligibleForReview ||
         window?.CanPrepareDraft == true);

    private static async Task<PromotionApplicationWindowResponse?> BuildWindowAsync(
        SpmeDbContext db,
        Guid employeeId,
        PromotionAssessment assessment,
        CancellationToken cancellationToken)
    {
        var employment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.IsCurrent, cancellationToken);
        var path = await db.PromotionPaths.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assessment.PromotionPathId, cancellationToken);
        if (employment is null || path is null)
            return null;

        return await PromotionStatusWindowBuilder.BuildAsync(
            db,
            employeeId,
            employment.StaffCategory,
            employment.GradeId,
            employment.AppointmentDate,
            employment.PromotionDate,
            employment.EffectiveFrom,
            path,
            cancellationToken);
    }

    private static async Task<PromotionAssessment> CreateAssessmentAsync(
        SpmeDbContext db,
        Csir.Spme.Domain.Hr.Employee employee,
        Csir.Spme.Domain.Hr.EmploymentRecord employment,
        PromotionCycle cycle,
        PromotionPath path,
        Guid sourceGradeId,
        CancellationToken cancellationToken)
    {
        var qualifications = await db.EducationRecords.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id && item.QualificationLevel == path.RequiredQualificationLevel)
            .Select(item => new { item.InstitutionRecognitionStatus, item.RelevantFieldStatus })
            .ToListAsync(cancellationToken);
        var appraisals = await db.PerformanceAppraisals.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id && item.Status == AppraisalStatuses.Approved)
            .Select(item => new { item.Outcome, item.ApprovedAt })
            .ToListAsync(cancellationToken);
        var selfReportedPromotionDate = await db.EmployeeGradePromotionDates.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id && item.GradeId == sourceGradeId)
            .Select(item => (DateTime?)item.PromotionDate)
            .FirstOrDefaultAsync(cancellationToken);
        var presentGradeStartDate = PromotionStatusWindowBuilder.ResolvePresentGradeStartDate(
            employment.AppointmentDate,
            employment.PromotionDate,
            employment.EffectiveFrom,
            selfReportedPromotionDate);
        var evaluation = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            employment.StaffCategory,
            path.Status,
            presentGradeStartDate,
            path.MinimumYearsInSourceGrade,
            cycle.EffectivePromotionDate,
            qualifications.Any(item => item.InstitutionRecognitionStatus == "verified" && item.RelevantFieldStatus == "verified"),
            qualifications.Any(item => item.InstitutionRecognitionStatus == "rejected" || item.RelevantFieldStatus == "rejected"),
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "satisfactory"),
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "unsatisfactory")));
        var assessment = PromotionAssessment.Create(
            employee.Id,
            employee.InstituteId,
            cycle.Id,
            path.Id,
            employment.Id,
            sourceGradeId,
            path.TargetGradeId,
            DateTime.UtcNow.Date,
            cycle.EffectivePromotionDate,
            presentGradeStartDate,
            evaluation.ServiceRequirementMetOn,
            evaluation.CompletedSourceGradeYears,
            evaluation.EligibilityState,
            JsonSerializer.Serialize(evaluation.BlockingReasons),
            JsonSerializer.Serialize(evaluation.PendingHrChecks),
            JsonSerializer.Serialize(evaluation),
            null);
        db.PromotionAssessments.Add(assessment);
        if (!await db.PromotionStatusSnapshots.AnyAsync(item =>
                item.EmployeeId == employee.Id && item.PromotionCycleId == cycle.Id, cancellationToken))
        {
            db.PromotionStatusSnapshots.Add(PromotionStatusSnapshot.FromAssessment(
                assessment,
                employment.StaffCategory ?? string.Empty));
        }

        await db.SaveChangesAsync(cancellationToken);
        return assessment;
    }
}

internal sealed record PromotionAssessmentResolution(
    PromotionAssessment? Assessment,
    PromotionApplicationWindowResponse? ApplicationWindow,
    bool CanCreate,
    string? FailureKind)
{
    public static PromotionAssessmentResolution NotFound() => new(null, null, false, "not-found");
    public static PromotionAssessmentResolution Conflict(string message) => new(null, null, false, message);
}
