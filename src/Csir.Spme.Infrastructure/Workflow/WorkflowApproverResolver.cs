using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Workflow;

public sealed class WorkflowApproverResolver(SpmeDbContext db) : IWorkflowApproverResolver
{
    public async Task<IReadOnlyList<WorkflowApproverContact>> FindStageApproversAsync(
        Guid instituteId,
        Guid employeeId,
        string approvalStage,
        CancellationToken ct = default)
    {
        var roleName = approvalStage switch
        {
            LeaveApprovalStages.SectionHead => "HeadOfSection",
            LeaveApprovalStages.HeadOfDivision => "HeadOfDivision",
            LeaveApprovalStages.AdminDirector => "HeadOfAdmin",
            LeaveApprovalStages.InstituteDirector => "InstituteDirector",
            _ => null
        };
        if (roleName is null)
            return [];

        var target = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.IsCurrent)
            .OrderByDescending(record => record.EffectiveFrom)
            .Select(record => new { record.InstituteId, record.DivisionId, record.SectionId })
            .FirstOrDefaultAsync(ct);
        if (target is null || target.InstituteId != instituteId)
            return [];

        var candidates = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join employment in db.EmploymentRecords.AsNoTracking() on user.EmployeeId equals employment.EmployeeId
            where user.Email != null &&
                  user.EmployeeId != null &&
                  user.EmployeeId != employeeId &&
                  role.Name == roleName &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId
            select new
            {
                user.Id,
                Email = user.Email!,
                user.DisplayName,
                user.PhoneNumber,
                employment.DivisionId,
                employment.SectionId
            })
            .ToListAsync(ct);

        IEnumerable<WorkflowApproverContact> matched = approvalStage switch
        {
            LeaveApprovalStages.SectionHead when target.SectionId is Guid sectionId =>
                candidates.Where(candidate => candidate.SectionId == sectionId)
                    .Select(candidate => new WorkflowApproverContact(
                        candidate.Id, candidate.Email, candidate.DisplayName, candidate.PhoneNumber)),
            LeaveApprovalStages.HeadOfDivision when target.DivisionId is Guid divisionId =>
                candidates.Where(candidate => candidate.DivisionId == divisionId)
                    .Select(candidate => new WorkflowApproverContact(
                        candidate.Id, candidate.Email, candidate.DisplayName, candidate.PhoneNumber)),
            LeaveApprovalStages.AdminDirector or LeaveApprovalStages.InstituteDirector =>
                candidates.Select(candidate => new WorkflowApproverContact(
                    candidate.Id, candidate.Email, candidate.DisplayName, candidate.PhoneNumber)),
            _ => []
        };

        return matched
            .GroupBy(candidate => candidate.Email, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<string>> BuildSkeletalStaffChainAsync(
        Guid instituteId,
        Guid employeeId,
        CancellationToken ct = default)
    {
        var employment = await db.EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.IsCurrent)
            .OrderByDescending(record => record.EffectiveFrom)
            .Select(record => new { record.DivisionId, record.SectionId })
            .FirstOrDefaultAsync(ct);
        if (employment is null)
            return [];

        var chain = new List<string>();
        if (employment.SectionId.HasValue)
        {
            var sectionApprovers = await FindStageApproversAsync(
                instituteId, employeeId, LeaveApprovalStages.SectionHead, ct);
            if (sectionApprovers.Count > 0)
                chain.Add(LeaveApprovalStages.SectionHead);
        }

        if (employment.DivisionId.HasValue)
            chain.Add(LeaveApprovalStages.HeadOfDivision);

        chain.Add(LeaveApprovalStages.AdminDirector);
        chain.Add(LeaveApprovalStages.InstituteDirector);

        var requesterUserId = await db.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employeeId)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct);

        if (!requesterUserId.HasValue)
            return chain;

        var filtered = new List<string>();
        foreach (var stage in chain)
        {
            var approvers = await FindStageApproversAsync(instituteId, employeeId, stage, ct);
            if (approvers.Any(approver => approver.UserId == requesterUserId.Value))
                continue;
            filtered.Add(stage);
        }

        return filtered;
    }
}
