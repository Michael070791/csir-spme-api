using Csir.Spme.Application.Common;
using Csir.Spme.Application.Knowledge;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Api.Auth;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class KnowledgeEndpoints
{
    public static void MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var technologies = endpoints.MapGroup("/api/v2/technologies")
            .WithGroupName("v2")
            .WithTags("Technologies")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        technologies.MapGet("", GetTechnologiesAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadKnowledge)
            .WithName("Technologies_List")
            .WithSummary("List technologies in the caller's authorized institute scope.")
            .WithDescription("Returns the institute-scoped catalogue of research technologies, innovations, and technical capabilities. Clients can filter by institute, publication status, and technology type for knowledge management and reporting workflows.")
            .Produces<ListResponse<TechnologyResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        technologies.MapPost("", CreateTechnologyAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteKnowledge)
            .WithName("Technologies_Create")
            .WithSummary("Create a technology record.")
            .WithDescription("Creates a draft technology catalogue entry for an authorized institute. The API validates required descriptive metadata, lead employee scope, uniqueness of the institute code, year bounds, and writes an audit event.")
            .Produces<DataResponse<TechnologyResponse>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        technologies.MapGet("/{id:guid}", GetTechnologyAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadKnowledge)
            .WithName("Technologies_Get")
            .WithSummary("Get a technology record.")
            .WithDescription("Returns the full technology catalogue entry, including description, application area, lead employee, intellectual-property flag, publication status, audit timestamps, and an ETag for safe edits.")
            .Produces<DataResponse<TechnologyResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        technologies.MapPatch("/{id:guid}", UpdateTechnologyAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteKnowledge)
            .WithName("Technologies_Update")
            .WithSummary("Update a technology record.")
            .WithDescription("Updates editable technology metadata and allowed publication status transitions. Clients must provide the latest ETag in the If-Match header; archived technologies remain immutable.")
            .Produces<DataResponse<TechnologyResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        technologies.MapDelete("/{id:guid}", DeleteTechnologyAsync)
            .RequireAuthorization(AuthorizationPolicies.WriteKnowledge)
            .WithName("Technologies_Delete")
            .WithSummary("Delete an unreferenced draft technology.")
            .WithDescription("Deletes an accessible technology only when it is still draft and has no publication references. Published or archived technologies are retained and should be archived instead of hard-deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetTechnologiesAsync(
        TechnologyService service,
        ICursorCodec cursorCodec,
        Guid? instituteId,
        string? status,
        string? technologyType,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            instituteId, status, technologyType, limit, cursor, sort, direction, cancellationToken);
        if (result.IsFailure)
            return EndpointProblems.FromError(result.Error!);
        return TypedResults.Ok(ResponseEnvelope.List(
            context,
            result.Value!.Items.Select(Map).ToList(),
            result.Value.Next is null ? null : cursorCodec.Encode(result.Value.Next)));
    }

    private static async Task<IResult> CreateTechnologyAsync(
        CreateTechnologyRequest request,
        TechnologyService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new CreateTechnologyCommand(
            request.InstituteId, request.Code, request.Name, request.Description,
            request.ApplicationArea, request.LeadEmployeeId, request.TechnologyType,
            request.YearIntroduced, request.HasIntellectualProperty),
            cancellationToken);
        if (result.IsFailure)
        {
            return EndpointProblems.FromError(result.Error!);
        }

        var response = WithEtag(context, Map(result.Value!), result.Value!.Etag);
        return TypedResults.Created(
            $"/api/v2/technologies/{response.Id}",
            ResponseEnvelope.Data(context, response));
    }

    private static async Task<IResult> GetTechnologyAsync(
        Guid id,
        TechnologyService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> UpdateTechnologyAsync(
        Guid id,
        UpdateTechnologyRequest request,
        TechnologyService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, new UpdateTechnologyCommand(
            request.Name, request.Description, request.ApplicationArea, request.LeadEmployeeId,
            request.TechnologyType, request.YearIntroduced, request.HasIntellectualProperty,
            request.Status),
            ExpectedRowVersion(context),
            cancellationToken);

        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.Ok(ResponseEnvelope.Data(
                context, WithEtag(context, Map(result.Value!), result.Value!.Etag)));
    }

    private static async Task<IResult> DeleteTechnologyAsync(
        Guid id,
        TechnologyService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsFailure
            ? EndpointProblems.FromError(result.Error!)
            : TypedResults.NoContent();
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

    private static TechnologyResponse Map(TechnologyDto dto) => new(
        dto.Id, dto.InstituteId, dto.Code, dto.Name, dto.Description, dto.ApplicationArea,
        dto.LeadEmployeeId, dto.TechnologyType, dto.YearIntroduced, dto.HasIntellectualProperty,
        dto.Status, dto.Etag, dto.CreatedAt, dto.UpdatedAt);

}
