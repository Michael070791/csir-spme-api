using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class HolidayPeriodEndpoints
{
    public static void MapHolidayPeriodEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var periods = endpoints.MapGroup("/api/v2/holiday-periods")
            .WithGroupName("v2")
            .WithTags("Leave")
            .RequireAuthorization()
            .WithDescription("Institute and CSIR-wide holiday periods define the availability date window used by the skeletal staff workflow. Institute callers are constrained to their own scope, while platform administration manages CSIR-wide periods.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        periods.MapGet("", ListAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("HolidayPeriods_List")
            .WithSummary("List accessible holiday periods.")
            .WithDescription("Lists holiday periods visible to the caller, optionally filtered by leave year and status. Institute callers receive CSIR-wide periods plus periods for their institute, while platform administration may list all scopes; missing institute scope is forbidden.")
            .Produces<CollectionResponse<HolidayPeriodResponse>>(StatusCodes.Status200OK);
        periods.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.ReadLeave).WithName("HolidayPeriods_Get")
            .WithSummary("Get one holiday period.")
            .WithDescription("Returns one CSIR-wide or caller-institute holiday period with its current ETag. Platform administration may read every scope, while inaccessible and missing identifiers share the same non-disclosing not-found response.")
            .Produces<HolidayPeriodResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);
        periods.MapPost("", CreateAsync).RequireAuthorization(AuthorizationPolicies.ManageLeave).WithName("HolidayPeriods_Create")
            .WithSummary("Create a holiday period.")
            .WithDescription("Creates an institute or CSIR-wide holiday period and returns its ETag. Institute managers are constrained to their authenticated institute, only platform administration may create CSIR-wide periods, and duplicate scope and leave-year combinations return conflict.")
            .Produces<HolidayPeriodResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);
        periods.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(AuthorizationPolicies.ManageLeave).WithName("HolidayPeriods_Update")
            .WithSummary("Update a holiday period with an ETag.")
            .WithDescription("Updates dates, availability window, status, and notes for a holiday period the caller may manage. Institute managers cannot alter CSIR-wide periods; the current If-Match ETag is required and stale versions return precondition failed.")
            .Produces<HolidayPeriodResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status412PreconditionFailed);
        periods.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(AuthorizationPolicies.ManageLeave).WithName("HolidayPeriods_Delete")
            .WithSummary("Delete an unused draft holiday period with an ETag.")
            .WithDescription("Deletes only a manageable holiday period that remains in draft and has no skeletal staff requests. The current If-Match ETag is required; non-draft or referenced periods return conflict, stale versions return precondition failed, and inaccessible records return not found.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    private static async Task<IResult> ListAsync(HttpContext context, SpmeDbContext db, short? leaveYear, string? status, CancellationToken ct)
    {
        var instituteId = InstituteId(context);
        if (!instituteId.HasValue && !IsPlatform(context))
            return EndpointProblems.FromError(Error.Forbidden("An institute scope is required."));

        var query = db.HolidayPeriods.AsNoTracking().AsQueryable();
        if (instituteId.HasValue)
            query = query.Where(x => x.ScopeType == ScopeTypes.CsirWide || (x.ScopeType == ScopeTypes.Institute && x.InstituteId == instituteId));
        if (leaveYear.HasValue) query = query.Where(x => x.LeaveYear == leaveYear.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());

        var items = await query.OrderByDescending(x => x.LeaveYear).ThenBy(x => x.ScopeType).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<HolidayPeriodResponse>(items.Select(Map).ToList(), items.Count));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var period = await db.HolidayPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return period is null || !CanRead(context, period)
            ? EndpointProblems.FromError(Error.NotFound("Holiday period not found."))
            : WithEtag(context, TypedResults.Ok(Map(period)), period.RowVersion);
    }

    private static async Task<IResult> CreateAsync(CreateHolidayPeriodRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var institute = ResolveTargetInstitute(context, request.ScopeType, request.InstituteId);
        if (institute.IsFailure) return EndpointProblems.FromError(institute.Error!);

        if (await db.HolidayPeriods.AnyAsync(x => x.ScopeType == request.ScopeType.Trim() && x.InstituteId == institute.Value && x.LeaveYear == request.LeaveYear, ct))
            return EndpointProblems.FromError(Error.Conflict("A holiday period already exists for this scope and leave year."));

        var created = HolidayPeriod.Create(
            request.ScopeType.Trim(), institute.Value, request.LeaveYear,
            request.ChristmasStartDate, request.ChristmasEndDate, request.NewYearStartDate, request.NewYearEndDate,
            request.AvailabilityStartDate, request.AvailabilityEndDate, request.Status.Trim(), request.Notes);
        if (created.IsFailure) return EndpointProblems.FromError(created.Error!);

        db.HolidayPeriods.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.ETag = ConcurrencyToken.Format(created.Value!.RowVersion);
        return TypedResults.Created($"/api/v2/holiday-periods/{created.Value.Id}", Map(created.Value));
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateHolidayPeriodRequest request, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var period = await db.HolidayPeriods.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (period is null || !CanManage(context, period)) return EndpointProblems.FromError(Error.NotFound("Holiday period not found."));
        if (!TryApplyEtag(context, db, period, out var problem)) return problem!;

        var updated = period.Update(
            request.ChristmasStartDate, request.ChristmasEndDate, request.NewYearStartDate, request.NewYearEndDate,
            request.AvailabilityStartDate, request.AvailabilityEndDate, request.Status.Trim(), request.Notes);
        if (updated.IsFailure) return EndpointProblems.FromError(updated.Error!);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return EndpointProblems.FromError(Error.PreconditionFailed(
                "The holiday period was modified by another request. Reload it and retry."));
        }

        context.Response.Headers.ETag = ConcurrencyToken.Format(period.RowVersion);
        return TypedResults.Ok(Map(period));
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        var period = await db.HolidayPeriods.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (period is null || !CanManage(context, period)) return EndpointProblems.FromError(Error.NotFound("Holiday period not found."));
        if (period.Status != HolidayPeriodStatuses.Draft) return EndpointProblems.FromError(Error.StateTransition("Only draft holiday periods can be deleted."));
        if (!TryApplyEtag(context, db, period, out var problem)) return problem!;
        if (await db.SkeletalStaffRequests.AnyAsync(x => x.HolidayPeriodId == id, ct)) return EndpointProblems.FromError(Error.Conflict("A holiday period with skeletal staff requests cannot be deleted."));

        db.HolidayPeriods.Remove(period);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return EndpointProblems.FromError(Error.PreconditionFailed(
                "The holiday period was modified by another request. Reload it and retry."));
        }

        return TypedResults.NoContent();
    }

    internal static HolidayPeriodResponse Map(HolidayPeriod period) => new(
        period.Id, period.ScopeType, period.InstituteId, period.LeaveYear,
        period.ChristmasStartDate, period.ChristmasEndDate, period.NewYearStartDate, period.NewYearEndDate,
        period.AvailabilityStartDate, period.AvailabilityEndDate, period.Status,
        period.Notes, ConcurrencyToken.Format(period.RowVersion), period.CreatedAt, period.UpdatedAt);

    internal static bool IsPlatform(HttpContext context) => context.User.IsInRole(SpmeRoles.PlatformAdmin);
    internal static Guid? InstituteId(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;
    internal static bool CanRead(HttpContext context, HolidayPeriod period) =>
        IsPlatform(context) || period.ScopeType == ScopeTypes.CsirWide || (period.ScopeType == ScopeTypes.Institute && InstituteId(context) == period.InstituteId);
    internal static bool CanManage(HttpContext context, HolidayPeriod period) =>
        IsPlatform(context) || (period.ScopeType == ScopeTypes.Institute && InstituteId(context) == period.InstituteId);

    internal static Result<Guid?> ResolveTargetInstitute(HttpContext context, string scopeType, Guid? requestedInstitute)
    {
        var normalized = scopeType.Trim();
        if (normalized == ScopeTypes.CsirWide)
            return IsPlatform(context) ? Result<Guid?>.Success(null) : Result<Guid?>.Failure(Error.Forbidden("Only platform administration may manage CSIR-wide holiday periods."));
        if (normalized != ScopeTypes.Institute) return Result<Guid?>.Failure(Error.Validation("Holiday period scope must be csir-wide or institute."));

        var current = InstituteId(context);
        if (current.HasValue && requestedInstitute.HasValue && current != requestedInstitute)
            return Result<Guid?>.Failure(Error.Forbidden("You are not authorized for that institute."));
        var institute = current ?? requestedInstitute;
        return institute.HasValue ? Result<Guid?>.Success(institute) : Result<Guid?>.Failure(Error.Validation("An institute is required."));
    }

    internal static bool TryApplyEtag(HttpContext context, SpmeDbContext db, Csir.Spme.Domain.Common.BaseEntity entity, out IResult? problem)
    {
        if (!ConcurrencyToken.TryParse(context.Request.Headers[HeaderNames.IfMatch].ToString(), out var rowVersion))
        {
            problem = EndpointProblems.FromError(Error.PreconditionFailed("An If-Match header is required."));
            return false;
        }

        db.SetOriginalRowVersion(entity, rowVersion);
        problem = null;
        return true;
    }

    private static IResult WithEtag<T>(HttpContext context, Ok<T> result, byte[] rowVersion)
    {
        context.Response.Headers.ETag = ConcurrencyToken.Format(rowVersion);
        return result;
    }
}
