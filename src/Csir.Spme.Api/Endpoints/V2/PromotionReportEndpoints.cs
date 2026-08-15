using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Domain.Common;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionReportEndpoints
{
    public static void MapPromotionReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints
            .MapGroup("/api/v2/promotion-submissions/{promotionSubmissionId:guid}/reports")
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reports.MapGet("/{reportType}", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadPromotionReports)
            .WithName("PromotionReports_Get")
            .WithSummary("Get a promotion submission report draft.")
            .WithDescription("Returns one structured report provisioned from a locked promotion-submission requirement. Employees can retrieve only their own report, while authorized HR reviewers are restricted to their effective institute scope. The response includes the current ETag for concurrency-safe autosave.")
            .Produces<PromotionReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reports.MapPut("/{reportType}", ReplaceAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteOwnPromotionReports)
            .WithName("PromotionReports_Replace")
            .WithSummary("Replace a promotion submission report draft.")
            .WithDescription("Fully replaces the title and versioned structured content of the authenticated employee's report. The parent submission must be draft or returned, and the request must include the current report ETag in If-Match; HR reviewers cannot alter employee-authored report content.")
            .Produces<PromotionReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    private static async Task<IResult> GetAsync(
        Guid promotionSubmissionId,
        string reportType,
        PromotionReportService service,
        HttpContext context,
        CancellationToken ct)
    {
        var allowCsirWideReview = context.User.IsInRole(SpmeRoles.PlatformAdmin);
        var allowInstituteReview =
            context.User.HasClaim("permission", "promotions.read") ||
            allowCsirWideReview ||
            context.User.IsInRole(SpmeRoles.InstituteAdmin) ||
            context.User.IsInRole(SpmeRoles.HrAdmin);

        var result = await service.GetAsync(
            promotionSubmissionId,
            reportType,
            allowInstituteReview,
            allowCsirWideReview,
            ct);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = Map(result.Value!);
        context.Response.Headers.ETag = response.Etag;
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ReplaceAsync(
        Guid promotionSubmissionId,
        string reportType,
        ReplacePromotionReportRequest request,
        PromotionReportService service,
        HttpContext context,
        CancellationToken ct)
    {
        if (request.Content is null ||
            request.Content.Sections is null ||
            request.Content.Sections.Any(section => section is null))
        {
            return EndpointProblems.FromError(Error.Validation(
                "Structured report content and its sections collection are required."));
        }

        var command = new ReplacePromotionReportCommand(
            request.Title,
            new PromotionReportContentDto(
                request.Content.SchemaVersion,
                request.Content.Sections.Select(section =>
                    new PromotionReportSectionDto(
                        section.Code,
                        section.Heading,
                        section.Content.ValueKind == System.Text.Json.JsonValueKind.Undefined
                            ? default
                            : section.Content.Clone()))
                    .ToList()));

        var result = await service.ReplaceAsync(
            promotionSubmissionId,
            reportType,
            command,
            ExpectedRowVersion(context),
            ct);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = Map(result.Value!);
        context.Response.Headers.ETag = response.Etag;
        return TypedResults.Ok(response);
    }

    private static byte[]? ExpectedRowVersion(HttpContext context)
    {
        var ifMatch = context.Request.Headers[HeaderNames.IfMatch].ToString();
        return ConcurrencyToken.TryParse(ifMatch, out var rowVersion) ? rowVersion : null;
    }

    private static PromotionReportResponse Map(PromotionReportDto report) => new(
        report.Id,
        report.PromotionSubmissionId,
        report.RequirementSnapshotId,
        report.ReportType,
        report.Title,
        new PromotionReportContentResponse(
            report.Content.SchemaVersion,
            report.Content.Sections.Select(section =>
                new PromotionReportSectionResponse(
                    section.Code,
                    section.Heading,
                    section.Content))
                .ToList()),
        report.Status,
        report.RenderedFileId,
        report.LastSavedAt,
        report.FinalizedAt,
        report.Etag,
        report.UpdatedAt);
}
