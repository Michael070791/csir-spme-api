namespace Csir.Spme.Application.Common.Interfaces;

public sealed record WorkflowApprovalTokenIssueResult(string RawToken, DateTimeOffset ExpiresAt);

public sealed record WorkflowApprovalTokenValidation(
    Guid ResourceId,
    Guid ApproverUserId,
    string Purpose,
    string Stage,
    DateTimeOffset ExpiresAt);

public interface IWorkflowApprovalTokenService
{
    Task<WorkflowApprovalTokenIssueResult> IssueAsync(
        string purpose,
        Guid resourceId,
        Guid approverUserId,
        string stage,
        CancellationToken ct = default);

    Task RevokeUnusedAsync(
        string purpose,
        Guid resourceId,
        string stage,
        CancellationToken ct = default);

    Task<WorkflowApprovalTokenValidation?> ValidateForUserAsync(
        string rawToken,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> ConsumeAsync(
        string rawToken,
        Guid userId,
        CancellationToken ct = default);
}

public sealed record WorkflowApproverContact(
    Guid UserId,
    string Email,
    string DisplayName,
    string? Phone);

public interface IWorkflowApproverResolver
{
    Task<IReadOnlyList<WorkflowApproverContact>> FindStageApproversAsync(
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> BuildSkeletalStaffChainAsync(
        Guid instituteId,
        Guid employeeId,
        CancellationToken ct = default);
}
