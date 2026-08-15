using System.Security.Claims;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Api.Auth;

/// <summary>
/// Institute-scoped HR access helpers: V2 roles plus read-only legacy StaffUser compatibility.
/// </summary>
public static class InstituteStaffAccess
{
    public const string StaffUserIdentityType = "StaffUser";

    public static bool IsPlatformAdmin(ClaimsPrincipal user) =>
        user.IsInRole(SpmeRoles.PlatformAdmin);

    public static bool IsStaffUserIdentity(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirstValue("identity_type"),
            StaffUserIdentityType,
            StringComparison.OrdinalIgnoreCase);

    public static bool HasLegacyStaffManagementRole(ClaimsPrincipal user) =>
        LegacyStaffManagementRoles.All.Any(user.IsInRole);

    public static bool HasLegacyStaffManagementRole(IEnumerable<string> roles) =>
        roles.Any(LegacyStaffManagementRoles.Contains);

    /// <summary>
    /// True for preserved StaffUser / legacy staff-management roles that may receive
    /// institute-scoped read access without V2 write privileges.
    /// </summary>
    public static bool HasStaffManagementReadCompatibility(ClaimsPrincipal user)
    {
        if (IsSolelyEmployeeSelfService(user))
            return false;

        return IsStaffUserIdentity(user) || HasLegacyStaffManagementRole(user);
    }

    /// <summary>
    /// True for legacy HR/Admin/Writer roles that may mutate institute-scoped employee data.
    /// Still requires an institute claim and never expands beyond institute scope.
    /// </summary>
    public static bool HasStaffManagementWriteCompatibility(ClaimsPrincipal user)
    {
        if (IsSolelyEmployeeSelfService(user))
            return false;

        return LegacyStaffManagementRoles.WriteCompatible.Any(user.IsInRole);
    }

    public static bool IsV2InstituteScopedManager(ClaimsPrincipal user) =>
        user.IsInRole(SpmeRoles.HrAdmin) || user.IsInRole(SpmeRoles.InstituteAdmin);

    public static bool CanReadInstituteHr(ClaimsPrincipal user) =>
        IsPlatformAdmin(user) ||
        IsV2InstituteScopedManager(user) ||
        HasStaffManagementReadCompatibility(user);

    public static bool CanManageInstituteHr(ClaimsPrincipal user) =>
        IsPlatformAdmin(user) ||
        user.IsInRole(SpmeRoles.HrAdmin) ||
        HasStaffManagementWriteCompatibility(user);

    /// <summary>
    /// Org dashboard and most HR reads require an institute for scoped managers and legacy readers.
    /// PlatformAdmin remains CSIR-wide. Leave list has separate unscoped HrAdmin rules.
    /// Employee directory/verification uses <see cref="RequireInstituteAssignmentForEmployees"/>.
    /// </summary>
    public static bool RequiresInstituteAssignment(ClaimsPrincipal user) =>
        !IsPlatformAdmin(user) &&
        (IsV2InstituteScopedManager(user) || HasStaffManagementReadCompatibility(user));

    public static Guid? ReadInstituteId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("institute_id"), out var instituteId) ? instituteId : null;

    public static Domain.Common.Error? RequireInstituteAssignment(ClaimsPrincipal user)
    {
        if (!RequiresInstituteAssignment(user))
            return null;

        return ReadInstituteId(user).HasValue
            ? null
            : Domain.Common.Error.Forbidden("An institute assignment is required.");
    }

    /// <summary>
    /// PlatformAdmin and HrAdmin (including unscoped HrAdmin) may read/verify employees CSIR-wide.
    /// Unscoped InstituteAdmin and StaffUser remain fail-closed.
    /// </summary>
    public static Domain.Common.Error? RequireInstituteAssignmentForEmployees(ClaimsPrincipal user)
    {
        if (IsPlatformAdmin(user) || user.IsInRole(SpmeRoles.HrAdmin))
            return null;

        return RequireInstituteAssignment(user);
    }

    public static bool IsInstituteScopedManagementRole(string? role) =>
        string.Equals(role, SpmeRoles.HrAdmin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SpmeRoles.InstituteAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlatformAdminIdentityType(string? identityType) =>
        string.Equals(identityType, SpmeRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool IsStaffUserIdentityType(string? identityType) =>
        string.Equals(identityType, StaffUserIdentityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsSolelyEmployeeSelfService(ClaimsPrincipal user)
    {
        if (IsStaffUserIdentity(user) || HasLegacyStaffManagementRole(user) || IsV2InstituteScopedManager(user))
            return false;

        var identityType = user.FindFirstValue("identity_type");
        if (!string.Equals(identityType, SpmeRoles.Employee, StringComparison.OrdinalIgnoreCase))
            return false;

        var roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        return roles.Length == 0 ||
               roles.All(role => string.Equals(role, SpmeRoles.Employee, StringComparison.OrdinalIgnoreCase));
    }
}
