using System.Security.Claims;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using Csir.Spme.Api.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class AppraisalEndpoints
{
    public static void MapAppraisalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var root = endpoints.MapGroup("/api/v2/performance-appraisals").WithGroupName("v2").WithTags("Appraisals")
            .RequireAuthorization().WithDescription("Confidential annual performance appraisal forms follow the official CSIR Parts I through VI workflow and are visible only to the employee and currently assigned reviewers until final approval.")
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);

        root.MapGet("/me", MineAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnAppraisals).WithName("PerformanceAppraisals_Me")
            .WithSummary("List my appraisals.").WithDescription("Lists appraisal summaries only for the authenticated employee identity within its verified institute scope.").Produces<CollectionResponse<AppraisalSummaryResponse>>();
        root.MapGet("/review-queue", ReviewQueueAsync).RequireAuthorization().WithName("PerformanceAppraisals_ReviewQueue")
            .WithSummary("List assigned appraisal reviews.").WithDescription("Lists only appraisals currently assigned to the authenticated HOD or final approver.").Produces<CollectionResponse<AppraisalSummaryResponse>>();
        root.MapGet("", FinalListAsync).RequireAuthorization(AuthorizationPolicies.ReadAppraisals).WithName("PerformanceAppraisals_List")
            .WithSummary("List final appraisals.").WithDescription("Lists approved appraisal summaries for HR's authenticated institute; draft confidential form content is excluded.").Produces<CollectionResponse<AppraisalSummaryResponse>>();
        root.MapGet("/{id:guid}", GetAsync).WithName("PerformanceAppraisals_Get").WithSummary("Get an appraisal form.")
            .WithDescription("Returns official structured appraisal form data to the employee or assigned reviewer; HR final-read access is limited to approved appraisals and decline reasons are excluded.")
            .Produces<PerformanceAppraisalResponse>().ProducesProblem(StatusCodes.Status404NotFound);

        MapMutation<SaveAppraisalPlanningRequest>(root, [HttpMethods.Patch, HttpMethods.Put], "/{id:guid}/planning", "PerformanceAppraisals_SavePlanning", "Save appraisal planning", "Saves official Part I training and Part II targets, resources, timelines, and competencies with immutable target version history. PATCH is canonical; PUT remains a compatibility alias.", SavePlanningAsync);
        MapMutation<AppraisalAttestationRequest>(root, HttpMethods.Post, "/{id:guid}/submit-planning", "PerformanceAppraisals_SubmitPlanning", "Sign and submit appraisal planning", "Records the employee's authenticated planning attestation and submits the plan to the assigned HOD after routing is resolved.", SubmitPlanningAsync);
        MapMutation<AppraisalAttestationRequest>(root, HttpMethods.Post, "/{id:guid}/confirm-planning", "PerformanceAppraisals_ConfirmPlanning", "Agree and sign appraisal planning", "Records the assigned HOD's authenticated agreement, locks the planning baseline, and opens the midyear staff stage.", ConfirmPlanningAsync);
        MapMutation<AppraisalDirectorReturnRequest>(root, HttpMethods.Post, "/{id:guid}/return-planning", "PerformanceAppraisals_ReturnPlanning", "Return appraisal planning", "Returns submitted planning to the employee with a mandatory employee-visible correction reason.", ReturnPlanningAsync);
        MapMutation<SaveAppraisalMidyearRequest>(root, [HttpMethods.Patch, HttpMethods.Put], "/{id:guid}/midyear", "PerformanceAppraisals_SaveMidyear", "Save staff midyear review", "Saves the employee's official midyear progress entries without permitting employee edits to HOD remarks. PATCH is canonical; PUT remains a compatibility alias.", SaveMidyearAsync);
        MapMutation<AppraisalAttestationRequest>(root, HttpMethods.Post, "/{id:guid}/submit-midyear", "PerformanceAppraisals_SubmitMidyear", "Sign and submit staff midyear review", "Records the employee's authenticated midyear submission and sends progress to the assigned HOD.", SubmitMidyearAsync);
        MapMutation<SaveHodMidyearReviewRequest>(root, [HttpMethods.Patch, HttpMethods.Put], "/{id:guid}/hod-midyear-review", "PerformanceAppraisals_SaveHodMidyear", "Save HOD midyear review", "Autosaves the HOD's official remarks and proposed target amendments without creating a signed version or overwriting the employee's progress. PATCH is canonical; PUT remains a compatibility alias.", SaveHodMidyearAsync);
        MapMutation<AppraisalHodSubmissionRequest>(root, HttpMethods.Post, "/{id:guid}/submit-hod-midyear-review", "PerformanceAppraisals_SubmitHodMidyear", "Submit HOD midyear review", "Submits the HOD midyear version to the employee; after a decline a response is mandatory.", SubmitHodMidyearAsync);
        MapMutation<AppraisalStaffSignatureRequest>(root, HttpMethods.Post, "/{id:guid}/midyear-signature", "PerformanceAppraisals_MidyearSignature", "Record the midyear employee signature", "Records acceptance or a mandatory decline reason; acceptance applies mutually reviewed target amendments and routes to final-approver midyear review.", MidyearSignatureAsync);
        MapMutation<AppraisalMidyearDirectorApprovalRequest>(root, HttpMethods.Post, "/{id:guid}/midyear-director-approve", "PerformanceAppraisals_MidyearDirectorApprove", "Approve the midyear review", "The assigned final approver records the official progress comment and alone opens year-end self-assessment.", MidyearApproveAsync);
        MapMutation<AppraisalDirectorReturnRequest>(root, HttpMethods.Post, "/{id:guid}/midyear-director-return", "PerformanceAppraisals_MidyearDirectorReturn", "Return the midyear review", "The assigned final approver records a mandatory return reason and sends the form to HOD midyear review.", MidyearReturnAsync);
        MapMutation<SaveAppraisalYearEndRequest>(root, [HttpMethods.Patch, HttpMethods.Put], "/{id:guid}/year-end", "PerformanceAppraisals_SaveYearEnd", "Save staff year-end results", "Saves the employee's Part III work accomplished, completion percentage, and reason or constraint entries. PATCH is canonical; PUT remains a compatibility alias.", SaveYearEndAsync);
        MapMutation<AppraisalAttestationRequest>(root, HttpMethods.Post, "/{id:guid}/submit-year-end", "PerformanceAppraisals_SubmitYearEnd", "Sign and submit staff year-end results", "Records the employee's authenticated Part III submission and sends it to the assigned HOD.", SubmitYearEndAsync);
        MapMutation<SaveHodAppraisalAssessmentRequest>(root, [HttpMethods.Patch, HttpMethods.Put], "/{id:guid}/hod-assessment", "PerformanceAppraisals_SaveHodAssessment", "Save the HOD assessment", "Autosaves the official Part III target assessment and Part IV factor ratings and calculates the two category scores without creating a signed version. PATCH is canonical; PUT remains a compatibility alias.", SaveHodAsync);
        MapMutation<AppraisalHodSubmissionRequest>(root, HttpMethods.Post, "/{id:guid}/submit-hod-assessment", "PerformanceAppraisals_SubmitHodAssessment", "Submit the HOD assessment", "Submits the latest assessment for employee signature; after a decline the HOD response is mandatory.", SubmitHodAsync);
        MapMutation<AppraisalStaffSignatureRequest>(root, HttpMethods.Post, "/{id:guid}/staff-signature", "PerformanceAppraisals_StaffSignature", "Record the year-end employee signature", "Records acceptance or a mandatory decline reason; decline returns the versioned assessment to the HOD.", YearEndSignatureAsync);
        MapMutation<AppraisalDirectorReturnRequest>(root, HttpMethods.Post, "/{id:guid}/director-return", "PerformanceAppraisals_DirectorReturn", "Return the year-end assessment", "The assigned final approver records a reason and returns the appraisal to the HOD; changed scores or comments require another employee signature.", DirectorReturnAsync);
        MapMutation<AppraisalDirectorApprovalRequest>(root, HttpMethods.Post, "/{id:guid}/director-approve", "PerformanceAppraisals_DirectorApprove", "Approve the final appraisal", "The assigned final approver records official Part V comments and recommendation lines and generates the immutable final official PDF.", DirectorApproveAsync);
        root.MapGet("/{id:guid}/final-document", FinalDocumentAsync).WithName("PerformanceAppraisals_FinalDocument")
            .WithSummary("Download the final appraisal document.").WithDescription("Downloads the immutable branded official appraisal PDF only after final approval and only for an authorized participant or HR final reader.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(StatusCodes.Status404NotFound);
        root.MapGet("/{id:guid}/document", FinalDocumentAsync).WithName("PerformanceAppraisals_Document")
            .WithSummary("Download the final appraisal document.").WithDescription("Canonical final-PDF route. Only a final-approved appraisal is downloadable by an authorized participant or HR final reader.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapMutation(RouteGroupBuilder group, string method, string pattern, string name, string summary, string description,
        Delegate handler) => group.MapMethods(pattern, [method], handler).WithName(name).WithSummary(summary).WithDescription(description)
        .Produces<PerformanceAppraisalResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    private static void MapMutation<T>(RouteGroupBuilder group, string method, string pattern, string name, string summary, string description,
        Delegate handler) => MapMutation(group, method, pattern, name, summary, description, handler);
    private static void MapMutation<T>(RouteGroupBuilder group, string[] methods, string pattern, string name, string summary, string description,
        Delegate handler) => group.MapMethods(pattern, methods, handler).WithName(name).WithSummary(summary).WithDescription(description)
        .Produces<PerformanceAppraisalResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed);

    private static async Task<IResult> MineAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var employee = Employee(context); var institute = Institute(context);
        if (!employee.HasValue || !institute.HasValue) return EndpointProblems.FromError(Error.NotFound("Employee appraisal not found."));
        var items = await db.PerformanceAppraisals.AsNoTracking()
            .Where(x => x.EmployeeId == employee && x.InstituteId == institute)
            .OrderByDescending(x => x.AppraisalPeriodStart).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<AppraisalSummaryResponse>(await Summaries(items, db, true, context, ct), items.Count));
    }

    private static async Task<IResult> ReviewQueueAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var user = User(context); var institute = Institute(context);
        if (!user.HasValue || !institute.HasValue) return EndpointProblems.FromError(Error.Forbidden("A scoped user identity is required."));
        var mayReview = HasPermission(context, SpmePermissions.AppraisalsReview);
        var mayApprove = HasPermission(context, SpmePermissions.AppraisalsFinalApprove);
        if (!mayReview && !mayApprove) return EndpointProblems.FromError(Error.Forbidden("The appraisal review permission is required."));
        var items = await db.PerformanceAppraisals.AsNoTracking()
            .Where(x => x.InstituteId == institute &&
                ((mayReview && x.HodUserId == user && x.Status != AppraisalStatuses.Approved) ||
                 (mayApprove && x.DirectorUserId == user &&
                    (x.Status == AppraisalStatuses.MidyearDirectorReview || x.Status == AppraisalStatuses.DirectorReview))))
            .OrderBy(x => x.AppraisalPeriodEnd)
            .ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<AppraisalSummaryResponse>(await Summaries(items, db, true, context, ct), items.Count));
    }

    private static async Task<IResult> FinalListAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var institute = Institute(context); if (!institute.HasValue) return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));
        var items = await db.PerformanceAppraisals.AsNoTracking().Where(x => x.InstituteId == institute && x.Status == AppraisalStatuses.Approved).OrderByDescending(x => x.AppraisalPeriodEnd).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<AppraisalSummaryResponse>(await Summaries(items, db, false, context, ct), items.Count));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var appraisal = await db.PerformanceAppraisals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (appraisal is null || !CanRead(context, appraisal)) return NotFound();
        var hrFinal = IsHrFinalReader(context, appraisal); if (hrFinal && appraisal.Status != AppraisalStatuses.Approved) return NotFound();
        var cycle = await db.AppraisalCycles.AsNoTracking().FirstAsync(x => x.Id == appraisal.AppraisalCycleId, ct);
        var response = MapDetail(appraisal, cycle, db, !hrFinal, context, ct); context.Response.Headers.ETag = ConcurrencyToken.Format(appraisal.RowVersion);
        return TypedResults.Ok(await response);
    }

    private static async Task<IResult> SavePlanningAsync(Guid id, SaveAppraisalPlanningRequest request, HttpContext context,
        SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        if (request.Targets.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count() != request.Targets.Count(x => x.Id.HasValue))
            return Validation("A target may appear only once in the planning form.");

        var existing = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).ToListAsync(ct);
        var omitted = existing.Where(x => request.Targets.All(y => y.Id != x.Id)).ToList();
        db.AppraisalTargets.RemoveRange(omitted);
        var normalizedTargets = new List<AppraisalTargetInput>();
        short order = 0;
        foreach (var input in request.Targets)
        {
            order++; var target = input.Id.HasValue ? existing.FirstOrDefault(x => x.Id == input.Id) : null;
            if (input.Id.HasValue && target is null) return Validation("A target identifier does not belong to this appraisal.");
            if (target is null)
            {
                target = new AppraisalTarget(id, order, input.CoreArea, input.Target, input.ResourcesRequired, input.Timeline);
                db.AppraisalTargets.Add(target);
            }
            else
            {
                if (target.CoreArea != input.CoreArea.Trim() || target.Target != input.Target.Trim() ||
                    target.ResourcesRequired != input.ResourcesRequired.Trim() || target.Timeline != Trim(input.Timeline))
                {
                    var version = (short)(await db.AppraisalTargetVersions.CountAsync(x => x.AppraisalTargetId == target.Id, ct) + 1);
                    db.AppraisalTargetVersions.Add(new AppraisalTargetVersion(target.Id, version, target.CoreArea,
                        target.Target, target.ResourcesRequired, target.Timeline, DateTimeOffset.UtcNow));
                }
                target.Update(order, input.CoreArea, input.Target, input.ResourcesRequired, input.Timeline);
            }
            normalizedTargets.Add(new AppraisalTargetInput(target.Id, target.CoreArea, target.Target,
                target.ResourcesRequired, target.Timeline));
        }
        db.AppraisalTrainingRecords.RemoveRange(db.AppraisalTrainingRecords.Where(x => x.PerformanceAppraisalId == id));
        db.AppraisalTrainingRecords.AddRange(request.TrainingReceived
            .Where(x => !string.IsNullOrWhiteSpace(x.Institution) && x.Date.HasValue && !string.IsNullOrWhiteSpace(x.Programme))
            .Select(x => new AppraisalTrainingRecord(id, x.Institution, x.Date!.Value, x.Programme)));
        db.AppraisalKeyCompetencies.RemoveRange(db.AppraisalKeyCompetencies.Where(x => x.PerformanceAppraisalId == id));
        db.AppraisalKeyCompetencies.AddRange(request.KeyCompetencies.Select((x, i) => new AppraisalKeyCompetency(id, (short)(i + 1), x)));
        var normalized = request with
        {
            Targets = normalizedTargets,
            KeyCompetencies = request.KeyCompetencies.Select(x => x.Trim()).ToList()
        };
        var result = a.SavePlanning(normalized); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal.planning-saved", "PerformanceAppraisal", id.ToString(), null, "draft-saved", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SubmitPlanningAsync(Guid id, AppraisalAttestationRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        if (!request.Attested) return Validation("Authenticated attestation is required to submit appraisal planning.");
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var planning = Read<SaveAppraisalPlanningRequest>(a.PlanningJson);
        if (planning is null || planning.Targets.Count == 0 || planning.KeyCompetencies.Count == 0 ||
            planning.Targets.Any(x => string.IsNullOrWhiteSpace(x.CoreArea) || string.IsNullOrWhiteSpace(x.Target) ||
                string.IsNullOrWhiteSpace(x.ResourcesRequired) || string.IsNullOrWhiteSpace(x.Timeline)) ||
            planning.KeyCompetencies.Any(string.IsNullOrWhiteSpace) ||
            planning.TrainingReceived.Any(x => string.IsNullOrWhiteSpace(x.Institution) || !x.Date.HasValue ||
                string.IsNullOrWhiteSpace(x.Programme)))
            return Validation("Complete and save the required planning fields before submission.");
        var result = a.SubmitPlanning(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.PlanningEmployee, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, a.HodUserId, "planning-submitted", ct);
        await audit.RecordAsync("appraisal.planning-submitted", "PerformanceAppraisal", id.ToString(), "status=planning", $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> ConfirmPlanningAsync(Guid id, AppraisalAttestationRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        if (!request.Attested) return Validation("Authenticated attestation is required to agree the planning baseline.");
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var result = a.ConfirmPlanning(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.PlanningHod, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, await UserForEmployee(a.EmployeeId, db, ct), "planning-confirmed", ct);
        await audit.RecordAsync("appraisal.planning-confirmed", "PerformanceAppraisal", id.ToString(), "status=planning-review", $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> ReturnPlanningAsync(Guid id, AppraisalDirectorReturnRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return Validation("A correction reason is required.");
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var result = a.ReturnPlanning(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await Notice(outbox, a, await UserForEmployee(a.EmployeeId, db, ct), "planning-returned", ct);
        await audit.RecordAsync("appraisal.planning-returned", "PerformanceAppraisal", id.ToString(), "status=planning-review",
            $"status={a.Status};reason={request.Reason.Trim()}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SaveMidyearAsync(Guid id, SaveAppraisalMidyearRequest request, HttpContext context,
        SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var targetIds = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        var competencies = await db.AppraisalKeyCompetencies.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Competency).ToListAsync(ct);
        if (request.TargetReviews.Count != targetIds.Count || request.TargetReviews.Select(x => x.TargetId).Distinct().Count() != targetIds.Count ||
            request.TargetReviews.Any(x => !targetIds.Contains(x.TargetId)))
            return Validation("Exactly one progress-review draft entry is required for every agreed target.");
        if (request.CompetencyReviews.Count != competencies.Count ||
            request.CompetencyReviews.Select(x => x.Competency.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != competencies.Count ||
            request.CompetencyReviews.Any(x => !competencies.Contains(x.Competency, StringComparer.OrdinalIgnoreCase)))
            return Validation("Exactly one progress-review draft entry is required for every agreed competency.");
        var result = a.SaveMidyear(request); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        db.AppraisalMidyearTargetReviews.RemoveRange(db.AppraisalMidyearTargetReviews.Where(x => x.PerformanceAppraisalId == id));
        db.AppraisalMidyearTargetReviews.AddRange(request.TargetReviews.Select(x => new AppraisalMidyearTargetReview(id, x.TargetId, x.ProgressReview, null)));
        db.AppraisalMidyearCompetencyReviews.RemoveRange(db.AppraisalMidyearCompetencyReviews.Where(x => x.PerformanceAppraisalId == id));
        db.AppraisalMidyearCompetencyReviews.AddRange(request.CompetencyReviews.Select(x => new AppraisalMidyearCompetencyReview(id, x.Competency, x.ProgressReview, null)));
        await audit.RecordAsync("appraisal.midyear-saved", "PerformanceAppraisal", id.ToString(), null, "draft-saved", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SubmitMidyearAsync(Guid id, AppraisalAttestationRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        if (!request.Attested) return Validation("Authenticated attestation is required to submit the midyear review.");
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var midyear = Read<SaveAppraisalMidyearRequest>(a.MidyearJson);
        var targetIds = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        var competencies = await db.AppraisalKeyCompetencies.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Competency).ToListAsync(ct);
        if (midyear is null || midyear.TargetReviews.Count != targetIds.Count ||
            midyear.TargetReviews.Select(x => x.TargetId).Distinct().Count() != targetIds.Count ||
            midyear.TargetReviews.Any(x => !targetIds.Contains(x.TargetId) || string.IsNullOrWhiteSpace(x.ProgressReview)) ||
            midyear.CompetencyReviews.Count != competencies.Count ||
            midyear.CompetencyReviews.Select(x => x.Competency.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != competencies.Count ||
            midyear.CompetencyReviews.Any(x => !competencies.Contains(x.Competency, StringComparer.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(x.ProgressReview)) || string.IsNullOrWhiteSpace(midyear.TrainingNeed))
            return Validation("Complete and save the required midyear fields before submission.");
        var result = a.SubmitMidyear(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.MidyearEmployeeSubmission, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, a.HodUserId, "midyear-submitted", ct);
        await audit.RecordAsync("appraisal.midyear-submitted", "PerformanceAppraisal", id.ToString(), "status=midyear", $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SaveHodMidyearAsync(Guid id, SaveHodMidyearReviewRequest request, HttpContext context,
        SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (a.Status != AppraisalStatuses.MidyearReview) return State("The appraisal is not at HOD midyear review.");
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).ToListAsync(ct);
        var competencies = await db.AppraisalKeyCompetencies.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Competency).ToListAsync(ct);
        if (request.TargetRemarks.Count != targets.Count || request.TargetRemarks.Select(x => x.TargetId).Distinct().Count() != targets.Count ||
            request.TargetRemarks.Any(x => targets.All(t => t.Id != x.TargetId)))
            return Validation("Exactly one official HOD remark entry is required for every target.");
        if (request.CompetencyRemarks.Count != competencies.Count ||
            request.CompetencyRemarks.Select(x => x.Competency.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != competencies.Count ||
            request.CompetencyRemarks.Any(x => !competencies.Contains(x.Competency, StringComparer.OrdinalIgnoreCase)))
            return Validation("Exactly one official HOD remark entry is required for every competency.");
        if (request.TargetAmendments.Select(x => x.TargetId).Distinct().Count() != request.TargetAmendments.Count ||
            request.TargetAmendments.Any(x => targets.All(t => t.Id != x.TargetId)))
            return Validation("A target amendment draft must reference one unique target in this appraisal.");
        var result = a.SaveHodMidyearReview(request); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal.hod-midyear-saved", "PerformanceAppraisal", id.ToString(), null, "draft-saved", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SubmitHodMidyearAsync(Guid id, AppraisalHodSubmissionRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var review = Read<SaveHodMidyearReviewRequest>(a.HodMidyearReviewJson);
        if (review is null || review.TargetRemarks.Any(x => string.IsNullOrWhiteSpace(x.Remarks)) ||
            review.CompetencyRemarks.Any(x => string.IsNullOrWhiteSpace(x.Remarks)) ||
            review.TargetAmendments.Any(x => string.IsNullOrWhiteSpace(x.RevisedTarget) ||
                string.IsNullOrWhiteSpace(x.RevisedResourcesRequired) || string.IsNullOrWhiteSpace(x.Reason)))
            return Validation("Complete and save all HOD remarks and proposed target amendments before submission.");
        var result = a.SubmitMidyearReview(request.ResponseToDecline); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        var now = DateTimeOffset.UtcNow;
        var version = (short)(await db.AppraisalHodSubmissions.CountAsync(x => x.PerformanceAppraisalId == id && x.Phase == AppraisalPhases.Midyear, ct) + 1);
        var submission = new AppraisalHodSubmission(id, AppraisalPhases.Midyear, version, a.HodUserId!.Value,
            request.ResponseToDecline, review.TrainingNeedComment, now);
        db.AppraisalHodSubmissions.Add(submission);
        db.AppraisalMidyearTargetRemarks.AddRange(review.TargetRemarks.Select(x => new AppraisalMidyearTargetRemark(submission.Id, x.TargetId, x.Remarks)));
        db.AppraisalMidyearCompetencyRemarks.AddRange(review.CompetencyRemarks.Select(x => new AppraisalMidyearCompetencyRemark(submission.Id, x.Competency, x.Remarks)));
        var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).ToDictionaryAsync(x => x.Id, ct);
        foreach (var input in review.TargetAmendments)
        {
            var proposed = await db.AppraisalTargetAmendments
                .Where(x => x.AppraisalTargetId == input.TargetId && x.Status == "proposed").ToListAsync(ct);
            foreach (var previous in proposed) previous.Supersede(now);
            var amendmentVersion = (short)(await db.AppraisalTargetAmendments.CountAsync(x => x.AppraisalTargetId == input.TargetId, ct) + 1);
            db.AppraisalTargetAmendments.Add(new AppraisalTargetAmendment(id, targets[input.TargetId], amendmentVersion,
                input.RevisedTarget, input.RevisedResourcesRequired, input.RevisedTimeline, input.Reason, now));
        }
        await AddSignature(id, AppraisalPhases.MidyearHod, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, await UserForEmployee(a.EmployeeId, db, ct), "midyear-review-submitted", ct);
        await audit.RecordAsync("appraisal.hod-midyear-submitted", "PerformanceAppraisal", id.ToString(), "status=midyear-review", $"status={a.Status};version={version}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> MidyearSignatureAsync(Guid id, AppraisalStaffSignatureRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var now = DateTimeOffset.UtcNow; var result = a.RecordStaffSignature(AppraisalPhases.Midyear, request.Accepted, request.Comments, request.DeclineReason, now); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.Midyear, User(context)!.Value, request.Accepted, request.Comments, request.DeclineReason, db, ct, now);
        if (request.Accepted) { var amendments = await db.AppraisalTargetAmendments.Where(x => x.PerformanceAppraisalId == id && x.Status == "proposed").ToListAsync(ct); var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).ToDictionaryAsync(x => x.Id, ct); foreach (var amendment in amendments) amendment.Accept(targets[amendment.AppraisalTargetId], now); }
        await Notice(outbox, a, request.Accepted ? a.DirectorUserId : a.HodUserId, request.Accepted ? "midyear-signed" : "midyear-declined", ct);
        await audit.RecordAsync(request.Accepted ? "appraisal.midyear-signed" : "appraisal.midyear-signature-declined",
            "PerformanceAppraisal", id.ToString(), null, $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> MidyearApproveAsync(Guid id, AppraisalMidyearDirectorApprovalRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadDirector(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        if (string.IsNullOrWhiteSpace(request.CommentsOnProgress)) return Validation("The Director's midyear comment is required."); var result = a.ApproveMidyearByDirector(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        db.AppraisalDirectorDecisions.Add(new AppraisalDirectorDecision(id, AppraisalPhases.Midyear, await DecisionVersion(id, AppraisalPhases.Midyear, db, ct), "approved", User(context)!.Value, request.CommentsOnProgress, null, null, DateTimeOffset.UtcNow));
        await Notice(outbox, a, await UserForEmployee(a.EmployeeId, db, ct), "midyear-approved", ct);
        await Notice(outbox, a, a.HodUserId, "midyear-approved", ct);
        await audit.RecordAsync("appraisal.midyear-approved", "PerformanceAppraisal", id.ToString(), "status=midyear-director-review", $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> MidyearReturnAsync(Guid id, AppraisalDirectorReturnRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadDirector(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!; if (string.IsNullOrWhiteSpace(request.Reason)) return Validation("A return reason is required.");
        var result = a.ReturnMidyearByDirector(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!); db.AppraisalDirectorDecisions.Add(new AppraisalDirectorDecision(id, AppraisalPhases.Midyear, await DecisionVersion(id, AppraisalPhases.Midyear, db, ct), "returned", User(context)!.Value, request.Reason, request.Reason, null, DateTimeOffset.UtcNow));
        await Notice(outbox, a, a.HodUserId, "midyear-returned", ct);
        await audit.RecordAsync("appraisal.midyear-returned", "PerformanceAppraisal", id.ToString(), "status=midyear-director-review",
            $"status={a.Status};reason={request.Reason.Trim()}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SaveYearEndAsync(Guid id, SaveAppraisalYearEndRequest request, HttpContext context,
        SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        if (request.TargetResults.Count != targets.Count || request.TargetResults.Select(x => x.TargetId).Distinct().Count() != targets.Count ||
            request.TargetResults.Any(x => !targets.Contains(x.TargetId) || x.WorkCompletedPercentage is < 0 or > 100))
            return Validation("Exactly one year-end draft result with a percentage from 0 through 100 is required for every target.");
        var result = a.SaveYearEnd(request); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        db.AppraisalYearEndResults.RemoveRange(db.AppraisalYearEndResults.Where(x => x.PerformanceAppraisalId == id));
        db.AppraisalYearEndResults.AddRange(request.TargetResults.Select(x => new AppraisalYearEndResult(id, x.TargetId,
            x.WorkAccomplished, x.WorkCompletedPercentage, x.ExtentAndConstraints)));
        await audit.RecordAsync("appraisal.year-end-saved", "PerformanceAppraisal", id.ToString(), null, "draft-saved", ct);
        return await Saved(a, cycle, context, db, ct);
    }
    private static async Task<IResult> SubmitYearEndAsync(Guid id, AppraisalAttestationRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        if (!request.Attested) return Validation("Authenticated attestation is required to submit the year-end assessment.");
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var yearEnd = Read<SaveAppraisalYearEndRequest>(a.YearEndJson);
        var targetIds = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        if (yearEnd is null || yearEnd.TargetResults.Count != targetIds.Count ||
            yearEnd.TargetResults.Select(x => x.TargetId).Distinct().Count() != targetIds.Count ||
            yearEnd.TargetResults.Any(x => !targetIds.Contains(x.TargetId) || x.WorkCompletedPercentage is < 0 or > 100 ||
                string.IsNullOrWhiteSpace(x.WorkAccomplished) || string.IsNullOrWhiteSpace(x.ExtentAndConstraints)))
            return Validation("Complete and save the required year-end fields before submission.");
        var result = a.SubmitYearEnd(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.YearEndEmployeeSubmission, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, a.HodUserId, "year-end-submitted", ct);
        await audit.RecordAsync("appraisal.year-end-submitted", "PerformanceAppraisal", id.ToString(), "status=year-end", $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SaveHodAsync(Guid id, SaveHodAppraisalAssessmentRequest request, HttpContext context,
        SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value; if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var expected = AppraisalFactors.Behavioral.Concat(AppraisalFactors.Core).Select(x => x.Code).ToHashSet(); if (request.CompetencyRatings.Count != expected.Count || request.CompetencyRatings.Select(x => x.Code).ToHashSet().SetEquals(expected) is false || request.CompetencyRatings.Any(x => x.Rating is < 1 or > 5)) return Validation("Exactly one valid rating entry is required for every official Part IV factor; not-applicable may be null.");
        var behavioralCodes = AppraisalFactors.Behavioral.Select(x => x.Code).ToHashSet();
        var coreCodes = AppraisalFactors.Core.Select(x => x.Code).ToHashSet();
        var behavioral = AppraisalScoring.CategoryScore(request.CompetencyRatings.Where(x => behavioralCodes.Contains(x.Code)).Select(x => x.Rating));
        var core = AppraisalScoring.CategoryScore(request.CompetencyRatings.Where(x => coreCodes.Contains(x.Code)).Select(x => x.Rating));
        var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        if (request.TargetAssessments.Count != targets.Count || request.TargetAssessments.Select(x => x.TargetId).Distinct().Count() != targets.Count ||
            request.TargetAssessments.Any(x => !targets.Contains(x.TargetId) || x.Rating is < 1 or > 5))
            return Validation("Exactly one target assessment is required for every target using ratings from 1 through 5.");
        var result = a.SaveHodAssessment(request, behavioral, core); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal.hod-assessment-saved", "PerformanceAppraisal", id.ToString(), null, "draft-saved", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> SubmitHodAsync(Guid id, AppraisalHodSubmissionRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadHod(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var assessment = Read<SaveHodAppraisalAssessmentRequest>(a.HodAssessmentJson);
        var expected = AppraisalFactors.Behavioral.Concat(AppraisalFactors.Core).Select(x => x.Code).ToHashSet();
        var behavioralCodes = AppraisalFactors.Behavioral.Select(x => x.Code).ToHashSet();
        var coreCodes = AppraisalFactors.Core.Select(x => x.Code).ToHashSet();
        var targets = await db.AppraisalTargets.Where(x => x.PerformanceAppraisalId == id).Select(x => x.Id).ToListAsync(ct);
        if (assessment is null || assessment.TargetAssessments.Count != targets.Count ||
            assessment.TargetAssessments.Select(x => x.TargetId).Distinct().Count() != targets.Count ||
            assessment.TargetAssessments.Any(x => !targets.Contains(x.TargetId) || x.Rating is < 1 or > 5) ||
            assessment.CompetencyRatings.Count != expected.Count ||
            !assessment.CompetencyRatings.Select(x => x.Code).ToHashSet().SetEquals(expected) ||
            assessment.CompetencyRatings.Any(x => x.Rating is < 1 or > 5) ||
            !AppraisalScoring.CategoryScore(assessment.CompetencyRatings.Where(x => behavioralCodes.Contains(x.Code)).Select(x => x.Rating)).HasValue ||
            !AppraisalScoring.CategoryScore(assessment.CompetencyRatings.Where(x => coreCodes.Contains(x.Code)).Select(x => x.Rating)).HasValue ||
            string.IsNullOrWhiteSpace(assessment.SupervisorComments))
            return Validation("Complete and save all target assessments, official factors, and supervisor comments before submission.");
        var result = a.SubmitHodAssessment(request.ResponseToDecline); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        var version = (short)(await db.AppraisalHodSubmissions.CountAsync(x => x.PerformanceAppraisalId == id && x.Phase == AppraisalPhases.YearEnd, ct) + 1);
        var submission = new AppraisalHodSubmission(id, AppraisalPhases.YearEnd, version, a.HodUserId!.Value,
            request.ResponseToDecline, assessment.SupervisorComments, DateTimeOffset.UtcNow);
        db.AppraisalHodSubmissions.Add(submission);
        db.AppraisalTargetAssessmentRecords.AddRange(assessment.TargetAssessments.Select(x =>
            new AppraisalTargetAssessmentRecord(submission.Id, x.TargetId, x.Rating, x.Comments)));
        db.AppraisalCompetencyRatingRecords.AddRange(assessment.CompetencyRatings.Select(x =>
            new AppraisalCompetencyRatingRecord(submission.Id, x.Code, x.Rating)));
        await AddSignature(id, AppraisalPhases.YearEndHod, User(context)!.Value, true, null, null, db, ct);
        await Notice(outbox, a, await UserForEmployee(a.EmployeeId, db, ct), "hod-assessment-submitted", ct);
        await audit.RecordAsync("appraisal.hod-assessment-submitted", "PerformanceAppraisal", id.ToString(), "status=hod-assessment", $"status={a.Status};version={version}", ct);
        return await Saved(a, cycle, context, db, ct);
    }
    private static async Task<IResult> YearEndSignatureAsync(Guid id, AppraisalStaffSignatureRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadOwner(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        var now = DateTimeOffset.UtcNow;
        var result = a.RecordStaffSignature(AppraisalPhases.YearEnd, request.Accepted, request.Comments, request.DeclineReason, now);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await AddSignature(id, AppraisalPhases.YearEnd, User(context)!.Value, request.Accepted, request.Comments, request.DeclineReason, db, ct, now);
        await Notice(outbox, a, request.Accepted ? a.DirectorUserId : a.HodUserId,
            request.Accepted ? "year-end-signed" : "year-end-declined", ct);
        await audit.RecordAsync(request.Accepted ? "appraisal.year-end-signed" : "appraisal.year-end-signature-declined",
            "PerformanceAppraisal", id.ToString(), null, $"status={a.Status}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> DirectorReturnAsync(Guid id, AppraisalDirectorReturnRequest request, HttpContext context,
        SpmeDbContext db, IWorkflowNotificationOutbox outbox, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadDirector(id, context, db, ct); if (loaded is null) return NotFound(); var (a, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, a, cycle, out var problem)) return problem!;
        if (string.IsNullOrWhiteSpace(request.Reason)) return Validation("A return reason is required.");
        var result = a.ReturnByDirector(); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        db.AppraisalDirectorDecisions.Add(new AppraisalDirectorDecision(id, AppraisalPhases.YearEnd,
            await DecisionVersion(id, AppraisalPhases.YearEnd, db, ct), "returned", User(context)!.Value,
            request.Reason, request.Reason, null, DateTimeOffset.UtcNow));
        await Notice(outbox, a, a.HodUserId, "year-end-returned", ct);
        await audit.RecordAsync("appraisal.year-end-returned", "PerformanceAppraisal", id.ToString(), "status=director-review",
            $"status={a.Status};reason={request.Reason.Trim()}", ct);
        return await Saved(a, cycle, context, db, ct);
    }

    private static async Task<IResult> DirectorApproveAsync(Guid id, AppraisalDirectorApprovalRequest request, HttpContext context, SpmeDbContext db,
        IWorkflowNotificationOutbox outbox, IFileStorageService storage, IAuditService audit, CancellationToken ct)
    {
        var loaded = await LoadDirector(id, context, db, ct);
        if (loaded is null) return NotFound();
        var (appraisal, cycle) = loaded.Value;
        if (!ApplyEtag(context, db, appraisal, cycle, out var problem)) return problem!;
        if (string.IsNullOrWhiteSpace(request.CommentsOnWork)) return Validation("The Director's comments on work accomplished are required.");
        if (appraisal.Status != AppraisalStatuses.DirectorReview || !appraisal.TotalScore.HasValue)
            return State("The appraisal is not ready for final approval.");

        var now = DateTimeOffset.UtcNow;
        var document = AppraisalPdf.Build(await BuildFinalForm(appraisal, cycle, request, now, db, ct));
        var storageKey = $"appraisals/{appraisal.InstituteId:N}/{appraisal.Id:N}/final/{Guid.NewGuid():N}.pdf";
        await using var stream = new MemoryStream(document);
        var uploaded = await storage.UploadAsync(stream, storageKey, "application/pdf", ct);
        var file = new FileRecord(
            uploaded.StorageKey,
            $"CSIR-performance-appraisal-{cycle.Year}.pdf",
            "application/pdf",
            uploaded.SizeBytes,
            uploaded.Checksum,
            "performance-appraisal-final-form",
            appraisal.InstituteId,
            "confidential");
        file.MarkScanStatus("clean");
        db.FileRecords.Add(file);

        var approved = appraisal.ApproveByDirector(request, User(context)!.Value, file.Id, now);
        if (approved.IsFailure) return EndpointProblems.FromError(approved.Error!);
        db.AppraisalDirectorDecisions.Add(new AppraisalDirectorDecision(
            id,
            AppraisalPhases.YearEnd,
            await DecisionVersion(id, AppraisalPhases.YearEnd, db, ct),
            "approved",
            User(context)!.Value,
            request.CommentsOnWork,
            null,
            JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            now));
        await Notice(outbox, appraisal, await UserForEmployee(appraisal.EmployeeId, db, ct), "year-end-approved", ct);
        await Notice(outbox, appraisal, appraisal.HodUserId, "year-end-approved", ct);
        await audit.RecordAsync("appraisal.final-approved", "PerformanceAppraisal", appraisal.Id.ToString(), null,
            $"outcome={appraisal.Outcome};score={appraisal.TotalScore}", ct);
        return await Saved(appraisal, cycle, context, db, ct);
    }

    private static async Task<IResult> FinalDocumentAsync(Guid id, HttpContext context, SpmeDbContext db, IFileStorageService storage, CancellationToken ct)
    {
        var appraisal = await db.PerformanceAppraisals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (appraisal is null || appraisal.Status != AppraisalStatuses.Approved || !appraisal.FinalDocumentFileId.HasValue || !CanRead(context, appraisal)) return NotFound(); var file = await db.FileRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == appraisal.FinalDocumentFileId, ct); if (file is null) return NotFound(); var stream = await storage.DownloadAsync(file.StorageKey, ct); return stream is null ? NotFound() : TypedResults.Stream(stream, "application/pdf", "CSIR-performance-appraisal.pdf");
    }

    private static async Task Notice(IWorkflowNotificationOutbox outbox, PerformanceAppraisal a, Guid? recipient, string evt, CancellationToken ct) { if (recipient.HasValue) await outbox.StageAppraisalNoticeAsync(a.Id, recipient.Value, evt, "Confidential appraisal action", "An appraisal requires your action. Open the secure portal to review it.", $"{a.Status}:{a.UpdatedAt.UtcTicks}", ct); }

    private static async Task AddSignature(Guid appraisalId, string phase, Guid signerUserId, bool accepted,
        string? comments, string? declineReason, SpmeDbContext db, CancellationToken ct, DateTimeOffset? recordedAt = null)
    {
        var attempt = (short)(await db.AppraisalSignatureRecords.CountAsync(
            x => x.PerformanceAppraisalId == appraisalId && x.Phase == phase, ct) + 1);
        db.AppraisalSignatureRecords.Add(new AppraisalSignatureRecord(appraisalId, phase, attempt, accepted,
            comments, declineReason, signerUserId, recordedAt ?? DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> Saved(PerformanceAppraisal a, AppraisalCycle cycle, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return EndpointProblems.FromError(Error.PreconditionFailed("The appraisal was modified by another request. Reload it and retry.")); }
        var realtime = context.RequestServices.GetRequiredService<IHubContext<HrRealtimeHub>>();
        context.Response.OnCompleted(() => realtime.Clients.Group(HrRealtimeGroups.Institute(a.InstituteId.ToString()))
            .SendAsync("workflow-updated", new { resource = "appraisals", action = "changed" }, CancellationToken.None));
        context.Response.Headers.ETag = ConcurrencyToken.Format(a.RowVersion);
        return TypedResults.Ok(await MapDetail(a, cycle, db, true, context, ct));
    }
    private static bool ApplyEtag(HttpContext context, SpmeDbContext db, PerformanceAppraisal a, AppraisalCycle cycle, out IResult? problem)
    {
        if (!cycle.IsStageWindowOpen(a.Status, DateTime.UtcNow))
        {
            problem = EndpointProblems.FromError(Error.Conflict("The appraisal cycle is closed or the current stage window is not open."));
            return false;
        }

        return HolidayPeriodEndpoints.TryApplyEtag(context, db, a, out problem);
    }
    private static async Task<(PerformanceAppraisal, AppraisalCycle)?> LoadOwner(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var employee = Employee(context); var institute = Institute(context);
        if (!employee.HasValue || !institute.HasValue || !HasPermission(context, SpmePermissions.AppraisalsSelf)) return null;
        var appraisal = await db.PerformanceAppraisals.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employee && x.InstituteId == institute, ct);
        return appraisal is null ? null : (appraisal, await db.AppraisalCycles.AsNoTracking().FirstAsync(x => x.Id == appraisal.AppraisalCycleId && x.InstituteId == institute, ct));
    }
    private static async Task<(PerformanceAppraisal, AppraisalCycle)?> LoadHod(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var user = User(context); var institute = Institute(context);
        if (!user.HasValue || !institute.HasValue || !HasPermission(context, SpmePermissions.AppraisalsReview)) return null;
        var appraisal = await db.PerformanceAppraisals.FirstOrDefaultAsync(x => x.Id == id && x.HodUserId == user && x.InstituteId == institute, ct);
        return appraisal is null ? null : (appraisal, await db.AppraisalCycles.AsNoTracking().FirstAsync(x => x.Id == appraisal.AppraisalCycleId && x.InstituteId == institute, ct));
    }
    private static async Task<(PerformanceAppraisal, AppraisalCycle)?> LoadDirector(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var user = User(context); var institute = Institute(context);
        if (!user.HasValue || !institute.HasValue || !HasPermission(context, SpmePermissions.AppraisalsFinalApprove)) return null;
        var appraisal = await db.PerformanceAppraisals.FirstOrDefaultAsync(x => x.Id == id && x.DirectorUserId == user && x.InstituteId == institute, ct);
        return appraisal is null ? null : (appraisal, await db.AppraisalCycles.AsNoTracking().FirstAsync(x => x.Id == appraisal.AppraisalCycleId && x.InstituteId == institute, ct));
    }

    internal static async Task<AppraisalSummaryResponse> MapSummaryAsync(PerformanceAppraisal a, AppraisalCycle cycle,
        SpmeDbContext db, bool includeRoutingReason, CancellationToken ct, HttpContext? context = null)
    {
        var employee = await db.Employees.AsNoTracking().FirstAsync(x => x.Id == a.EmployeeId && x.InstituteId == a.InstituteId, ct);
        var hod = a.HodUserId.HasValue ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.HodUserId && x.InstituteId == a.InstituteId, ct) : null;
        var director = a.DirectorUserId.HasValue ? await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.DirectorUserId && x.InstituteId == a.InstituteId, ct) : null;
        var signatures = await db.AppraisalSignatureRecords.AsNoTracking().Where(x => x.PerformanceAppraisalId == a.Id).ToListAsync(ct);
        var disagreement = signatures.Where(x => x.Phase is AppraisalPhases.Midyear or AppraisalPhases.YearEnd)
            .GroupBy(x => x.Phase).Any(group => !group.OrderByDescending(x => x.Attempt).First().Accepted);
        return new AppraisalSummaryResponse(a.Id, cycle.Id, cycle.Name, cycle.Year, a.AppraisalPeriodStart,
            a.AppraisalPeriodEnd, employee.Id, Display(employee), employee.StaffId, a.InstituteId, a.HodUserId,
            hod?.DisplayName, a.DirectorUserId, director?.DisplayName, a.Status, a.Status, !a.RoutingResolved,
            includeRoutingReason ? a.RoutingExceptionReason : null, disagreement, signatures.Count,
            a.FinalDocumentFileId.HasValue, a.TotalScore, AppraisalScoring.Band(a.TotalScore),
            context is null ? [] : Actions(a, cycle, context), ConcurrencyToken.Format(a.RowVersion),
            cycle.DeadlineFor(a.Status), a.UpdatedAt);
    }

    private static async Task<PerformanceAppraisalResponse> MapDetail(PerformanceAppraisal a, AppraisalCycle cycle,
        SpmeDbContext db, bool confidential, HttpContext context, CancellationToken ct)
    {
        var summary = await MapSummaryAsync(a, cycle, db, confidential, ct, context);
        var isEmployeeOrHod = Employee(context) == a.EmployeeId || User(context) == a.HodUserId;
        var signatureRows = (await db.AppraisalSignatureRecords.AsNoTracking()
            .Where(x => x.PerformanceAppraisalId == a.Id).ToListAsync(ct))
            .OrderBy(x => x.RecordedAt).ToList();
        var signatures = signatureRows.Select(x => new AppraisalSignatureAttemptResponse(
            x.Phase, x.Attempt, x.Accepted,
            x.Accepted || isEmployeeOrHod ? x.Comments : null,
            isEmployeeOrHod ? x.DeclineReason : null,
            x.RecordedAt)).ToList();
        if (!confidential)
            signatures = signatures.Where(x => x.Accepted).Select(x => x with { DeclineReason = null }).ToList();

        var auditRows = (await db.AuditRecords.AsNoTracking()
            .Where(x => x.TargetType == "PerformanceAppraisal" && x.TargetId == a.Id.ToString())
            .ToListAsync(ct)).OrderBy(x => x.OccurredAt).ToList();
        var history = auditRows.Select(x => new AppraisalHistoryResponse(
            x.Action,
            HistoryStage(x.Action),
            x.OccurredAt,
            isEmployeeOrHod && (x.Action.EndsWith("returned", StringComparison.Ordinal) || x.Action.EndsWith("rerouted", StringComparison.Ordinal))
                ? x.AfterSummary : null)).ToList();
        var latestMidyearDecision = await db.AppraisalDirectorDecisions.AsNoTracking()
            .Where(x => x.PerformanceAppraisalId == a.Id && x.Phase == AppraisalPhases.Midyear)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        var midyearDecision = latestMidyearDecision is null ? null : new AppraisalMidyearDirectorDecisionResponse(
            latestMidyearDecision.Decision,
            latestMidyearDecision.CommentsOnWork,
            isEmployeeOrHod || User(context) == a.DirectorUserId ? latestMidyearDecision.ReturnReason : null,
            latestMidyearDecision.DecidedAt);
        var completeness = Completeness(a);

        return new PerformanceAppraisalResponse(
            summary,
            Read<AppraisalEmployeeSnapshot>(a.EmployeeSnapshotJson)!,
            Read<AppraisalAppraiserSnapshot>(a.AppraiserSnapshotJson)!,
            Read<AppraisalAppraiserSnapshot>(a.ApproverSnapshotJson)!,
            Read<SaveAppraisalPlanningRequest>(a.PlanningJson),
            Read<SaveAppraisalMidyearRequest>(a.MidyearJson),
            Read<SaveHodMidyearReviewRequest>(a.HodMidyearReviewJson),
            Read<SaveAppraisalYearEndRequest>(a.YearEndJson),
            Read<SaveHodAppraisalAssessmentRequest>(a.HodAssessmentJson),
            signatures,
            midyearDecision,
            a.Status == AppraisalStatuses.Approved ? Read<AppraisalDirectorApprovalRequest>(a.DirectorAssessmentJson) : null,
            new AppraisalScoreResponse(a.BehavioralScore, a.CoreScore, a.TotalScore, AppraisalScoring.Band(a.TotalScore)),
            AppraisalFactors.Behavioral.Select(x => new AppraisalFactorResponse(x.Code, x.Label)).ToList(),
            AppraisalFactors.Core.Select(x => new AppraisalFactorResponse(x.Code, x.Label)).ToList(),
            AppraisalFactors.BehavioralRatingGuidance.Select(x => new AppraisalRatingGuidanceResponse(x.Rating, x.Label, x.Explanation)).ToList(),
            AppraisalFactors.CoreRatingGuidance.Select(x => new AppraisalRatingGuidanceResponse(x.Rating, x.Label, x.Explanation)).ToList(),
            completeness,
            history);
    }

    private static T? Read<T>(string json) { if (string.IsNullOrWhiteSpace(json) || json == "{}") return default; return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
    private static async Task<List<AppraisalSummaryResponse>> Summaries(List<PerformanceAppraisal> items, SpmeDbContext db,
        bool reason, HttpContext context, CancellationToken ct)
    {
        var result = new List<AppraisalSummaryResponse>();
        foreach (var appraisal in items)
        {
            var cycle = await db.AppraisalCycles.AsNoTracking().FirstAsync(
                x => x.Id == appraisal.AppraisalCycleId && x.InstituteId == appraisal.InstituteId, ct);
            result.Add(await MapSummaryAsync(appraisal, cycle, db, reason, ct, context));
        }
        return result;
    }
    private static async Task<Guid?> UserForEmployee(Guid employeeId, SpmeDbContext db, CancellationToken ct) => await db.Users.Where(x => x.EmployeeId == employeeId && x.AccountStatus == "active").Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    private static async Task<short> DecisionVersion(Guid id, string phase, SpmeDbContext db, CancellationToken ct) => (short)(await db.AppraisalDirectorDecisions.CountAsync(x => x.PerformanceAppraisalId == id && x.Phase == phase, ct) + 1);
    private static string[] Actions(PerformanceAppraisal a, AppraisalCycle cycle, HttpContext context)
    {
        if (a.Status == AppraisalStatuses.Approved && CanRead(context, a)) return ["download-final-document"];
        if (!cycle.IsStageWindowOpen(a.Status, DateTime.UtcNow)) return [];
        if (Employee(context) == a.EmployeeId && HasPermission(context, SpmePermissions.AppraisalsSelf))
        {
            return a.Status switch
            {
                AppraisalStatuses.Planning => ["save-planning", "submit-planning"],
                AppraisalStatuses.Midyear => ["save-midyear", "submit-midyear"],
                AppraisalStatuses.MidyearStaffSignature => ["midyear-signature"],
                AppraisalStatuses.YearEnd => ["save-year-end", "submit-year-end"],
                AppraisalStatuses.StaffSignature => ["staff-signature"],
                _ => []
            };
        }

        if (User(context) == a.HodUserId && HasPermission(context, SpmePermissions.AppraisalsReview))
        {
            return a.Status switch
            {
                AppraisalStatuses.PlanningReview => ["confirm-planning", "return-planning"],
                AppraisalStatuses.MidyearReview => ["save-hod-midyear-review", "submit-hod-midyear-review"],
                AppraisalStatuses.HodAssessment => ["save-hod-assessment", "submit-hod-assessment"],
                _ => []
            };
        }

        if (User(context) == a.DirectorUserId && HasPermission(context, SpmePermissions.AppraisalsFinalApprove))
        {
            return a.Status switch
            {
                AppraisalStatuses.MidyearDirectorReview => ["midyear-director-approve", "midyear-director-return"],
                AppraisalStatuses.DirectorReview => ["director-approve", "director-return"],
                _ => []
            };
        }

        return [];
    }

    private static bool CanRead(HttpContext context, PerformanceAppraisal a) => Institute(context) == a.InstituteId &&
        ((Employee(context) == a.EmployeeId && HasPermission(context, SpmePermissions.AppraisalsSelf)) ||
        (User(context) == a.HodUserId && HasPermission(context, SpmePermissions.AppraisalsReview)) ||
        (User(context) == a.DirectorUserId && HasPermission(context, SpmePermissions.AppraisalsFinalApprove) &&
            a.Status is AppraisalStatuses.MidyearDirectorReview or AppraisalStatuses.DirectorReview or AppraisalStatuses.Approved) ||
        IsHrFinalReader(context, a));
    private static bool IsHrFinalReader(HttpContext context, PerformanceAppraisal a) => HasPermission(context, SpmePermissions.AppraisalsFinalRead) && Institute(context) == a.InstituteId;
    private static bool HasPermission(HttpContext context, string permission) => context.User.HasClaim("permission", permission);
    private static Guid? User(HttpContext context) => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var id) ? id : null;
    private static Guid? Employee(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("employee_id"), out var id) ? id : null;
    private static Guid? Institute(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;
    private static string Display(Employee e) => string.Join(' ', new[] { e.PreferredName, e.OtherNames, e.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string HistoryStage(string action) => action switch
    {
        var value when value.Contains("planning", StringComparison.Ordinal) => "planning",
        var value when value.Contains("midyear", StringComparison.Ordinal) => "midyear",
        var value when value.Contains("year-end", StringComparison.Ordinal) || value.Contains("hod-assessment", StringComparison.Ordinal) || value.Contains("final-approved", StringComparison.Ordinal) => "year-end",
        _ => "routing"
    };

    private static AppraisalCompletenessResponse Completeness(PerformanceAppraisal appraisal)
    {
        var missing = new List<string>();
        if (!appraisal.RoutingResolved) missing.Add("Verified appraiser and final-approver routing");

        if (appraisal.Status == AppraisalStatuses.Planning)
        {
            var planning = Read<SaveAppraisalPlanningRequest>(appraisal.PlanningJson);
            if (planning is null || planning.Targets.Count == 0) missing.Add("Part II targets");
            if (planning is null || planning.KeyCompetencies.Count == 0) missing.Add("Part II key competencies");
            if (planning?.Targets.Any(x => string.IsNullOrWhiteSpace(x.CoreArea) || string.IsNullOrWhiteSpace(x.Target) ||
                string.IsNullOrWhiteSpace(x.ResourcesRequired) || string.IsNullOrWhiteSpace(x.Timeline)) is true)
                missing.Add("Core area, SMART target, resources, and timeline for every target");
            if (planning?.KeyCompetencies.Any(string.IsNullOrWhiteSpace) is true)
                missing.Add("Every listed key competency");
            if (planning?.TrainingReceived.Any(x => string.IsNullOrWhiteSpace(x.Institution) || !x.Date.HasValue ||
                string.IsNullOrWhiteSpace(x.Programme)) is true)
                missing.Add("Institution, date, and programme for every training entry");
        }
        else if (appraisal.Status == AppraisalStatuses.Midyear)
        {
            var midyear = Read<SaveAppraisalMidyearRequest>(appraisal.MidyearJson);
            if (midyear is null || midyear.TargetReviews.Count == 0) missing.Add("Mid-year target progress reviews");
            if (midyear is null || midyear.CompetencyReviews.Count == 0) missing.Add("Mid-year competency progress reviews");
            if (string.IsNullOrWhiteSpace(midyear?.TrainingNeed)) missing.Add("Mid-year training needs");
        }
        else if (appraisal.Status == AppraisalStatuses.MidyearReview)
        {
            var review = Read<SaveHodMidyearReviewRequest>(appraisal.HodMidyearReviewJson);
            if (review is null || review.TargetRemarks.Count == 0 || review.TargetRemarks.Any(x => string.IsNullOrWhiteSpace(x.Remarks)))
                missing.Add("HOD remarks for every target");
            if (review is null || review.CompetencyRemarks.Count == 0 || review.CompetencyRemarks.Any(x => string.IsNullOrWhiteSpace(x.Remarks)))
                missing.Add("HOD remarks for every competency");
            if (review?.TargetAmendments.Any(x => string.IsNullOrWhiteSpace(x.RevisedTarget) ||
                string.IsNullOrWhiteSpace(x.RevisedResourcesRequired) || string.IsNullOrWhiteSpace(x.Reason)) is true)
                missing.Add("Complete values and reason for every proposed target adjustment");
        }
        else if (appraisal.Status == AppraisalStatuses.YearEnd)
        {
            var yearEnd = Read<SaveAppraisalYearEndRequest>(appraisal.YearEndJson);
            if (yearEnd is null || yearEnd.TargetResults.Count == 0) missing.Add("Part III results for every target");
            if (yearEnd?.TargetResults.Any(x => string.IsNullOrWhiteSpace(x.WorkAccomplished) ||
                string.IsNullOrWhiteSpace(x.ExtentAndConstraints) || x.WorkCompletedPercentage is < 0 or > 100) is true)
                missing.Add("Work accomplished, completion percentage, and constraints for every target");
        }
        else if (appraisal.Status == AppraisalStatuses.HodAssessment)
        {
            var assessment = Read<SaveHodAppraisalAssessmentRequest>(appraisal.HodAssessmentJson);
            if (assessment is null || assessment.TargetAssessments.Count == 0) missing.Add("Supervisor target assessments");
            if (assessment is null || assessment.CompetencyRatings.Count != AppraisalFactors.Behavioral.Count + AppraisalFactors.Core.Count)
                missing.Add("All 20 official Part IV factor responses");
            if (assessment is not null && (!assessment.CompetencyRatings.Any(x => x.Rating.HasValue && AppraisalFactors.Behavioral.Any(f => f.Code == x.Code)) ||
                !assessment.CompetencyRatings.Any(x => x.Rating.HasValue && AppraisalFactors.Core.Any(f => f.Code == x.Code))))
                missing.Add("At least one applicable rating in each Part IV category");
            if (string.IsNullOrWhiteSpace(assessment?.SupervisorComments)) missing.Add("Comments by supervisor on appraisee");
        }
        else if (appraisal.Status == AppraisalStatuses.MidyearStaffSignature)
            missing.Add("Employee mid-year response and signature");
        else if (appraisal.Status == AppraisalStatuses.StaffSignature)
            missing.Add("Comments by appraisee or 'No comment' and employee signature");
        else if (appraisal.Status == AppraisalStatuses.MidyearDirectorReview)
            missing.Add("Director mid-year comment and decision");
        else if (appraisal.Status == AppraisalStatuses.DirectorReview)
            missing.Add("Part V Director comments, recommendations, and signature");

        return new AppraisalCompletenessResponse(missing.Count == 0, missing.Distinct().ToList());
    }

    private static IResult Validation(string message) => EndpointProblems.FromError(Error.Validation(message));
    private static IResult State(string message) => EndpointProblems.FromError(Error.StateTransition(message));
    private static IResult NotFound() => EndpointProblems.FromError(Error.NotFound("Performance appraisal not found."));

    private static async Task<AppraisalPdfForm> BuildFinalForm(PerformanceAppraisal appraisal,
        AppraisalCycle cycle, AppraisalDirectorApprovalRequest directorAssessment, DateTimeOffset approvedAt,
        SpmeDbContext db, CancellationToken ct)
    {
        var employee = Read<AppraisalEmployeeSnapshot>(appraisal.EmployeeSnapshotJson)!;
        var appraiser = Read<AppraisalAppraiserSnapshot>(appraisal.AppraiserSnapshotJson)!;
        var planning = Read<SaveAppraisalPlanningRequest>(appraisal.PlanningJson);
        var midyear = Read<SaveAppraisalMidyearRequest>(appraisal.MidyearJson);
        var hodMidyear = Read<SaveHodMidyearReviewRequest>(appraisal.HodMidyearReviewJson);
        var yearEnd = Read<SaveAppraisalYearEndRequest>(appraisal.YearEndJson);
        var hod = Read<SaveHodAppraisalAssessmentRequest>(appraisal.HodAssessmentJson);
        var targets = await db.AppraisalTargets.AsNoTracking().Where(x => x.PerformanceAppraisalId == appraisal.Id)
            .OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var signatures = await db.AppraisalSignatureRecords.AsNoTracking()
            .Where(x => x.PerformanceAppraisalId == appraisal.Id && x.Accepted).ToListAsync(ct);
        var employeeSignature = signatures.Where(x => x.Phase == AppraisalPhases.YearEnd)
            .OrderByDescending(x => x.Attempt).FirstOrDefault();
        var userIds = signatures.Select(x => x.EmployeeUserId)
            .Append(appraisal.DirectorUserId ?? Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var employeeName = Display(employee);
        var appraiserName = Display(appraiser);
        var directorName = appraisal.DirectorUserId.HasValue && users.TryGetValue(appraisal.DirectorUserId.Value, out var director)
            ? director.DisplayName
            : string.Empty;

        AppraisalPdfSignature Signature(string phase, string fallbackName)
        {
            var signature = signatures.Where(x => x.Phase == phase).OrderByDescending(x => x.Attempt).FirstOrDefault();
            var name = signature is not null && users.TryGetValue(signature.EmployeeUserId, out var signer)
                ? signer.DisplayName
                : fallbackName;
            return new AppraisalPdfSignature(name, signature?.RecordedAt);
        }

        var targetRows = targets.Select(target =>
        {
            var targetReview = midyear?.TargetReviews.FirstOrDefault(x => x.TargetId == target.Id);
            var targetRemark = hodMidyear?.TargetRemarks.FirstOrDefault(x => x.TargetId == target.Id);
            var targetResult = yearEnd?.TargetResults.FirstOrDefault(x => x.TargetId == target.Id);
            var targetAssessment = hod?.TargetAssessments.FirstOrDefault(x => x.TargetId == target.Id);
            return new AppraisalPdfTarget(
                target.Id,
                target.DisplayOrder,
                target.CoreArea,
                target.Target,
                target.ResourcesRequired,
                target.Timeline,
                targetReview?.ProgressReview,
                targetRemark?.Remarks,
                targetResult?.WorkAccomplished,
                targetResult?.WorkCompletedPercentage,
                targetResult?.ExtentAndConstraints,
                targetAssessment?.Rating,
                targetAssessment?.Comments);
        }).ToArray();

        var competencyRows = (planning?.KeyCompetencies ?? []).Select((competency, index) =>
        {
            var progress = midyear?.CompetencyReviews.FirstOrDefault(x =>
                string.Equals(x.Competency, competency, StringComparison.OrdinalIgnoreCase));
            var remark = hodMidyear?.CompetencyRemarks.FirstOrDefault(x =>
                string.Equals(x.Competency, competency, StringComparison.OrdinalIgnoreCase));
            return new AppraisalPdfCompetencyProgress((short)(index + 1), competency, progress?.ProgressReview, remark?.Remarks);
        }).ToArray();

        var trainingNeed = string.Join("\n", new[] { midyear?.TrainingNeed, hodMidyear?.TrainingNeedComment }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return new AppraisalPdfForm(
            cycle.StartDate,
            cycle.EndDate,
            new AppraisalPdfEmployee(
                employee.Title,
                employee.Surname,
                employee.FirstName,
                employee.OtherNames,
                employee.PresentGrade,
                employee.SalaryGradeStep,
                employee.DateOfPresentGrade,
                employee.Institute,
                employee.DivisionUnit,
                employee.DateOfFirstAppointment),
            new AppraisalPdfAppraiser(
                appraiser.Title,
                appraiser.Surname,
                appraiser.FirstName,
                appraiser.OtherNames,
                appraiser.PositionOfAppraiser),
            (planning?.TrainingReceived ?? []).Select(x => new AppraisalPdfTraining(x.Institution, x.Date, x.Programme)).ToArray(),
            targetRows,
            planning?.KeyCompetencies ?? [],
            competencyRows,
            trainingNeed,
            (hod?.CompetencyRatings ?? []).Select(x => new AppraisalPdfCompetencyRating(x.Code, x.Rating)).ToArray(),
            appraisal.BehavioralScore,
            appraisal.CoreScore,
            appraisal.TotalScore,
            hod?.SupervisorComments,
            employeeSignature?.Comments,
            new AppraisalPdfDirectorAssessment(
                directorAssessment.CommentsOnWork,
                directorAssessment.ConsiderPromotionTo,
                directorAssessment.PerformanceBonus,
                directorAssessment.Training,
                directorAssessment.Reassignment,
                directorAssessment.ReprimandOrCaution,
                directorAssessment.TerminationOfAppointment),
            Signature(AppraisalPhases.PlanningEmployee, employeeName),
            Signature(AppraisalPhases.PlanningHod, appraiserName),
            Signature(AppraisalPhases.Midyear, employeeName),
            Signature(AppraisalPhases.MidyearHod, appraiserName),
            Signature(AppraisalPhases.YearEndEmployeeSubmission, employeeName),
            Signature(AppraisalPhases.YearEndHod, appraiserName),
            Signature(AppraisalPhases.YearEnd, employeeName),
            new AppraisalPdfSignature(directorName, approvedAt));
    }

    private static string Display(AppraisalAppraiserSnapshot snapshot) => string.Join(' ',
        new[] { snapshot.Title, snapshot.FirstName, snapshot.OtherNames, snapshot.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string Display(AppraisalEmployeeSnapshot snapshot) => string.Join(' ',
        new[] { snapshot.Title, snapshot.FirstName, snapshot.OtherNames, snapshot.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
