using Csir.Spme.Application.Common;
using Csir.Spme.Application.Projects;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Api.Auth;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class ProjectsEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/api/v2/projects")
            .WithGroupName("v2")
            .WithTags("Projects")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        projects.MapGet("", GetProjectsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadProjects)
            .WithName("Projects_List")
            .WithSummary("List projects in the caller's authorized institute scope.")
            .WithDescription("Returns a paged project register for monitoring CSIR research, development, consultancy, capacity-building, infrastructure, and other institutional initiatives. Clients can filter by owning institute, lifecycle status, project nature, lead employee, and linked strategic thrust.")
            .Produces<ListResponse<ProjectResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        projects.MapPost("", CreateProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteProjects)
            .WithName("Projects_Create")
            .WithSummary("Create a project record.")
            .WithDescription("Creates a draft project for an authorized institute. The API validates controlled values, date ranges, currency format, owning institute access, lead employee scope, thrust scope, duplicate project codes, and records an audit event.")
            .Produces<DataResponse<ProjectResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/{id:guid}", GetProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadProjects)
            .WithName("Projects_Get")
            .WithSummary("Get a project record.")
            .WithDescription("Returns the full project profile, including business case, planned and actual results, lifecycle status, budget metadata, strategic linkage, audit timestamps, and an ETag for safe updates.")
            .Produces<DataResponse<ProjectResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPatch("/{id:guid}", UpdateProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteProjects)
            .WithName("Projects_Update")
            .WithSummary("Update a project record.")
            .WithDescription("Updates editable project content and lifecycle status using optimistic concurrency. Clients must send the latest ETag in the If-Match header; archived and cancelled projects remain immutable.")
            .Produces<DataResponse<ProjectResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        projects.MapDelete("/{id:guid}", DeleteProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteProjects)
            .WithName("Projects_Delete")
            .WithSummary("Delete a draft project.")
            .WithDescription("Deletes an accessible draft project only when it has no milestones, funding entries, sponsors, or updates. Active, completed, cancelled, and archived projects are retained for auditability.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapPost("/{id:guid}/submit", SubmitProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveProjects)
            .WithName("Projects_Submit")
            .WithSummary("Submit a draft project into active execution.")
            .WithDescription("Moves a draft project to the active lifecycle state through the domain transition rules and records the status change in the audit log.")
            .Produces<DataResponse<ProjectResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapPost("/{id:guid}/archive", ArchiveProjectAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveProjects)
            .WithName("Projects_Archive")
            .WithSummary("Archive a project.")
            .WithDescription("Archives an accessible project that should remain retained but no longer appear as active work. Archived projects are immutable after the transition.")
            .Produces<DataResponse<ProjectResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetProjectsAsync(
        ProjectService service,
        ICursorCodec cursorCodec,
        Guid? instituteId,
        string? status,
        string? nature,
        Guid? leadEmployeeId,
        Guid? thrustId,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(
            instituteId, status, nature, leadEmployeeId, thrustId,
            limit, cursor, sort, direction, cancellationToken);
        if (result.IsFailure)
            return EndpointProblems.FromError(result.Error!);
        return TypedResults.Ok(ResponseEnvelope.List(
            context,
            result.Value!.Items.Select(Map).ToList(),
            result.Value.Next is null ? null : cursorCodec.Encode(result.Value.Next)));
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        ProjectService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateProjectCommand(
            request.InstituteId, request.Code, request.Name, request.Objective, request.Justification,
            request.ExpectedResult, request.Nature, request.StartDate, request.EndDate, request.Currency,
            request.BudgetAmount, request.Innovation, request.Impact, request.LeadEmployeeId, request.ThrustId),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/projects/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetProjectAsync(
        Guid id,
        ProjectService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid id,
        UpdateProjectRequest request,
        ProjectService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, new UpdateProjectCommand(
            request.Name, request.Objective, request.Justification, request.ExpectedResult,
            request.ActualResult, request.Nature, request.StartDate, request.EndDate, request.Currency,
            request.BudgetAmount, request.Innovation, request.Impact, request.LeadEmployeeId,
            request.ThrustId, request.Status),
            ExpectedRowVersion(context),
            cancellationToken);

        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> DeleteProjectAsync(
        Guid id,
        ProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.NoContent();
    }

    private static async Task<IResult> SubmitProjectAsync(
        Guid id,
        ProjectService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.SubmitAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> ArchiveProjectAsync(
        Guid id,
        ProjectService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ArchiveAsync(id, cancellationToken);
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

    private static ProjectResponse Map(ProjectDto dto) => new(
        dto.Id, dto.InstituteId, dto.Code, dto.Name, dto.Objective, dto.Justification,
        dto.ExpectedResult, dto.ActualResult, dto.Status, dto.Nature, dto.StartDate,
        dto.EndDate, dto.Currency, dto.BudgetAmount, dto.Innovation, dto.Impact,
        dto.LeadEmployeeId, dto.ThrustId, dto.Etag, dto.CreatedAt, dto.UpdatedAt);

}
