namespace Csir.Spme.Domain.Constants;

/// <summary>
/// Preserved V1 staff-management role names kept by legacy import.
/// These must never be auto-mapped to V2 management roles.
/// </summary>
public static class LegacyStaffManagementRoles
{
    public const string HR = "HR";
    public const string Admin = "Admin";
    public const string Reader = "Reader";
    public const string Writer = "Writer";
    public const string DG = "DG";
    public const string Director = "Director";

    public static readonly string[] All = [HR, Admin, Reader, Writer, DG, Director];

    /// <summary>
    /// Legacy roles that may mutate institute-scoped employee records.
    /// Reader/DG/Director remain read-compatible only.
    /// </summary>
    public static readonly string[] WriteCompatible = [HR, Admin, Writer];

    public static bool Contains(string? role) =>
        !string.IsNullOrWhiteSpace(role) &&
        All.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    public static bool IsWriteCompatible(string? role) =>
        !string.IsNullOrWhiteSpace(role) &&
        WriteCompatible.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
}
