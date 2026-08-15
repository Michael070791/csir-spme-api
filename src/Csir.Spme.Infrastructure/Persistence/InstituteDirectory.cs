using Csir.Spme.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Persistence;

internal sealed class InstituteDirectory : IInstituteDirectory
{
    private readonly SpmeDbContext _db;

    public InstituteDirectory(SpmeDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> ResolveInstituteIdAsync(string idOrCodeOrNameOrAlias, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idOrCodeOrNameOrAlias))
        {
            return null;
        }

        var value = idOrCodeOrNameOrAlias.Trim();
        if (Guid.TryParse(value, out var id) &&
            await _db.Institutes.AnyAsync(institute => institute.Id == id && institute.IsActive, ct))
        {
            return id;
        }

        var normalized = value.ToUpperInvariant();
        var instituteId = await _db.Institutes.AsNoTracking()
            .Where(institute => institute.IsActive &&
                (institute.Code == value || institute.Name == value || institute.NormalizedName == normalized))
            .Select(institute => (Guid?)institute.Id)
            .FirstOrDefaultAsync(ct);

        if (instituteId.HasValue)
        {
            return instituteId;
        }

        return await _db.InstituteAliases.AsNoTracking()
            .Where(alias => alias.NormalizedAlias == normalized)
            .Join(_db.Institutes.AsNoTracking().Where(institute => institute.IsActive),
                alias => alias.InstituteId, institute => institute.Id, (alias, institute) => (Guid?)institute.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> InstituteExistsAsync(Guid instituteId, CancellationToken ct) =>
        _db.Institutes.AnyAsync(institute => institute.Id == instituteId && institute.IsActive, ct);

    public async Task<EmployeeScope?> GetEmployeeScopeAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await _db.Employees.AsNoTracking()
            .Where(candidate => candidate.Id == employeeId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.InstituteId,
                candidate.ProfileStatus
            })
            .FirstOrDefaultAsync(ct);

        if (employee is null)
        {
            return null;
        }

        var positionTypeId = await _db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.IsCurrent)
            .Select(record => record.PositionTypeId)
            .FirstOrDefaultAsync(ct);

        return new EmployeeScope(employee.Id, employee.InstituteId, employee.ProfileStatus, positionTypeId);
    }

    public async Task<IReadOnlyDictionary<Guid, EmployeeScope>> ListEmployeeScopesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken ct)
    {
        if (employeeIds.Count == 0)
            return new Dictionary<Guid, EmployeeScope>();

        var uniqueIds = employeeIds.Distinct().ToArray();
        var employees = await _db.Employees.AsNoTracking()
            .Where(candidate => uniqueIds.Contains(candidate.Id))
            .Select(candidate => new { candidate.Id, candidate.InstituteId, candidate.ProfileStatus })
            .ToListAsync(ct);
        var positionTypes = await _db.EmploymentRecords.AsNoTracking()
            .Where(record => record.IsCurrent && uniqueIds.Contains(record.EmployeeId))
            .Select(record => new { record.EmployeeId, record.PositionTypeId })
            .ToListAsync(ct);
        var positionByEmployee = positionTypes
            .GroupBy(record => record.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().PositionTypeId);

        return employees.ToDictionary(
            employee => employee.Id,
            employee => new EmployeeScope(
                employee.Id,
                employee.InstituteId,
                employee.ProfileStatus,
                positionByEmployee.GetValueOrDefault(employee.Id)));
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetCurrentStaffCategoriesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken ct)
    {
        if (employeeIds.Count == 0)
            return new Dictionary<Guid, string?>();

        var uniqueIds = employeeIds.Distinct().ToArray();
        var rows = await _db.EmploymentRecords.AsNoTracking()
            .Where(record => record.IsCurrent && uniqueIds.Contains(record.EmployeeId))
            .Select(record => new { record.EmployeeId, record.StaffCategory })
            .ToListAsync(ct);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().StaffCategory);
    }

    public Task<bool> FileExistsAsync(Guid fileId, CancellationToken ct) =>
        _db.FileRecords.AnyAsync(file => file.Id == fileId && !file.IsDeleted, ct);
}
