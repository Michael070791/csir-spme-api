using System.Security.Cryptography;
using System.Text;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Workflow;

public sealed class WorkflowApprovalTokenService(
    SpmeDbContext db,
    TimeProvider timeProvider) : IWorkflowApprovalTokenService
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(48);

    public async Task<WorkflowApprovalTokenIssueResult> IssueAsync(
        string purpose,
        Guid resourceId,
        Guid approverUserId,
        string stage,
        CancellationToken ct = default)
    {
        await RevokeUnusedAsync(purpose, resourceId, stage, ct);

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(rawBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var tokenHash = Hash(rawToken);
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(TokenLifetime);
        db.WorkflowApprovalTokens.Add(WorkflowApprovalToken.Create(
            purpose, resourceId, approverUserId, stage, tokenHash, expiresAt, now));
        return new WorkflowApprovalTokenIssueResult(rawToken, expiresAt);
    }

    public async Task RevokeUnusedAsync(
        string purpose,
        Guid resourceId,
        string stage,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var tokens = await db.WorkflowApprovalTokens
            .Where(token => token.Purpose == purpose &&
                            token.ResourceId == resourceId &&
                            token.Stage == stage &&
                            token.ConsumedAt == null &&
                            token.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.Revoke(now);
    }

    public async Task<WorkflowApprovalTokenValidation?> ValidateForUserAsync(
        string rawToken,
        Guid userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var token = await db.WorkflowApprovalTokens.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == Hash(rawToken), ct);
        if (token is null || !token.IsActive(timeProvider.GetUtcNow()) || token.ApproverUserId != userId)
            return null;

        return new WorkflowApprovalTokenValidation(
            token.ResourceId,
            token.ApproverUserId,
            token.Purpose,
            token.Stage,
            token.ExpiresAt);
    }

    public async Task<bool> ConsumeAsync(string rawToken, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var token = await db.WorkflowApprovalTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == Hash(rawToken), ct);
        if (token is null || !token.IsActive(timeProvider.GetUtcNow()) || token.ApproverUserId != userId)
            return false;

        token.Consume(timeProvider.GetUtcNow());
        return true;
    }

    internal static string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
