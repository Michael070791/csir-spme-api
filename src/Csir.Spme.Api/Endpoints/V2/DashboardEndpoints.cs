using System.Security.Claims;
using System.Text.Json;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v2/me/dashboard", GetDashboardAsync)
            .WithGroupName("v2")
            .WithTags("Staff Portal")
            .WithName("StaffPortal_GetDashboard")
            .WithSummary("Get the authenticated employee dashboard.")
            .WithDescription("Returns a bounded, non-cacheable projection derived only from the authenticated employee and institute claims.")
            .RequireAuthorization()
            .Produces<StaffDashboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("employee_id"), out var employeeId) ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(context.User.FindFirstValue("institute_id"), out var instituteId))
            return EndpointProblems.FromError(Error.NotFound("Staff dashboard not found."));

        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == employeeId && x.InstituteId == instituteId && x.ProfileStatus == "active", ct);
        if (employee is null) return EndpointProblems.FromError(Error.NotFound("Staff dashboard not found."));

        var currentEmployment = await db.EmploymentRecords.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.InstituteId == instituteId && x.IsCurrent)
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var year = (short)DateTime.UtcNow.Year;
        var balanceRows = await db.LeaveBalances.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.LeaveYear == year)
            .OrderBy(x => x.LeaveType)
            .Take(12).ToListAsync(ct);
        var balances = balanceRows.Select(x => new DashboardLeaveBalanceResponse(x.LeaveType, x.LeaveYear,
            x.TotalDays, x.UsedDays, x.PendingDays, x.RemainingDays)).ToList();

        var roleNames = await (from userRole in db.UserRoles
                               join role in db.Roles on userRole.RoleId equals role.Id
                               where userRole.UserId == userId
                               select role.Name!).ToHashSetAsync(ct);
        var memoQuery = db.Memos.AsNoTracking()
            .Where(x => x.Status == "published" && (
                x.InstituteId == instituteId ||
                db.MemoAudiences.Any(audience =>
                    audience.MemoId == x.Id &&
                    (audience.InstituteId == instituteId || audience.EmployeeId == employeeId))));
        var memoCandidates = db.Database.IsSqlite()
            ? (await memoQuery.ToListAsync(ct)).OrderByDescending(x => x.PublishedAt).Take(50).ToList()
            : await memoQuery.OrderByDescending(x => x.PublishedAt).Take(50).ToListAsync(ct);
        var memoIds = memoCandidates.Select(x => x.Id).ToArray();
        var audiences = await db.MemoAudiences.AsNoTracking()
            .Where(x => memoIds.Contains(x.MemoId)).ToListAsync(ct);
        var latestMemo = memoCandidates.FirstOrDefault(memo => MemoAudienceMatcher.Matches(
            audiences.Where(x => x.MemoId == memo.Id),
            employeeId,
            instituteId,
            currentEmployment?.DivisionId,
            currentEmployment?.SectionId,
            roleNames));

        var unreadCount = await db.Notifications.AsNoTracking()
            .CountAsync(x => x.RecipientUserId == userId && !x.IsRead, ct);
        var promotionQuery = (from snapshot in db.PromotionStatusSnapshots.AsNoTracking()
                               join cycle in db.PromotionCycles.AsNoTracking() on snapshot.PromotionCycleId equals cycle.Id
                               where snapshot.EmployeeId == employeeId && snapshot.InstituteId == instituteId
                               select new { snapshot, cycle });
        var promotion = db.Database.IsSqlite()
            ? (await promotionQuery.ToListAsync(ct)).OrderByDescending(x => x.cycle.CycleYear)
                .ThenByDescending(x => x.snapshot.CalculatedAt).FirstOrDefault()
            : await promotionQuery.OrderByDescending(x => x.cycle.CycleYear)
                .ThenByDescending(x => x.snapshot.CalculatedAt).FirstOrDefaultAsync(ct);
        DashboardPromotionResponse? promotionResponse = null;
        if (promotion is not null)
        {
            var targetGrade = promotion.snapshot.TargetGradeId.HasValue
                ? await db.Grades.AsNoTracking().Where(x => x.Id == promotion.snapshot.TargetGradeId.Value)
                    .Select(x => new DashboardGradeResponse(x.Code, x.Name)).FirstOrDefaultAsync(ct)
                : null;
            promotionResponse = new DashboardPromotionResponse(
                promotion.snapshot.AssessmentState, promotion.snapshot.EligibilityState,
                promotion.snapshot.PromotionSubmissionStatus,
                targetGrade is null ? null : new DashboardNextPromotionResponse(targetGrade),
                promotion.snapshot.EligibilityState == "eligible-for-review"
                    ? "Review and complete your promotion submission."
                    : "Your promotion record will be updated after HR review.",
                promotion.snapshot.CalculatedAt);
        }

        var skeletalQuery = db.SkeletalStaffRequests.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.InstituteId == instituteId &&
                x.Status != SkeletalStaffRequestStatuses.Cancelled &&
                x.Status != SkeletalStaffRequestStatuses.Rejected &&
                x.Status != SkeletalStaffRequestStatuses.Completed)
            .Select(x => new { x.Id, x.Status, x.SelectedDatesJson, x.CreatedAt });
        var skeletal = db.Database.IsSqlite()
            ? (await skeletalQuery.ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).FirstOrDefault()
            : await skeletalQuery.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

        var completion = StaffProfileCompletion.Calculate(employee, currentEmployment is not null);

        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        return TypedResults.Ok(new StaffDashboardResponse(
            balances,
            latestMemo is null ? null : new DashboardMemoResponse(latestMemo.Id, latestMemo.Title,
                latestMemo.Body, latestMemo.PublishedAt ?? latestMemo.CreatedAt),
            promotionResponse,
            unreadCount,
            completion,
            skeletal is null ? null : new DashboardSkeletalResponse(skeletal.Id, skeletal.Status,
                ParseDates(skeletal.SelectedDatesJson), skeletal.CreatedAt),
            DateTimeOffset.UtcNow));
    }

    private static IReadOnlyList<DateTime> ParseDates(string json)
    {
        try { return JsonSerializer.Deserialize<DateTime[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

public sealed record DashboardLeaveBalanceResponse(
    string LeaveType, short LeaveYear, decimal TotalDays, decimal UsedDays, decimal PendingDays, decimal RemainingDays);
public sealed record DashboardMemoResponse(Guid Id, string Title, string Body, DateTimeOffset PublishedAt);
public sealed record DashboardGradeResponse(string Code, string Name);
public sealed record DashboardNextPromotionResponse(DashboardGradeResponse TargetGrade);
public sealed record DashboardPromotionResponse(
    string AssessmentState, string? EligibilityState, string? PromotionSubmissionStatus,
    DashboardNextPromotionResponse? NextPromotion, string NextAction, DateTimeOffset CalculatedAt);
public sealed record DashboardSkeletalResponse(Guid Id, string Status, IReadOnlyList<DateTime> SelectedDates, DateTimeOffset CreatedAt);
public sealed record StaffDashboardResponse(
    IReadOnlyList<DashboardLeaveBalanceResponse> LeaveBalances,
    DashboardMemoResponse? LatestMemo,
    DashboardPromotionResponse? PromotionStatus,
    int UnreadNotificationCount,
    int ProfileCompletion,
    DashboardSkeletalResponse? SkeletalStaffStatus,
    DateTimeOffset CalculatedAt);
