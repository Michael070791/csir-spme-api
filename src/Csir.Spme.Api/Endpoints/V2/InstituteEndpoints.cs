using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class InstituteEndpoints
{
    public static void MapInstituteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var institutes = endpoints.MapGroup("/api/v2/institutes")
            .WithGroupName("v2")
            .WithTags("Institutes")
            .WithDescription("Active CSIR institute catalogue resources constrained to the caller's effective institute scope. Institute identifiers returned by these operations never override authorization on later requests.")
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        institutes.MapGet("", GetInstitutesAsync)
            .WithName("Institutes_List")
            .WithSummary("List active institutes in the caller's effective scope.")
            .WithDescription("Returns active institutes visible to the caller. Platform administrators can view the full catalogue, while institute-scoped callers receive only their assigned institute and callers without a valid scope are rejected.")
            .Produces<CollectionResponse<InstituteResponse>>(StatusCodes.Status200OK);
        institutes.MapGet("/{id:guid}", GetInstituteAsync)
            .WithName("Institutes_Get")
            .WithSummary("Get an active institute.")
            .WithDescription("Returns the detailed active institute record only when the identifier is within the caller's effective scope. Missing, inactive, and out-of-scope institutes use the same non-disclosing not-found response.")
            .Produces<InstituteDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        institutes.MapGet("/{id:guid}/divisions", GetInstituteDivisionsAsync)
            .WithName("Institutes_Divisions_List")
            .WithSummary("List an institute's active divisions.")
            .WithDescription("Returns active divisions belonging to an accessible institute, ordered by name. A client cannot use the institute route identifier to bypass its authenticated institute scope.")
            .Produces<CollectionResponse<DivisionResponse>>(StatusCodes.Status200OK);

        var divisions = endpoints.MapGroup("/api/v2/divisions")
            .WithGroupName("v2")
            .WithTags("Institutes")
            .WithDescription("Institute division catalogue resources used to organize employees, sections, and communication audiences. Reads and mutations always enforce the caller's authenticated institute scope.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        divisions.MapGet("", GetDivisionsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .WithName("Divisions_List")
            .WithSummary("List active divisions.")
            .WithDescription("Returns active divisions in the caller's effective institute scope, ordered by name. Platform administrators may supply an institute filter; other callers cannot select a different institute.")
            .Produces<CollectionResponse<DivisionResponse>>(StatusCodes.Status200OK);
        divisions.MapPost("", CreateDivisionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOrganization)
            .WithName("Divisions_Create")
            .WithSummary("Create an institute division.")
            .WithDescription("Creates an active division in the caller's institute, or in the explicitly selected institute for a platform administrator. The institute must be active and division names must be unique within it.")
            .Produces<DivisionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        divisions.MapGet("/{id:guid}", GetDivisionAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .WithName("Divisions_Get")
            .WithSummary("Get an active division.")
            .WithDescription("Returns an active division only when its owning institute is within the caller's effective scope. Missing, inactive, and cross-institute divisions are represented by the same not-found response.")
            .Produces<DivisionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        divisions.MapPatch("/{id:guid}", UpdateDivisionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOrganization)
            .WithName("Divisions_Update")
            .WithSummary("Update an institute division.")
            .WithDescription("Updates the name, code, and active state of a division in the caller's institute scope. Names remain unique per institute, and inaccessible division identifiers receive a non-disclosing not-found response.")
            .Produces<DivisionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        divisions.MapGet("/{id:guid}/sections", GetDivisionSectionsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .WithName("Divisions_Sections_List")
            .WithSummary("List an active division's sections.")
            .WithDescription("Returns active sections belonging to an accessible active division, ordered by name. Missing, inactive, and cross-institute divisions receive a non-disclosing not-found response.")
            .Produces<CollectionResponse<SectionResponse>>(StatusCodes.Status200OK);

        var sections = endpoints.MapGroup("/api/v2/sections")
            .WithGroupName("v2")
            .WithTags("Institutes")
            .WithDescription("Institute section catalogue resources nested logically beneath divisions. Every read and mutation derives institute authorization through the section's owning division.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        sections.MapGet("", GetSectionsAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .WithName("Sections_List")
            .WithSummary("List active sections.")
            .WithDescription("Returns active sections in the caller's effective institute scope, optionally filtered by division. Platform administrators can read across institutes, while scoped callers remain constrained through division ownership.")
            .Produces<CollectionResponse<SectionResponse>>(StatusCodes.Status200OK);
        sections.MapPost("", CreateSectionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOrganization)
            .WithName("Sections_Create")
            .WithSummary("Create a division section.")
            .WithDescription("Creates an active section beneath an accessible active division. Section names must be unique within that division, and inaccessible or missing divisions use a non-disclosing not-found response.")
            .Produces<SectionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        sections.MapGet("/{id:guid}", GetSectionAsync)
            .RequireAuthorization(AuthorizationPolicies.ReadOrganization)
            .WithName("Sections_Get")
            .WithSummary("Get an active section.")
            .WithDescription("Returns an active section only when its active parent division belongs to an institute visible to the caller. Missing, inactive, and cross-institute resources are represented as not found.")
            .Produces<SectionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
        sections.MapPatch("/{id:guid}", UpdateSectionAsync)
            .RequireAuthorization(AuthorizationPolicies.ManageOrganization)
            .WithName("Sections_Update")
            .WithSummary("Update a division section.")
            .WithDescription("Updates the name, code, and active state of a section whose parent division is in scope. Names remain unique within the division, and inaccessible identifiers receive a non-disclosing not-found response.")
            .Produces<SectionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetInstitutesAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var isPlatform = IsPlatform(context);
        var instituteId = isPlatform ? null : CurrentInstituteId(context);
        if (!instituteId.HasValue && !isPlatform)
            return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));
        var query = db.Institutes.AsNoTracking().Where(x => x.IsActive);
        if (instituteId.HasValue)
            query = query.Where(x => x.Id == instituteId.Value);

        var items = await query.OrderBy(x => x.Name)
            .Select(x => new InstituteResponse(x.Id, x.Code, x.Name, x.Kind, x.EmailDomain))
            .ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<InstituteResponse>(items, items.Count));
    }

    private static async Task<IResult> GetInstituteAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!CanAccessInstitute(context, id))
            return EndpointProblems.FromError(Error.NotFound("Institute not found."));

        var item = await db.Institutes.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new InstituteDetailResponse(x.Id, x.Code, x.Name, x.Kind, x.EmailDomain, x.Address, x.IsActive))
            .FirstOrDefaultAsync(ct);
        return item is null ? EndpointProblems.FromError(Error.NotFound("Institute not found.")) : TypedResults.Ok(item);
    }

    private static async Task<IResult> GetInstituteDivisionsAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!CanAccessInstitute(context, id))
            return EndpointProblems.FromError(Error.NotFound("Institute not found."));
        return await ListDivisionsAsync(db, id, ct);
    }

    private static async Task<IResult> GetDivisionsAsync(HttpContext context, SpmeDbContext db, Guid? instituteId, CancellationToken ct)
    {
        var effective = ResolveInstituteFilter(context, instituteId);
        if (effective.IsFailure)
            return EndpointProblems.FromError(effective.Error!);
        return await ListDivisionsAsync(db, effective.Value, ct);
    }

    private static async Task<IResult> ListDivisionsAsync(SpmeDbContext db, Guid? instituteId, CancellationToken ct)
    {
        var query = db.Divisions.AsNoTracking().Where(x => x.IsActive);
        if (instituteId.HasValue)
            query = query.Where(x => x.InstituteId == instituteId.Value);
        var items = await query.OrderBy(x => x.Name)
            .Select(x => new DivisionResponse(x.Id, x.InstituteId, x.Code, x.Name))
            .ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<DivisionResponse>(items, items.Count));
    }

    private static async Task<IResult> CreateDivisionAsync(CreateDivisionRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return EndpointProblems.Unprocessable("Division name is required.");
        var targetInstitute = ResolveTargetInstitute(context, request.InstituteId);
        if (targetInstitute.IsFailure)
            return EndpointProblems.FromError(targetInstitute.Error!);
        if (!await db.Institutes.AnyAsync(x => x.Id == targetInstitute.Value && x.IsActive, ct))
            return EndpointProblems.FromError(Error.NotFound("Institute not found."));
        if (await db.Divisions.AnyAsync(x => x.InstituteId == targetInstitute.Value && x.Name == request.Name.Trim(), ct))
            return EndpointProblems.FromError(Error.Conflict("A division with that name already exists."));

        var division = new Division(targetInstitute.Value, request.Name.Trim());
        if (!string.IsNullOrWhiteSpace(request.Code))
            division.Update(request.Name, request.Code);
        db.Divisions.Add(division);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v2/divisions/{division.Id}", new DivisionResponse(division.Id, division.InstituteId, division.Code, division.Name));
    }

    private static async Task<IResult> GetDivisionAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.Divisions.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new DivisionResponse(x.Id, x.InstituteId, x.Code, x.Name)).FirstOrDefaultAsync(ct);
        return item is null || !CanAccessInstitute(context, item.InstituteId)
            ? EndpointProblems.FromError(Error.NotFound("Division not found.")) : TypedResults.Ok(item);
    }

    private static async Task<IResult> UpdateDivisionAsync(Guid id, UpdateDivisionRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var division = await db.Divisions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (division is null || !CanAccessInstitute(context, division.InstituteId))
            return EndpointProblems.FromError(Error.NotFound("Division not found."));
        if (await db.Divisions.AnyAsync(x => x.Id != id && x.InstituteId == division.InstituteId && x.Name == request.Name.Trim(), ct))
            return EndpointProblems.FromError(Error.Conflict("A division with that name already exists."));
        var updated = division.Update(request.Name, request.Code);
        if (updated.IsFailure)
            return EndpointProblems.FromError(updated.Error!);
        if (request.IsActive == false) division.Deactivate();
        if (request.IsActive == true) division.Activate();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new DivisionResponse(division.Id, division.InstituteId, division.Code, division.Name));
    }

    private static async Task<IResult> GetDivisionSectionsAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var division = await db.Divisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (division is null || !CanAccessInstitute(context, division.InstituteId))
            return EndpointProblems.FromError(Error.NotFound("Division not found."));
        return await ListSectionsAsync(db, id, ct);
    }

    private static async Task<IResult> GetSectionsAsync(Guid? divisionId, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var isPlatform = IsPlatform(context);
        var instituteId = isPlatform ? null : CurrentInstituteId(context);
        if (!instituteId.HasValue && !isPlatform)
            return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));
        var query = db.Sections.AsNoTracking().Where(x => x.IsActive);
        if (divisionId.HasValue)
            query = query.Where(x => x.DivisionId == divisionId.Value);
        var items = await query.Join(db.Divisions, section => section.DivisionId, division => division.Id, (section, division) => new { section, division })
            .Where(x => x.division.IsActive && (!instituteId.HasValue || x.division.InstituteId == instituteId.Value))
            .OrderBy(x => x.section.Name)
            .Select(x => new SectionResponse(x.section.Id, x.section.DivisionId, x.section.Code, x.section.Name))
            .ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<SectionResponse>(items, items.Count));
    }

    private static async Task<IResult> ListSectionsAsync(SpmeDbContext db, Guid divisionId, CancellationToken ct)
    {
        var items = await db.Sections.AsNoTracking().Where(x => x.DivisionId == divisionId && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new SectionResponse(x.Id, x.DivisionId, x.Code, x.Name)).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<SectionResponse>(items, items.Count));
    }

    private static async Task<IResult> CreateSectionAsync(CreateSectionRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var division = await db.Divisions.FirstOrDefaultAsync(x => x.Id == request.DivisionId && x.IsActive, ct);
        if (division is null || !CanAccessInstitute(context, division.InstituteId))
            return EndpointProblems.FromError(Error.NotFound("Division not found."));
        if (await db.Sections.AnyAsync(x => x.DivisionId == request.DivisionId && x.Name == request.Name.Trim(), ct))
            return EndpointProblems.FromError(Error.Conflict("A section with that name already exists in this division."));

        var section = new Section(request.DivisionId, request.Name.Trim());
        section.Update(request.Name, request.Code);
        db.Sections.Add(section);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v2/sections/{section.Id}", new SectionResponse(section.Id, section.DivisionId, section.Code, section.Name));
    }

    private static async Task<IResult> GetSectionAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.Sections.AsNoTracking().Join(db.Divisions, section => section.DivisionId, division => division.Id,
            (section, division) => new { section, division }).Where(x => x.section.Id == id && x.section.IsActive && x.division.IsActive)
            .Select(x => new { x.division.InstituteId, Response = new SectionResponse(x.section.Id, x.section.DivisionId, x.section.Code, x.section.Name) }).FirstOrDefaultAsync(ct);
        return item is null || !CanAccessInstitute(context, item.InstituteId)
            ? EndpointProblems.FromError(Error.NotFound("Section not found.")) : TypedResults.Ok(item.Response);
    }

    private static async Task<IResult> UpdateSectionAsync(Guid id, UpdateSectionRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var item = await db.Sections.Join(db.Divisions, section => section.DivisionId, division => division.Id,
            (section, division) => new { section, division }).FirstOrDefaultAsync(x => x.section.Id == id, ct);
        if (item is null || !CanAccessInstitute(context, item.division.InstituteId))
            return EndpointProblems.FromError(Error.NotFound("Section not found."));
        if (await db.Sections.AnyAsync(x => x.Id != id && x.DivisionId == item.section.DivisionId && x.Name == request.Name.Trim(), ct))
            return EndpointProblems.FromError(Error.Conflict("A section with that name already exists in this division."));
        var updated = item.section.Update(request.Name, request.Code);
        if (updated.IsFailure) return EndpointProblems.FromError(updated.Error!);
        if (request.IsActive == false) item.section.Deactivate();
        if (request.IsActive == true) item.section.Activate();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new SectionResponse(item.section.Id, item.section.DivisionId, item.section.Code, item.section.Name));
    }

    private static Guid? CurrentInstituteId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;

    private static bool IsPlatform(HttpContext context) => context.User.IsInRole(SpmeRoles.PlatformAdmin);

    private static bool CanAccessInstitute(HttpContext context, Guid instituteId)
    {
        if (IsPlatform(context))
            return true;

        var current = CurrentInstituteId(context);
        return current.HasValue && current.Value == instituteId;
    }

    private static Result<Guid?> ResolveInstituteFilter(HttpContext context, Guid? requested)
    {
        if (IsPlatform(context))
            return Result<Guid?>.Success(requested);

        var current = CurrentInstituteId(context);
        if (current.HasValue && requested.HasValue && current.Value != requested.Value)
            return Result<Guid?>.Failure(Error.CrossInstitute("You are not authorized to access that institute."));
        if (current.HasValue)
            return Result<Guid?>.Success(current);
        return Result<Guid?>.Failure(Error.Forbidden("An institute scope is required."));
    }

    private static Result<Guid> ResolveTargetInstitute(HttpContext context, Guid? requested)
    {
        if (IsPlatform(context))
            return requested.HasValue
                ? Result<Guid>.Success(requested.Value)
                : Result<Guid>.Failure(Error.Validation("An institute must be selected."));

        var current = CurrentInstituteId(context);
        return current.HasValue
            ? Result<Guid>.Success(current.Value)
            : Result<Guid>.Failure(Error.Forbidden("An institute scope is required."));
    }
}
