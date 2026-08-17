using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Iam;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Identity;

public static class EmployeeLeadershipIdentitySync
{
    private const string ScientificSecretaryRole = "ScientificSecretary";
    public static bool HasScientificSecretaryDesignation(IReadOnlyList<string>? leadershipRoles) =>
        leadershipRoles?.Any(IsScientificSecretaryLabel) == true;

    public static bool IsScientificSecretaryLabel(string role)
    {
        var normalized = role.Trim().ToLowerInvariant().Replace('_', '-');
        return normalized.Contains("scientific secretary", StringComparison.Ordinal) ||
               normalized == "scientificsecretary";
    }

    public static async Task SyncScientificSecretaryRoleAsync(
        UserManager<User> userManager,
        IAuditService audit,
        Guid employeeId,
        IReadOnlyList<string>? leadershipRoles,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(candidate => candidate.EmployeeId == employeeId, cancellationToken);
        if (user is null)
            return;

        var shouldHaveRole = HasScientificSecretaryDesignation(leadershipRoles);
        var hasRole = await userManager.IsInRoleAsync(user, ScientificSecretaryRole);

        if (shouldHaveRole && !hasRole)
        {
            var addResult = await userManager.AddToRoleAsync(user, ScientificSecretaryRole);
            if (addResult.Succeeded)
                await audit.RecordAndSaveAsync(
                    "users.assign-roles",
                    "User",
                    user.Id.ToString(),
                    before: null,
                    after: ScientificSecretaryRole,
                    ct: cancellationToken);
        }
        else if (!shouldHaveRole && hasRole)
        {
            var removeResult = await userManager.RemoveFromRoleAsync(user, ScientificSecretaryRole);
            if (removeResult.Succeeded)
                await audit.RecordAndSaveAsync(
                    "users.assign-roles",
                    "User",
                    user.Id.ToString(),
                    before: ScientificSecretaryRole,
                    after: null,
                    ct: cancellationToken);
        }
    }
}
