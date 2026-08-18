using System.ComponentModel.DataAnnotations;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Reporting;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class StaffQuarterlyReportEndpoints
{
    public static void MapStaffQuarterlyReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/api/v2/staff-quarterly-reports")
            .WithGroupName("v2").WithTags("Staff quarterly reports").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        reports.MapGet("/me", ListMineAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_ListMine").WithSummary("List the authenticated employee's quarterly reports.")
            .WithDescription("Returns only quarterly reports owned by the authenticated employee. A linked employee identity and permission to manage personal reports are required; missing identity links are returned as a non-disclosing not-found error.")
            .Produces<ListResponse<StaffQuarterlyReportResponse>>();
        reports.MapGet("/review-queue", ListReviewQueueAsync).RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_ListReviewQueue").WithSummary("List quarterly reports assigned to the authenticated reviewer.")
            .WithDescription("Returns reports assigned to the authenticated reviewer within the reviewer's institute. Reading staff quarterly reports and a valid reviewer identity link are required, and unavailable identity links produce a non-disclosing not-found error.")
            .Produces<ListResponse<StaffQuarterlyReportResponse>>();
        reports.MapGet("/options", GetOptionsAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_GetOptions").WithSummary("Get safe quarterly report authoring options.")
            .WithDescription("Returns institute-scoped open reporting periods, projects, technologies, and eligible reviewers for the authenticated employee. The operation may initialize open quarters for the current year and requires a linked employee identity.")
            .Produces<DataResponse<StaffQuarterlyReportOptions>>();
        reports.MapGet("/reviewer-search", SearchReviewersAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_SearchReviewers")
            .WithSummary("Search institute staff who can receive a quarterly report.")
            .WithDescription("Searches eligible reviewer candidates in the authenticated employee's institute, excluding the employee. The optional query narrows safe staff results, and an unavailable employee identity is returned without exposing another institute.")
            .Produces<ListResponse<StaffQuarterlyReviewerOption>>();
        reports.MapGet("/my-form-one-projects", ListMyFormOneProjectsAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_ListMyFormOneProjects").WithSummary("List the authenticated employee's Form 1 projects.")
            .WithDescription("Returns Form 1 projects owned by the authenticated employee, including PIN status and completion state.")
            .Produces<ListResponse<StaffQuarterlyFormOneSummary>>();
        reports.MapGet("/form-one-projects", ListInstituteFormOneProjectsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReviewStaffReports)
            .WithName("StaffQuarterlyReports_ListInstituteFormOneProjects")
            .WithSummary("List institute Form 1 projects for Scientific Secretary PIN assignment.")
            .WithDescription("Returns institute-scoped Form 1 projects for Scientific Secretary PIN assignment within the caller's institute.")
            .Produces<ListResponse<StaffQuarterlyFormOneSummary>>();
        reports.MapGet("/collation", ListCollationAsync)
            .RequireAuthorization(AuthorizationPolicies.ReviewStaffReports)
            .WithName("StaffQuarterlyReports_ListCollation")
            .WithSummary("List submitted quarterly reports for institute collation.")
            .WithDescription("Returns submitted and approved Form 2 quarterly reports for the caller's institute and reporting period. Scientific Secretary access is required; drafts and other institutes are excluded.")
            .Produces<ListResponse<StaffQuarterlyCollationEntry>>();
        reports.MapPut("/projects/{projectId:guid}/pin", AssignProjectPinAsync)
            .RequireAuthorization(AuthorizationPolicies.ReviewStaffReports)
            .WithName("StaffQuarterlyReports_AssignProjectPin")
            .WithSummary("Assign or correct a project PIN.")
            .WithDescription("Assigns or corrects the CSIR PIN on an institute Form 1 project. Scientific Secretary access and the latest If-Match ETag are required.")
            .Produces<DataResponse<StaffQuarterlyProjectInceptionResponse>>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        reports.MapGet("/projects/{projectId:guid}", GetProjectInceptionAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_GetProjectInception")
            .WithSummary("Get Form 1 inception details for an institute project.")
            .WithDescription("Returns Form 1 inception details only when the project belongs to the authenticated employee's institute and the caller may read that project. Missing or out-of-scope projects use the same non-disclosing not-found response.")
            .Produces<DataResponse<StaffQuarterlyProjectInceptionResponse>>();
        reports.MapGet("/files/{fileId:guid}", DownloadFileAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_DownloadFile")
            .WithSummary("Download an authorized quarterly report attachment or concept note.")
            .WithDescription("Streams a confidential report image or project concept note only to an authorized owner, assigned reviewer, or project reader in the same institute. Missing and inaccessible files are not disclosed, while infected or quarantined files are forbidden.");
        reports.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_Get").WithSummary("Get an owned or explicitly assigned quarterly report.")
            .WithDescription("Returns a quarterly report only to its authenticated employee owner or explicitly assigned reviewer within scope. The response includes the current ETag for later conditional changes, and inaccessible identifiers return not found.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>();
        reports.MapPost("", CreateAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_Create").WithSummary("Create an employee-owned quarterly report draft.")
            .WithDescription("Creates a draft owned by the authenticated employee after validating the institute-scoped reporting period, reviewer, projects, technologies, and Form 2 progress. The response includes an ETag and validation failures use problem details.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>(StatusCodes.Status201Created);
        reports.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_Update").WithSummary("Update an owned draft or returned quarterly report.")
            .WithDescription("Updates only an authenticated employee's own editable draft or returned report. Supply the latest If-Match ETag; missing or stale values return precondition failed, while invalid institute-scoped selections return validation errors.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>().ProducesProblem(StatusCodes.Status412PreconditionFailed);
        reports.MapPost("/{id:guid}/submit", SubmitAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_Submit").WithSummary("Submit an owned quarterly report to the selected reviewer.")
            .WithDescription("Submits an owned editable report using the latest If-Match ETag. The reporting period and reviewer must remain eligible, each linked project needs completed Form 1 and Form 2 content, and attachments must have acceptable scan states; lifecycle conflicts return conflict.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>().ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        reports.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_Approve").WithSummary("Approve an assigned submitted quarterly report.")
            .WithDescription("Approves a submitted report only for its explicitly assigned reviewer in the same institute. The current If-Match ETag is required; inaccessible assignments return not found, invalid lifecycle changes return conflict, and stale versions return precondition failed.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>().ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        reports.MapPost("/{id:guid}/return", ReturnAsync).RequireAuthorization(AuthorizationPolicies.ReadStaffQuarterlyReports)
            .WithName("StaffQuarterlyReports_Return").WithSummary("Return an assigned quarterly report for correction.")
            .WithDescription("Returns a submitted report to its employee owner only when the authenticated caller is the assigned reviewer in the same institute. A return reason and current If-Match ETag are required; lifecycle and concurrency failures use problem details.")
            .Produces<DataResponse<StaffQuarterlyReportResponse>>().ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        reports.MapPost("/project-drafts", CreateProjectDraftAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_CreateProjectDraft")
            .WithSummary("Create a Form 1 project for the authenticated employee.")
            .WithDescription("Creates an institute-scoped Form 1 project owned by the authenticated employee, or returns an existing project matched by name. Staff never send PIN or the internal catalog code; the server generates the code and Scientific Secretary assigns PIN later.")
            .Produces<DataResponse<StaffQuarterlyCatalogOption>>(StatusCodes.Status200OK)
            .Produces<DataResponse<StaffQuarterlyCatalogOption>>(StatusCodes.Status201Created);
        reports.MapPut("/projects/{projectId:guid}/inception", UpsertProjectInceptionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_UpsertProjectInception")
            .WithSummary("Create or update Form 1 inception for an existing project draft.")
            .WithDescription("Creates or updates Form 1 data for a project in the authenticated employee's institute and may mark inception complete. Completed Form 1 records are locked against later edits; invalid fields or an out-of-institute principal investigator produce validation errors.")
            .Produces<DataResponse<StaffQuarterlyProjectInceptionResponse>>();
        reports.MapPost("/projects/{projectId:guid}/concept-note-upload-sessions", CreateConceptNoteUploadSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_CreateConceptNoteUploadSession")
            .WithSummary("Create a direct upload session for a project concept note.")
            .WithDescription("Creates a time-limited direct upload session for a concept note on an institute-scoped project whose Form 1 remains editable. File name, supported content type, declared size up to the configured concept-note limit, and SHA-256 checksum are validated.")
            .Produces<DataResponse<StaffQuarterlyUploadSessionResponse>>(StatusCodes.Status201Created);
        reports.MapDelete("/projects/{projectId:guid}/concept-note", RemoveConceptNoteAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_RemoveConceptNote")
            .WithSummary("Remove a concept note while Form 1 is still editable.")
            .WithDescription("Removes and marks deleted the concept note attached to an institute-scoped project for the authenticated employee. The project and note must exist and Form 1 must still be editable; completed inception records return a lifecycle conflict.");
        reports.MapPost("/{id:guid}/image-upload-sessions", CreateImageUploadSessionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_CreateImageUploadSession")
            .WithSummary("Create a direct upload session for a quarterly report image.")
            .WithDescription("Creates a time-limited direct upload session for an image on the authenticated employee's editable report. File name, supported image type, declared size up to the configured image limit, SHA-256 checksum, and configured per-report image count are enforced.")
            .Produces<DataResponse<StaffQuarterlyUploadSessionResponse>>(StatusCodes.Status201Created);
        reports.MapDelete("/{id:guid}/images/{fileId:guid}", RemoveImageAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_RemoveImage")
            .WithSummary("Remove a report image from an editable quarterly report.")
            .WithDescription("Removes and marks deleted an image attached to the authenticated employee's own editable quarterly report. Missing, inaccessible, or non-editable reports and images use non-disclosing not-found responses.");
        reports.MapPost("/upload-sessions/{sessionId:guid}/complete", CompleteUploadAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_CompleteUploadSession")
            .WithSummary("Complete a staff quarterly report upload session.")
            .WithDescription("Completes only the authenticated employee's own upload session after verifying declared size, content type, file signature, SHA-256 checksum, ownership, editability, and image-count limits. The confidential file is malware-scanned before attachment; invalid content or lifecycle conflicts use problem details.")
            .Produces<DataResponse<StaffQuarterlyFileMetadata>>();
        reports.MapPost("/technology-drafts", CreateTechnologyDraftAsync).RequireAuthorization(AuthorizationPolicies.ManageOwnReports)
            .WithName("StaffQuarterlyReports_CreateTechnologyDraft")
            .WithSummary("Create an institute technology draft for quarterly reporting.")
            .WithDescription("Creates an institute-scoped technology owned by the authenticated employee, or resolves an existing technology with the same code or name. Required catalog fields are validated and no resource from another institute is exposed.")
            .Produces<DataResponse<StaffQuarterlyCatalogOption>>(StatusCodes.Status200OK)
            .Produces<DataResponse<StaffQuarterlyCatalogOption>>(StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListMineAsync(StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListMineAsync(ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> ListReviewQueueAsync(StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListReviewQueueAsync(ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> GetOptionsAsync(StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.GetOptionsAsync(ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> SearchReviewersAsync(
        string? q, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.SearchReviewersAsync(q, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> ListMyFormOneProjectsAsync(
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListMyFormOneProjectsAsync(ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> ListInstituteFormOneProjectsAsync(
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListInstituteFormOneProjectsAsync(ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> ListCollationAsync(
        Guid reportingPeriodId, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.ListCollationAsync(reportingPeriodId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.List(context, result.Value!, null));
    }

    private static async Task<IResult> AssignProjectPinAsync(
        Guid projectId, AssignProjectPinRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.AssignProjectPinAsync(projectId, new(request.Pin), ParseIfMatch(context), ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> GetProjectInceptionAsync(
        Guid projectId, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.GetProjectInceptionAsync(projectId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> DownloadFileAsync(
        Guid fileId, StaffQuarterlyReportService service, CancellationToken ct)
    {
        var result = await service.DownloadFileAsync(fileId, ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        return Results.File(result.Value!.Stream, result.Value.ContentType, result.Value.FileName);
    }

    private static async Task<IResult> GetAsync(Guid id, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct) =>
        ResourceResponse(await service.GetAsync(id, ct), context);

    private static async Task<IResult> CreateAsync(SaveStaffQuarterlyReportRequest request, StaffQuarterlyReportService service,
        HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateAsync(Map(request), ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        SetEtag(context, result.Value!.Etag);
        return TypedResults.Created($"/api/v2/staff-quarterly-reports/{result.Value.Id}", ResponseEnvelope.Data(context, result.Value));
    }

    private static async Task<IResult> UpdateAsync(Guid id, SaveStaffQuarterlyReportRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct) =>
        ResourceResponse(await service.UpdateAsync(id, Map(request), ParseIfMatch(context), ct), context);

    private static async Task<IResult> SubmitAsync(Guid id, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct) =>
        ResourceResponse(await service.SubmitAsync(id, ParseIfMatch(context), ct), context);

    private static async Task<IResult> ApproveAsync(Guid id, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct) =>
        ResourceResponse(await service.ApproveAsync(id, ParseIfMatch(context), ct), context);

    private static async Task<IResult> ReturnAsync(Guid id, ReturnReportRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct) =>
        ResourceResponse(await service.ReturnAsync(id, request.ReturnReason, ParseIfMatch(context), ct), context);

    private static async Task<IResult> CreateProjectDraftAsync(CreateStaffQuarterlyProjectDraftRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateProjectDraftAsync(new(MapInception(request.Inception)), ct);
        return CatalogDraftResponse(result, context, "staff-quarterly-reports/projects");
    }

    private static async Task<IResult> UpsertProjectInceptionAsync(
        Guid projectId, SaveStaffQuarterlyProjectInceptionRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.UpsertProjectInceptionAsync(projectId, MapInception(request), ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> CreateConceptNoteUploadSessionAsync(
        Guid projectId, CreateStaffQuarterlyUploadSessionRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateConceptNoteUploadSessionAsync(projectId, MapUpload(request), ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        return TypedResults.Created($"/api/v2/staff-quarterly-reports/upload-sessions/{result.Value!.Id:D}",
            ResponseEnvelope.Data(context, result.Value));
    }

    private static async Task<IResult> CreateImageUploadSessionAsync(
        Guid id, CreateStaffQuarterlyUploadSessionRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateImageUploadSessionAsync(id, MapUpload(request), ct);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        return TypedResults.Created($"/api/v2/staff-quarterly-reports/upload-sessions/{result.Value!.Id:D}",
            ResponseEnvelope.Data(context, result.Value));
    }

    private static async Task<IResult> CompleteUploadAsync(
        Guid sessionId, StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CompleteUploadAsync(sessionId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) :
            TypedResults.Ok(ResponseEnvelope.Data(context, result.Value!));
    }

    private static async Task<IResult> RemoveConceptNoteAsync(
        Guid projectId, StaffQuarterlyReportService service, CancellationToken ct)
    {
        var result = await service.RemoveConceptNoteAsync(projectId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) : TypedResults.NoContent();
    }

    private static async Task<IResult> RemoveImageAsync(
        Guid id, Guid fileId, StaffQuarterlyReportService service, CancellationToken ct)
    {
        var result = await service.RemoveImageAsync(id, fileId, ct);
        return result.IsFailure ? EndpointProblems.FromError(result.Error!) : TypedResults.NoContent();
    }

    private static async Task<IResult> CreateTechnologyDraftAsync(CreateStaffQuarterlyTechnologyDraftRequest request,
        StaffQuarterlyReportService service, HttpContext context, CancellationToken ct)
    {
        var result = await service.CreateTechnologyDraftAsync(new(request.Code, request.Name, request.Description,
            request.ApplicationArea, request.TechnologyType, request.YearIntroduced, request.HasIntellectualProperty), ct);
        return CatalogDraftResponse(result, context, "technologies");
    }

    private static IResult CatalogDraftResponse(
        Result<StaffQuarterlyCatalogOption> result, HttpContext context, string resource)
    {
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        var payload = ResponseEnvelope.Data(context, result.Value!);
        return result.Value!.AlreadyExisted
            ? TypedResults.Ok(payload)
            : TypedResults.Created($"/api/v2/{resource}/{result.Value.Id}", payload);
    }

    private static IResult ResourceResponse(Result<StaffQuarterlyReportResponse> result, HttpContext context)
    {
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        SetEtag(context, result.Value!.Etag);
        return TypedResults.Ok(ResponseEnvelope.Data(context, result.Value));
    }

    private static SaveStaffQuarterlyReportCommand Map(SaveStaffQuarterlyReportRequest request) => new(
        request.ReportingPeriodId, request.ReviewerUserId, request.Title, request.Abstract,
        request.WorkSummary, request.KeyResults, request.ConclusionNextSteps,
        request.ProjectIds, request.TechnologyIds,
        request.ProjectProgress.Select(item => new SaveStaffQuarterlyProjectProgressCommand(
            item.ProjectId, item.ProgressSummary, item.ProgressKeyResults, item.Challenges,
            item.NextQuarterActivities, item.WayForward, item.ConferencePapersProduced,
            item.IpTechnologiesProtected)).ToList());

    private static SaveStaffQuarterlyProjectInceptionCommand MapInception(
        SaveStaffQuarterlyProjectInceptionRequest request) => new(
        request.Name, request.Objective, request.Justification, request.Method, request.Nature,
        request.StartDate, request.EndDate, request.BudgetAmount, request.Currency, request.LeadEmployeeId,
        request.EstimatedDuration, request.SponsorName, request.Location, request.CollaboratingInstitute,
        request.ParticipatingScientists, request.ExpectedBeneficiaries, request.PotentialTechnology,
        request.Commercialization, request.ContributionToKnowledge, request.CompleteInception);

    private static CreateStaffQuarterlyUploadSessionCommand MapUpload(
        CreateStaffQuarterlyUploadSessionRequest request) => new(
        request.FileName, request.ContentType, request.ByteLength, request.Sha256Checksum);

    private static byte[]? ParseIfMatch(HttpContext context) =>
        ConcurrencyToken.TryParse(context.Request.Headers[HeaderNames.IfMatch].ToString(), out var token) ? token : null;
    private static void SetEtag(HttpContext context, string etag) => context.Response.Headers.ETag = etag;
}

public sealed record SaveStaffQuarterlyReportRequest(
    Guid ReportingPeriodId,
    Guid ReviewerUserId,
    [property: Required, StringLength(512)] string Title,
    [property: StringLength(4000)] string? Abstract,
    [property: Required, StringLength(4000)] string WorkSummary,
    [property: StringLength(4000)] string? KeyResults,
    [property: StringLength(4000)] string? ConclusionNextSteps,
    [property: MaxLength(100)] IReadOnlyList<Guid> ProjectIds,
    [property: MaxLength(100)] IReadOnlyList<Guid> TechnologyIds,
    IReadOnlyList<SaveStaffQuarterlyProjectProgressRequest> ProjectProgress);

public sealed record SaveStaffQuarterlyProjectProgressRequest(
    Guid ProjectId,
    [property: Required, StringLength(4000)] string ProgressSummary,
    [property: StringLength(4000)] string? ProgressKeyResults,
    [property: StringLength(4000)] string? Challenges,
    [property: StringLength(4000)] string? NextQuarterActivities,
    [property: StringLength(4000)] string? WayForward,
    int ConferencePapersProduced,
    int IpTechnologiesProtected);

public sealed record CreateStaffQuarterlyProjectDraftRequest(
    [property: Required] SaveStaffQuarterlyProjectInceptionRequest Inception);

public sealed record AssignProjectPinRequest(
    [property: Required, StringLength(64, MinimumLength = 1)] string Pin);

public sealed record SaveStaffQuarterlyProjectInceptionRequest(
    [property: Required, StringLength(256)] string Name,
    [property: Required, StringLength(4000)] string Objective,
    [property: Required, StringLength(4000)] string Justification,
    [property: Required, StringLength(4000)] string Method,
    [property: Required, StringLength(64)] string Nature,
    DateTime StartDate,
    DateTime? EndDate,
    [property: Required] decimal BudgetAmount,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    [property: Required] Guid LeadEmployeeId,
    [property: Required, StringLength(128)] string EstimatedDuration,
    [property: Required, StringLength(256)] string SponsorName,
    [property: Required, StringLength(256)] string Location,
    [property: StringLength(512)] string? CollaboratingInstitute,
    [property: StringLength(4000)] string? ParticipatingScientists,
    [property: StringLength(4000)] string? ExpectedBeneficiaries,
    [property: StringLength(4000)] string? PotentialTechnology,
    [property: StringLength(4000)] string? Commercialization,
    [property: StringLength(4000)] string? ContributionToKnowledge,
    bool CompleteInception);

public sealed record CreateStaffQuarterlyUploadSessionRequest(
    [property: Required, StringLength(512)] string FileName,
    [property: Required, StringLength(128)] string ContentType,
    [property: Required] long ByteLength,
    [property: Required, StringLength(64, MinimumLength = 64)] string Sha256Checksum);

public sealed record CreateStaffQuarterlyTechnologyDraftRequest(
    [property: Required, StringLength(64)] string Code,
    [property: Required, StringLength(256)] string Name,
    [property: Required, StringLength(4000)] string Description,
    [property: Required, StringLength(256)] string ApplicationArea,
    [property: Required, StringLength(64)] string TechnologyType,
    short? YearIntroduced,
    bool HasIntellectualProperty);
