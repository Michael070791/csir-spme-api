namespace Csir.Spme.Application.Common.Interfaces;

/// <summary>Institute and employee reference lookups shared by feature services.</summary>
public interface IInstituteDirectory
{
    /// <summary>Resolves an institute by id, code, name, or alias. Returns null when no active institute matches.</summary>
    Task<Guid?> ResolveInstituteIdAsync(string idOrCodeOrNameOrAlias, CancellationToken ct);

    Task<bool> InstituteExistsAsync(Guid instituteId, CancellationToken ct);

    Task<EmployeeScope?> GetEmployeeScopeAsync(Guid employeeId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, EmployeeScope>> ListEmployeeScopesAsync(
        IReadOnlyCollection<Guid> employeeIds, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string?>> GetCurrentStaffCategoriesAsync(
        IReadOnlyCollection<Guid> employeeIds, CancellationToken ct);

    Task<bool> FileExistsAsync(Guid fileId, CancellationToken ct);
}

/// <summary>Minimal employee data needed for scoping and leave validation.</summary>
public sealed record EmployeeScope(Guid EmployeeId, Guid InstituteId, string ProfileStatus, Guid? PositionTypeId);
