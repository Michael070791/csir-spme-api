using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Plan;
using Csir.Spme.Api.Auth;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PlanningEndpoints
{
    public static void MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v2")
            .WithGroupName("v2")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/strategic-plans", ListStrategicPlansAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadStrategicPlans)
            .WithName("StrategicPlans_List")
            .WithTags("Strategic Plans")
            .WithSummary("List strategic plans.")
            .WithDescription("Returns a cursor-paged list of strategic plans limited to the caller's effective institute scope.")
            .Produces<ListResponse<StrategicPlanResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        api.MapPost("/strategic-plans", CreateStrategicPlanAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteStrategicPlans)
            .WithName("StrategicPlans_Create")
            .WithTags("Strategic Plans")
            .WithSummary("Create a strategic plan.")
            .WithDescription("Creates an institute-scoped draft strategic plan with a unique code. An Idempotency-Key is required, and the plan and immutable audit event are committed transactionally.")
            .Produces<DataResponse<StrategicPlanResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/strategic-plans/{id:guid}", GetStrategicPlanAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadStrategicPlans)
            .WithName("StrategicPlans_Get")
            .WithTags("Strategic Plans")
            .WithSummary("Get a strategic plan.")
            .WithDescription("Returns one accessible strategic plan with the current opaque ETag used for concurrency-safe mutations.")
            .Produces<DataResponse<StrategicPlanResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/strategic-plans/{id:guid}", UpdateStrategicPlanAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteStrategicPlans)
            .WithName("StrategicPlans_Update")
            .WithTags("Strategic Plans")
            .WithSummary("Update a draft strategic plan.")
            .WithDescription("Updates an accessible draft strategic plan and requires its current opaque ETag in the If-Match header.")
            .Produces<DataResponse<StrategicPlanResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapPost("/strategic-plans/{id:guid}/activate", ActivateStrategicPlanAsync)
            .RequireAuthorization(AuthorizationPolicies.ActivateStrategicPlans)
            .WithName("StrategicPlans_Activate")
            .WithTags("Strategic Plans")
            .WithSummary("Activate a strategic plan.")
            .WithDescription("Activates an accessible draft when the institute has no other active plan for an overlapping planning range. Idempotency-Key and If-Match headers are required.")
            .Produces<DataResponse<StrategicPlanResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapGet("/thrusts", ListThrustsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadThrusts)
            .WithName("Thrusts_List")
            .WithTags("Thrusts")
            .WithSummary("List thrusts.")
            .WithDescription("Returns a cursor-paged list of thrusts, optionally filtered by strategic plan and status, within the caller's effective institute scope. Read-thrust permission is required, and invalid paging or filter input returns a validation problem.")
            .Produces<ListResponse<ThrustResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapPost("/strategic-plans/{planId:guid}/thrusts", CreateThrustAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteThrusts)
            .WithName("Thrusts_Create")
            .WithTags("Thrusts")
            .WithSummary("Create a thrust.")
            .WithDescription("Creates a thrust under an accessible strategic plan in the caller's effective institute scope. Write-thrust permission is required; invalid input, a missing or hidden plan, and duplicate codes return validation, not-found, or conflict problems.")
            .Produces<DataResponse<ThrustResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/strategic-plans/{planId:guid}/thrusts", ListPlanThrustsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadThrusts)
            .WithName("Thrusts_ListByStrategicPlan")
            .WithTags("Thrusts")
            .WithSummary("List thrusts for a strategic plan.")
            .WithDescription("Returns a cursor-paged list of thrusts for an accessible strategic plan, optionally filtered by status, within the caller's effective institute scope. Read-thrust permission is required; invalid input or a missing or hidden plan returns a problem response.")
            .Produces<ListResponse<ThrustResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/thrusts/{id:guid}", GetThrustAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadThrusts)
            .WithName("Thrusts_Get")
            .WithTags("Thrusts")
            .WithSummary("Get a thrust.")
            .WithDescription("Returns one thrust and its current opaque ETag when the resource is visible in the caller's effective institute scope. Read-thrust permission is required, and missing or out-of-scope identifiers return not found.")
            .Produces<DataResponse<ThrustResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/thrusts/{id:guid}", UpdateThrustAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteThrusts)
            .WithName("Thrusts_Update")
            .WithTags("Thrusts")
            .WithSummary("Update a thrust.")
            .WithDescription("Updates an accessible thrust's editable fields and status using the current opaque ETag from If-Match. Write-thrust permission and institute scope apply; invalid input, hidden resources, or stale concurrency tokens return validation, not-found, or precondition problems.")
            .Produces<DataResponse<ThrustResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapGet("/thrusts/{thrustId:guid}/outputs", ListOutputsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOutputs)
            .WithName("Outputs_List")
            .WithTags("Outputs")
            .WithSummary("List outputs.")
            .WithDescription("Returns a cursor-paged list of outputs for an accessible thrust, optionally filtered by status, within the caller's effective institute scope. Read-output permission is required; invalid input or a missing or hidden thrust returns a problem response.")
            .Produces<ListResponse<OutputResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/thrusts/{thrustId:guid}/outputs", CreateOutputAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteOutputs)
            .WithName("Outputs_Create")
            .WithTags("Outputs")
            .WithSummary("Create an output.")
            .WithDescription("Creates an output under an accessible thrust and returns its opaque ETag. Write-output permission and the caller's effective institute scope are enforced; invalid input, a missing parent, or a duplicate code returns validation, not-found, or conflict.")
            .Produces<DataResponse<OutputResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/outputs", ListRootOutputsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOutputs)
            .WithName("Outputs_ListAtRoot")
            .WithTags("Outputs")
            .WithSummary("List outputs in the caller's effective institute scope.")
            .WithDescription("Returns a cursor-paged output list across the caller's effective institute scope, with optional thrust and status filters. Read-output permission is required; invalid paging or filter input and an inaccessible requested parent return documented problem responses.")
            .Produces<ListResponse<OutputResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/outputs", CreateRootOutputAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteOutputs)
            .WithName("Outputs_CreateAtRoot")
            .WithTags("Outputs")
            .WithSummary("Create an output for a specified thrust.")
            .WithDescription("Creates an output for the thrust identified in the request and returns its opaque ETag. Write-output permission and effective institute scope are enforced; invalid input, a missing or hidden thrust, or a duplicate code returns validation, not-found, or conflict.")
            .Produces<DataResponse<OutputResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/outputs/{id:guid}", GetOutputAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOutputs)
            .WithName("Outputs_Get")
            .WithTags("Outputs")
            .WithSummary("Get an output.")
            .WithDescription("Returns one output and its current opaque ETag when visible in the caller's effective institute scope. Read-output permission is required, and a missing or out-of-scope identifier returns the same not-found problem.")
            .Produces<DataResponse<OutputResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/outputs/{id:guid}", UpdateOutputAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteOutputs)
            .WithName("Outputs_Update")
            .WithTags("Outputs")
            .WithSummary("Update an output.")
            .WithDescription("Updates editable output fields and status using the current opaque ETag supplied through If-Match. Write-output permission and institute scope apply; invalid input, a hidden resource, or a stale token returns validation, not-found, or precondition failure.")
            .Produces<DataResponse<OutputResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapGet("/outputs/{outputId:guid}/indicators", ListIndicatorsByOutputAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadIndicators)
            .WithName("Indicators_ListByOutput")
            .WithTags("Indicators")
            .WithSummary("List indicators for an output.")
            .WithDescription("Returns a cursor-paged list of indicators for an accessible output, optionally filtered by status, within the caller's effective institute scope. Read-indicator permission is required; invalid input or a missing or hidden output returns a problem response.")
            .Produces<ListResponse<IndicatorResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/thrusts/{thrustId:guid}/indicators", ListIndicatorsByThrustAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadIndicators)
            .WithName("Indicators_ListByThrust")
            .WithTags("Indicators")
            .WithSummary("List indicators for a thrust.")
            .WithDescription("Returns a cursor-paged list of indicators across outputs belonging to an accessible thrust, optionally filtered by status. Read-indicator permission and effective institute scope are enforced; invalid input or a hidden thrust returns a problem response.")
            .Produces<ListResponse<IndicatorResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/outputs/{outputId:guid}/indicators", CreateIndicatorAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteIndicators)
            .WithName("Indicators_Create")
            .WithTags("Indicators")
            .WithSummary("Create an indicator.")
            .WithDescription("Creates an indicator under an accessible output and returns its current opaque ETag. Write-indicator permission and institute scope are required; invalid input, a missing or hidden output, or a duplicate code returns validation, not-found, or conflict.")
            .Produces<DataResponse<IndicatorResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/indicators/{id:guid}", GetIndicatorAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadIndicators)
            .WithName("Indicators_Get")
            .WithTags("Indicators")
            .WithSummary("Get an indicator.")
            .WithDescription("Returns one indicator and its current opaque ETag when visible in the caller's effective institute scope. Read-indicator permission is required, and missing or out-of-scope identifiers return the same not-found problem.")
            .Produces<DataResponse<IndicatorResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/indicators/{id:guid}", UpdateIndicatorAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteIndicators)
            .WithName("Indicators_Update")
            .WithTags("Indicators")
            .WithSummary("Update an indicator.")
            .WithDescription("Updates editable indicator fields and status using the current opaque ETag supplied through If-Match. Write-indicator permission and institute scope apply; invalid input, a hidden resource, or a stale token returns validation, not-found, or precondition failure.")
            .Produces<DataResponse<IndicatorResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapGet("/indicators/{indicatorId:guid}/measurements", ListIndicatorDataAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadIndicators)
            .WithName("IndicatorMeasurements_List")
            .WithTags("Indicator Measurements")
            .WithSummary("List indicator measurements.")
            .WithDescription("Returns a cursor-paged list of measurements for an accessible indicator within the caller's effective institute scope. Read-indicator permission is required; invalid paging input or a missing or hidden indicator returns a problem response.")
            .Produces<ListResponse<IndicatorDataResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/indicators/{indicatorId:guid}/measurements", CreateIndicatorDataAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteIndicators)
            .WithName("IndicatorMeasurements_Create")
            .WithTags("Indicator Measurements")
            .WithSummary("Create an indicator measurement.")
            .WithDescription("Creates a reporting-period measurement for an accessible indicator, calculates its persisted result, and returns an opaque ETag. Write-indicator permission and institute scope apply; invalid references, hidden resources, or duplicate measurements return validation, not-found, or conflict.")
            .Produces<DataResponse<IndicatorDataResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/indicator-measurements/{id:guid}", GetIndicatorDataAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadIndicators)
            .WithName("IndicatorMeasurements_Get")
            .WithTags("Indicator Measurements")
            .WithSummary("Get an indicator measurement.")
            .WithDescription("Returns one indicator measurement and its current opaque ETag when visible in the caller's effective institute scope. Read-indicator permission is required, and a missing or out-of-scope identifier returns not found.")
            .Produces<DataResponse<IndicatorDataResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPatch("/indicator-measurements/{id:guid}", UpdateIndicatorDataAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteIndicators)
            .WithName("IndicatorMeasurements_Update")
            .WithTags("Indicator Measurements")
            .WithSummary("Update an indicator measurement.")
            .WithDescription("Updates an accessible measurement's value, remarks, and evidence reference using the current opaque ETag from If-Match. Write-indicator permission and institute scope apply; invalid input, conflicts, hidden records, or stale tokens return the documented problems.")
            .Produces<DataResponse<IndicatorDataResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        api.MapDelete("/indicator-measurements/{id:guid}", DeleteIndicatorDataAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteIndicators)
            .WithName("IndicatorMeasurements_Delete")
            .WithTags("Indicator Measurements")
            .WithSummary("Delete an indicator measurement.")
            .WithDescription("Deletes an accessible indicator measurement when domain rules permit removal. Write-indicator permission and the caller's effective institute scope are enforced; missing or hidden records return not found, while protected records return conflict.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListStrategicPlansAsync(
        StrategicPlanService service,
        ICursorCodec cursorCodec,
        Guid? instituteId,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            instituteId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.List(
                context,
                result.Value!.Items.Select(Map).ToList(),
                result.Value.Next is null ? null : cursorCodec.Encode(result.Value.Next)));
    }

    private static async Task<IResult> CreateStrategicPlanAsync(
        CreateStrategicPlanRequest request,
        StrategicPlanService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateStrategicPlanCommand(
            request.InstituteId, request.Code, request.Name, request.Definition, request.Objective,
            request.StartYear, request.EndYear), cancellationToken);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/strategic-plans/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetStrategicPlanAsync(
        Guid id, StrategicPlanService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateStrategicPlanAsync(
        Guid id,
        UpdateStrategicPlanRequest request,
        StrategicPlanService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, new UpdateStrategicPlanCommand(
            request.Name, request.Definition, request.Objective, request.StartYear, request.EndYear),
            ExpectedRowVersion(context), cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ActivateStrategicPlanAsync(
        Guid id, StrategicPlanService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(id, ExpectedRowVersion(context), cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ListThrustsAsync(
        ThrustService service,
        ICursorCodec cursorCodec,
        Guid? strategicPlanId,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(strategicPlanId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> CreateThrustAsync(
        Guid planId,
        CreateThrustRequest request,
        ThrustService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(planId,
            new CreateThrustCommand(request.Code, request.Title, request.Description, request.Objective, request.DisplayOrder),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/thrusts/{response.Id}", ResponseEnvelope.Data(context, response));
    }

    private static Task<IResult> ListPlanThrustsAsync(
        Guid planId,
        ThrustService service,
        ICursorCodec cursorCodec,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken) =>
        ListThrustsAsync(service, cursorCodec, planId, status, limit, cursor, sort, direction, context, cancellationToken);

    private static async Task<IResult> GetThrustAsync(
        Guid id,
        ThrustService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateThrustAsync(
        Guid id,
        UpdateThrustRequest request,
        ThrustService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id,
            new UpdateThrustCommand(request.Title, request.Description, request.Objective, request.DisplayOrder, request.Status),
            ExpectedRowVersion(context),
            cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ListOutputsAsync(
        Guid thrustId,
        OutputService service,
        ICursorCodec cursorCodec,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(thrustId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> CreateOutputAsync(
        Guid thrustId,
        CreateOutputRequest request,
        OutputService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateOutputCommand(thrustId, request.Code, request.Description, request.OwnerUserId, request.DueDate, request.DisplayOrder),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/outputs/{response.Id}", ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> ListRootOutputsAsync(
        OutputService service,
        ICursorCodec cursorCodec,
        Guid? thrustId,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(thrustId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> CreateRootOutputAsync(
        CreateRootOutputRequest request,
        OutputService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateOutputCommand(request.ThrustId, request.Code, request.Description,
                request.OwnerUserId, request.DueDate, request.DisplayOrder),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/outputs/{response.Id}", ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetOutputAsync(
        Guid id,
        OutputService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateOutputAsync(
        Guid id,
        UpdateOutputRequest request,
        OutputService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id,
            new UpdateOutputCommand(request.Description, request.OwnerUserId, request.DueDate, request.DisplayOrder, request.Status),
            ExpectedRowVersion(context),
            cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ListIndicatorsByOutputAsync(
        Guid outputId,
        IndicatorService service,
        ICursorCodec cursorCodec,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListByOutputAsync(outputId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> ListIndicatorsByThrustAsync(
        Guid thrustId,
        IndicatorService service,
        ICursorCodec cursorCodec,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListByThrustAsync(thrustId, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> CreateIndicatorAsync(
        Guid outputId,
        CreateIndicatorRequest request,
        IndicatorService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(outputId,
            new CreateIndicatorCommand(request.Code, request.Description, request.UnitOfMeasure,
                request.BaselineValue, request.TargetValue, request.VerificationMethod, request.DueDate),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/indicators/{response.Id}", ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetIndicatorAsync(
        Guid id,
        IndicatorService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateIndicatorAsync(
        Guid id,
        UpdateIndicatorRequest request,
        IndicatorService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id,
            new UpdateIndicatorCommand(request.Description, request.UnitOfMeasure, request.BaselineValue,
                request.TargetValue, request.VerificationMethod, request.DueDate, request.Status),
            ExpectedRowVersion(context),
            cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ListIndicatorDataAsync(
        Guid indicatorId,
        IndicatorMeasurementService service,
        ICursorCodec cursorCodec,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListByIndicatorAsync(indicatorId, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context, Map));
    }

    private static async Task<IResult> CreateIndicatorDataAsync(
        Guid indicatorId,
        CreateIndicatorDataRequest request,
        IndicatorMeasurementService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(indicatorId,
            new CreateIndicatorMeasurementCommand(request.ReportingPeriodId, request.Value, request.Remarks, request.EvidenceFileId),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/indicator-measurements/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetIndicatorDataAsync(
        Guid id,
        IndicatorMeasurementService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateIndicatorDataAsync(
        Guid id,
        UpdateIndicatorDataRequest request,
        IndicatorMeasurementService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id,
            new UpdateIndicatorMeasurementCommand(request.Value, request.Remarks, request.EvidenceFileId),
            ExpectedRowVersion(context),
            cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> DeleteIndicatorDataAsync(
        Guid id,
        IndicatorMeasurementService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.NoContent();
    }

    private static ListResponse<TResponse> ToPage<TDto, TResponse>(
        ListSlice<TDto> slice,
        ICursorCodec cursorCodec,
        HttpContext context,
        Func<TDto, TResponse> map)
    {
        var nextCursor = slice.Next is null ? null : cursorCodec.Encode(slice.Next);
        return ResponseEnvelope.List(context, slice.Items.Select(map).ToList(), nextCursor);
    }

    private static byte[]? ExpectedRowVersion(HttpContext context)
    {
        var ifMatch = context.Request.Headers[HeaderNames.IfMatch].ToString();
        return ConcurrencyToken.TryParse(ifMatch, out var rowVersion) ? rowVersion : null;
    }

    private static TResponse WithEtag<TResponse>(HttpContext context, TResponse response, string etag)
    {
        context.Response.Headers.ETag = etag;
        return response;
    }

    private static ThrustResponse Map(ThrustDto dto) => new(
        dto.Id, dto.StrategicPlanId, dto.InstituteId, dto.Code, dto.Title,
        dto.Description, dto.Objective, dto.DisplayOrder, dto.Status, dto.Etag);

    private static StrategicPlanResponse Map(StrategicPlanDto dto) => new(
        dto.Id, dto.InstituteId, dto.Code, dto.Name, dto.Definition, dto.Objective,
        dto.StartYear, dto.EndYear, dto.Status, dto.Etag, dto.CreatedAt, dto.UpdatedAt);

    private static OutputResponse Map(OutputDto dto) => new(
        dto.Id, dto.ThrustId, dto.Code, dto.Description, dto.OwnerUserId,
        dto.DueDate, dto.Status, dto.DisplayOrder, dto.Etag);

    private static IndicatorResponse Map(IndicatorDto dto) => new(
        dto.Id, dto.OutputId, dto.Code, dto.Description, dto.UnitOfMeasure,
        dto.BaselineValue, dto.TargetValue, dto.VerificationMethod, dto.DueDate,
        dto.Status, dto.Etag);

    private static IndicatorDataResponse Map(IndicatorMeasurementDto dto) => new(
        dto.Id, dto.IndicatorId, dto.ReportingPeriodId, dto.Value, dto.Variance,
        dto.Remarks, dto.EvidenceFileId, dto.RecordedByUserId, dto.Etag);
}
