using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Application.Common.Pagination;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/api/v2/reports")
            .WithGroupName("v2")
            .WithTags("Reports")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        reports.MapGet("", GetReportsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadReports)
            .WithName("Reports_List")
            .WithSummary("List reports in the caller's authorized institute scope.")
            .WithDescription("Returns a paged register of reports used to monitor strategic-plan delivery across strategic, research and development, performance, project, and HR reporting categories. Institute-scoped callers are restricted to their own institute, while platform administrators may filter across institutes.")
            .Produces<ListResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reports.MapPost("", CreateReportAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteReports)
            .WithName("Reports_Create")
            .WithSummary("Create a report.")
            .WithDescription("Creates a report in the draft workflow state for an authorized institute and reporting period. The reporting period determines ownership unless it is CSIR-wide, in which case the caller must provide or inherit an institute scope.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reports.MapGet("/{id:guid}", GetReportAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadReports)
            .WithName("Reports_Get")
            .WithSummary("Get a report.")
            .WithDescription("Returns the complete report content, reporting category, workflow status, review history, audit timestamps, and the current ETag required for concurrency-safe content updates.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reports.MapPatch("/{id:guid}", UpdateReportAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteReports)
            .WithName("Reports_Update")
            .WithSummary("Update an editable report.")
            .WithDescription("Updates the content of a draft or returned report using optimistic concurrency. Clients must send the latest ETag in If-Match; submitted and approved reports remain immutable through this operation.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        reports.MapDelete("/{id:guid}", DeleteReportAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteReports)
            .WithName("Reports_Delete")
            .WithSummary("Delete an editable report.")
            .WithDescription("Deletes an accessible report only while it remains draft or returned and has no structured metrics. Submitted and approved reports are retained to preserve workflow decisions and audit history.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reports.MapPost("/{id:guid}/submit", SubmitReportAsync)
            .RequireAuthorization(AuthorizationPolicies.SubmitReports)
            .WithName("Reports_Submit")
            .WithSummary("Submit a report for review.")
            .WithDescription("Moves a draft or returned report into the submitted state, records the submitting user and timestamp, clears the previous correction reason, and writes an audit event.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reports.MapPost("/{id:guid}/approve", ApproveReportAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveReports)
            .WithName("Reports_Approve")
            .WithSummary("Approve a submitted report.")
            .WithDescription("Approves an accessible submitted report, records the approving user and timestamp, and makes the approved content immutable through normal report editing operations.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reports.MapPost("/{id:guid}/return", ReturnReportAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveReports)
            .WithName("Reports_Return")
            .WithSummary("Return a report for correction.")
            .WithDescription("Returns a submitted report to its originating institute with an author-visible correction reason. The returned report becomes editable and can be resubmitted through the same review workflow.")
            .Produces<DataResponse<ReportResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetReportsAsync(
        ReportService service,
        ICursorCodec cursorCodec,
        string? instituteId,
        string? reportType,
        string? status,
        Guid? reportingPeriodId,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(
            instituteId, reportType, status, reportingPeriodId,
            limit, cursor, sort, direction, cancellationToken);
        if (result.IsFailure)
            return EndpointProblems.FromError(result.Error!);
        return TypedResults.Ok(ResponseEnvelope.List(
            context,
            result.Value!.Items.Select(Map).ToList(),
            result.Value.Next is null ? null : cursorCodec.Encode(result.Value.Next)));
    }

    private static async Task<IResult> CreateReportAsync(
        CreateReportRequest request,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateReportCommand(
            request.InstituteId, request.ReportingPeriodId, request.ReportType, request.Title,
            request.Summary, request.Abstract, request.KeyResults, request.Conclusion),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/reports/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetReportAsync(
        Guid id,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateReportAsync(
        Guid id,
        UpdateReportRequest request,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, new UpdateReportCommand(
            request.Title, request.Summary, request.Abstract, request.KeyResults, request.Conclusion),
            ExpectedRowVersion(context),
            cancellationToken);

        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> DeleteReportAsync(
        Guid id,
        ReportService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.NoContent();
    }

    private static async Task<IResult> SubmitReportAsync(
        Guid id,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ApproveReportAsync(
        Guid id,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ReturnReportAsync(
        Guid id,
        ReturnReportRequest request,
        ReportService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ReturnAsync(
            id, new ReturnReportCommand(request.ReturnReason), cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
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

    private static ReportResponse Map(ReportDto dto) => new(
        dto.Id, dto.InstituteId, dto.ReportingPeriodId, dto.ReportType, dto.Title,
        dto.Summary, dto.Abstract, dto.KeyResults, dto.Conclusion, dto.Status,
        dto.SubmittedAt, dto.ApprovedAt, dto.ReturnReason, dto.Etag, dto.CreatedAt, dto.UpdatedAt);

    private static ReportResponse Map(Csir.Spme.Domain.Reporting.Report report) => new(
        report.Id, report.InstituteId, report.ReportingPeriodId, report.ReportType, report.Title,
        report.Summary, report.Abstract, report.KeyResults, report.Conclusion, report.Status,
        report.SubmittedAt, report.ApprovedAt, report.ReturnReason,
        ConcurrencyToken.Format(report.RowVersion), report.CreatedAt, report.UpdatedAt);
}
