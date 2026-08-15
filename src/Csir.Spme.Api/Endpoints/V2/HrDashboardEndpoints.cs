using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class HrDashboardEndpoints
{
    public static void MapHrDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v2/hr/dashboard", GetHrDashboardAsync)
            .WithGroupName("v2")
            .WithTags("Human Resources")
            .WithName("HrDashboard_Get")
            .WithSummary("Get institute-scoped HR dashboard aggregates.")
            .WithDescription("Returns employee, leave, and pending promotion counts for the caller's institute. Platform administrators may optionally filter with instituteId.")
            .RequireAuthorization(AuthorizationPolicies.ReadHrDashboard)
            .Produces<DataResponse<HrDashboardResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetHrDashboardAsync(
        HttpContext context,
        SpmeDbContext db,
        Guid? instituteId,
        CancellationToken cancellationToken)
    {
        var scopeError = InstituteStaffAccess.RequireInstituteAssignment(context.User);
        if (scopeError is not null)
            return EndpointProblems.FromError(scopeError);

        Guid resolvedInstituteId;
        if (InstituteStaffAccess.IsPlatformAdmin(context.User))
        {
            if (instituteId.HasValue)
            {
                var exists = await db.Institutes.AsNoTracking()
                    .AnyAsync(candidate => candidate.Id == instituteId.Value && candidate.IsActive, cancellationToken);
                if (!exists)
                    return EndpointProblems.FromError(Error.NotFound("Institute not found."));
                resolvedInstituteId = instituteId.Value;
            }
            else
            {
                // Platform-wide aggregate when no institute filter is supplied.
                return TypedResults.Ok(ResponseEnvelope.Data(context, await AggregateAsync(db, instituteId: null, cancellationToken)));
            }
        }
        else
        {
            var claimInstituteId = InstituteStaffAccess.ReadInstituteId(context.User)!.Value;
            if (instituteId.HasValue && instituteId.Value != claimInstituteId)
                return EndpointProblems.FromError(Error.NotFound("Institute not found."));
            resolvedInstituteId = claimInstituteId;
        }

        var data = await AggregateAsync(db, resolvedInstituteId, cancellationToken);
        return TypedResults.Ok(ResponseEnvelope.Data(context, data));
    }

    private static async Task<HrDashboardResponse> AggregateAsync(
        SpmeDbContext db,
        Guid? instituteId,
        CancellationToken cancellationToken)
    {
        var employees = db.Employees.AsNoTracking().AsQueryable();
        var leaveRequests = db.LeaveRequests.AsNoTracking().AsQueryable();
        var submissions = db.PromotionSubmissions.AsNoTracking().AsQueryable();

        if (instituteId.HasValue)
        {
            employees = employees.Where(employee => employee.InstituteId == instituteId.Value);
            leaveRequests = leaveRequests.Where(request => request.InstituteId == instituteId.Value);
            submissions = submissions.Where(submission => submission.InstituteId == instituteId.Value);
        }

        var today = DateTime.UtcNow.Date;
        var totalEmployees = await employees.CountAsync(cancellationToken);
        var onLeaveToday = await leaveRequests.CountAsync(
            request => request.Status == LeaveRequestStatuses.Approved &&
                       request.StartDate <= today &&
                       request.EndDate >= today,
            cancellationToken);
        var openLeaveRequests = await leaveRequests.CountAsync(
            request => request.Status == LeaveRequestStatuses.Submitted ||
                       request.Status == LeaveRequestStatuses.UnderReview,
            cancellationToken);
        var pendingPromotions = await submissions.CountAsync(
            submission => submission.Status == PromotionConstants.SubmissionSubmitted,
            cancellationToken);

        return new HrDashboardResponse(totalEmployees, onLeaveToday, pendingPromotions, openLeaveRequests);
    }
}
