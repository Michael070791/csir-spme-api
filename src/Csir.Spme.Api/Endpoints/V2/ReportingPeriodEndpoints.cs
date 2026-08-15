using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Reporting;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

/// <summary>Reporting-period catalogue routes used by reports and indicator measurements.</summary>
internal static class ReportingPeriodEndpoints
{
    public static void MapReportingPeriodEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var periods = endpoints.MapGroup("/api/v2/reporting-periods")
            .WithGroupName("v2")
            .WithTags("Reporting Periods")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        periods.MapGet("", ListAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadReports)
            .WithName("ReportingPeriods_List")
            .WithSummary("List reporting periods in the caller's authorized institute scope.")
            .WithDescription("Returns a cursor-paged reporting-period catalogue. Institute-scoped callers receive their own periods and CSIR-wide periods; CSIR-wide callers may filter by institute.")
            .Produces<ListResponse<ReportingPeriodResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        periods.MapPost("", CreateAsync)
            .DisableValidation()
            .RequireAuthorization(AuthorizationPolicies.WriteReports)
            .WithName("ReportingPeriods_Create")
            .WithSummary("Create a reporting period.")
            .WithDescription("Creates a draft reporting period after validating controlled values, dates, institute scope, code uniqueness, and ownership. An Idempotency-Key header is required so a retry returns the original creation result. The creation is audited.")
            .Produces<DataResponse<ReportingPeriodResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        periods.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadReports)
            .WithName("ReportingPeriods_Get")
            .WithSummary("Get a reporting period.")
            .WithDescription("Returns an accessible reporting period, including its measurement lifecycle status and current ETag.")
            .Produces<DataResponse<ReportingPeriodResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        MapTransition(periods, "/{id:guid}/open", "ReportingPeriods_Open",
            "Open a reporting period.", OpenAsync);
        MapTransition(periods, "/{id:guid}/close", "ReportingPeriods_Close",
            "Close a reporting period.", CloseAsync);
        MapTransition(periods, "/{id:guid}/finalize", "ReportingPeriods_Finalize",
            "Finalize a reporting period.", FinalizeAsync);
    }

    private static async Task<IResult> ListAsync(
        ReportingPeriodService service,
        ICursorCodec cursorCodec,
        Guid? instituteId,
        string? periodType,
        string? status,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            instituteId, periodType, status, limit, cursor, sort, direction, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ToPage(result.Value!, cursorCodec, context));
    }

    private static async Task<IResult> CreateAsync(
        CreateReportingPeriodRequest request,
        ReportingPeriodService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateReportingPeriodCommand(
                request.ScopeType,
                request.InstituteId,
                request.Code,
                request.Name,
                request.PeriodType,
                request.StartDate,
                request.EndDate,
                request.DueDate),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!));
        return TypedResults.Created(
            $"/api/v2/reporting-periods/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ReportingPeriodService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(context, WithEtag(context, Map(result.Value!))));
    }

    private static void MapTransition(
        RouteGroupBuilder periods,
        string pattern,
        string name,
        string summary,
        Func<Guid, ReportingPeriodService, HttpContext, CancellationToken, Task<IResult>> handler) =>
        periods.MapPost(pattern, handler)
            .RequireAuthorization(AuthorizationPolicies.WriteReports)
            .WithName(name)
            .WithSummary(summary)
            .WithDescription("Performs an explicit lifecycle transition after validating state and the current If-Match ETag, then records the mutation in the audit log.")
            .Produces<DataResponse<ReportingPeriodResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

    private static Task<IResult> OpenAsync(
        Guid id, ReportingPeriodService service, HttpContext context, CancellationToken ct) =>
        TransitionResponse(service.OpenAsync(id, ExpectedRowVersion(context), ct), context);

    private static Task<IResult> CloseAsync(
        Guid id, ReportingPeriodService service, HttpContext context, CancellationToken ct) =>
        TransitionResponse(service.CloseAsync(id, ExpectedRowVersion(context), ct), context);

    private static Task<IResult> FinalizeAsync(
        Guid id, ReportingPeriodService service, HttpContext context, CancellationToken ct) =>
        TransitionResponse(service.FinalizeAsync(id, ExpectedRowVersion(context), ct), context);

    private static async Task<IResult> TransitionResponse(
        Task<Result<ReportingPeriodDto>> operation, HttpContext context)
    {
        var result = await operation;
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(context, WithEtag(context, Map(result.Value!))));
    }

    private static byte[]? ExpectedRowVersion(HttpContext context)
    {
        var ifMatch = context.Request.Headers[HeaderNames.IfMatch].ToString();
        return ConcurrencyToken.TryParse(ifMatch, out var rowVersion) ? rowVersion : null;
    }

    private static ListResponse<ReportingPeriodResponse> ToPage(
        ListSlice<ReportingPeriodDto> slice,
        ICursorCodec cursorCodec,
        HttpContext context) =>
        ResponseEnvelope.List(
            context,
            slice.Items.Select(Map).ToList(),
            slice.Next is null ? null : cursorCodec.Encode(slice.Next));

    private static ReportingPeriodResponse Map(ReportingPeriodDto dto) => new(
        dto.Id,
        dto.ScopeType,
        dto.InstituteId,
        dto.Code,
        dto.Name,
        dto.PeriodType,
        dto.StartDate,
        dto.EndDate,
        dto.DueDate,
        dto.Status,
        dto.Etag,
        dto.CreatedAt,
        dto.UpdatedAt);

    private static ReportingPeriodResponse WithEtag(HttpContext context, ReportingPeriodResponse response)
    {
        context.Response.Headers.ETag = response.Etag;
        return response;
    }
}
