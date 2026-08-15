using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class HolidayEndpoints
{
    public static void MapHolidayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var holidays = endpoints.MapGroup("/api/v2/holidays").WithGroupName("v2").WithTags("Leave");
        holidays.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadHolidays).WithName("Holidays_List")
            .WithSummary("List holidays visible to the caller.")
            .WithDescription("Lists holidays in the optional inclusive date range. Institute callers receive CSIR-wide holidays plus holidays for their authenticated institute, while platform administration may list all scopes; callers without an effective scope are forbidden.");
        holidays.MapPost("", CreateAsync).RequireAuthorization(AuthorizationPolicies.ManageHolidays).WithName("Holidays_Create")
            .WithSummary("Create an institute or CSIR-wide holiday.")
            .WithDescription("Creates a holiday in the caller's authorized scope. Institute managers are constrained to their authenticated institute, only platform administration may create CSIR-wide holidays, invalid scope or fields return validation errors, and duplicate name and date entries return conflict.");
        holidays.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(AuthorizationPolicies.ManageHolidays).WithName("Holidays_Update")
            .WithSummary("Update an accessible holiday.")
            .WithDescription("Updates the name, date, full-day flag, Islamic-calendar flag, and notes without changing the holiday's scope. Institute managers may update only their institute holidays, platform administration may update any holiday, and duplicates return conflict.");
        holidays.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(AuthorizationPolicies.ManageHolidays).WithName("Holidays_Delete")
            .WithSummary("Delete an accessible holiday.")
            .WithDescription("Deletes a holiday within the caller's management scope. Institute managers may delete only holidays assigned to their authenticated institute, platform administration may delete any holiday, and missing or inaccessible identifiers share a non-disclosing not-found response.");
    }

    private static async Task<IResult> ListAsync(HttpContext context, SpmeDbContext db, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var instituteId = CurrentInstituteId(context);
        if (!instituteId.HasValue && !IsPlatform(context))
            return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));
        var query = db.Holidays.AsNoTracking();
        if (instituteId.HasValue)
            query = query.Where(x => x.ScopeType == "csir-wide" || (x.ScopeType == "institute" && x.InstituteId == instituteId.Value));
        if (from.HasValue) query = query.Where(x => x.HolidayDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.HolidayDate <= to.Value.Date);
        var holidays = await query.OrderBy(x => x.HolidayDate).ThenBy(x => x.Name).ToListAsync(ct);
        var items = holidays.Select(MapHoliday).ToList();
        return TypedResults.Ok(new CollectionResponse<HolidayResponse>(items, items.Count));
    }

    private static async Task<IResult> CreateAsync(CreateHolidayRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var instituteId = ResolveTargetInstitute(context, request.ScopeType, request.InstituteId);
        if (instituteId.IsFailure) return EndpointProblems.FromError(instituteId.Error!);
        var created = Holiday.Create(request.ScopeType, instituteId.Value, request.Name, request.HolidayDate, request.IsFullDay, request.IsIslamic, request.Notes);
        if (created.IsFailure) return EndpointProblems.FromError(created.Error!);
        var holiday = created.Value!;
        if (await db.Holidays.AnyAsync(x => x.ScopeType == holiday.ScopeType && x.InstituteId == holiday.InstituteId && x.HolidayDate == holiday.HolidayDate && x.Name == holiday.Name, ct))
            return EndpointProblems.FromError(Error.Conflict("A holiday with the same name and date already exists in this scope."));
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v2/holidays/{holiday.Id}", MapHoliday(holiday));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateHolidayRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (holiday is null || !CanAccess(context, holiday)) return EndpointProblems.FromError(Error.NotFound("Holiday not found."));
        if (await db.Holidays.AnyAsync(x => x.Id != id && x.ScopeType == holiday.ScopeType && x.InstituteId == holiday.InstituteId && x.HolidayDate == request.HolidayDate.Date && x.Name == request.Name.Trim(), ct))
            return EndpointProblems.FromError(Error.Conflict("A holiday with the same name and date already exists in this scope."));
        var updated = holiday.Update(request.Name, request.HolidayDate, request.IsFullDay, request.IsIslamic, request.Notes);
        if (updated.IsFailure) return EndpointProblems.FromError(updated.Error!);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(MapHoliday(holiday));
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (holiday is null || !CanAccess(context, holiday)) return EndpointProblems.FromError(Error.NotFound("Holiday not found."));
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static HolidayResponse Map(Holiday holiday) => MapHoliday(holiday);
    private static HolidayResponse MapHoliday(Holiday holiday) => new(holiday.Id, holiday.ScopeType, holiday.InstituteId, holiday.Name, holiday.HolidayDate, holiday.IsFullDay, holiday.IsIslamic, holiday.Notes, $"\"{holiday.UpdatedAt.UtcTicks}\"");

    private static Result<Guid?> ResolveTargetInstitute(HttpContext context, string scopeType, Guid? requestedInstitute)
    {
        if (scopeType == "csir-wide")
        {
            return IsPlatform(context) ? Result<Guid?>.Success(null) : Result<Guid?>.Failure(Error.Forbidden("Only platform administration may create CSIR-wide holidays."));
        }
        if (scopeType != "institute") return Result<Guid?>.Failure(Error.Validation("Holiday scope must be csir-wide or institute."));
        var current = CurrentInstituteId(context);
        if (current.HasValue && requestedInstitute.HasValue && current.Value != requestedInstitute.Value)
            return Result<Guid?>.Failure(Error.Forbidden("You are not authorized for that institute."));
        var institute = current ?? requestedInstitute;
        return institute.HasValue ? Result<Guid?>.Success(institute) : Result<Guid?>.Failure(Error.Validation("An institute is required."));
    }

    private static bool CanAccess(HttpContext context, Holiday holiday) =>
        IsPlatform(context) || (holiday.ScopeType == "institute" && CurrentInstituteId(context) == holiday.InstituteId);
    private static bool IsPlatform(HttpContext context) => context.User.IsInRole(SpmeRoles.PlatformAdmin);
    private static Guid? CurrentInstituteId(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;
}
