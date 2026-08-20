using System.Security.Claims;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class WorkflowApprovalEndpoints
{
    public static void MapWorkflowApprovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v2/workflow-approvals")
            .WithGroupName("v2")
            .WithTags("Leave")
            .RequireAuthorization()
            .WithDescription("Authenticated workflow approval tokens for leave and skeletal staff email actions.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/preview", PreviewAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveLeave)
            .WithName("WorkflowApprovals_Preview")
            .WithSummary("Preview a workflow approval token for the signed-in approver.")
            .Produces<WorkflowApprovalPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/decide", DecideAsync)
            .RequireAuthorization(AuthorizationPolicies.ApproveLeave)
            .WithName("WorkflowApprovals_Decide")
            .WithSummary("Approve or reject using a workflow approval token.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> PreviewAsync(
        WorkflowApprovalPreviewRequest request,
        HttpContext context,
        IWorkflowApprovalTokenService tokens,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var userId = UserId(context);
        if (!userId.HasValue)
            return EndpointProblems.FromError(Error.Forbidden("An authenticated user is required."));

        var validation = await tokens.ValidateForUserAsync(request.Token.Trim(), userId.Value, ct);
        if (validation is null)
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        return validation.Purpose switch
        {
            WorkflowApprovalPurposes.Leave => await PreviewLeaveAsync(validation, db, ct),
            WorkflowApprovalPurposes.SkeletalStaff => await PreviewSkeletalAsync(validation, context, db, ct),
            _ => EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."))
        };
    }

    private static async Task<IResult> DecideAsync(
        WorkflowApprovalDecideRequest request,
        HttpContext context,
        IWorkflowApprovalTokenService tokens,
        LeaveRequestService leaveService,
        SpmeDbContext db,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApproverResolver approverResolver,
        CancellationToken ct)
    {
        var userId = UserId(context);
        if (!userId.HasValue)
            return EndpointProblems.FromError(Error.Forbidden("An authenticated user is required."));

        var validation = await tokens.ValidateForUserAsync(request.Token.Trim(), userId.Value, ct);
        if (validation is null)
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not ("approve" or "reject"))
            return EndpointProblems.FromError(Error.Validation("Decision must be approve or reject."));
        if (decision == "reject" && string.IsNullOrWhiteSpace(request.Reason))
            return EndpointProblems.FromError(Error.Validation("A rejection reason is required."));

        return validation.Purpose switch
        {
            WorkflowApprovalPurposes.Leave => await DecideLeaveAsync(
                validation, request, leaveService, tokens, ct),
            WorkflowApprovalPurposes.SkeletalStaff => await DecideSkeletalAsync(
                validation, request, context, db, audit, notifications, approverResolver, tokens, ct),
            _ => EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."))
        };
    }

    private static async Task<IResult> PreviewLeaveAsync(
        WorkflowApprovalTokenValidation validation,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var request = await db.LeaveRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == validation.ResourceId, ct);
        if (request is null || request.CurrentApprovalStage != validation.Stage)
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        return TypedResults.Ok(new WorkflowApprovalPreviewResponse(
            validation.Purpose,
            validation.ResourceId,
            validation.Stage,
            "Leave request awaiting approval",
            $"{request.LeaveType} leave from {request.StartDate:dd MMM yyyy} to {request.EndDate:dd MMM yyyy}",
            $"/leave/{validation.ResourceId:D}",
            ConcurrencyToken.Format(request.RowVersion)));
    }

    private static async Task<IResult> PreviewSkeletalAsync(
        WorkflowApprovalTokenValidation validation,
        HttpContext context,
        SpmeDbContext db,
        CancellationToken ct)
    {
        var request = await db.SkeletalStaffRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == validation.ResourceId, ct);
        if (request is null || !SkeletalStaffEndpoints.CanAccessStageInstitute(context, request) ||
            request.CurrentApprovalStage != validation.Stage ||
            !await SkeletalStaffEndpoints.CanDecideCurrentStageAsync(context, request, db, ct))
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        return TypedResults.Ok(new WorkflowApprovalPreviewResponse(
            validation.Purpose,
            validation.ResourceId,
            validation.Stage,
            "Skeletal staff request awaiting approval",
            $"Skeletal staff period from {request.SelectedStartDate:dd MMM yyyy} to {request.SelectedEndDate:dd MMM yyyy}",
            $"/skeletal-staff/{validation.ResourceId:D}",
            ConcurrencyToken.Format(request.RowVersion)));
    }

    private static async Task<IResult> DecideLeaveAsync(
        WorkflowApprovalTokenValidation validation,
        WorkflowApprovalDecideRequest request,
        LeaveRequestService leaveService,
        IWorkflowApprovalTokenService tokens,
        CancellationToken ct)
    {
        var leave = await leaveService.GetAsync(validation.ResourceId, ct);
        if (leave.IsFailure || leave.Value!.CurrentApprovalStage != validation.Stage)
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        var etag = request.Etag ?? leave.Value.Etag;
        if (!ConcurrencyToken.TryParse(etag, out var rowVersion))
            return EndpointProblems.FromError(Error.PreconditionFailed("An If-Match header or ETag is required."));

        var result = request.Decision.Trim().Equals("approve", StringComparison.OrdinalIgnoreCase)
            ? await leaveService.ApproveAsync(validation.ResourceId,
                new LeaveDecisionCommand(request.Comments, null), rowVersion, ct)
            : await leaveService.RejectAsync(validation.ResourceId,
                new LeaveDecisionCommand(request.Comments, null), request.Reason, rowVersion, ct);
        if (result.IsFailure)
            return EndpointProblems.FromError(result.Error!);

        await tokens.ConsumeAsync(request.Token.Trim(), validation.ApproverUserId, ct);
        return TypedResults.Ok();
    }

    private static async Task<IResult> DecideSkeletalAsync(
        WorkflowApprovalTokenValidation validation,
        WorkflowApprovalDecideRequest request,
        HttpContext context,
        SpmeDbContext db,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        IWorkflowApproverResolver approverResolver,
        IWorkflowApprovalTokenService tokens,
        CancellationToken ct)
    {
        var item = await db.SkeletalStaffRequests.FirstOrDefaultAsync(candidate => candidate.Id == validation.ResourceId, ct);
        if (item is null || !SkeletalStaffEndpoints.CanAccessStageInstitute(context, item) ||
            item.CurrentApprovalStage != validation.Stage ||
            !await SkeletalStaffEndpoints.CanDecideCurrentStageAsync(context, item, db, ct))
            return EndpointProblems.FromError(Error.NotFound("The approval link is invalid or has expired."));

        var rowVersion = request.Etag;
        if (!ConcurrencyToken.TryParse(rowVersion, out var parsedRowVersion))
            return EndpointProblems.FromError(Error.PreconditionFailed("An If-Match header or ETag is required."));
        db.SetOriginalRowVersion(item, parsedRowVersion);

        if (request.Decision.Trim().Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            var chain = await approverResolver.BuildSkeletalStaffChainAsync(item.InstituteId, item.EmployeeId, ct);
            var stageIndex = Array.IndexOf(chain.ToArray(), item.CurrentApprovalStage);
            var nextStage = stageIndex >= 0 && stageIndex + 1 < chain.Count ? chain[stageIndex + 1] : null;
            var approved = item.Approve(item.CurrentApprovalStage, nextStage);
            if (approved.IsFailure) return EndpointProblems.FromError(approved.Error!);
            var approverUserId = await PersistedUserIdAsync(context, db, ct);
            var sequence = (short)(await db.SkeletalStaffApprovals.CountAsync(x => x.SkeletalStaffRequestId == item.Id && x.ApprovalStage == item.CurrentApprovalStage, ct) + 1);
            db.SkeletalStaffApprovals.Add(Domain.Leave.SkeletalStaffApproval.Create(
                item.Id, approverUserId, validation.Stage, ApprovalDecisions.Approved, request.Comments, sequence));
            await audit.RecordAsync("skeletal-staff-request.approved", "SkeletalStaffRequest", item.Id.ToString(), $"stage={validation.Stage}", $"status={item.Status}", ct);
            if (nextStage is not null)
                await notifications.StageSkeletalStaffAwaitingApprovalAsync(
                    item.Id,
                    item.InstituteId,
                    item.EmployeeId,
                    nextStage,
                    item.SelectedStartDate ?? DateTime.MinValue,
                    item.SelectedEndDate ?? DateTime.MinValue,
                    ct);
            else
                await notifications.StageSkeletalStaffDecisionAsync(item.Id, item.InstituteId, item.EmployeeId, "approved", null, ct);
        }
        else
        {
            var rejected = item.Reject(validation.Stage, request.Reason!);
            if (rejected.IsFailure) return EndpointProblems.FromError(rejected.Error!);
            var approverUserId = await PersistedUserIdAsync(context, db, ct);
            var sequence = (short)(await db.SkeletalStaffApprovals.CountAsync(x => x.SkeletalStaffRequestId == item.Id && x.ApprovalStage == validation.Stage, ct) + 1);
            db.SkeletalStaffApprovals.Add(Domain.Leave.SkeletalStaffApproval.Create(
                item.Id, approverUserId, validation.Stage, ApprovalDecisions.Rejected, request.Comments ?? request.Reason, sequence));
            await audit.RecordAsync("skeletal-staff-request.rejected", "SkeletalStaffRequest", item.Id.ToString(), $"stage={validation.Stage}", "status=rejected", ct);
            await notifications.StageSkeletalStaffDecisionAsync(item.Id, item.InstituteId, item.EmployeeId, "rejected", request.Reason, ct);
        }

        await tokens.ConsumeAsync(request.Token.Trim(), validation.ApproverUserId, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok();
    }

    private static Guid? UserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task<Guid?> PersistedUserIdAsync(HttpContext context, SpmeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(ct);
    }

}
