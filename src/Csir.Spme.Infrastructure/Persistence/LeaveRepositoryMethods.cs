using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext
{
    async Task<IReadOnlyList<LeaveDelegateOption>> ILeaveRequestRepository.ListDelegateOptionsAsync(
        Guid instituteId,
        Guid excludeEmployeeId,
        Guid? sectionId,
        Guid? divisionId,
        CancellationToken ct)
    {
        var rows = await (
            from employee in Employees.AsNoTracking()
            join employment in EmploymentRecords.AsNoTracking().Where(record => record.IsCurrent)
                on employee.Id equals employment.EmployeeId
            where employee.InstituteId == instituteId &&
                  employee.Id != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.InstituteId == instituteId &&
                  (sectionId == null || employment.SectionId == sectionId) &&
                  (divisionId == null || employment.DivisionId == divisionId)
            orderby employee.Surname, employee.OtherNames
            select new
            {
                employee.Id,
                employee.StaffId,
                employee.PreferredName,
                employee.OtherNames,
                employee.Surname,
                employment.JobTitle
            })
            .Take(200)
            .ToListAsync(ct);

        return rows
            .Select(row => new LeaveDelegateOption(
                row.Id,
                row.StaffId,
                ResolveDelegateDisplayName(row.PreferredName, row.OtherNames, row.Surname, row.StaffId),
                row.JobTitle))
            .ToList();
    }

    private static string ResolveDelegateDisplayName(
        string? preferredName, string? otherNames, string? surname, string staffId)
    {
        if (!string.IsNullOrWhiteSpace(preferredName))
            return preferredName.Trim();
        var composed = $"{otherNames} {surname}".Trim();
        return string.IsNullOrWhiteSpace(composed) ? staffId : composed;
    }

    async Task<IReadOnlyList<LeaveDelegateDivisionOption>> ILeaveRequestRepository.ListDelegateDivisionsAsync(
        Guid instituteId, Guid? excludeDivisionId, CancellationToken ct) => await Divisions.AsNoTracking()
        .Where(division => division.InstituteId == instituteId &&
            division.IsActive &&
            (!excludeDivisionId.HasValue || division.Id != excludeDivisionId.Value))
        .OrderBy(division => division.Name)
        .Select(division => new LeaveDelegateDivisionOption(division.Id, division.Name))
        .Take(100)
        .ToListAsync(ct);
    Task<LeaveRequest?> ILeaveRequestRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        LeaveRequests.FindAsync([id], ct).AsTask();

    async Task<ListSlice<LeaveRequest>> ILeaveRequestRepository.ListAsync(
        Guid? instituteScope, Guid? employeeId, string? status, string? leaveType,
        KeysetPage page, CancellationToken ct)
    {
        var query = LeaveRequests.AsNoTracking().AsQueryable();
        if (instituteScope.HasValue) query = query.Where(x => x.InstituteId == instituteScope.Value);
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(leaveType)) query = query.Where(x => x.LeaveType == leaveType);

        var ordered = page.Sort switch
        {
            "endDate" => page.Descending
                ? query.OrderByDescending(x => x.EndDate).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.EndDate).ThenBy(x => x.Id),
            "leaveType" => page.Descending
                ? query.OrderByDescending(x => x.LeaveType).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.LeaveType).ThenBy(x => x.Id),
            "status" => page.Descending
                ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            _ => page.Descending
                ? query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.StartDate).ThenBy(x => x.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, x => page.Sort switch
        {
            "endDate" => x.EndDate.ToString("O"),
            "leaveType" => x.LeaveType,
            "status" => x.Status,
            _ => x.StartDate.ToString("O")
        });
    }

    Task<bool> ILeaveRequestRepository.HasOverlappingActiveRequestAsync(
        Guid employeeId, DateTime startDate, DateTime endDate, Guid? excludeId, CancellationToken ct) =>
        LeaveRequests.AsNoTracking().AnyAsync(x =>
            x.EmployeeId == employeeId &&
            (!excludeId.HasValue || x.Id != excludeId.Value) &&
            x.Status != LeaveRequestStatuses.Draft &&
            x.Status != LeaveRequestStatuses.Rejected &&
            x.Status != LeaveRequestStatuses.Cancelled &&
            x.StartDate <= endDate.Date && x.EndDate >= startDate.Date, ct);

    Task<LeaveBalance?> ILeaveRequestRepository.FindBalanceAsync(
        Guid employeeId, string leaveType, short leaveYear, CancellationToken ct) =>
        LeaveBalances.SingleOrDefaultAsync(x =>
            x.EmployeeId == employeeId && x.LeaveType == leaveType && x.LeaveYear == leaveYear, ct);

    Task<LeavePolicy?> ILeaveRequestRepository.FindApplicablePolicyAsync(
        Guid instituteId, string leaveType, Guid? positionTypeId, DateTime onDate, CancellationToken ct) =>
        LeavePolicies.AsNoTracking()
            .Where(x => x.LeaveType == leaveType && x.EffectiveFrom <= onDate.Date &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= onDate.Date) &&
                (x.ScopeType == ScopeTypes.CsirWide || x.InstituteId == instituteId) &&
                (!x.PositionTypeId.HasValue || x.PositionTypeId == positionTypeId))
            .OrderByDescending(x => x.InstituteId.HasValue)
            .ThenByDescending(x => x.PositionTypeId.HasValue)
            .ThenByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

    async Task<IReadOnlyList<DateTime>> ILeaveRequestRepository.GetHolidayDatesAsync(
        Guid instituteId, DateTime startDate, DateTime endDate, CancellationToken ct) =>
        await Holidays.AsNoTracking()
            .Where(x => x.HolidayDate >= startDate.Date && x.HolidayDate <= endDate.Date &&
                (x.ScopeType == ScopeTypes.CsirWide || x.InstituteId == instituteId))
            .Select(x => x.HolidayDate)
            .ToListAsync(ct);

    async Task<short> ILeaveRequestRepository.NextApprovalSequenceAsync(
        Guid leaveRequestId, string approvalStage, CancellationToken ct) =>
        (short)(await LeaveRequestApprovals.CountAsync(x =>
            x.LeaveRequestId == leaveRequestId && x.ApprovalStage == approvalStage, ct) + 1);

    Task<LeaveResumption?> ILeaveRequestRepository.FindResumptionByRequestAsync(Guid leaveRequestId, CancellationToken ct) =>
        LeaveResumptions.SingleOrDefaultAsync(x => x.LeaveRequestId == leaveRequestId, ct);

    async Task<IReadOnlyList<LeaveBalance>> ILeaveRequestRepository.ListBalancesAsync(
        Guid employeeId, short leaveYear, CancellationToken ct) =>
        await LeaveBalances.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.LeaveYear == leaveYear)
            .OrderBy(x => x.LeaveType).ToListAsync(ct);

    async Task<IReadOnlyList<LeaveBalance>> ILeaveRequestRepository.ListTrackedBalancesAsync(
        IReadOnlyCollection<Guid> employeeIds, string leaveType, short leaveYear, CancellationToken ct)
    {
        if (employeeIds.Count == 0)
            return [];

        var uniqueIds = employeeIds.Distinct().ToArray();
        return await LeaveBalances
            .Where(balance => uniqueIds.Contains(balance.EmployeeId) &&
                              balance.LeaveType == leaveType &&
                              balance.LeaveYear == leaveYear)
            .ToListAsync(ct);
    }

    Task<LeaveApprovalScope?> ILeaveRequestRepository.GetApprovalScopeAsync(Guid employeeId, CancellationToken ct) =>
        EmploymentRecords.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.IsCurrent)
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new LeaveApprovalScope(x.EmployeeId, x.InstituteId, x.DivisionId, x.SectionId))
            .FirstOrDefaultAsync(ct);

    void ILeaveRequestRepository.Add(LeaveRequest request) => LeaveRequests.Add(request);
    void ILeaveRequestRepository.AddApproval(LeaveRequestApproval approval) => LeaveRequestApprovals.Add(approval);
    void ILeaveRequestRepository.AddBalance(LeaveBalance balance) => LeaveBalances.Add(balance);
    void ILeaveRequestRepository.AddResumption(LeaveResumption resumption) => LeaveResumptions.Add(resumption);
    void ILeaveRequestRepository.AddResumptionApproval(LeaveResumptionApproval approval) => LeaveResumptionApprovals.Add(approval);
}
