using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Domain.Promotions;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionFormAssessmentEndpoints
{
    public static void MapPromotionFormAssessmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v2/promotion-submissions/{promotionSubmissionId:guid}")
            .WithGroupName("v2")
            .WithTags("Promotions")
            .RequireAuthorization();

        group.MapPut("/hod-assessment", (Guid promotionSubmissionId, ReplacePromotionReportRequest request,
                PromotionReportService service, HttpContext context, CancellationToken ct) =>
            ReplaceWorkflowAsync(promotionSubmissionId, "hod-assessment", request, service, context, ct,
                aggregate => IsHod(context) &&
                             aggregate.Submission.InstituteId == CurrentInstituteId(context) &&
                             aggregate.Submission.Status is PromotionConstants.SubmissionSubmitted
                                 or PromotionConstants.SubmissionUnderReview
                                 or PromotionConstants.SubmissionAcknowledged))
            .RequireAuthorization(AuthorizationPolicies.WritePromotions)
            .WithName("PromotionSubmissions_ReplaceHodAssessment")
            .WithSummary("Replace the HOD CSIR FORM 2 assessment.")
            .WithDescription("Fully replaces the structured HOD assessment for a submission in the caller's institute while it is submitted, under review, or acknowledged. The request must include the current report ETag in If-Match, and inaccessible submissions are represented as not found.")
            .Produces<PromotionReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        group.MapPut("/director-assessment", (Guid promotionSubmissionId, ReplacePromotionReportRequest request,
                PromotionReportService service, HttpContext context, CancellationToken ct) =>
            ReplaceWorkflowAsync(promotionSubmissionId, "director-assessment", request, service, context, ct,
                aggregate => IsDirector(context) &&
                             aggregate.Submission.InstituteId == CurrentInstituteId(context) &&
                             aggregate.Submission.Status is PromotionConstants.SubmissionSubmitted
                                 or PromotionConstants.SubmissionUnderReview
                                 or PromotionConstants.SubmissionAcknowledged))
            .RequireAuthorization(AuthorizationPolicies.WritePromotions)
            .WithName("PromotionSubmissions_ReplaceDirectorAssessment")
            .WithSummary("Replace the Director CSIR FORM 2 assessment.")
            .WithDescription("Fully replaces the structured Director assessment for a submission in the caller's institute while it is submitted, under review, or acknowledged. The request must include the current report ETag in If-Match, and inaccessible submissions are represented as not found.")
            .Produces<PromotionReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        group.MapPut("/applicant-hod-response", (Guid promotionSubmissionId, ReplacePromotionReportRequest request,
                PromotionReportService service, HttpContext context, CancellationToken ct) =>
            ReplaceWorkflowAsync(promotionSubmissionId, "applicant-hod-response", request, service, context, ct,
                aggregate => CurrentEmployeeId(context) == aggregate.Submission.EmployeeId &&
                             aggregate.Submission.Status is PromotionConstants.SubmissionSubmitted
                                 or PromotionConstants.SubmissionUnderReview
                                 or PromotionConstants.SubmissionAcknowledged))
            .RequireAuthorization(AuthorizationPolicies.WriteOwnPromotionReports)
            .WithName("PromotionSubmissions_ReplaceApplicantHodResponse")
            .WithSummary("Replace the applicant response to the HOD recommendation.")
            .WithDescription("Fully replaces the authenticated applicant's structured response to the HOD recommendation while the submission is submitted, under review, or acknowledged. The request must include the current report ETag in If-Match, and another employee's submission is represented as not found.")
            .Produces<PromotionReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    private static async Task<IResult> ReplaceWorkflowAsync(
        Guid promotionSubmissionId,
        string reportType,
        ReplacePromotionReportRequest request,
        PromotionReportService service,
        HttpContext context,
        CancellationToken ct,
        Func<PromotionReportAggregate, bool> authorize)
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

        var result = await service.ReplaceWorkflowAsync(
            promotionSubmissionId,
            reportType,
            command,
            ExpectedRowVersion(context),
            authorize,
            ct);
        if (result.IsFailure)
            return EndpointProblems.FromError(result.Error!);

        var response = PromotionReportEndpoints.MapPublic(result.Value!);
        context.Response.Headers.ETag = response.Etag;
        return TypedResults.Ok(response);
    }

    private static byte[]? ExpectedRowVersion(HttpContext context)
    {
        var ifMatch = context.Request.Headers[HeaderNames.IfMatch].ToString();
        return ConcurrencyToken.TryParse(ifMatch, out var rowVersion) ? rowVersion : null;
    }

    private static Guid? CurrentInstituteId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("institute_id"), out var instituteId) ? instituteId : null;

    private static Guid? CurrentEmployeeId(HttpContext context)
    {
        var self = context.User.FindFirstValue("self");
        if (self is not null && self.StartsWith("Self:", StringComparison.Ordinal) &&
            Guid.TryParse(self["Self:".Length..], out var employeeId))
            return employeeId;

        return Guid.TryParse(context.User.FindFirstValue("employee_id"), out employeeId) ? employeeId : null;
    }

    private static bool IsHod(HttpContext context) =>
        string.Equals(context.User.FindFirstValue("is_hod"), "true", StringComparison.OrdinalIgnoreCase) ||
        context.User.IsInRole(SpmeRoles.InstituteAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin) ||
        context.User.IsInRole(SpmeRoles.PlatformAdmin);

    private static bool IsDirector(HttpContext context) =>
        string.Equals(context.User.FindFirstValue("is_director"), "true", StringComparison.OrdinalIgnoreCase) ||
        context.User.IsInRole(SpmeRoles.InstituteAdmin) ||
        context.User.IsInRole(SpmeRoles.HrAdmin) ||
        context.User.IsInRole(SpmeRoles.PlatformAdmin);
}
