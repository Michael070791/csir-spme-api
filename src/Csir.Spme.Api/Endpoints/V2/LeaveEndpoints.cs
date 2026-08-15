using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class LeaveEndpoints
{
    public static void MapLeaveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var leaveTypes = endpoints.MapGroup("/api/v2/leave-types")
            .WithGroupName("v2").WithTags("Leave")
            .RequireAuthorization(AuthorizationPolicies.ReadLeave);
        leaveTypes.MapGet("", ListLeaveTypes).WithName("LeaveTypes_List").WithSummary("List leave types.")
            .WithDescription("Returns policy-backed leave type metadata with optional requestability, gender, staff-category, and policy-status filters for authenticated staff.")
            .Produces<ListResponse<LeaveTypeMetadataResponse>>();
        leaveTypes.MapGet("/{code}", GetLeaveType).WithName("LeaveTypes_Get").WithSummary("Get a leave type.")
            .WithDescription("Returns one policy-backed leave type by its stable code, including requestability and staff eligibility metadata without exposing persistence details.")
            .Produces<DataResponse<LeaveTypeMetadataResponse>>();

        var requests = endpoints.MapGroup("/api/v2/leave-requests")
            .WithGroupName("v2").WithTags("Leave").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        endpoints.MapGet("/api/v2/leave-delegates/me", ListDelegatesAsync)
            .WithGroupName("v2").WithTags("Leave")
            .RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveDelegates_ListMine")
            .WithSummary("List leave handover delegates for the authenticated employee.")
            .WithDescription("Returns active staff in the caller's section when available, otherwise the caller's division. When neither scope has peers, alternate institute divisions are offered and may be selected with divisionId.")
            .Produces<DataResponse<LeaveDelegateOptionsDto>>();
        requests.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave)
            .WithName("LeaveRequests_List").WithSummary("List accessible leave requests.")
            .WithDescription("Returns a bounded cursor page of leave requests scoped to the authenticated employee or authorized institute leave administrator.")
            .Produces<ListResponse<LeaveRequestDto>>();
        requests.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave)
            .WithName("LeaveRequests_Get").WithSummary("Get an accessible leave request.")
            .WithDescription("Returns one institute-scoped leave request with an ETag for safe subsequent updates and non-disclosing not-found behavior.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/calculate-working-days", CalculateWorkingDaysAsync)
            .RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_CalculateWorkingDays").WithSummary("Calculate policy-aware working days.")
            .WithDescription("Calculates chargeable working days for the authenticated employee using configured weekends, holidays, leave policy, and eligibility rules.")
            .Produces<DataResponse<WorkingDaysResponse>>();
        requests.MapPost("", CreateAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_Create").WithSummary("Create a draft leave request.")
            .WithDescription("Creates an employee-owned draft after policy, date, balance, delegate, and supporting-document validation; an idempotency key is required.")
            .Produces<DataResponse<LeaveRequestDto>>(StatusCodes.Status201Created);
        requests.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_Update").WithSummary("Update a draft leave request.")
            .WithDescription("Updates an accessible draft using If-Match optimistic concurrency and revalidates all policy, balance, date, delegate, and document requirements.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/{id:guid}/submit", SubmitAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_Submit").WithSummary("Submit a draft leave request.")
            .WithDescription("Submits an owned draft into the configured approval chain and reserves its balance atomically using ETag and idempotency safeguards.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave)
            .WithName("LeaveRequests_Approve").WithSummary("Approve the current leave stage.")
            .WithDescription("Records an authorized institute approver decision, advances the approval chain, and consumes the reserved balance at final approval.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(AuthorizationPolicies.ApproveLeave)
            .WithName("LeaveRequests_Reject").WithSummary("Reject the current leave stage.")
            .WithDescription("Records an authorized rejection with its employee-visible reason and releases the reserved balance using ETag and idempotency safeguards.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/{id:guid}/cancel", CancelAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_Cancel").WithSummary("Cancel an owned leave request.")
            .WithDescription("Cancels an accessible leave request when its lifecycle permits and restores pending or consumed balance amounts as appropriate.")
            .Produces<DataResponse<LeaveRequestDto>>();
        requests.MapPost("/{id:guid}/resume", ResumeAsync).RequireAuthorization(AuthorizationPolicies.RequestLeave)
            .WithName("LeaveRequests_Resume").WithSummary("Submit or approve leave resumption.")
            .WithDescription("Submits an employee resumption record or lets an authorized approver complete a pending resumption with concurrency protection.")
            .Produces<DataResponse<LeaveRequestDto>>();

        var balances = endpoints.MapGroup("/api/v2/leave-balances")
            .WithGroupName("v2").WithTags("Leave").RequireAuthorization(AuthorizationPolicies.ReadLeave);
        balances.MapGet("/me", GetMyBalancesAsync).WithName("LeaveBalances_GetMine")
            .WithSummary("Get the authenticated employee's leave balances.")
            .WithDescription("Returns only the bearer-token employee's annual leave balances, including used, pending, adjusted, and remaining day totals.")
            .Produces<ListResponse<LeaveBalanceDto>>();
        balances.MapGet("", GetBalancesAsync).WithName("LeaveBalances_List")
            .WithSummary("Get an employee's leave balances.")
            .WithDescription("Returns institute-scoped balances for an employee when the caller is that employee or holds authorized leave-read permission.")
            .Produces<ListResponse<LeaveBalanceDto>>();
        balances.MapGet("/employee/{employeeId:guid}/{leaveYear:int}", GetCompatibilityBalancesAsync)
            .WithName("LeaveBalances_ListByEmployee").WithSummary("Get balances using the V1-compatible route.")
            .WithDescription("Provides the documented compatibility route for institute-scoped employee balances while enforcing the same V2 authorization rules.")
            .Produces<ListResponse<LeaveBalanceDto>>();

        var assignments = endpoints.MapGroup("/api/v2/leave-balances")
            .WithGroupName("v2").WithTags("Leave")
            .RequireAuthorization(AuthorizationPolicies.ManageLeave)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        assignments.MapPost("/assignments", AssignAnnualLeaveAsync)
            .WithName("LeaveBalances_Assign")
            .WithSummary("Assign annual leave days to one employee.")
            .WithDescription("Sets the current-year annual leave entitlement for one institute-scoped employee without changing used, pending, or adjusted days. PlatformAdmin, HrAdmin, and preserved HR/Admin/Writer staff-management roles may assign 0 to 366 days. Already used and pending leave cannot exceed the new entitlement. The assignment is auditable and updates remaining days shown in the employee directory.")
            .Produces<DataResponse<LeaveBalanceDto>>();
        assignments.MapPost("/bulk-assignments", BulkAssignAnnualLeaveAsync)
            .WithName("LeaveBalances_BulkAssign")
            .WithSummary("Assign annual leave days to selected employees.")
            .WithDescription("Sets the annual leave entitlement for up to 100 selected employees in one request. An optional staffCategory of junior-staff, senior-staff, or senior-member limits the assignment to matching current employment records and skips the rest. Out-of-scope employees use a non-disclosing not-found outcome. Used and pending leave are preserved.")
            .Produces<DataResponse<BulkAssignAnnualLeaveResult>>();
    }

    private static async Task<IResult> ListDelegatesAsync(
        Guid? divisionId,
        LeaveRequestService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.ListMyDelegateOptionsAsync(divisionId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static IResult ListLeaveTypes(
        bool? requestable,
        string? gender,
        string? category,
        string? policyStatus,
        HttpContext context)
    {
        var items = LeaveTypeCatalog.List(requestable, gender, category, policyStatus);
        return TypedResults.Ok(ResponseEnvelope.List(context, items, null));
    }

    private static IResult GetLeaveType(string code, HttpContext context)
    {
        var item = string.IsNullOrWhiteSpace(code) ? null : LeaveTypeCatalog.Find(code);
        return item is null
            ? EndpointProblems.FromError(Error.NotFound("Leave type not found."))
            : TypedResults.Ok(ResponseEnvelope.Data(context, item));
    }

    private static async Task<IResult> ListAsync(
        [FromServices] LeaveRequestService service, Guid? employeeId, string? status, string? leaveType,
        int? limit, string? cursor, string? sort, string? direction, HttpContext context, CancellationToken ct)
    {
        if (string.Equals(sort, "createdAt", StringComparison.OrdinalIgnoreCase)) sort = "startDate";
        var result = await service.ListAsync(employeeId, status, leaveType, limit, cursor, sort, direction, ct);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.List(
                context, result.Value!.Items, service.EncodeCursor(result.Value.Next)));
    }

    private static async Task<IResult> GetAsync(Guid id, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        SetEtag(context, result.Value!.Etag);
        return TypedResults.Ok(ResponseEnvelope.Data(context, result.Value));
    }

    private static async Task<IResult> CalculateWorkingDaysAsync(
        CalculateWorkingDaysRequest request, [FromServices] LeaveRequestService service,
        HttpContext context, CancellationToken ct)
    {
        var result = await service.CalculateWorkingDaysAsync(request.LeaveType, request.StartDate, request.EndDate, ct);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(context, new WorkingDaysResponse(
                result.Value!.WorkingDays,
                result.Value.StartDate,
                result.Value.EndDate,
                result.Value.ExpectedReturnDate)));
    }

    private static async Task<IResult> CreateAsync(
        CreateLeaveRequestRequest request, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateAsync(new CreateLeaveRequestCommand(
            request.EmployeeId, request.LeaveType, request.StartDate, request.EndDate, request.Reason,
            request.HandoverNotes, request.DelegateEmployeeId, request.MedicalDocumentFileId,
            request.AdmissionLetterFileId, request.HandoverDocumentFileId), ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        SetEtag(context, result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/leave-requests/{result.Value.Id}",
            ResponseEnvelope.Data(context, result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateLeaveRequestRequest request, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, new UpdateLeaveRequestCommand(
            request.LeaveType, request.StartDate, request.EndDate, request.Reason, request.HandoverNotes,
            request.DelegateEmployeeId, request.MedicalDocumentFileId, request.AdmissionLetterFileId,
            request.HandoverDocumentFileId), ParseIfMatch(context), ct);
        return ResultResponse(result, context);
    }

    private static Task<IResult> SubmitAsync(Guid id, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct) =>
        CommandResponse(service.SubmitAsync(id, ParseIfMatch(context), ct), context);

    private static Task<IResult> ApproveAsync(
        Guid id, LeaveDecisionRequest request, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct) =>
        CommandResponse(service.ApproveAsync(id, new LeaveDecisionCommand(request.Comments, request.SignatureName), ParseIfMatch(context), ct), context);

    private static Task<IResult> RejectAsync(
        Guid id, RejectLeaveRequest request, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct) =>
        CommandResponse(service.RejectAsync(id, new LeaveDecisionCommand(request.Comments, request.SignatureName), request.Reason, ParseIfMatch(context), ct), context);

    private static Task<IResult> CancelAsync(Guid id, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct) =>
        CommandResponse(service.CancelAsync(id, ParseIfMatch(context), ct), context);

    private static Task<IResult> ResumeAsync(
        Guid id, ResumeLeaveRequest request, [FromServices] LeaveRequestService service, HttpContext context, CancellationToken ct) =>
        CommandResponse(service.ResumeAsync(id, new ResumeLeaveCommand(request.ResumptionDate, request.EmployeeSignatureName), ParseIfMatch(context), ct), context);

    private static async Task<IResult> GetMyBalancesAsync(
        HttpContext context, [FromServices] LeaveRequestService service, short? leaveYear, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("employee_id"), out var employeeId))
            return EndpointProblems.FromError(Error.NotFound("Leave balance not found."));
        return await BalanceResponse(
            service, employeeId, leaveYear ?? (short)DateTime.UtcNow.Year, context, ct);
    }

    private static Task<IResult> GetBalancesAsync(
        Guid employeeId, [FromServices] LeaveRequestService service, short? leaveYear,
        HttpContext context, CancellationToken ct) =>
        BalanceResponse(service, employeeId, leaveYear ?? (short)DateTime.UtcNow.Year, context, ct);

    private static Task<IResult> GetCompatibilityBalancesAsync(
        Guid employeeId, short leaveYear, [FromServices] LeaveRequestService service,
        HttpContext context, CancellationToken ct) =>
        BalanceResponse(service, employeeId, leaveYear, context, ct);

    private static async Task<IResult> AssignAnnualLeaveAsync(
        AssignAnnualLeaveRequest request,
        [FromServices] LeaveRequestService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.AssignAnnualEntitlementAsync(
            new AssignAnnualLeaveCommand(request.EmployeeId, request.TotalDays, request.LeaveYear), ct);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> BulkAssignAnnualLeaveAsync(
        BulkAssignAnnualLeaveRequest request,
        [FromServices] LeaveRequestService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.BulkAssignAnnualEntitlementAsync(
            new BulkAssignAnnualLeaveCommand(
                request.EmployeeIds ?? [],
                request.TotalDays,
                request.LeaveYear,
                request.StaffCategory),
            ct);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> BalanceResponse(
        LeaveRequestService service, Guid employeeId, short leaveYear, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListBalancesAsync(employeeId, leaveYear, ct);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> CommandResponse(Task<Result<LeaveRequestDto>> task, HttpContext context) =>
        ResultResponse(await task, context);

    private static IResult ResultResponse(Result<LeaveRequestDto> result, HttpContext context)
    {
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        SetEtag(context, result.Value!.Etag);
        return TypedResults.Ok(ResponseEnvelope.Data(context, result.Value));
    }

    private static byte[]? ParseIfMatch(HttpContext context) =>
        ConcurrencyToken.TryParse(context.Request.Headers.IfMatch.ToString(), out var token) ? token : null;

    private static void SetEtag(HttpContext context, string etag) => context.Response.Headers.ETag = etag;
}
