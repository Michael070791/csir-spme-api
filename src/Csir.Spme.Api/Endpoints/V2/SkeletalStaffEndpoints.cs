using System.Security.Claims;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class SkeletalStaffEndpoints
{
    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<SkeletalStaffApprovalResponse>> EmptyApprovals =
        new Dictionary<Guid, IReadOnlyList<SkeletalStaffApprovalResponse>>();

    public static void MapSkeletalStaffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var requests = endpoints.MapGroup("/api/v2/skeletal-staff-requests")
            .WithGroupName("v2")
            .WithTags("Leave")
            .RequireAuthorization()
            .WithDescription("Authenticated skeletal staff requests record employee availability during an open holiday period, controlled approval decisions, completed service, and one-time annual leave credit eligibility. Monetary allowances are intentionally not calculated until an approved allowance policy exists.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        requests.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("SkeletalStaffRequests_List")
            .WithSummary("List scoped skeletal staff requests.")
            .WithDescription("Lists paged skeletal staff requests within the caller's effective scope. Employees can see only their own records; HR or platform managers may filter by employee, holiday period, and status, with page size limited to 100.")
            .Produces<PageResponse<SkeletalStaffRequestResponse>>(StatusCodes.Status200OK);
        requests.MapGet("/active-holiday-period", GetActiveHolidayPeriodAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("SkeletalStaffRequests_GetActiveHolidayPeriod")
            .WithSummary("Get the effective open holiday period.")
            .WithDescription("Returns the currently effective open holiday period for the caller's institute and availability date, preferring an institute-specific period over a CSIR-wide period. A valid employee or institute scope is required; no match returns not found.")
            .Produces<HolidayPeriodResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);
        requests.MapPost("", CreateAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_Create")
            .WithSummary("Create an employee-owned skeletal staff draft.")
            .WithDescription("Creates a draft for the authenticated active employee during an effective open holiday period. Availability must be confirmed, selected dates must be distinct and within the period window, and only one request per employee and period is allowed; the response includes an ETag.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("SkeletalStaffRequests_Get")
            .WithSummary("Get a scoped skeletal staff request.")
            .WithDescription("Returns one request with its approval history and current ETag. Employees are limited to their own request, institute HR is limited to its institute, and platform managers may read across institutes; inaccessible identifiers return not found.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);
        requests.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_Update")
            .WithSummary("Update a draft skeletal staff request with an ETag.")
            .WithDescription("Updates an accessible employee-owned draft while its holiday period remains open. Availability and selected dates are revalidated, and the current If-Match ETag is required; stale versions return precondition failed and invalid lifecycle state returns conflict.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status412PreconditionFailed);
        requests.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_Delete")
            .WithSummary("Delete an employee-owned draft with an ETag.")
            .WithDescription("Deletes only an accessible employee-owned request that is still in draft status. The current If-Match ETag is required; non-draft requests return a lifecycle conflict, stale versions return precondition failed, and inaccessible identifiers return not found.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/submit", SubmitAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_Submit")
            .WithSummary("Submit a skeletal staff draft for approval.")
            .WithDescription("Submits the authenticated employee's accessible draft into the configured Section Head, Head of Division, and Institute Director approval chain. The current If-Match ETag is required; invalid state or stale versions return problem details.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status412PreconditionFailed);
        requests.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave).WithName("SkeletalStaffRequests_Approve")
            .WithSummary("Approve the current skeletal staff workflow stage.")
            .WithDescription("Approves only the current workflow stage when the caller has the matching role and employee organizational scope; platform administration may decide any stage. The current If-Match ETag is required, each decision is audited, and invalid transitions return conflict.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave).WithName("SkeletalStaffRequests_Reject")
            .WithSummary("Reject the current skeletal staff workflow stage.")
            .WithDescription("Rejects only the current workflow stage when the caller has the matching role and organizational scope; platform administration may decide any stage. A reason and current If-Match ETag are required, the decision is audited, and invalid transitions return conflict.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_Cancel")
            .WithSummary("Cancel a draft or pending skeletal staff request with an ETag.")
            .WithDescription("Cancels an accessible request owned by the authenticated employee when its lifecycle still permits cancellation. The current If-Match ETag is required; invalid transitions return conflict, stale versions return precondition failed, and inaccessible identifiers return not found.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/complete", CompleteAsync).RequireAuthorization(AuthorizationPolicies.ManageLeave).WithName("SkeletalStaffRequests_Complete")
            .WithSummary("Confirm approved skeletal staff service is complete.")
            .WithDescription("Marks approved skeletal staff service complete for an institute-scoped HR manager or platform administrator. The current If-Match ETag is required; only the approved lifecycle state can be completed, and inaccessible records use a non-disclosing not-found response.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/credit-leave", CreditLeaveAsync).RequireAuthorization(AuthorizationPolicies.ManageLeave).WithName("SkeletalStaffRequests_CreditLeave")
            .WithSummary("Apply the holiday period leave credit exactly once.")
            .WithDescription("Credits the completed employee's annual leave balance for the requested leave year using the holiday period's configured deduction days. Institute-scoped HR or platform management and the current If-Match ETag are required; duplicate or invalid lifecycle credits return conflict.")
            .Produces<SkeletalStaffRequestResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapGet("/{id:guid}/allowance-report", GetAllowanceReportAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("SkeletalStaffRequests_GetAllowanceReport")
            .WithSummary("Generate the completed-service allowance eligibility report.")
            .WithDescription("Generates a scoped report only after skeletal staff service is completed, including employee, institute, holiday period, approval, and leave-credit status. It records leave-credit eligibility only because no monetary allowance type or rate is configured.")
            .Produces<SkeletalStaffAllowanceReportResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapGet("/pending-approvals", ListPendingApprovalsAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave).WithName("SkeletalStaffRequests_ListPendingApprovals")
            .WithSummary("List skeletal staff requests awaiting the caller's approval stage.")
            .Produces<CollectionResponse<SkeletalStaffRequestResponse>>(StatusCodes.Status200OK);
        requests.MapPost("/{id:guid}/resend-approval", ResendApprovalAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave).WithName("SkeletalStaffRequests_ResendApproval")
            .WithSummary("Resend the current stage approval notifications and rotate email tokens.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        requests.MapGet("/{id:guid}/service-report", GetServiceReportAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("SkeletalStaffRequests_GetServiceReport")
            .WithSummary("Download the branded skeletal staff service report PDF.")
            .Produces(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status409Conflict);
        requests.MapPost("/{id:guid}/send-service-report", SendServiceReportAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave).WithName("SkeletalStaffRequests_SendServiceReport")
            .WithSummary("Send the skeletal staff service report to Head of Admin.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAsync(HttpContext context, SpmeDbContext db, Guid? employeeId, Guid? holidayPeriodId, string? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var manager = IsManager(context);
        var employeeScope = EmployeeId(context);
        var instituteScope = HolidayPeriodEndpoints.InstituteId(context);
        if (!manager && !employeeScope.HasValue) return EndpointProblems.FromError(Error.Forbidden("An employee identity is required."));
        if (manager && !HolidayPeriodEndpoints.IsPlatform(context) && !instituteScope.HasValue) return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));

        var effectiveEmployee = manager ? employeeId : employeeScope;
        var query = db.SkeletalStaffRequests.AsNoTracking().AsQueryable();
        if (!HolidayPeriodEndpoints.IsPlatform(context) && instituteScope.HasValue) query = query.Where(x => x.InstituteId == instituteScope.Value);
        if (effectiveEmployee.HasValue) query = query.Where(x => x.EmployeeId == effectiveEmployee.Value);
        if (holidayPeriodId.HasValue) query = query.Where(x => x.HolidayPeriodId == holidayPeriodId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var requests = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var approvals = await LoadApprovalsAsync(db, requests.Select(x => x.Id), ct);
        return TypedResults.Ok(new PageResponse<SkeletalStaffRequestResponse>(requests.Select(x => Map(x, approvals)).ToList(), total, page, pageSize));
    }

    private static async Task<IResult> GetActiveHolidayPeriodAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var institute = await ResolveEffectiveInstituteAsync(context, db, ct);
        if (institute.IsFailure) return EndpointProblems.FromError(institute.Error!);

        var today = DateTime.UtcNow.Date;
        var period = await db.HolidayPeriods.AsNoTracking()
            .Where(x => x.Status == HolidayPeriodStatuses.Open && x.AvailabilityStartDate <= today && x.AvailabilityEndDate >= today &&
                (x.ScopeType == ScopeTypes.CsirWide || (x.ScopeType == ScopeTypes.Institute && x.InstituteId == institute.Value)))
            .OrderByDescending(x => x.ScopeType == ScopeTypes.Institute)
            .FirstOrDefaultAsync(ct);
        return period is null
            ? EndpointProblems.FromError(Error.NotFound("No active holiday period is available."))
            : TypedResults.Ok(HolidayPeriodEndpoints.Map(period));
    }

    private static async Task<IResult> CreateAsync(CreateSkeletalStaffRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var employee = await ResolveOwnerEmployeeAsync(context, db, ct);
        if (employee.IsFailure) return EndpointProblems.FromError(employee.Error!);
        if (!request.ConfirmAvailability) return EndpointProblems.FromError(Error.Validation("Availability must be confirmed."));

        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.HolidayPeriodId, ct);
        if (period is null || !IsEffectiveOpenPeriod(period, employee.Value!.InstituteId)) return EndpointProblems.FromError(Error.NotFound("Holiday period not found."));
        var dates = ValidateDates(request.SelectedDates, period);
        if (dates.IsFailure) return EndpointProblems.FromError(dates.Error!);
        if (await db.SkeletalStaffRequests.AnyAsync(x => x.EmployeeId == employee.Value.EmployeeId && x.HolidayPeriodId == period.Id, ct))
            return EndpointProblems.FromError(Error.Conflict("A skeletal staff request already exists for this holiday period."));

        var created = SkeletalStaffRequest.CreateDraft(
            employee.Value.EmployeeId, employee.Value.InstituteId, period.Id, SerializeDates(dates.Value!),
            dates.Value![0], dates.Value![^1], request.SignatureName, request.Comment);
        if (created.IsFailure) return EndpointProblems.FromError(created.Error!);

        db.SkeletalStaffRequests.Add(created.Value!);
        await audit.RecordAsync("skeletal-staff-request.created", "SkeletalStaffRequest", created.Value!.Id.ToString(), null, $"status={created.Value.Status}", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(created.Value.RowVersion);
        return TypedResults.Created($"/api/v2/skeletal-staff-requests/{created.Value.Id}", Map(created.Value, EmptyApprovals));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var request = await db.SkeletalStaffRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null || !CanAccess(context, request)) return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        var approvals = await LoadApprovalsAsync(db, [id], ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(request.RowVersion);
        return TypedResults.Ok(Map(request, approvals));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateSkeletalStaffRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || !CanAccess(context, item)) return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        if (!CanManageOwnRequest(context, item)) return EndpointProblems.FromError(Error.Forbidden("You are not authorized to edit this skeletal staff request."));
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item, out var problem)) return problem!;
        if (!request.ConfirmAvailability) return EndpointProblems.FromError(Error.Validation("Availability must be confirmed."));

        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.HolidayPeriodId, ct);
        if (period is null || !IsEffectiveOpenPeriod(period, item.InstituteId)) return EndpointProblems.FromError(Error.Conflict("The associated holiday period is no longer open."));
        var dates = ValidateDates(request.SelectedDates, period);
        if (dates.IsFailure) return EndpointProblems.FromError(dates.Error!);
        var updated = item.UpdateDraft(SerializeDates(dates.Value!), dates.Value![0], dates.Value![^1], request.SignatureName, request.Comment);
        if (updated.IsFailure) return EndpointProblems.FromError(updated.Error!);

        await audit.RecordAsync("skeletal-staff-request.updated", "SkeletalStaffRequest", item.Id.ToString(), null, "draft-updated", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.RowVersion);
        return TypedResults.Ok(Map(item, EmptyApprovals));
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || !CanAccess(context, item)) return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        if (!CanManageOwnRequest(context, item)) return EndpointProblems.FromError(Error.Forbidden("You are not authorized to delete this skeletal staff request."));
        if (item.Status != SkeletalStaffRequestStatuses.Draft) return EndpointProblems.FromError(Error.StateTransition("Only draft skeletal staff requests can be deleted."));
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item, out var problem)) return problem!;

        db.SkeletalStaffRequests.Remove(item);
        await audit.RecordAsync("skeletal-staff-request.deleted", "SkeletalStaffRequest", id.ToString(), "status=draft", null, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SubmitAsync(
        Guid id,
        HttpContext context,
        SpmeDbContext db,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApproverResolver approverResolver,
        CancellationToken ct)
    {
        var item = await FindOwnedAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!CanManageOwnRequest(context, item.Value!)) return EndpointProblems.FromError(Error.Forbidden("You are not authorized to submit this skeletal staff request."));
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;

        var chain = await approverResolver.BuildSkeletalStaffChainAsync(item.Value!.InstituteId, item.Value.EmployeeId, ct);
        if (chain.Count == 0)
            return EndpointProblems.FromError(Error.Validation("A current employment record is required before submitting skeletal staff availability."));
        foreach (var stage in chain)
        {
            var approvers = await approverResolver.FindStageApproversAsync(
                item.Value.InstituteId, item.Value.EmployeeId, stage, ct);
            if (approvers.Count == 0)
            {
                return EndpointProblems.FromError(stage switch
                {
                    LeaveApprovalStages.HeadOfDivision => Error.Validation("Head of Division must be assigned before skeletal staff requests can be submitted."),
                    LeaveApprovalStages.AdminDirector => Error.Validation("Head of Admin must be assigned before skeletal staff requests can be submitted."),
                    _ => Error.Validation("A required approver must be assigned before skeletal staff requests can be submitted.")
                });
            }
        }

        var submitted = item.Value.Submit(chain[0], DateTimeOffset.UtcNow);
        if (submitted.IsFailure) return EndpointProblems.FromError(submitted.Error!);
        await audit.RecordAsync("skeletal-staff-request.submitted", "SkeletalStaffRequest", id.ToString(), "status=draft", $"status={item.Value.Status}", ct);
        await notifications.StageSkeletalStaffAwaitingApprovalAsync(
            item.Value.Id,
            item.Value.InstituteId,
            item.Value.EmployeeId,
            chain[0],
            ParseDates(item.Value.SelectedDatesJson),
            ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.Value.RowVersion);
        return TypedResults.Ok(Map(item.Value, EmptyApprovals));
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        SkeletalStaffDecisionRequest request,
        HttpContext context,
        SpmeDbContext db,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApproverResolver approverResolver,
        CancellationToken ct)
    {
        var item = await FindForStageDecisionAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;

        var chain = await approverResolver.BuildSkeletalStaffChainAsync(item.Value!.InstituteId, item.Value.EmployeeId, ct);
        var stageIndex = Array.IndexOf(chain.ToArray(), item.Value.CurrentApprovalStage);
        if (stageIndex < 0) return EndpointProblems.FromError(Error.StateTransition("The skeletal staff request is not awaiting an approval decision."));
        var stage = chain[stageIndex];
        var nextStage = stageIndex + 1 < chain.Count ? chain[stageIndex + 1] : null;
        var approved = item.Value.Approve(stage, nextStage);
        if (approved.IsFailure) return EndpointProblems.FromError(approved.Error!);

        var sequence = (short)(await db.SkeletalStaffApprovals.CountAsync(x => x.SkeletalStaffRequestId == id && x.ApprovalStage == stage, ct) + 1);
        var approverUserId = await PersistedUserIdAsync(context, db, ct);
        db.SkeletalStaffApprovals.Add(SkeletalStaffApproval.Create(id, approverUserId, stage, ApprovalDecisions.Approved, request.Comments, sequence));
        await audit.RecordAsync("skeletal-staff-request.approved", "SkeletalStaffRequest", id.ToString(), $"stage={stage}", $"status={item.Value.Status}", ct);
        if (nextStage is not null)
            await notifications.StageSkeletalStaffAwaitingApprovalAsync(
                item.Value.Id, item.Value.InstituteId, item.Value.EmployeeId, nextStage,
                ParseDates(item.Value.SelectedDatesJson), ct);
        else
            await notifications.StageSkeletalStaffDecisionAsync(
                item.Value.Id, item.Value.InstituteId, item.Value.EmployeeId, "approved", null, ct);
        await db.SaveChangesAsync(ct);
        var approvals = await LoadApprovalsAsync(db, [id], ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.Value.RowVersion);
        return TypedResults.Ok(Map(item.Value, approvals));
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        RejectSkeletalStaffRequest request,
        HttpContext context,
        SpmeDbContext db,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        CancellationToken ct)
    {
        var item = await FindForStageDecisionAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;

        var stage = item.Value!.CurrentApprovalStage;
        var rejected = item.Value.Reject(stage, request.Reason);
        if (rejected.IsFailure) return EndpointProblems.FromError(rejected.Error!);
        var sequence = (short)(await db.SkeletalStaffApprovals.CountAsync(x => x.SkeletalStaffRequestId == id && x.ApprovalStage == stage, ct) + 1);
        var approverUserId = await PersistedUserIdAsync(context, db, ct);
        db.SkeletalStaffApprovals.Add(SkeletalStaffApproval.Create(id, approverUserId, stage, ApprovalDecisions.Rejected, request.Comments ?? request.Reason, sequence));
        await audit.RecordAsync("skeletal-staff-request.rejected", "SkeletalStaffRequest", id.ToString(), $"stage={stage}", "status=rejected", ct);
        await notifications.StageSkeletalStaffDecisionAsync(item.Value.Id, item.Value.InstituteId, item.Value.EmployeeId, "rejected", request.Reason, ct);
        await db.SaveChangesAsync(ct);
        var approvals = await LoadApprovalsAsync(db, [id], ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.Value.RowVersion);
        return TypedResults.Ok(Map(item.Value, approvals));
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var item = await FindOwnedAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!CanManageOwnRequest(context, item.Value!)) return EndpointProblems.FromError(Error.Forbidden("You are not authorized to cancel this skeletal staff request."));
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;
        var cancelled = item.Value!.Cancel();
        if (cancelled.IsFailure) return EndpointProblems.FromError(cancelled.Error!);

        await audit.RecordAsync("skeletal-staff-request.cancelled", "SkeletalStaffRequest", id.ToString(), null, "status=cancelled", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.Value.RowVersion);
        return TypedResults.Ok(Map(item.Value, EmptyApprovals));
    }

    private static async Task<IResult> CompleteAsync(Guid id, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var item = await FindManagedAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;
        var completed = item.Value!.Complete(DateTimeOffset.UtcNow);
        if (completed.IsFailure) return EndpointProblems.FromError(completed.Error!);

        await audit.RecordAsync("skeletal-staff-request.completed", "SkeletalStaffRequest", id.ToString(), "status=approved", "status=completed", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(item.Value.RowVersion);
        return TypedResults.Ok(Map(item.Value, EmptyApprovals));
    }

    private static async Task<IResult> CreditLeaveAsync(Guid id, CreditSkeletalStaffLeaveRequest request, HttpContext context, SpmeDbContext db, IAuditService audit, CancellationToken ct)
    {
        var item = await FindManagedAsync(id, context, db, ct);
        if (item.IsFailure) return EndpointProblems.FromError(item.Error!);
        if (!HolidayPeriodEndpoints.TryApplyEtag(context, db, item.Value!, out var problem)) return problem!;
        var skeletalRequest = item.Value!;
        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == skeletalRequest.HolidayPeriodId, ct);
        if (period is null) return EndpointProblems.FromError(Error.NotFound("Holiday period not found."));

        var balance = await db.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == skeletalRequest.EmployeeId && x.LeaveType == LeaveTypes.Annual && x.LeaveYear == request.LeaveYear, ct);
        if (balance is null)
        {
            balance = LeaveBalance.Create(skeletalRequest.EmployeeId, LeaveTypes.Annual, request.LeaveYear, 0m);
            db.LeaveBalances.Add(balance);
        }
        if (period.DeductionDays > 0)
        {
            var adjusted = balance.AddAdjustment(period.DeductionDays);
            if (adjusted.IsFailure) return EndpointProblems.FromError(adjusted.Error!);
        }
        var credited = skeletalRequest.CreditLeave(request.LeaveYear, DateTimeOffset.UtcNow);
        if (credited.IsFailure) return EndpointProblems.FromError(credited.Error!);

        await audit.RecordAsync("skeletal-staff-request.leave-credited", "SkeletalStaffRequest", id.ToString(), null, $"leaveYear={request.LeaveYear};days={period.DeductionDays}", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(skeletalRequest.RowVersion);
        return TypedResults.Ok(Map(skeletalRequest, EmptyApprovals));
    }

    private static async Task<IResult> GetAllowanceReportAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null || !CanAccess(context, item)) return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        if (item.Status != SkeletalStaffRequestStatuses.Completed) return EndpointProblems.FromError(Error.StateTransition("The allowance report is available after the skeletal staff request is completed."));

        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.HolidayPeriodId, ct);
        if (period is null) return EndpointProblems.FromError(Error.NotFound("Holiday period not found."));
        var employee = await db.Employees.AsNoTracking().Where(x => x.Id == item.EmployeeId)
            .Select(x => new SkeletalStaffEmployeeSummaryResponse(x.Id, x.StaffId, x.Surname, x.OtherNames))
            .FirstOrDefaultAsync(ct);
        var institute = await db.Institutes.AsNoTracking().Where(x => x.Id == item.InstituteId)
            .Select(x => new SkeletalStaffInstituteSummaryResponse(x.Id, x.Code, x.Name))
            .FirstOrDefaultAsync(ct);
        if (employee is null || institute is null) return EndpointProblems.FromError(Error.NotFound("Skeletal staff report data not found."));
        var approvals = await LoadApprovalsAsync(db, [id], ct);
        var creditStatus = item.LeaveCreditedAt.HasValue ? "credited" : "pending-credit";
        var eligibility = new SkeletalStaffAllowanceEligibilityResponse(
            "leave-credit", creditStatus, null, null, period.DeductionDays, item.LeaveCreditYear, item.LeaveCreditedAt,
            "No approved monetary allowance type or rate is configured. This report records leave-credit eligibility only.");
        return TypedResults.Ok(new SkeletalStaffAllowanceReportResponse(
            1, DateTimeOffset.UtcNow, Map(item, approvals), employee, institute, HolidayPeriodEndpoints.Map(period), eligibility,
            "not-configured"));
    }

    private static async Task<IResult> ListPendingApprovalsAsync(
        HttpContext context,
        SpmeDbContext db,
        IWorkflowApproverResolver approverResolver,
        CancellationToken ct)
    {
        if (!context.User.IsInRole(SpmeRoles.HeadOfSection) &&
            !context.User.IsInRole(SpmeRoles.HeadOfDivision) &&
            !context.User.IsInRole(SpmeRoles.HeadOfAdmin) &&
            !context.User.IsInRole(SpmeRoles.InstituteDirector) &&
            !HolidayPeriodEndpoints.IsPlatform(context))
            return EndpointProblems.FromError(Error.Forbidden("You are not authorized to review skeletal staff approvals."));

        var instituteScope = HolidayPeriodEndpoints.InstituteId(context);
        var query = db.SkeletalStaffRequests.AsNoTracking()
            .Where(request => request.Status == SkeletalStaffRequestStatuses.Submitted ||
                              request.Status == SkeletalStaffRequestStatuses.UnderReview);
        if (!HolidayPeriodEndpoints.IsPlatform(context) && instituteScope.HasValue)
            query = query.Where(request => request.InstituteId == instituteScope.Value);

        var candidates = await query.OrderByDescending(request => request.SubmittedAt).ToListAsync(ct);
        var pending = new List<SkeletalStaffRequest>();
        foreach (var candidate in candidates)
        {
            if (await CanDecideCurrentStageAsync(context, candidate, db, ct))
                pending.Add(candidate);
        }

        var approvals = await LoadApprovalsAsync(db, pending.Select(request => request.Id), ct);
        return TypedResults.Ok(new CollectionResponse<SkeletalStaffRequestResponse>(
            pending.Select(request => Map(request, approvals)).ToList(),
            pending.Count));
    }

    private static async Task<IResult> ResendApprovalAsync(
        Guid id,
        HttpContext context,
        SpmeDbContext db,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApprovalTokenService tokenService,
        CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null || !CanAccess(context, item))
            return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        if (string.IsNullOrWhiteSpace(item.CurrentApprovalStage))
            return EndpointProblems.FromError(Error.StateTransition("The skeletal staff request is not awaiting approval."));

        var canResend = HolidayPeriodEndpoints.IsPlatform(context) ||
                        IsManager(context) ||
                        await CanDecideCurrentStageAsync(context, item, db, ct);
        if (!canResend)
            return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));

        await tokenService.RevokeUnusedAsync(
            WorkflowApprovalPurposes.SkeletalStaff,
            item.Id,
            item.CurrentApprovalStage,
            ct);
        await notifications.StageSkeletalStaffAwaitingApprovalAsync(
            item.Id,
            item.InstituteId,
            item.EmployeeId,
            item.CurrentApprovalStage,
            ParseDates(item.SelectedDatesJson),
            ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetServiceReportAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.AsNoTracking().FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null || !CanAccess(context, item))
            return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == item.HolidayPeriodId, ct);
        if (period is null || !IsPeriodEnded(period))
            return EndpointProblems.FromError(Error.StateTransition("The service report is available only after the holiday period ends."));
        if (item.Status is not (SkeletalStaffRequestStatuses.Approved or SkeletalStaffRequestStatuses.Completed))
            return EndpointProblems.FromError(Error.StateTransition("The service report is available only for approved skeletal staff requests."));

        var pdf = await BuildServiceReportPdfAsync(db, item, period, ct);
        return Results.File(pdf, "application/pdf", $"skeletal-staff-service-report-{item.Id:N}.pdf");
    }

    private static async Task<IResult> SendServiceReportAsync(
        Guid id,
        HttpContext context,
        SpmeDbContext db,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApproverResolver approverResolver,
        CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.AsNoTracking().FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null || !CanManageOwnRequest(context, item))
            return EndpointProblems.FromError(Error.NotFound("Skeletal staff request not found."));
        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == item.HolidayPeriodId, ct);
        if (period is null || !IsPeriodEnded(period))
            return EndpointProblems.FromError(Error.StateTransition("The service report can be sent only after the holiday period ends."));
        if (item.Status is not (SkeletalStaffRequestStatuses.Approved or SkeletalStaffRequestStatuses.Completed))
            return EndpointProblems.FromError(Error.StateTransition("The service report can be sent only for approved skeletal staff requests."));

        var recipients = await approverResolver.FindStageApproversAsync(
            item.InstituteId, item.EmployeeId, LeaveApprovalStages.AdminDirector, ct);
        var recipient = recipients.FirstOrDefault();
        if (recipient is null)
            return EndpointProblems.FromError(Error.Validation("Head of Admin must be assigned before the service report can be sent."));

        var employeeName = await db.Employees.AsNoTracking()
            .Where(employee => employee.Id == item.EmployeeId)
            .Select(employee => new { employee.PreferredName, employee.OtherNames, employee.Surname })
            .FirstOrDefaultAsync(ct);
        var staffDisplayName = employeeName?.PreferredName ??
            string.Join(' ', new[] { employeeName?.OtherNames, employeeName?.Surname }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var approvals = await LoadApprovalsAsync(db, [id], ct);
        var pdf = await BuildServiceReportPdfAsync(db, item, period, ct);
        await notifications.StageSkeletalStaffServiceReportAsync(new SkeletalStaffServiceReportNotification(
            item.Id,
            item.InstituteId,
            item.EmployeeId,
            recipient.UserId,
            recipient.DisplayName,
            recipient.Email,
            recipient.Phone,
            staffDisplayName,
            $"{period.LeaveYear} holiday period",
            period.AvailabilityStartDate,
            period.AvailabilityEndDate,
            ParseDates(item.SelectedDatesJson),
            approvals.GetValueOrDefault(id, []).Select(approval =>
                new SkeletalStaffApprovalTrailEntry(
                    approval.ApprovalStage,
                    approval.Decision,
                    approval.DecidedAt,
                    approval.Comments)).ToList(),
            item.LeaveCreditedAt.HasValue ? "credited" : "pending-credit",
            pdf,
            true), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<byte[]> BuildServiceReportPdfAsync(
        SpmeDbContext db,
        SkeletalStaffRequest item,
        HolidayPeriod period,
        CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking()
            .Where(candidate => candidate.Id == item.EmployeeId)
            .Select(candidate => new { candidate.StaffId, candidate.Surname, candidate.OtherNames, candidate.PreferredName })
            .FirstOrDefaultAsync(ct);
        var institute = await db.Institutes.AsNoTracking()
            .Where(candidate => candidate.Id == item.InstituteId)
            .Select(candidate => new { candidate.Code, candidate.Name })
            .FirstOrDefaultAsync(ct);
        var employment = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == item.EmployeeId && record.IsCurrent)
            .Select(record => new { record.DivisionId, record.SectionId })
            .FirstOrDefaultAsync(ct);
        string? divisionName = null;
        string? sectionName = null;
        if (employment?.DivisionId is Guid divisionId)
            divisionName = await db.Divisions.AsNoTracking().Where(division => division.Id == divisionId).Select(division => division.Name).FirstOrDefaultAsync(ct);
        if (employment?.SectionId is Guid sectionId)
            sectionName = await db.Sections.AsNoTracking().Where(section => section.Id == sectionId).Select(section => section.Name).FirstOrDefaultAsync(ct);
        var approvals = await LoadApprovalsAsync(db, [item.Id], ct);
        var lines = new List<string>
        {
            $"Staff: {employee?.PreferredName ?? $"{employee?.OtherNames} {employee?.Surname}".Trim()} ({employee?.StaffId})",
            $"Institute: {institute?.Name} ({institute?.Code})",
            $"Division: {divisionName ?? "Not assigned"}",
            $"Section: {sectionName ?? "Not assigned"}",
            $"Holiday period: {period.LeaveYear}",
            $"Availability window: {period.AvailabilityStartDate:dd MMM yyyy} to {period.AvailabilityEndDate:dd MMM yyyy}",
            $"Selected dates: {string.Join(", ", ParseDates(item.SelectedDatesJson).Select(date => date.ToString("dd MMM yyyy")))}",
            $"Leave credit status: {(item.LeaveCreditedAt.HasValue ? "credited" : "pending-credit")}",
            string.Empty,
            "Approval trail"
        };
        foreach (var approval in approvals.GetValueOrDefault(item.Id, []))
            lines.Add($"{approval.ApprovalStage}: {approval.Decision} on {approval.DecidedAt:dd MMM yyyy HH:mm}");
        return SkeletalStaffServiceReportPdf.Build(new SkeletalStaffServiceReportPdf.SkeletalStaffServiceReportContent(lines));
    }

    private static bool IsPeriodEnded(HolidayPeriod period) =>
        period.Status is HolidayPeriodStatuses.Closed or HolidayPeriodStatuses.Finalized ||
        period.AvailabilityEndDate.Date < DateTime.UtcNow.Date;

    private static async Task<Result<SkeletalStaffRequest>> FindOwnedAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null || !CanAccess(context, item)
            ? Result<SkeletalStaffRequest>.Failure(Error.NotFound("Skeletal staff request not found."))
            : Result<SkeletalStaffRequest>.Success(item);
    }

    private static async Task<Result<SkeletalStaffRequest>> FindManagedAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null || !IsManager(context) || !CanAccess(context, item)
            ? Result<SkeletalStaffRequest>.Failure(Error.NotFound("Skeletal staff request not found."))
            : Result<SkeletalStaffRequest>.Success(item);
    }

    private static async Task<Result<SkeletalStaffRequest>> FindForStageDecisionAsync(
        Guid id,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);
        if (item is null || !CanAccessStageInstitute(context, item))
            return Result<SkeletalStaffRequest>.Failure(Error.NotFound("Skeletal staff request not found."));
        if (!await CanDecideCurrentStageAsync(context, item, db, ct))
            return Result<SkeletalStaffRequest>.Failure(Error.NotFound("Skeletal staff request not found."));
        return Result<SkeletalStaffRequest>.Success(item);
    }

    internal static async Task<bool> CanDecideCurrentStageAsync(
        HttpContext context,
        SkeletalStaffRequest item,
        SpmeDbContext db,
        CancellationToken ct)
    {
        if (HolidayPeriodEndpoints.IsPlatform(context))
            return true;

        var ownerScope = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == item.EmployeeId && record.IsCurrent)
            .Select(record => new { record.InstituteId, record.DivisionId, record.SectionId })
            .FirstOrDefaultAsync(ct);
        if (ownerScope is null || ownerScope.InstituteId != item.InstituteId)
            return false;

        if (item.CurrentApprovalStage == LeaveApprovalStages.InstituteDirector)
        {
            return context.User.IsInRole(SpmeRoles.InstituteDirector) &&
                HolidayPeriodEndpoints.InstituteId(context) == item.InstituteId;
        }

        var approverEmployeeId = EmployeeId(context);
        if (!approverEmployeeId.HasValue)
            return false;
        var approverScope = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == approverEmployeeId.Value && record.IsCurrent)
            .Select(record => new { record.InstituteId, record.DivisionId, record.SectionId })
            .FirstOrDefaultAsync(ct);
        if (approverScope is null || approverScope.InstituteId != item.InstituteId)
            return false;

        return item.CurrentApprovalStage switch
        {
            LeaveApprovalStages.SectionHead =>
                context.User.IsInRole(SpmeRoles.HeadOfSection) &&
                ownerScope.SectionId.HasValue &&
                ownerScope.SectionId == approverScope.SectionId,
            LeaveApprovalStages.HeadOfDivision =>
                context.User.IsInRole(SpmeRoles.HeadOfDivision) &&
                ownerScope.DivisionId.HasValue &&
                ownerScope.DivisionId == approverScope.DivisionId,
            LeaveApprovalStages.AdminDirector =>
                context.User.IsInRole(SpmeRoles.HeadOfAdmin) &&
                HolidayPeriodEndpoints.InstituteId(context) == item.InstituteId,
            _ => false
        };
    }

    private static async Task<Result<Csir.Spme.Application.Common.Interfaces.EmployeeScope>> ResolveOwnerEmployeeAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var employeeId = EmployeeId(context);
        if (!employeeId.HasValue) return Result<Csir.Spme.Application.Common.Interfaces.EmployeeScope>.Failure(Error.Forbidden("An employee identity is required."));
        var employee = await db.Employees.AsNoTracking().Where(x => x.Id == employeeId.Value && x.ProfileStatus == EmployeeProfileStatuses.Active)
            .Select(x => new Csir.Spme.Application.Common.Interfaces.EmployeeScope(x.Id, x.InstituteId, x.ProfileStatus, null)).FirstOrDefaultAsync(ct);
        if (employee is null || (HolidayPeriodEndpoints.InstituteId(context).HasValue && employee.InstituteId != HolidayPeriodEndpoints.InstituteId(context)))
            return Result<Csir.Spme.Application.Common.Interfaces.EmployeeScope>.Failure(Error.NotFound("Employee not found."));
        return Result<Csir.Spme.Application.Common.Interfaces.EmployeeScope>.Success(employee);
    }

    private static async Task<Result<Guid>> ResolveEffectiveInstituteAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var employeeId = EmployeeId(context);
        if (employeeId.HasValue && !IsManager(context))
        {
            var employeeInstitute = await db.Employees.AsNoTracking().Where(x => x.Id == employeeId.Value && x.ProfileStatus == EmployeeProfileStatuses.Active).Select(x => (Guid?)x.InstituteId).FirstOrDefaultAsync(ct);
            return employeeInstitute.HasValue ? Result<Guid>.Success(employeeInstitute.Value) : Result<Guid>.Failure(Error.NotFound("Employee not found."));
        }

        var instituteId = HolidayPeriodEndpoints.InstituteId(context);
        return instituteId.HasValue || HolidayPeriodEndpoints.IsPlatform(context)
            ? instituteId.HasValue ? Result<Guid>.Success(instituteId.Value) : Result<Guid>.Failure(Error.Validation("An institute scope is required."))
            : Result<Guid>.Failure(Error.Forbidden("An institute scope is required."));
    }

    private static Result<List<DateTime>> ValidateDates(IReadOnlyList<DateTime> selectedDates, HolidayPeriod period)
    {
        var dates = selectedDates.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
        if (dates.Count == 0 || dates.Count != selectedDates.Count || dates.Any(x => x < period.AvailabilityStartDate || x > period.AvailabilityEndDate))
            return Result<List<DateTime>>.Failure(Error.Validation("Selected dates must be distinct and within the holiday period availability window."));
        return Result<List<DateTime>>.Success(dates);
    }

    private static bool IsEffectiveOpenPeriod(HolidayPeriod period, Guid instituteId) =>
        period.Status == HolidayPeriodStatuses.Open &&
        (period.ScopeType == ScopeTypes.CsirWide || (period.ScopeType == ScopeTypes.Institute && period.InstituteId == instituteId));

    private static bool IsManager(HttpContext context) => context.User.IsInRole(SpmeRoles.HrAdmin) || HolidayPeriodEndpoints.IsPlatform(context);
    private static Guid? EmployeeId(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("employee_id"), out var id) ? id : null;
    private static async Task<Guid?> PersistedUserIdAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;

        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(ct);
    }

    private static bool CanAccess(HttpContext context, SkeletalStaffRequest item) =>
        IsManager(context)
            ? HolidayPeriodEndpoints.IsPlatform(context) || HolidayPeriodEndpoints.InstituteId(context) == item.InstituteId
            : EmployeeId(context) == item.EmployeeId;
    internal static bool CanAccessStageInstitute(HttpContext context, SkeletalStaffRequest item) =>
        HolidayPeriodEndpoints.IsPlatform(context) || HolidayPeriodEndpoints.InstituteId(context) == item.InstituteId;
    private static bool CanManageOwnRequest(HttpContext context, SkeletalStaffRequest item) => IsManager(context) || EmployeeId(context) == item.EmployeeId;

    private static string SerializeDates(IReadOnlyList<DateTime> dates) => JsonSerializer.Serialize(dates.Select(x => x.Date));
    private static IReadOnlyList<DateTime> ParseDates(string json) => JsonSerializer.Deserialize<List<DateTime>>(json)?.Select(x => x.Date).OrderBy(x => x).ToList() ?? [];

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SkeletalStaffApprovalResponse>>> LoadApprovalsAsync(SpmeDbContext db, IEnumerable<Guid> requestIds, CancellationToken ct)
    {
        var ids = requestIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, IReadOnlyList<SkeletalStaffApprovalResponse>>();
        var approvals = await db.SkeletalStaffApprovals.AsNoTracking().Where(x => ids.Contains(x.SkeletalStaffRequestId))
            .OrderBy(x => x.Sequence).Select(x => new { x.SkeletalStaffRequestId, Response = new SkeletalStaffApprovalResponse(x.Id, x.ApprovalStage, x.Decision, x.Comments, x.DecidedAt, x.Sequence) }).ToListAsync(ct);
        return approvals.GroupBy(x => x.SkeletalStaffRequestId).ToDictionary(x => x.Key, x => (IReadOnlyList<SkeletalStaffApprovalResponse>)x.Select(y => y.Response).ToList());
    }

    private static SkeletalStaffRequestResponse Map(SkeletalStaffRequest item, IReadOnlyDictionary<Guid, IReadOnlyList<SkeletalStaffApprovalResponse>> approvals) => new(
        item.Id, item.EmployeeId, item.HolidayPeriodId, ParseDates(item.SelectedDatesJson), item.SelectedStartDate ?? DateTime.MinValue, item.SelectedEndDate ?? DateTime.MinValue,
        item.Status, item.CurrentApprovalStage, item.SignatureName, item.Comment, item.RejectionReason, item.SubmittedAt, item.CompletedAt,
        item.LeaveCreditYear, item.LeaveCreditedAt, approvals.GetValueOrDefault(item.Id, []), ConcurrencyToken.Format(item.RowVersion), item.CreatedAt, item.UpdatedAt);
}
