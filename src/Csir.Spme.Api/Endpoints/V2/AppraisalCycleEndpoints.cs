using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Infrastructure.Jobs;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class AppraisalCycleEndpoints
{
    private static readonly string[] AppraiserRoles =
        [SpmeRoles.HeadOfSection, SpmeRoles.HeadOfDivision, SpmeRoles.InstituteDirector, SpmeRoles.DeputyDirectorGeneral];
    private static readonly string[] FinalApproverRoles =
        [SpmeRoles.InstituteDirector, SpmeRoles.DeputyDirectorGeneral, SpmeRoles.DirectorGeneral];
    private static readonly string[] RoutingRoles = AppraiserRoles.Concat(FinalApproverRoles).Distinct().ToArray();

    public static void MapAppraisalCycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cycles = endpoints.MapGroup("/api/v2/appraisal-cycles").WithGroupName("v2").WithTags("Appraisals")
            .RequireAuthorization(AuthorizationPolicies.ManageAppraisalCycles)
            .WithDescription("HR administration manages institute-scoped annual appraisal cycles, routing, deadlines, reminders, and completion oversight without receiving confidential draft form content.")
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);

        cycles.MapGet("", ListAsync).WithName("AppraisalCycles_List").WithSummary("List appraisal cycles.")
            .WithDescription("Lists annual appraisal cycles for the authenticated institute, including server-derived administrative actions and stage windows.")
            .Produces<CollectionResponse<AppraisalCycleResponse>>();
        cycles.MapPost("", CreateAsync).WithName("AppraisalCycles_Create").WithSummary("Create an annual appraisal cycle.")
            .WithDescription("Creates one draft annual cycle for an institute and year with strictly ordered, non-overlapping planning, midyear, and year-end windows.")
            .Produces<AppraisalCycleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict);
        cycles.MapGet("/{id:guid}", GetAsync).WithName("AppraisalCycles_Get").WithSummary("Get an appraisal cycle.")
            .WithDescription("Returns one institute-scoped cycle with its current ETag and server-derived administrative actions.")
            .Produces<AppraisalCycleResponse>().ProducesProblem(StatusCodes.Status404NotFound);
        cycles.MapPatch("/{id:guid}", UpdateAsync).WithName("AppraisalCycles_Update").WithSummary("Update a draft appraisal cycle.")
            .WithDescription("Updates a draft cycle's name and stage windows using the required If-Match ETag; open and closed cycles cannot be edited.")
            .Produces<AppraisalCycleResponse>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        cycles.MapPost("/{id:guid}/activate", ActivateAsync).WithName("AppraisalCycles_Activate").WithSummary("Activate an appraisal cycle.")
            .WithDescription("Activates a draft annual cycle, creates one appraisal for every active institute employee, and records routing exceptions where verified leadership routing is incomplete.").Produces<AppraisalCycleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        cycles.MapPost("/{id:guid}/open", ActivateAsync).WithName("AppraisalCycles_Open").WithSummary("Open an appraisal cycle.")
            .WithDescription("Compatibility alias that activates a draft annual cycle, creates the active employee roster, and records unresolved reviewer routes.").Produces<AppraisalCycleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        cycles.MapPost("/{id:guid}/close", CloseAsync).WithName("AppraisalCycles_Close").WithSummary("Close an appraisal cycle.")
            .WithDescription("Closes an open annual appraisal cycle, prevents further workflow submissions, and requires an audited HR reopen before work can resume.").Produces<AppraisalCycleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        cycles.MapPost("/{id:guid}/reopen", ReopenAsync).WithName("AppraisalCycles_Reopen").WithSummary("Reopen an appraisal cycle.")
            .WithDescription("Reopens a closed cycle using a mandatory reason retained in the cycle and audit trail.").Produces<AppraisalCycleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        cycles.MapGet("/{id:guid}/assignment-candidates", CandidatesAsync).WithName("AppraisalCycles_AssignmentCandidates")
            .WithSummary("List appraisal assignment candidates.")
            .WithDescription("Returns institute-scoped, non-confidential HOD and final-approver candidates and their eligible routing roles; it does not expose system-user administration data.")
            .Produces<CollectionResponse<AppraisalAssignmentCandidateResponse>>();
        cycles.MapPost("/{id:guid}/assignments", AssignAsync).WithName("AppraisalCycles_Assign")
            .WithSummary("Create an employee appraisal assignment.")
            .WithDescription("Creates one employee appraisal for a non-closed cycle using its current If-Match ETag. HOD and final approver must be distinct eligible users, or unresolved routing must include an exception reason.")
            .Produces<AppraisalSummaryResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        cycles.MapPut("/{id:guid}/assignments/{employeeId:guid}", UpdateAssignmentAsync).WithName("AppraisalCycles_UpdateAssignment")
            .WithSummary("Update an employee appraisal assignment.")
            .WithDescription("Reroutes an employee appraisal using If-Match while preserving its form data and requiring distinct eligible assigned reviewers.")
            .Produces<AppraisalSummaryResponse>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        cycles.MapGet("/{id:guid}/assignments", RosterAsync).WithName("AppraisalCycles_Assignments")
            .WithSummary("List appraisal assignments.").WithDescription("Lists non-confidential employee routing, assignment exceptions, reviewer identities, and current workflow status for the institute cycle.")
            .Produces<CollectionResponse<AppraisalSummaryResponse>>();
        cycles.MapGet("/{id:guid}/roster", RosterAsync).WithName("AppraisalCycles_Roster")
            .WithSummary("Get the appraisal roster.").WithDescription("Lists non-confidential employee routing and workflow status for HR cycle oversight.")
            .Produces<CollectionResponse<AppraisalSummaryResponse>>();
        cycles.MapGet("/{id:guid}/metrics", MetricsAsync).WithName("AppraisalCycles_Metrics")
            .WithSummary("Get appraisal cycle metrics.").WithDescription("Returns aggregate workflow counts, overdue work, disagreements, and completion percentage without draft form content.")
            .Produces<AppraisalCycleMetricsResponse>();
        cycles.MapPost("/{id:guid}/reminders/run", RunRemindersAsync).WithName("AppraisalCycles_RunReminders")
            .WithSummary("Run appraisal deadline reminders.").WithDescription("Stages confidential reminders at 7, 3, and 1 day before a stage deadline and once overdue, deduplicated per appraisal, stage, and offset.")
            .Produces<AppraisalReminderRunResponse>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed);
        cycles.MapPost("/{id:guid}/reminders", RunRemindersAsync).WithName("AppraisalCycles_Reminders")
            .WithSummary("Run appraisal deadline reminders.").WithDescription("Canonical reminder route. Uses the current cycle If-Match ETag and stages private reminders at 7, 3, and 1 day before a stage deadline and daily while overdue, with retry-safe deduplication.")
            .Produces<AppraisalReminderRunResponse>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    private static async Task<IResult> ListAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var institute = Institute(context); if (!institute.HasValue) return ForbiddenScope();
        var items = await db.AppraisalCycles.AsNoTracking().Where(x => x.InstituteId == institute).OrderByDescending(x => x.Year).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<AppraisalCycleResponse>(items.Select(Map).ToList(), items.Count));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, false, ct); return cycle is null ? NotFoundCycle() : WithEtag(context, TypedResults.Ok(Map(cycle)), cycle.RowVersion);
    }

    private static async Task<IResult> CreateAsync(CreateAppraisalCycleRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var institute = Institute(context); if (!institute.HasValue) return ForbiddenScope();
        if (await db.AppraisalCycles.AnyAsync(x => x.InstituteId == institute && x.Year == request.Year, ct))
            return EndpointProblems.FromError(Error.Conflict("An appraisal cycle already exists for this institute and year."));
        var result = AppraisalCycle.Create(institute.Value, request.Name, request.Year, request.StartDate, request.EndDate,
            request.PlanningStart, request.PlanningEnd, request.MidyearStart, request.MidyearEnd, request.YearEndStart, request.YearEndEnd);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal-cycle.created", "AppraisalCycle", result.Value!.Id.ToString(), null, $"year={result.Value.Year}", ct);
        db.AppraisalCycles.Add(result.Value!); await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(result.Value.RowVersion);
        return TypedResults.Created($"/api/v2/appraisal-cycles/{result.Value.Id}", Map(result.Value));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateAppraisalCycleRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct); if (cycle is null) return NotFoundCycle();
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, cycle, out var problem)) return problem!;
        var result = cycle.Update(request.Name, request.StartDate, request.EndDate, request.PlanningStart, request.PlanningEnd,
            request.MidyearStart, request.MidyearEnd, request.YearEndStart, request.YearEndEnd);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal-cycle.updated", "AppraisalCycle", id.ToString(), null, "windows-updated", ct);
        if (!await Save(db, ct)) return Stale();
        return WithEtag(context, TypedResults.Ok(Map(cycle)), cycle.RowVersion);
    }

    private static async Task<IResult> ActivateAsync(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct);
        if (cycle is null) return NotFoundCycle();
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, cycle, out var problem)) return problem!;

        var opened = cycle.Open();
        if (opened.IsFailure) return EndpointProblems.FromError(opened.Error!);

        var instituteName = await db.Institutes.AsNoTracking()
            .Where(x => x.Id == cycle.InstituteId)
            .Select(x => x.Name)
            .FirstAsync(ct);
        var employees = await db.Employees.AsNoTracking()
            .Where(x => x.InstituteId == cycle.InstituteId && x.ProfileStatus == "active")
            .OrderBy(x => x.Surname)
            .ThenBy(x => x.PreferredName)
            .ToListAsync(ct);
        var employments = await db.EmploymentRecords.AsNoTracking()
            .Where(x => x.InstituteId == cycle.InstituteId && x.IsCurrent && x.ServiceStatus == "active")
            .ToDictionaryAsync(x => x.EmployeeId, ct);
        var gradeIds = employments.Values.Where(x => x.GradeId.HasValue).Select(x => x.GradeId!.Value).Distinct().ToList();
        var gradeNames = await db.Grades.AsNoTracking().Where(x => gradeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var divisionIds = employments.Values.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId!.Value).Distinct().ToList();
        var divisionNames = await db.Divisions.AsNoTracking().Where(x => divisionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var sectionIds = employments.Values.Where(x => x.SectionId.HasValue).Select(x => x.SectionId!.Value).Distinct().ToList();
        var sectionNames = await db.Sections.AsNoTracking().Where(x => sectionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var assignedEmployeeIds = await db.PerformanceAppraisals.AsNoTracking()
            .Where(x => x.AppraisalCycleId == cycle.Id)
            .Select(x => x.EmployeeId)
            .ToHashSetAsync(ct);
        var activeUsers = await db.Users.AsNoTracking()
            .Where(x => x.InstituteId == cycle.InstituteId && x.AccountStatus == "active")
            .ToDictionaryAsync(x => x.Id, ct);
        var employeesById = employees.ToDictionary(x => x.Id);
        var routes = await RoutingUsersAsync(cycle.InstituteId, db, ct);

        var created = 0;
        var exceptions = 0;
        foreach (var employee in employees.Where(x => !assignedEmployeeIds.Contains(x.Id)))
        {
            employments.TryGetValue(employee.Id, out var employment);
            var route = ResolveAutomaticRoute(employee.Id, employment, routes);
            if (!string.IsNullOrWhiteSpace(route.ExceptionReason)) exceptions++;

            var snapshot = new AppraisalEmployeeSnapshot(
                employee.Prefix,
                employee.Surname,
                employee.PreferredName,
                employee.OtherNames,
                employment?.GradeId is Guid gradeId && gradeNames.TryGetValue(gradeId, out var gradeName) ? gradeName : null,
                employment?.GradeStep,
                employment?.PromotionDate,
                instituteName,
                employment?.SectionId is Guid sectionId && sectionNames.TryGetValue(sectionId, out var sectionName) ? sectionName :
                    employment?.DivisionId is Guid divisionId && divisionNames.TryGetValue(divisionId, out var divisionName) ? divisionName : employment?.Organization,
                employment?.AppointmentDate);
            activeUsers.TryGetValue(route.HodUserId ?? Guid.Empty, out var hod);
            activeUsers.TryGetValue(route.DirectorUserId ?? Guid.Empty, out var director);
            db.PerformanceAppraisals.Add(PerformanceAppraisal.Assign(
                cycle.InstituteId,
                employee.Id,
                cycle,
                route.HodUserId,
                route.DirectorUserId,
                snapshot,
                Appraiser(hod, employeesById, employments),
                Appraiser(director, employeesById, employments),
                route.ExceptionReason));
            created++;
        }

        await audit.RecordAsync(
            "appraisal-cycle.activated",
            "AppraisalCycle",
            id.ToString(),
            null,
            $"roster-created={created};routing-exceptions={exceptions}",
            ct);
        if (!await Save(db, ct)) return Stale();
        return WithEtag(context, TypedResults.Ok(Map(cycle)), cycle.RowVersion);
    }
    private static Task<IResult> CloseAsync(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct) =>
        Transition(id, context, db, audit, "closed", cycle => cycle.Close(), ct);
    private static Task<IResult> ReopenAsync(Guid id, ReopenAppraisalCycleRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct) =>
        Transition(id, context, db, audit, "reopened", cycle => cycle.Reopen(request.Reason), ct);

    private static async Task<IResult> Transition(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit,
        string action, Func<AppraisalCycle, Result<bool>> transition, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct); if (cycle is null) return NotFoundCycle();
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, cycle, out var problem)) return problem!;
        var result = transition(cycle); if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync($"appraisal-cycle.{action}", "AppraisalCycle", id.ToString(), null, $"status={cycle.Status};reason={cycle.ReopenReason}", ct);
        if (!await Save(db, ct)) return Stale();
        return WithEtag(context, TypedResults.Ok(Map(cycle)), cycle.RowVersion);
    }

    private static async Task<IResult> CandidatesAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, false, ct); if (cycle is null) return NotFoundCycle();
        var items = await RoutingUsersAsync(cycle.InstituteId, db, ct);
        var response = items.GroupBy(x => new { Id = x.UserId, x.EmployeeId, x.DisplayName })
            .Select(g => new AppraisalAssignmentCandidateResponse(g.Key.Id, g.Key.EmployeeId, g.Key.DisplayName,
                g.Select(x => x.Role).Distinct().Order().ToList()))
            .OrderBy(x => x.DisplayName).ToList();
        return TypedResults.Ok(new CollectionResponse<AppraisalAssignmentCandidateResponse>(response, response.Count));
    }

    private static async Task<IResult> AssignAsync(Guid id, AssignAppraisalRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct); if (cycle is null) return NotFoundCycle();
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, cycle, out var problem)) return problem!;
        if (cycle.Status == AppraisalCycleStatuses.Closed)
            return EndpointProblems.FromError(Error.Conflict("A closed appraisal cycle must be reopened before assignments can change."));
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.EmployeeId &&
            x.InstituteId == cycle.InstituteId && x.ProfileStatus == "active", ct);
        if (employee is null) return EndpointProblems.FromError(Error.NotFound("Employee not found."));
        if (await db.PerformanceAppraisals.AnyAsync(x => x.AppraisalCycleId == id && x.EmployeeId == request.EmployeeId, ct))
            return EndpointProblems.FromError(Error.Conflict("The employee is already assigned to this appraisal cycle."));
        var routing = await ValidateRouting(cycle.InstituteId, employee.Id, request.HodUserId, request.DirectorUserId, request.RoutingExceptionReason, db, ct);
        if (routing is not null) return EndpointProblems.FromError(routing);
        var employment = await db.EmploymentRecords.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.IsCurrent, ct);
        var instituteName = await db.Institutes.Where(x => x.Id == cycle.InstituteId).Select(x => x.Name).FirstAsync(ct);
        var hod = request.HodUserId.HasValue ? await db.Users.AsNoTracking().FirstAsync(x => x.Id == request.HodUserId, ct) : null;
        var director = request.DirectorUserId.HasValue ? await db.Users.AsNoTracking().FirstAsync(x => x.Id == request.DirectorUserId, ct) : null;
        var snapshot = await EmployeeSnapshot(employee, employment, instituteName, db, ct);
        var appraiser = await Appraiser(hod, db, ct);
        var appraisal = PerformanceAppraisal.Assign(cycle.InstituteId, employee.Id, cycle, request.HodUserId,
            request.DirectorUserId, snapshot, appraiser, await Appraiser(director, db, ct), request.RoutingExceptionReason);
        await audit.RecordAsync("appraisal.assigned", "PerformanceAppraisal", appraisal.Id.ToString(), null, "routing-created", ct);
        db.PerformanceAppraisals.Add(appraisal); await db.SaveChangesAsync(ct);
        var response = await AppraisalEndpoints.MapSummaryAsync(appraisal, cycle, db, false, ct);
        return TypedResults.Created($"/api/v2/performance-appraisals/{appraisal.Id}", response);
    }

    private static async Task<IResult> UpdateAssignmentAsync(Guid id, Guid employeeId, UpdateAppraisalAssignmentRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct); if (cycle is null) return NotFoundCycle();
        if (cycle.Status == AppraisalCycleStatuses.Closed)
            return EndpointProblems.FromError(Error.Conflict("A closed appraisal cycle must be reopened before assignments can change."));
        var appraisal = await db.PerformanceAppraisals.FirstOrDefaultAsync(x => x.AppraisalCycleId == id && x.EmployeeId == employeeId && x.InstituteId == cycle.InstituteId, ct);
        if (appraisal is null) return EndpointProblems.FromError(Error.NotFound("Appraisal assignment not found."));
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, appraisal, out var problem)) return problem!;
        var routing = await ValidateRouting(cycle.InstituteId, employeeId, request.HodUserId, request.DirectorUserId, request.RoutingExceptionReason, db, ct);
        if (routing is not null) return EndpointProblems.FromError(routing);
        var hod = request.HodUserId.HasValue ? await db.Users.AsNoTracking().FirstAsync(x => x.Id == request.HodUserId, ct) : null;
        var director = request.DirectorUserId.HasValue ? await db.Users.AsNoTracking().FirstAsync(x => x.Id == request.DirectorUserId, ct) : null;
        var result = appraisal.UpdateRouting(request.HodUserId, request.DirectorUserId, await Appraiser(hod, db, ct),
            await Appraiser(director, db, ct), request.RoutingExceptionReason);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await audit.RecordAsync("appraisal.rerouted", "PerformanceAppraisal", appraisal.Id.ToString(), null, "routing-updated", ct);
        if (!await Save(db, ct)) return Stale();
        return TypedResults.Ok(await AppraisalEndpoints.MapSummaryAsync(appraisal, cycle, db, false, ct));
    }

    private static async Task<IResult> RosterAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, false, ct); if (cycle is null) return NotFoundCycle();
        var appraisals = await db.PerformanceAppraisals.AsNoTracking().Where(x => x.AppraisalCycleId == id && x.InstituteId == cycle.InstituteId).ToListAsync(ct);
        var response = new List<AppraisalSummaryResponse>(); foreach (var item in appraisals) response.Add(await AppraisalEndpoints.MapSummaryAsync(item, cycle, db, true, ct));
        return TypedResults.Ok(new CollectionResponse<AppraisalSummaryResponse>(response, response.Count));
    }

    private static async Task<IResult> MetricsAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, false, ct); if (cycle is null) return NotFoundCycle();
        var items = await db.PerformanceAppraisals.AsNoTracking().Where(x => x.AppraisalCycleId == id && x.InstituteId == cycle.InstituteId).ToListAsync(ct);
        var counts = items.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count()); var now = DateTime.UtcNow.Date;
        var overdue = items.Count(x => x.Status != AppraisalStatuses.Approved && cycle.DeadlineFor(x.Status) < now);
        var appraisalIds = items.Select(a => a.Id).ToList();
        var signatureRows = await db.AppraisalSignatureRecords.AsNoTracking()
            .Where(x => appraisalIds.Contains(x.PerformanceAppraisalId) &&
                (x.Phase == AppraisalPhases.Midyear || x.Phase == AppraisalPhases.YearEnd)).ToListAsync(ct);
        var disagreements = signatureRows.GroupBy(x => new { x.PerformanceAppraisalId, x.Phase })
            .Count(group => !group.OrderByDescending(x => x.Attempt).First().Accepted);
        var approved = items.Count(x => x.Status == AppraisalStatuses.Approved);
        return TypedResults.Ok(new AppraisalCycleMetricsResponse(id, items.Count, counts, overdue, disagreements, approved, items.Count == 0 ? 0 : decimal.Round(approved * 100m / items.Count, 2)));
    }

    private static async Task<IResult> RunRemindersAsync(Guid id, HttpContext context, SpmeDbContext db,
        AppraisalReminderService reminders, CancellationToken ct)
    {
        var cycle = await FindCycle(id, context, db, true, ct); if (cycle is null) return NotFoundCycle();
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, cycle, out var problem)) return problem!;
        if (cycle.Status != AppraisalCycleStatuses.Open)
            return EndpointProblems.FromError(Error.Conflict("Reminders can run only while the appraisal cycle is open."));
        var result = await reminders.RunCycleAsync(cycle, DateTime.UtcNow.Date, "manual", ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(cycle.RowVersion);
        return TypedResults.Ok(new AppraisalReminderRunResponse(id, result.Processed, result.Staged));
    }

    private static async Task<Error?> ValidateRouting(Guid instituteId, Guid employeeId, Guid? hodId, Guid? directorId,
        string? reason, SpmeDbContext db, CancellationToken ct)
    {
        if (hodId.HasValue && hodId == directorId) return Error.Validation("The HOD and final approver must be distinct users.");
        if ((!hodId.HasValue || !directorId.HasValue) && string.IsNullOrWhiteSpace(reason)) return Error.Validation("Unresolved routing requires an exception reason.");
        var candidates = await RoutingUsersAsync(instituteId, db, ct);
        if (hodId.HasValue && !candidates.Any(x => x.UserId == hodId && AppraiserRoles.Contains(x.Role)))
            return Error.Validation("The assigned appraiser is not an eligible institute reviewer.");
        if (directorId.HasValue && !candidates.Any(x => x.UserId == directorId && FinalApproverRoles.Contains(x.Role)))
            return Error.Validation("The assigned final approver is not eligible for this institute.");
        if (hodId.HasValue && candidates.Any(x => x.UserId == hodId && x.EmployeeId == employeeId))
            return Error.Validation("Self-review is not permitted.");
        if (directorId.HasValue && candidates.Any(x => x.UserId == directorId && x.EmployeeId == employeeId))
            return Error.Validation("Self-approval is not permitted.");
        return null;
    }
    private static AppraisalAppraiserSnapshot Appraiser(Csir.Spme.Domain.Iam.User? user,
        IReadOnlyDictionary<Guid, Employee> employees, IReadOnlyDictionary<Guid, EmploymentRecord> employments)
    {
        if (user?.EmployeeId is Guid employeeId && employees.TryGetValue(employeeId, out var employee))
        {
            employments.TryGetValue(employeeId, out var employment);
            return new(employee.Prefix, employee.Surname, employee.PreferredName, employee.OtherNames, employment?.JobTitle);
        }
        return new(null, user?.DisplayName ?? string.Empty, null, null, null);
    }

    private static async Task<AppraisalAppraiserSnapshot> Appraiser(Csir.Spme.Domain.Iam.User? user, SpmeDbContext db, CancellationToken ct)
    {
        if (user?.EmployeeId is not Guid employeeId) return new(null, user?.DisplayName ?? string.Empty, null, null, null);
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null) return new(null, user.DisplayName, null, null, null);
        var position = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.IsCurrent)
            .Select(x => x.JobTitle).FirstOrDefaultAsync(ct);
        return new(employee.Prefix, employee.Surname, employee.PreferredName, employee.OtherNames, position);
    }

    private static async Task<AppraisalEmployeeSnapshot> EmployeeSnapshot(Employee employee, EmploymentRecord? employment,
        string instituteName, SpmeDbContext db, CancellationToken ct)
    {
        var grade = employment?.GradeId is Guid gradeId
            ? await db.Grades.AsNoTracking().Where(x => x.Id == gradeId).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;
        var division = employment?.SectionId is Guid sectionId
            ? await db.Sections.AsNoTracking().Where(x => x.Id == sectionId).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : employment?.DivisionId is Guid divisionId
                ? await db.Divisions.AsNoTracking().Where(x => x.Id == divisionId).Select(x => x.Name).FirstOrDefaultAsync(ct)
                : employment?.Organization;
        return new(employee.Prefix, employee.Surname, employee.PreferredName, employee.OtherNames, grade,
            employment?.GradeStep, employment?.PromotionDate, instituteName, division, employment?.AppointmentDate);
    }

    private static AppraisalRoute ResolveAutomaticRoute(Guid employeeId, EmploymentRecord? employment, IReadOnlyList<RoutingUser> users)
    {
        var employeeRoles = users.Where(x => x.EmployeeId == employeeId).Select(x => x.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var instituteDirector = FindUser(users, SpmeRoles.InstituteDirector, null, null, employeeId);
        var deputyDirectorGeneral = FindUser(users, SpmeRoles.DeputyDirectorGeneral, null, null, employeeId);
        var directorGeneral = FindUser(users, SpmeRoles.DirectorGeneral, null, null, employeeId);
        var appraiserMatch = new RouteMatch(null, false);
        var approverMatch = instituteDirector;
        var reasons = new List<string>();

        if (employment is null)
        {
            reasons.Add("No active employment record is available for automatic appraisal routing.");
        }
        else if (employeeRoles.Contains(SpmeRoles.InstituteDirector))
        {
            appraiserMatch = deputyDirectorGeneral;
            approverMatch = directorGeneral;
        }
        else if (employeeRoles.Contains(SpmeRoles.HeadOfDivision))
        {
            appraiserMatch = instituteDirector;
            approverMatch = Prefer(deputyDirectorGeneral, directorGeneral);
        }
        else if (employeeRoles.Contains(SpmeRoles.HeadOfSection))
        {
            appraiserMatch = FindUser(users, SpmeRoles.HeadOfDivision, employment.DivisionId, null, employeeId);
        }
        else if (employment.SectionId.HasValue)
        {
            appraiserMatch = FindUser(users, SpmeRoles.HeadOfSection, employment.DivisionId, employment.SectionId, employeeId);
        }
        else
        {
            appraiserMatch = FindUser(users, SpmeRoles.HeadOfDivision, employment.DivisionId, null, employeeId);
        }

        if (appraiserMatch.IsAmbiguous) reasons.Add("More than one eligible appraiser matches the employee's verified organisational route.");
        else if (!appraiserMatch.UserId.HasValue) reasons.Add(employeeRoles.Contains(SpmeRoles.InstituteDirector)
            ? "No configured Deputy Director General is available to appraise the Institute Director."
            : "No eligible appraiser is configured for the employee's verified organisational route.");
        if (approverMatch.IsAmbiguous) reasons.Add("More than one eligible final approver matches the employee's verified organisational route.");
        else if (!approverMatch.UserId.HasValue) reasons.Add(employeeRoles.Contains(SpmeRoles.InstituteDirector)
            ? "No configured Director General is available to finally approve the Institute Director's appraisal."
            : employeeRoles.Contains(SpmeRoles.HeadOfDivision)
                ? "The Head of Division requires a configured superior as final approver."
                : "No eligible final approver is configured for the employee's verified organisational route.");
        var appraiser = appraiserMatch.UserId;
        var approver = approverMatch.UserId;
        if (appraiser.HasValue && appraiser == approver)
        {
            approver = null;
            reasons.Add("The automatic appraiser and final approver resolve to the same user.");
        }

        return new AppraisalRoute(appraiser, approver, reasons.Count == 0 ? null : string.Join(' ', reasons));
    }

    private static RouteMatch Prefer(RouteMatch primary, RouteMatch fallback) =>
        primary.UserId.HasValue || primary.IsAmbiguous ? primary : fallback;

    private static RouteMatch FindUser(IReadOnlyList<RoutingUser> users, string role, Guid? divisionId, Guid? sectionId, Guid employeeId)
    {
        var matches = users.Where(x => x.Role.Equals(role, StringComparison.OrdinalIgnoreCase) && x.EmployeeId != employeeId)
            .Where(x => !divisionId.HasValue || x.DivisionId == divisionId)
            .Where(x => !sectionId.HasValue || x.SectionId == sectionId)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        return matches.Count == 1 ? new RouteMatch(matches[0], false) : new RouteMatch(null, matches.Count > 1);
    }

    private static async Task<List<RoutingUser>> RoutingUsersAsync(Guid instituteId, SpmeDbContext db, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking()
            .Where(x => x.InstituteId == instituteId && x.AccountStatus == "active")
            .Select(x => new { x.Id, x.EmployeeId, x.DisplayName })
            .ToListAsync(ct);
        var userIds = users.Select(x => x.Id).ToList();
        var identityRoles = await (from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId) && RoutingRoles.Contains(role.Name!)
            select new { userRole.UserId, Role = role.Name! }).ToListAsync(ct);
        var employeeIds = users.Where(x => x.EmployeeId.HasValue).Select(x => x.EmployeeId!.Value).ToList();
        var employments = (await db.EmploymentRecords.AsNoTracking()
                .Where(x => employeeIds.Contains(x.EmployeeId) && x.InstituteId == instituteId && x.IsCurrent && x.ServiceStatus == "active")
                .ToListAsync(ct))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(record => record.EffectiveFrom).First());
        var userById = users.ToDictionary(x => x.Id);
        var result = new Dictionary<(Guid UserId, string Role), RoutingUser>();

        foreach (var identityRole in identityRoles)
        {
            var user = userById[identityRole.UserId];
            employments.TryGetValue(user.EmployeeId ?? Guid.Empty, out var employment);
            result[(user.Id, identityRole.Role)] = new RoutingUser(user.Id, user.EmployeeId, user.DisplayName,
                identityRole.Role, employment?.DivisionId, employment?.SectionId);
        }

        foreach (var user in users.Where(x => x.EmployeeId.HasValue))
        {
            if (!employments.TryGetValue(user.EmployeeId!.Value, out var employment)) continue;
            foreach (var role in CanonicalLeadershipRoles(employment.LeadershipRoles))
                result[(user.Id, role)] = new RoutingUser(user.Id, user.EmployeeId, user.DisplayName, role,
                    employment.DivisionId, employment.SectionId);
        }

        return result.Values.ToList();
    }

    private static IEnumerable<string> CanonicalLeadershipRoles(string? leadershipRoles)
    {
        if (string.IsNullOrWhiteSpace(leadershipRoles)) yield break;
        foreach (var value in leadershipRoles.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var role = string.Join(' ', value.Replace('-', ' ').Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant();
            if (role.Contains("head of section", StringComparison.Ordinal) || role.Contains("section head", StringComparison.Ordinal))
                yield return SpmeRoles.HeadOfSection;
            else if (role.Contains("head of division", StringComparison.Ordinal) || role.Contains("division head", StringComparison.Ordinal))
                yield return SpmeRoles.HeadOfDivision;
            else if (role.Contains("institute director", StringComparison.Ordinal))
                yield return SpmeRoles.InstituteDirector;
            else if (role.Contains("deputy director general", StringComparison.Ordinal) || role == "ddg")
                yield return SpmeRoles.DeputyDirectorGeneral;
            else if (role.Contains("director general", StringComparison.Ordinal) || role == "dg")
                yield return SpmeRoles.DirectorGeneral;
        }
    }

    private sealed record RoutingUser(Guid UserId, Guid? EmployeeId, string DisplayName, string Role, Guid? DivisionId, Guid? SectionId);
    private sealed record RouteMatch(Guid? UserId, bool IsAmbiguous);
    private sealed record AppraisalRoute(Guid? HodUserId, Guid? DirectorUserId, string? ExceptionReason);
    private static Guid? Institute(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;
    private static Task<AppraisalCycle?> FindCycle(Guid id, HttpContext context, SpmeDbContext db, bool tracked, CancellationToken ct)
    { var query = tracked ? db.AppraisalCycles.AsQueryable() : db.AppraisalCycles.AsNoTracking(); var institute = Institute(context); return query.FirstOrDefaultAsync(x => x.Id == id && x.InstituteId == institute, ct); }
    private static AppraisalCycleResponse Map(AppraisalCycle x) => new(x.Id, x.InstituteId, x.Name, x.Year, x.StartDate, x.EndDate,
        x.PlanningStart, x.PlanningEnd, x.MidyearStart, x.MidyearEnd, x.YearEndStart, x.YearEndEnd, x.Status, x.ReopenReason,
        x.FormTemplateVersion, x.FormTemplateChecksum, ConcurrencyToken.Format(x.RowVersion), Actions(x), x.CreatedAt, x.UpdatedAt);
    private static string[] Actions(AppraisalCycle x) => x.Status switch
    { AppraisalCycleStatuses.Draft => ["edit", "activate", "manage-assignments"], AppraisalCycleStatuses.Open => ["close", "manage-assignments", "run-reminders"], AppraisalCycleStatuses.Closed => ["reopen"], _ => [] };
    private static async Task<bool> Save(SpmeDbContext db, CancellationToken ct) { try { await db.SaveChangesAsync(ct); return true; } catch (ConcurrencyConflictException) { return false; } }
    private static IResult WithEtag<T>(HttpContext context, Ok<T> result, byte[] version) { context.Response.Headers.ETag = ConcurrencyToken.Format(version); return result; }
    private static IResult Stale() => EndpointProblems.FromError(Error.PreconditionFailed("The appraisal resource was modified by another request. Reload it and retry."));
    private static IResult NotFoundCycle() => EndpointProblems.FromError(Error.NotFound("Appraisal cycle not found."));
    private static IResult ForbiddenScope() => EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));
}
