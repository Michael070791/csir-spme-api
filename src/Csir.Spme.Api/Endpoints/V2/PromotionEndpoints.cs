using System.Security.Claims;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var catalog = endpoints.MapGroup("/api/v2")
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization(AuthorizationPolicies.ReadPromotions);

        catalog.MapGet("/promotion-cycles", GetCyclesAsync)
            .WithName("PromotionCycles_List")
            .WithSummary("List configured promotion cycles.")
            .WithDescription("Returns every promotion cycle newest-first, including the fixed 1 January effective date and whether the cycle is planned, open, closed, or finalized. Institute HR uses this catalogue when opening assessments; it is not employee self-service.")
            .Produces<CollectionResponse<PromotionCycleResponse>>(StatusCodes.Status200OK);
        catalog.MapGet("/promotion-paths", GetPathsAsync)
            .WithName("PromotionPaths_List")
            .WithSummary("List configured promotion paths.")
            .WithDescription("Returns Senior Staff Conditions of Service Sections 20-22 paths, including the Section 22 administrative path that remains requires-policy-confirmation, plus the Senior Member Assistant Research Scientist to Research Scientist non-PhD upgrade (5 years, M.Sc.). Optional staffCategory and promotionStream filters narrow the catalogue. Each item includes canonical source and target grades, minimum years in the source grade, and the required qualification level.")
            .Produces<CollectionResponse<PromotionPathResponse>>(StatusCodes.Status200OK);
        catalog.MapGet("/promotions/eligibility", GetEligibilityAsync)
            .WithName("Promotions_Eligibility")
            .WithSummary("List institute promotion status snapshots.")
            .WithDescription("Returns persisted promotion status snapshots inside the caller's institute scope, newest cycle first. Optional cycleYear limits the list. This is the HR worklist of assessed employees, not a live eligibility calculator.")
            .Produces<CollectionResponse<PromotionStatusResponse>>(StatusCodes.Status200OK);
        catalog.MapGet("/promotions/due", GetDueAsync)
            .WithName("Promotions_Due")
            .WithSummary("List employees eligible for review.")
            .WithDescription("Returns institute-scoped status snapshots whose eligibilityState is eligible-for-review. Optional cycleYear limits the list. Eligible-for-review means documented checks passed; it is not promotion approval or entitlement.")
            .Produces<CollectionResponse<PromotionStatusResponse>>(StatusCodes.Status200OK);

        endpoints.MapPost("/api/v2/promotion-assessments", CreateAssessmentAsync)
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization(AuthorizationPolicies.WritePromotions)
            .WithName("PromotionAssessments_Create")
            .WithSummary("Create a promotion assessment for one employee and cycle.")
            .WithDescription("Evaluates the employee's current Conditions of Service job title against the matching catalog path, 1 January cycle date, verified qualification or satisfactory appraisal evidence, and path status. Duplicate employee-cycle-path rows, missing grades, and unmatched paths return conflict. The resulting snapshot is what unlocks start-promotion-submission for the employee.")
            .Produces<PromotionAssessmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        catalog.MapGet("/promotion-assessments/{assessmentId:guid}", GetAssessmentAsync)
            .WithName("PromotionAssessments_Get")
            .WithSummary("Get one promotion assessment.")
            .WithDescription("Returns a persisted assessment when it is visible in the caller's institute scope. Unknown identifiers and cross-institute identifiers share the same not-found outcome.")
            .Produces<PromotionAssessmentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var self = endpoints.MapGroup("/api/v2/promotion-status")
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization(AuthorizationPolicies.ReadOwnPromotionStatus);
        self.MapGet("/me", GetMyStatusAsync)
            .WithName("PromotionStatus_GetMine")
            .WithSummary("Get my promotion status.")
            .WithDescription("Returns the authenticated employee's confidential promotion status for an optional cycle year. When HR has not stored a snapshot, a live status is calculated from the open 1 January cycle, canonical grade, and evidence. Senior Members receive a coming-soon not-applicable result and cannot start an application. Unlinked identities receive a non-disclosing not-found response. Cache-Control is no-store.")
            .Produces<PromotionStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        endpoints.MapPost("/api/v2/promotion-status-lookups", LookupMyStatusAsync)
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization(AuthorizationPolicies.ReadOwnPromotionStatus)
            .RequireRateLimiting("promotion-status-lookup")
            .WithName("PromotionStatus_Lookup")
            .WithSummary("Verify my staff ID and category, then return promotion status.")
            .WithDescription("Confirms that the submitted staffId and staffCategory (junior-staff, senior-staff, or senior-member) match the authenticated employee, then returns the same payload as GET /promotion-status/me. Mismatches and missing links share a non-disclosing not-found outcome. Invalid categories or cycle years return 422. Limited to 5 requests per 15 minutes. This is not a login and never returns another employee's record.")
            .Produces<PromotionStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapGet("/api/v2/employees/{employeeId:guid}/promotion-status", GetEmployeeStatusAsync)
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization(AuthorizationPolicies.ReadPromotions)
            .WithName("Employees_GetPromotionStatus")
            .WithSummary("Get an employee's promotion status for HR.")
            .WithDescription("Returns one employee's promotion status for an optional cycle year when the employee is inside the caller's institute scope. Assessed records include criteria and permitted lifecycle actions. Cross-institute identifiers return not-found.")
            .Produces<PromotionStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Ok<CollectionResponse<PromotionCycleResponse>>> GetCyclesAsync(
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        var items = await db.PromotionCycles.AsNoTracking().OrderByDescending(cycle => cycle.CycleYear)
            .Select(cycle => new PromotionCycleResponse(cycle.CycleYear, cycle.EffectivePromotionDate, cycle.Status))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<PromotionCycleResponse>(items, items.Count));
    }

    private static async Task<Ok<CollectionResponse<PromotionPathResponse>>> GetPathsAsync(
        SpmeDbContext db,
        string? staffCategory,
        string? promotionStream,
        CancellationToken cancellationToken)
    {
        var query = db.PromotionPaths.AsNoTracking()
            .Where(path => path.Status == PromotionConstants.PathActive ||
                           path.Status == PromotionConstants.PathRequiresPolicyConfirmation);
        if (!string.IsNullOrWhiteSpace(staffCategory)) query = query.Where(path => path.StaffCategory == staffCategory);
        if (!string.IsNullOrWhiteSpace(promotionStream)) query = query.Where(path => path.PromotionStream == promotionStream);
        var items = await query.OrderBy(path => path.Code).Select(path => new PromotionPathResponse(
            path.Id, path.Code, path.SectionReference, path.StaffCategory, path.PromotionStream, path.SourceGradeId,
            path.TargetGradeId, path.MinimumYearsInSourceGrade, path.RequiredQualificationLevel, path.Status,
            path.EffectiveFrom, path.EffectiveTo)).ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<PromotionPathResponse>(items, items.Count));
    }

    private static async Task<Ok<CollectionResponse<PromotionStatusResponse>>> GetEligibilityAsync(
        SpmeDbContext db, HttpContext context, short? cycleYear, CancellationToken cancellationToken)
    {
        var items = await StatusQuery(db, context, cycleYear).ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<PromotionStatusResponse>(items, items.Count));
    }

    private static async Task<Ok<CollectionResponse<PromotionStatusResponse>>> GetDueAsync(
        SpmeDbContext db, HttpContext context, short? cycleYear, CancellationToken cancellationToken)
    {
        var items = await StatusQuery(db, context, cycleYear)
            .Where(status => status.EligibilityState == PromotionConstants.EligibilityEligibleForReview)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CollectionResponse<PromotionStatusResponse>(items, items.Count));
    }

    private static async Task<Results<Created<PromotionAssessmentResponse>, ProblemHttpResult>> CreateAssessmentAsync(
        CreatePromotionAssessmentRequest request, SpmeDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken);
        var cycle = await db.PromotionCycles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.PromotionCycleId, cancellationToken);
        if (employee is null || cycle is null || !HasInstituteAccess(context, employee.InstituteId))
            return EndpointProblems.FromError(Error.NotFound("Promotion assessment target not found."));

        var employment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeId == employee.Id && item.IsCurrent, cancellationToken);
        if (employment?.GradeId is null)
            return EndpointProblems.FromError(Error.Conflict("The employee does not have a current canonical grade."));

        var path = await MatchingPathQuery(db, employment.StaffCategory ?? string.Empty, employment.GradeId.Value, cycle.EffectivePromotionDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (path is null)
            return EndpointProblems.FromError(Error.Conflict("No approved promotion path applies to the employee's current grade."));

        var alreadyAssessed = await db.PromotionAssessments.AsNoTracking().AnyAsync(item =>
            item.EmployeeId == employee.Id && item.PromotionCycleId == cycle.Id && item.PromotionPathId == path.Id, cancellationToken);
        if (alreadyAssessed)
            return EndpointProblems.FromError(Error.Conflict("A promotion assessment already exists for this employee, cycle, and path."));

        var qualifications = await db.EducationRecords.AsNoTracking()
            .Where(item => item.EmployeeId == employee.Id && item.QualificationLevel == path.RequiredQualificationLevel)
            .Select(item => new { item.InstitutionRecognitionStatus, item.RelevantFieldStatus }).ToListAsync(cancellationToken);
        var appraisals = await db.PerformanceAppraisals.AsNoTracking().Where(item => item.EmployeeId == employee.Id)
            .Select(item => new { item.Outcome, item.ApprovedAt }).ToListAsync(cancellationToken);
        var evaluation = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            employment.StaffCategory, path.Status, employment.EffectiveFrom, path.MinimumYearsInSourceGrade,
            cycle.EffectivePromotionDate,
            qualifications.Any(item => item.InstitutionRecognitionStatus == "verified" && item.RelevantFieldStatus == "verified"),
            qualifications.Any(item => item.InstitutionRecognitionStatus == "rejected" || item.RelevantFieldStatus == "rejected"),
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "satisfactory"),
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "unsatisfactory")));
        var assessment = PromotionAssessment.Create(
            employee.Id, employee.InstituteId, cycle.Id, path.Id, employment.Id, employment.GradeId.Value, path.TargetGradeId,
            DateTime.UtcNow.Date, cycle.EffectivePromotionDate, employment.EffectiveFrom, evaluation.ServiceRequirementMetOn,
            evaluation.CompletedSourceGradeYears, evaluation.EligibilityState, JsonSerializer.Serialize(evaluation.BlockingReasons),
            JsonSerializer.Serialize(evaluation.PendingHrChecks), JsonSerializer.Serialize(evaluation), CurrentUserId(context));
        db.PromotionAssessments.Add(assessment);
        if (!await db.PromotionStatusSnapshots.AnyAsync(item => item.EmployeeId == employee.Id && item.PromotionCycleId == cycle.Id, cancellationToken))
            db.PromotionStatusSnapshots.Add(PromotionStatusSnapshot.FromAssessment(assessment, employment.StaffCategory ?? string.Empty));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToAssessmentResponse(assessment);
        return TypedResults.Created($"/api/v2/promotion-assessments/{assessment.Id}", response);
    }

    private static async Task<Results<Ok<PromotionAssessmentResponse>, ProblemHttpResult>> GetAssessmentAsync(
        Guid assessmentId, SpmeDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var instituteId = CurrentInstituteId(context);
        var query = db.PromotionAssessments.AsNoTracking().Where(item => item.Id == assessmentId);
        if (instituteId.HasValue) query = query.Where(item => item.InstituteId == instituteId.Value);
        var assessment = await query.SingleOrDefaultAsync(cancellationToken);
        return assessment is null ? EndpointProblems.FromError(Error.NotFound("Promotion assessment not found.")) : TypedResults.Ok(ToAssessmentResponse(assessment));
    }

    private static async Task<Results<Ok<PromotionStatusResponse>, ProblemHttpResult>> GetMyStatusAsync(
        SpmeDbContext db, HttpContext context, short? cycleYear, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryGetSelfEmployeeId(context, out var employeeId))
            return EndpointProblems.FromError(Error.NotFound("Promotion status not found."));
        return await ResolveSelfStatusAsync(db, context, employeeId, cycleYear, cancellationToken);
    }

    private static async Task<Results<Ok<PromotionStatusResponse>, ProblemHttpResult>> LookupMyStatusAsync(
        PromotionStatusLookupRequest request, SpmeDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (request.CycleYear is < 2000 or > 9999 || !IsSupportedStaffCategory(request.StaffCategory))
            return EndpointProblems.Unprocessable("The promotion status request is invalid.");
        if (!TryGetSelfEmployeeId(context, out var employeeId)) return EndpointProblems.FromError(Error.NotFound("Promotion status not found."));

        var normalizedStaffId = request.StaffId.Trim().ToUpperInvariant();
        var normalizedStaffCategory = request.StaffCategory.Trim().ToLowerInvariant();
        var identity = await (from employee in db.Employees.AsNoTracking()
                              join employment in db.EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
                              where employee.Id == employeeId && employment.IsCurrent
                              select new { employee.NormalizedStaffId, employment.StaffCategory })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null || identity.NormalizedStaffId != normalizedStaffId ||
            !string.Equals(identity.StaffCategory, normalizedStaffCategory, StringComparison.OrdinalIgnoreCase))
            return EndpointProblems.FromError(Error.NotFound("Promotion status not found."));

        var status = await ResolveSelfStatusAsync(db, context, employeeId, request.CycleYear, cancellationToken);
        return status;
    }

    private static async Task<Results<Ok<PromotionStatusResponse>, ProblemHttpResult>> GetEmployeeStatusAsync(
        Guid employeeId, SpmeDbContext db, HttpContext context, short? cycleYear, CancellationToken cancellationToken)
    {
        var query = StatusQuery(db, context, cycleYear, employeeId);
        var status = await query.FirstOrDefaultAsync(cancellationToken);
        return status is null
            ? EndpointProblems.FromError(Error.NotFound("Promotion status not found."))
            : TypedResults.Ok(await EnrichStatusAsync(db, status, cancellationToken));
    }

    private static async Task<Results<Ok<PromotionStatusResponse>, ProblemHttpResult>> ResolveSelfStatusAsync(
        SpmeDbContext db,
        HttpContext context,
        Guid employeeId,
        short? cycleYear,
        CancellationToken cancellationToken)
    {
        var snapshot = await StatusQuery(db, context, cycleYear, employeeId).FirstOrDefaultAsync(cancellationToken);
        if (snapshot is not null)
            return TypedResults.Ok(await EnrichStatusAsync(db, snapshot, cancellationToken));

        var live = await BuildLiveStatusAsync(db, employeeId, cycleYear, cancellationToken);
        return live is null
            ? EndpointProblems.FromError(Error.NotFound("Promotion status not found."))
            : TypedResults.Ok(live);
    }

    private static async Task<PromotionStatusResponse?> BuildLiveStatusAsync(
        SpmeDbContext db,
        Guid employeeId,
        short? cycleYear,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
            return null;

        var employment = await db.EmploymentRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.IsCurrent, cancellationToken);
        var cycles = db.PromotionCycles.AsNoTracking();
        if (cycleYear.HasValue)
            cycles = cycles.Where(cycle => cycle.CycleYear == cycleYear.Value);
        var cycle = await cycles
            .OrderBy(cycle => cycle.Status == PromotionConstants.CycleOpen ? 0 : 1)
            .ThenByDescending(cycle => cycle.CycleYear)
            .FirstOrDefaultAsync(cancellationToken);

        var staffCategory = employment?.StaffCategory ?? string.Empty;
        var appointmentDate = employment?.AppointmentDate;
        var lastPromotionDate = employment?.PromotionDate;
        var sourceEffective = employment?.EffectiveFrom;
        var placeholderYear = cycle?.CycleYear ?? cycleYear ?? PromotionConstants.CurrentCycleYear;
        var placeholderDate = cycle?.EffectivePromotionDate ?? new DateTime(placeholderYear, 1, 1);

        PromotionGradeRef? currentGrade = null;
        if (employment?.GradeId is Guid sourceGradeId)
        {
            var sourceGrade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceGradeId, cancellationToken);
            if (sourceGrade is not null)
                currentGrade = new PromotionGradeRef(sourceGrade.Code, sourceGrade.Name);
        }

        if (!PromotionEligibilityEvaluator.IsAssessableStaffCategory(staffCategory))
        {
            return LiveStatus(
                employee.StaffId, staffCategory, placeholderYear, placeholderDate,
                PromotionConstants.EligibilityNotApplicable,
                [new("staff-category", "not-met", staffCategory)],
                PromotionStatusMessages.ForStaffCategory(staffCategory),
                employment?.GradeId, null, currentGrade, null, null,
                appointmentDate, lastPromotionDate, sourceEffective);
        }

        if (cycle is null)
        {
            return LiveStatus(
                employee.StaffId, staffCategory, placeholderYear, placeholderDate,
                PromotionConstants.EligibilityNoRuleDefined,
                [new("staff-category", "satisfied", staffCategory)],
                PromotionStatusMessages.NoCycleOpen,
                employment?.GradeId, null, currentGrade, null, null,
                appointmentDate, lastPromotionDate, sourceEffective);
        }

        if (employment?.GradeId is null)
        {
            return LiveStatus(
                employee.StaffId, staffCategory, cycle.CycleYear, cycle.EffectivePromotionDate,
                PromotionConstants.EligibilityNeedsHrReview,
                [
                    new("staff-category", "satisfied", staffCategory),
                    new("time-in-source-grade", "pending-hr-review"),
                    new("qualification", "pending-hr-review"),
                    new("recognised-institution", "pending-hr-review"),
                    new("relevant-field", "pending-hr-review"),
                    new("satisfactory-appraisal", "pending-hr-review")
                ],
                PromotionStatusMessages.NoCanonicalGrade,
                null, null, currentGrade, null, null,
                appointmentDate, lastPromotionDate, sourceEffective);
        }

        var path = await MatchingPathQuery(db, staffCategory, employment.GradeId.Value, cycle.EffectivePromotionDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (path is null)
        {
            var noPathMessage = string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase)
                ? PromotionStatusMessages.SeniorMemberComingSoon
                : PromotionStatusMessages.NoMatchingPath;
            var noPathState = string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase)
                ? PromotionConstants.EligibilityNotApplicable
                : PromotionConstants.EligibilityNeedsHrReview;
            return LiveStatus(
                employee.StaffId, staffCategory, cycle.CycleYear, cycle.EffectivePromotionDate,
                noPathState,
                [
                    new("staff-category", "satisfied", staffCategory),
                    new("time-in-source-grade", "pending-hr-review"),
                    new("qualification", "pending-hr-review"),
                    new("recognised-institution", "pending-hr-review"),
                    new("relevant-field", "pending-hr-review"),
                    new("satisfactory-appraisal", "pending-hr-review")
                ],
                noPathMessage,
                employment.GradeId, null, currentGrade, null, null,
                appointmentDate, lastPromotionDate, sourceEffective);
        }

        var qualifications = await db.EducationRecords.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.QualificationLevel == path.RequiredQualificationLevel)
            .Select(item => new { item.InstitutionRecognitionStatus, item.RelevantFieldStatus })
            .ToListAsync(cancellationToken);
        var qualificationSatisfied = qualifications.Any(item =>
            item.InstitutionRecognitionStatus == "verified" && item.RelevantFieldStatus == "verified");
        var qualificationRejected = qualifications.Any(item =>
            item.InstitutionRecognitionStatus == "rejected" || item.RelevantFieldStatus == "rejected");
        var appraisals = await db.PerformanceAppraisals.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .Select(item => new { item.Outcome, item.ApprovedAt })
            .ToListAsync(cancellationToken);
        var evaluation = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            staffCategory,
            path.Status,
            sourceEffective ?? cycle.EffectivePromotionDate,
            path.MinimumYearsInSourceGrade,
            cycle.EffectivePromotionDate,
            qualificationSatisfied,
            qualificationRejected,
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "satisfactory"),
            appraisals.Any(item => item.ApprovedAt.HasValue && item.Outcome == "unsatisfactory")));

        PromotionNextPromotion? nextPromotion = null;
        if (path.TargetGradeId is Guid targetGradeId)
        {
            var targetGrade = await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == targetGradeId, cancellationToken);
            if (targetGrade is not null)
            {
                nextPromotion = new PromotionNextPromotion(
                    path.Code,
                    path.SectionReference,
                    new PromotionGradeRef(targetGrade.Code, targetGrade.Name),
                    path.MinimumYearsInSourceGrade,
                    evaluation.ServiceRequirementMetOn);
            }
        }

        return LiveStatus(
            employee.StaffId, staffCategory, cycle.CycleYear, cycle.EffectivePromotionDate,
            evaluation.EligibilityState,
            BuildCriteria(evaluation, path.MinimumYearsInSourceGrade, path.RequiredQualificationLevel, staffCategory),
            LiveNextAction(evaluation, cycle.CycleYear, path.MinimumYearsInSourceGrade, staffCategory),
            employment.GradeId, path.TargetGradeId, currentGrade, nextPromotion,
            evaluation.EligibilityState == PromotionConstants.EligibilityPolicyAmbiguity ? path.SectionReference : null,
            appointmentDate, lastPromotionDate, sourceEffective);
    }

    private static PromotionStatusResponse LiveStatus(
        string staffId, string staffCategory, short cycleYear, DateTime effectivePromotionDate,
        string eligibility, IReadOnlyList<PromotionStatusCriterion> criteria, string nextAction,
        Guid? sourceGradeId, Guid? targetGradeId, PromotionGradeRef? currentGrade, PromotionNextPromotion? nextPromotion,
        string? affectedSection, DateTime? appointmentDate, DateTime? lastPromotionDate, DateTime? sourceEffective) =>
        new(staffId, staffCategory, cycleYear, effectivePromotionDate, PromotionConstants.AssessmentNotAssessed,
            eligibility, null, null, null, sourceGradeId, targetGradeId, DateTimeOffset.UtcNow, criteria, [],
            nextAction, currentGrade, nextPromotion, affectedSection, appointmentDate, lastPromotionDate, sourceEffective);

    private static IQueryable<PromotionPath> MatchingPathQuery(
        SpmeDbContext db,
        string staffCategory,
        Guid sourceGradeId,
        DateTime effectivePromotionDate) =>
        db.PromotionPaths.AsNoTracking()
            .Where(item => item.StaffCategory == staffCategory && item.SourceGradeId == sourceGradeId)
            .Where(item => item.Status != PromotionConstants.PathInactive)
            .Where(item => item.EffectiveFrom <= effectivePromotionDate &&
                           (item.EffectiveTo == null || item.EffectiveTo >= effectivePromotionDate))
            .OrderBy(item => item.Status == PromotionConstants.PathActive ? 0 : 1);

    private static IReadOnlyList<PromotionStatusCriterion> BuildCriteria(
        PromotionEligibilityEvaluation evaluation, short minimumYears, string? requiredQualification, string staffCategory)
    {
        if (evaluation.EligibilityState == PromotionConstants.EligibilityNotApplicable)
            return [new("staff-category", "not-met", staffCategory)];

        var qualificationStatus = PromotionEligibilityEvaluator.EvidenceCriterionStatus(
            evaluation.QualificationSatisfied, evaluation.QualificationRejected);
        var appraisalStatus = PromotionEligibilityEvaluator.EvidenceCriterionStatus(
            evaluation.AppraisalSatisfied, evaluation.AppraisalRejected);
        return
        [
            new("staff-category", "satisfied", staffCategory),
            Criterion("time-in-source-grade", evaluation.BlockingReasons ?? [], evaluation.PendingHrChecks ?? [], "source-grade-service",
                $"{minimumYears} years"),
            new("qualification", qualificationStatus, requiredQualification),
            new("recognised-institution", qualificationStatus),
            new("relevant-field", qualificationStatus),
            new("satisfactory-appraisal", appraisalStatus)
        ];
    }

    private static string LiveNextAction(
        PromotionEligibilityEvaluation evaluation,
        short cycleYear,
        short requiredYears,
        string staffCategory) =>
        evaluation.EligibilityState switch
        {
            PromotionConstants.EligibilityEligibleForReview => PromotionStatusMessages.EligibleAwaitingAssessment,
            PromotionConstants.EligibilityNotYetEligible when evaluation.ServiceRequirementMetOn is DateTime metOn =>
                PromotionStatusMessages.NotYetEligible(requiredYears, metOn, cycleYear),
            PromotionConstants.EligibilityNotYetEligible => PromotionStatusMessages.NoCycleOpen,
            PromotionConstants.EligibilityPolicyAmbiguity => PromotionStatusMessages.PolicyAmbiguity,
            PromotionConstants.EligibilityNeedsHrReview => PromotionStatusMessages.NeedsHrReview,
            PromotionConstants.EligibilityNotEligible => PromotionStatusMessages.NotEligible,
            PromotionConstants.EligibilityNotApplicable => PromotionStatusMessages.ForStaffCategory(staffCategory),
            _ => PromotionStatusMessages.AwaitHrAssessment
        };

    private static IQueryable<PromotionStatusResponse> StatusQuery(SpmeDbContext db, HttpContext context, short? cycleYear, Guid? employeeId = null)
    {
        var instituteId = CurrentInstituteId(context);
        var query = from snapshot in db.PromotionStatusSnapshots.AsNoTracking()
                    join employee in db.Employees.AsNoTracking() on snapshot.EmployeeId equals employee.Id
                    join cycle in db.PromotionCycles.AsNoTracking() on snapshot.PromotionCycleId equals cycle.Id
                    select new { snapshot, employee, cycle };
        if (instituteId.HasValue) query = query.Where(item => item.snapshot.InstituteId == instituteId.Value);
        if (employeeId.HasValue) query = query.Where(item => item.snapshot.EmployeeId == employeeId.Value);
        if (cycleYear.HasValue) query = query.Where(item => item.cycle.CycleYear == cycleYear.Value);
        return query.OrderByDescending(item => item.cycle.CycleYear).Select(item => new PromotionStatusResponse(
            item.employee.StaffId, item.snapshot.StaffCategory, item.cycle.CycleYear, item.cycle.EffectivePromotionDate,
            item.snapshot.AssessmentState, item.snapshot.EligibilityState, item.snapshot.PromotionSubmissionStatus,
            item.snapshot.LatestAssessmentId, item.snapshot.LatestPromotionSubmissionId, item.snapshot.SourceGradeId,
            item.snapshot.TargetGradeId, item.snapshot.CalculatedAt));
    }

    private static async Task<PromotionStatusResponse> EnrichStatusAsync(
        SpmeDbContext db, PromotionStatusResponse status, CancellationToken ct)
    {
        var employmentDates = await (from employee in db.Employees.AsNoTracking()
                                     join employment in db.EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
                                     where employee.StaffId == status.StaffId && employment.IsCurrent
                                     select new { employment.AppointmentDate, employment.PromotionDate })
            .FirstOrDefaultAsync(ct);
        var appointmentDate = employmentDates?.AppointmentDate;
        var lastPromotionDate = employmentDates?.PromotionDate;
        DateTime? sourceGradeEffectiveDate = null;
        if (status.LatestAssessmentId is Guid assessmentId)
        {
            sourceGradeEffectiveDate = await db.PromotionAssessments.AsNoTracking()
                .Where(item => item.Id == assessmentId)
                .Select(item => (DateTime?)item.SourceGradeEffectiveDate)
                .FirstOrDefaultAsync(ct);
        }

        if (status.AssessmentState != PromotionConstants.AssessmentAssessed || status.LatestAssessmentId is null)
        {
            return status with
            {
                Criteria = [],
                AvailableActions = [],
                NextAction = status.StaffCategory is StaffCategories.SeniorMember or StaffCategories.JuniorStaff
                    ? PromotionStatusMessages.ForStaffCategory(status.StaffCategory)
                    : PromotionStatusMessages.AwaitHrAssessment,
                AppointmentDate = appointmentDate,
                LastPromotionDate = lastPromotionDate,
                SourceGradeEffectiveDate = sourceGradeEffectiveDate
            };
        }

        var assessment = await db.PromotionAssessments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == status.LatestAssessmentId.Value, ct);
        if (assessment is null)
            return status with
            {
                Criteria = [], AvailableActions = [], NextAction = PromotionStatusMessages.AwaitHrAssessment,
                AppointmentDate = appointmentDate, LastPromotionDate = lastPromotionDate,
                SourceGradeEffectiveDate = sourceGradeEffectiveDate
            };
        sourceGradeEffectiveDate = assessment.SourceGradeEffectiveDate;

        var path = await db.PromotionPaths.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assessment.PromotionPathId, ct);
        var sourceGrade = status.SourceGradeId is Guid sourceId
            ? await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceId, ct)
            : null;
        var targetGrade = status.TargetGradeId is Guid targetId
            ? await db.Grades.AsNoTracking().SingleOrDefaultAsync(item => item.Id == targetId, ct)
            : null;
        var blocking = DeserializeStrings(assessment.BlockingReasonsJson);
        var pending = DeserializeStrings(assessment.PendingHrChecksJson);
        var eligibility = status.EligibilityState;
        var currentGrade = sourceGrade is null ? null : new PromotionGradeRef(sourceGrade.Code, sourceGrade.Name);
        var nextPromotion = path is not null && targetGrade is not null &&
            eligibility is not PromotionConstants.EligibilityNotApplicable
            ? new PromotionNextPromotion(path.Code, path.SectionReference,
                new PromotionGradeRef(targetGrade.Code, targetGrade.Name),
                path.MinimumYearsInSourceGrade, assessment.ServiceRequirementMetOn)
            : null;
        var affectedSection = eligibility == PromotionConstants.EligibilityPolicyAmbiguity
            ? path?.SectionReference
            : null;
        var snapshot = TryReadEvaluation(assessment.EligibilitySnapshotJson);
        var criteria = snapshot is not null
            ? BuildCriteria(snapshot, path?.MinimumYearsInSourceGrade ?? 0, path?.RequiredQualificationLevel, status.StaffCategory).ToList()
            : eligibility == PromotionConstants.EligibilityNotApplicable
            ? new List<PromotionStatusCriterion>
            {
                new("staff-category", "not-met", status.StaffCategory)
            }
            : new List<PromotionStatusCriterion>
            {
                new("staff-category", "satisfied", status.StaffCategory),
                Criterion("time-in-source-grade", blocking, pending, "source-grade-service",
                    path is null ? null : $"{path.MinimumYearsInSourceGrade} years"),
                Criterion("qualification", blocking, pending, "qualification", path?.RequiredQualificationLevel),
                Criterion("recognised-institution", blocking, pending, "qualification"),
                Criterion("relevant-field", blocking, pending, "qualification"),
                Criterion("satisfactory-appraisal", blocking, pending, "satisfactory-appraisal")
            };

        var actions = new List<string>();
        var submissionStatus = status.PromotionSubmissionStatus;
        if (eligibility == PromotionConstants.EligibilityEligibleForReview &&
            (submissionStatus is null or PromotionConstants.SubmissionWithdrawn or PromotionConstants.SubmissionCancelled))
            actions.Add("start-promotion-submission");
        if (submissionStatus is PromotionConstants.SubmissionDraft or PromotionConstants.SubmissionReturned)
            actions.AddRange(["edit", "submit", "withdraw"]);
        else if (submissionStatus is PromotionConstants.SubmissionSubmitted
            or PromotionConstants.SubmissionUnderReview or PromotionConstants.SubmissionAcknowledged)
            actions.Add("withdraw");

        var nextAction = eligibility switch
        {
            PromotionConstants.EligibilityEligibleForReview when actions.Contains("start-promotion-submission") =>
                PromotionStatusMessages.EligibleStartSubmission,
            PromotionConstants.EligibilityEligibleForReview when actions.Contains("edit") =>
                PromotionStatusMessages.EligibleCompleteRequirements,
            PromotionConstants.EligibilityEligibleForReview => PromotionStatusMessages.EligibleWaitForHr,
            PromotionConstants.EligibilityNotYetEligible when assessment.ServiceRequirementMetOn is DateTime metOn =>
                PromotionStatusMessages.NotYetEligible(path?.MinimumYearsInSourceGrade ?? 0, metOn, status.CycleYear),
            PromotionConstants.EligibilityNotYetEligible => PromotionStatusMessages.NoCycleOpen,
            PromotionConstants.EligibilityPolicyAmbiguity => PromotionStatusMessages.PolicyAmbiguity,
            PromotionConstants.EligibilityNeedsHrReview => PromotionStatusMessages.NeedsHrReview,
            PromotionConstants.EligibilityNotEligible => PromotionStatusMessages.NotEligible,
            PromotionConstants.EligibilityNotApplicable => PromotionStatusMessages.ForStaffCategory(status.StaffCategory),
            _ => PromotionStatusMessages.AwaitHrAssessment
        };

        return status with
        {
            Criteria = criteria,
            AvailableActions = actions,
            NextAction = nextAction,
            CurrentGrade = currentGrade,
            NextPromotion = nextPromotion,
            AffectedPolicySection = affectedSection,
            AppointmentDate = appointmentDate,
            LastPromotionDate = lastPromotionDate,
            SourceGradeEffectiveDate = sourceGradeEffectiveDate
        };
    }

    private static PromotionStatusCriterion Criterion(
        string code, IReadOnlyList<string> blocking, IReadOnlyList<string> pending, string reasonCode, string? required = null)
    {
        if (blocking.Contains(reasonCode, StringComparer.OrdinalIgnoreCase))
            return new(code, "not-met", required);
        if (pending.Contains(reasonCode, StringComparer.OrdinalIgnoreCase))
            return new(code, "pending-hr-review", required);
        return new(code, "satisfied", required);
    }

    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Guid? CurrentInstituteId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("institute_id"), out var instituteId) ? instituteId : null;

    private static bool TryGetSelfEmployeeId(HttpContext context, out Guid employeeId)
    {
        employeeId = Guid.Empty;
        var self = context.User.FindFirstValue("self");
        if (self is not null && self.StartsWith("Self:", StringComparison.Ordinal) &&
            Guid.TryParse(self["Self:".Length..], out employeeId))
            return true;

        return Guid.TryParse(context.User.FindFirstValue("employee_id"), out employeeId);
    }

    private static bool IsSupportedStaffCategory(string staffCategory) =>
        StaffCategories.All.Contains(staffCategory.Trim(), StringComparer.OrdinalIgnoreCase);

    private static PromotionEligibilityEvaluation? TryReadEvaluation(string json)
    {
        try
        {
            var evaluation = JsonSerializer.Deserialize<PromotionEligibilityEvaluation>(json);
            if (evaluation is null || evaluation.BlockingReasons is null || evaluation.PendingHrChecks is null)
                return null;
            return evaluation;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasInstituteAccess(HttpContext context, Guid instituteId) =>
        !CurrentInstituteId(context).HasValue || CurrentInstituteId(context) == instituteId;

    private static Guid? CurrentUserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private static PromotionAssessmentResponse ToAssessmentResponse(PromotionAssessment assessment) => new(
        assessment.Id, assessment.EmployeeId, assessment.PromotionCycleId, assessment.PromotionPathId,
        assessment.SourceGradeId, assessment.TargetGradeId, assessment.EffectivePromotionDate,
        assessment.ServiceRequirementMetOn, assessment.CompletedSourceGradeYears, assessment.EligibilityState, assessment.AssessedAt);
}
