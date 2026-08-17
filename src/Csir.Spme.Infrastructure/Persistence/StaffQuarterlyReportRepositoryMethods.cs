using System.Data;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext
{
    private static readonly string[] HodIdentityRoles =
        ["HeadOfSection", "HeadOfDivision", "Director", "InstituteDirector"];

    private const string ScientificSecretaryRole = "ScientificSecretary";

    async Task<IApplicationTransaction> IStaffQuarterlyReportRepository.BeginSerializableTransactionAsync(
        CancellationToken ct) =>
        new EfApplicationTransaction(await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct));

    Task<Employee?> IStaffQuarterlyReportRepository.FindEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        Employees.AsNoTracking().SingleOrDefaultAsync(employee => employee.Id == employeeId, ct);

    async Task IStaffQuarterlyReportRepository.EnsureOpenCurrentYearQuartersAsync(
        Guid instituteId, int year, CancellationToken ct)
    {
        var quarterWindows = new (int Quarter, DateTime Start, DateTime End, string Name)[]
        {
            (1, new DateTime(year, 1, 1), new DateTime(year, 3, 31), $"First Quarter {year}"),
            (2, new DateTime(year, 4, 1), new DateTime(year, 6, 30), $"Second Quarter {year}"),
            (3, new DateTime(year, 7, 1), new DateTime(year, 9, 30), $"Third Quarter {year}"),
            (4, new DateTime(year, 10, 1), new DateTime(year, 12, 31), $"Fourth Quarter {year}")
        };

        var changed = false;
        foreach (var window in quarterWindows)
        {
            var code = $"{year}-Q{window.Quarter}";
            var existing = await ReportingPeriods
                .Where(period => period.PeriodType == ReportingPeriodTypes.Quarterly &&
                    period.ScopeType == ScopeTypes.Institute &&
                    period.InstituteId == instituteId &&
                    period.Code == code)
                .SingleOrDefaultAsync(ct);

            if (existing is null)
            {
                var created = ReportingPeriod.Create(
                    ScopeTypes.Institute,
                    instituteId,
                    code,
                    window.Name,
                    ReportingPeriodTypes.Quarterly,
                    window.Start,
                    window.End,
                    window.End.AddDays(15)).Value!;
                created.Open();
                ReportingPeriods.Add(created);
                changed = true;
                continue;
            }

            if (existing.Status == ReportingPeriodStatuses.Draft)
            {
                existing.Open();
                changed = true;
            }
        }

        if (changed)
            await SaveChangesAsync(ct);
    }

    async Task<IReadOnlyList<ReportingPeriod>> IStaffQuarterlyReportRepository.ListOpenQuarterlyPeriodsAsync(
        Guid instituteId, CancellationToken ct) => await ReportingPeriods.AsNoTracking()
        .Where(period => period.PeriodType == ReportingPeriodTypes.Quarterly &&
            period.Status == ReportingPeriodStatuses.Open &&
            (period.ScopeType == ScopeTypes.CsirWide || period.InstituteId == instituteId))
        .OrderByDescending(period => period.StartDate)
        .Take(20)
        .ToListAsync(ct);

    async Task<IReadOnlyList<Project>> IStaffQuarterlyReportRepository.ListProjectOptionsAsync(
        Guid instituteId, CancellationToken ct) => await Projects.AsNoTracking()
        .Where(project => project.InstituteId == instituteId &&
            project.Status != ProjectStatuses.Archived && project.Status != ProjectStatuses.Cancelled)
        .OrderBy(project => project.Name)
        .Take(200)
        .ToListAsync(ct);

    async Task<IReadOnlyList<Project>> IStaffQuarterlyReportRepository.ListEmployeeProjectOptionsAsync(
        Guid instituteId, Guid employeeId, Guid userId, CancellationToken ct) =>
        await Projects.AsNoTracking()
            .Where(project => project.InstituteId == instituteId &&
                project.Status != ProjectStatuses.Archived &&
                project.Status != ProjectStatuses.Cancelled &&
                (project.LeadEmployeeId == employeeId || project.CreatedByUserId == userId) &&
                ProjectInceptions.Any(inception => inception.ProjectId == project.Id))
            .OrderBy(project => project.Name)
            .Take(200)
            .ToListAsync(ct);

    async Task<IReadOnlyList<StaffQuarterlyFormOneSummary>> IStaffQuarterlyReportRepository.ListInstituteFormOneProjectsAsync(
        Guid instituteId, CancellationToken ct)
    {
        var rows = await (
            from project in Projects.AsNoTracking()
            join inception in ProjectInceptions.AsNoTracking() on project.Id equals inception.ProjectId
            where project.InstituteId == instituteId &&
                  project.Status != ProjectStatuses.Archived &&
                  project.Status != ProjectStatuses.Cancelled
            orderby project.Name
            select new { project.Id, project.Name, project.Pin, project.PinAssignedAt, inception.InceptionCompletedAt })
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(row => new StaffQuarterlyFormOneSummary(
            row.Id,
            row.Name,
            true,
            row.InceptionCompletedAt.HasValue,
            row.Pin,
            string.IsNullOrWhiteSpace(row.Pin) ? "pending" : "assigned",
            row.PinAssignedAt)).ToList();
    }

    Task<Csir.Spme.Domain.Org.Institute?> IStaffQuarterlyReportRepository.FindInstituteAsync(
        Guid instituteId, CancellationToken ct) =>
        Institutes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == instituteId, ct);

    async Task<IReadOnlyList<Technology>> IStaffQuarterlyReportRepository.ListTechnologyOptionsAsync(
        Guid instituteId, CancellationToken ct) => await Technologies.AsNoTracking()
        .Where(technology => technology.InstituteId == instituteId &&
            technology.Status != TechnologyStatuses.Archived)
        .OrderBy(technology => technology.Name)
        .Take(200)
        .ToListAsync(ct);

    async Task<IReadOnlyList<StaffQuarterlyReviewer>> IStaffQuarterlyReportRepository.ListReviewerOptionsAsync(
        Guid employeeId, Guid instituteId, CancellationToken ct)
    {
        var scope = await EmploymentRecords.AsNoTracking()
            .Where(record => record.EmployeeId == employeeId && record.InstituteId == instituteId && record.IsCurrent)
            .Select(record => new { record.SectionId, record.DivisionId })
            .SingleOrDefaultAsync(ct);
        if (scope is null)
            return [];

        if (scope.SectionId.HasValue)
        {
            var sectionHeads = await QueryHodCandidatesAsync(
                instituteId, employeeId, scope.SectionId, null, preferSectionLeadership: true, ct);
            if (sectionHeads.Count > 0)
                return MergeReviewers(sectionHeads, await QueryScientificSecretariesAsync(instituteId, employeeId, ct));
        }

        if (scope.DivisionId.HasValue)
        {
            var divisionHeads = await QueryHodCandidatesAsync(
                instituteId, employeeId, null, scope.DivisionId, preferSectionLeadership: false, ct);
            if (divisionHeads.Count > 0)
                return MergeReviewers(divisionHeads, await QueryScientificSecretariesAsync(instituteId, employeeId, ct));
        }

        return MergeReviewers(
            await QueryHodCandidatesAsync(instituteId, employeeId, null, null, preferSectionLeadership: false, ct),
            await QueryScientificSecretariesAsync(instituteId, employeeId, ct));
    }

    private static IReadOnlyList<StaffQuarterlyReviewer> MergeReviewers(
        IReadOnlyList<StaffQuarterlyReviewer> primary,
        IReadOnlyList<StaffQuarterlyReviewer> secondary)
    {
        var seen = primary.Select(item => item.User.Id).ToHashSet();
        return primary.Concat(secondary.Where(item => seen.Add(item.User.Id))).ToList();
    }

    private async Task<IReadOnlyList<StaffQuarterlyReviewer>> QueryScientificSecretariesAsync(
        Guid instituteId, Guid excludeEmployeeId, CancellationToken ct)
    {
        var identityMatches = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            join userRole in UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId &&
                  role.Name == ScientificSecretaryRole
            orderby employee.Surname, employee.OtherNames
            select new { User = user, Employee = employee })
            .Take(20)
            .ToListAsync(ct);

        var fromIdentity = identityMatches
            .GroupBy(item => item.User.Id)
            .Select(group => group.First())
            .Select(item => new StaffQuarterlyReviewer(item.User, item.Employee, ScientificSecretaryRole))
            .ToList();

        var employmentMatches = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            where user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId &&
                  employment.LeadershipRoles != null &&
                  (employment.LeadershipRoles.Contains("scientific secretary") ||
                   employment.LeadershipRoles.Contains("Scientific Secretary") ||
                   employment.LeadershipRoles.Contains("scientific-secretary") ||
                   employment.LeadershipRoles.Contains("ScientificSecretary"))
            orderby employee.Surname, employee.OtherNames
            select new { User = user, Employee = employee })
            .Take(20)
            .ToListAsync(ct);

        var employmentReviewers = employmentMatches
            .GroupBy(item => item.User.Id)
            .Select(group => group.First())
            .Select(item => new StaffQuarterlyReviewer(item.User, item.Employee, ScientificSecretaryRole))
            .ToList();

        return MergeReviewers(fromIdentity, employmentReviewers);
    }

    async Task<IReadOnlyList<StaffQuarterlyReviewer>> IStaffQuarterlyReportRepository.SearchStaffReviewerCandidatesAsync(
        Guid instituteId, Guid excludeEmployeeId, string? query, CancellationToken ct)
    {
        var term = string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();
        var matches = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            where user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId &&
                  (term == null ||
                   (user.DisplayName != null && user.DisplayName.ToLower().Contains(term)) ||
                   (employee.PreferredName != null && employee.PreferredName.ToLower().Contains(term)) ||
                   (employee.OtherNames != null && employee.OtherNames.ToLower().Contains(term)) ||
                   (employee.Surname != null && employee.Surname.ToLower().Contains(term)) ||
                   (employee.StaffId != null && employee.StaffId.ToLower().Contains(term)) ||
                   (user.Email != null && user.Email.ToLower().Contains(term)) ||
                   (employment.JobTitle != null && employment.JobTitle.ToLower().Contains(term)))
            orderby employee.Surname, employee.OtherNames
            select new { User = user, Employee = employee, employment.LeadershipRoles, employment.JobTitle })
            .Take(term is null ? 40 : 25)
            .ToListAsync(ct);

        var userIds = matches.Select(item => item.User.Id).Distinct().ToList();
        var rolesByUser = await (
            from userRole in UserRoles.AsNoTracking()
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(ct);

        return matches
            .GroupBy(item => item.User.Id)
            .Select(group => group.First())
            .Select(item =>
            {
                var roles = rolesByUser.Where(role => role.UserId == item.User.Id).Select(role => role.Role).ToList();
                return new StaffQuarterlyReviewer(
                    item.User,
                    item.Employee,
                    ResolveReviewerRole(roles, item.LeadershipRoles, item.JobTitle));
            })
            .OrderBy(item => item.Employee.Surname)
            .ThenBy(item => item.Employee.OtherNames)
            .ToList();
    }

    private async Task<IReadOnlyList<StaffQuarterlyReviewer>> QueryHodCandidatesAsync(
        Guid instituteId,
        Guid excludeEmployeeId,
        Guid? sectionId,
        Guid? divisionId,
        bool preferSectionLeadership,
        CancellationToken ct)
    {
        var matches = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            where user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId &&
                  (sectionId == null || employment.SectionId == sectionId) &&
                  (divisionId == null || employment.DivisionId == divisionId)
            orderby user.DisplayName
            select new { User = user, Employee = employee, employment.LeadershipRoles, employment.JobTitle })
            .Take(100)
            .ToListAsync(ct);

        if (matches.Count == 0)
            return [];

        var userIds = matches.Select(item => item.User.Id).Distinct().ToList();
        var rolesByUser = await (
            from userRole in UserRoles.AsNoTracking()
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(ct);

        var hods = matches
            .GroupBy(item => item.User.Id)
            .Select(group => group.First())
            .Select(item =>
            {
                var roles = rolesByUser.Where(role => role.UserId == item.User.Id).Select(role => role.Role).ToList();
                if (!IsHodCandidate(roles, item.LeadershipRoles, preferSectionLeadership, sectionId.HasValue, divisionId.HasValue))
                    return null;
                return new StaffQuarterlyReviewer(
                    item.User,
                    item.Employee,
                    ResolveReviewerRole(roles, item.LeadershipRoles, item.JobTitle));
            })
            .OfType<StaffQuarterlyReviewer>()
            .OrderBy(item => item.Employee.Surname)
            .ThenBy(item => item.Employee.OtherNames)
            .Take(50)
            .ToList();

        return hods;
    }

    private static bool IsHodCandidate(
        IReadOnlyList<string> roles,
        string? leadershipRoles,
        bool preferSectionLeadership,
        bool scopedToSection,
        bool scopedToDivision)
    {
        if (roles.Any(role => HodIdentityRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            return true;

        var leadership = leadershipRoles?.ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(leadership))
            return false;

        if (preferSectionLeadership || scopedToSection)
        {
            return leadership.Contains("head-of-section") ||
                   leadership.Contains("head of section") ||
                   leadership.Contains("section head");
        }

        if (scopedToDivision)
        {
            return leadership.Contains("head-of-division") ||
                   leadership.Contains("head of division") ||
                   leadership.Contains("division head") ||
                   leadership.Contains("director");
        }

        return leadership.Contains("head-of-section") ||
               leadership.Contains("head of section") ||
               leadership.Contains("head-of-division") ||
               leadership.Contains("head of division") ||
               leadership.Contains("director") ||
               leadership.Contains("section head") ||
               leadership.Contains("division head");
    }

    private static string ResolveReviewerRole(
        IReadOnlyList<string> roles, string? leadershipRoles, string? jobTitle)
    {
        if (roles.Contains("HeadOfSection", StringComparer.OrdinalIgnoreCase)) return "HeadOfSection";
        if (roles.Contains("HeadOfDivision", StringComparer.OrdinalIgnoreCase)) return "HeadOfDivision";
        if (roles.Contains(ScientificSecretaryRole, StringComparer.OrdinalIgnoreCase)) return ScientificSecretaryRole;
        if (roles.Contains("InstituteDirector", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("Director", StringComparer.OrdinalIgnoreCase))
            return "Director";

        var leadership = leadershipRoles?.ToLowerInvariant() ?? "";
        if (leadership.Contains("head-of-section") || leadership.Contains("head of section") ||
            leadership.Contains("section head"))
            return "HeadOfSection";
        if (leadership.Contains("head-of-division") || leadership.Contains("head of division") ||
            leadership.Contains("division head"))
            return "HeadOfDivision";
        if (leadership.Contains("director"))
            return "Director";

        return string.IsNullOrWhiteSpace(jobTitle) ? "Staff" : jobTitle.Trim();
    }

    async Task<StaffQuarterlyReviewer?> IStaffQuarterlyReportRepository.FindInstituteStaffReviewerAsync(
        Guid instituteId, Guid excludeEmployeeId, Guid reviewerUserId, CancellationToken ct)
    {
        var match = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            where user.Id == reviewerUserId &&
                  user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId
            select new { User = user, Employee = employee, employment.LeadershipRoles, employment.JobTitle })
            .FirstOrDefaultAsync(ct);
        if (match is null)
            return null;

        var roles = await (
            from userRole in UserRoles.AsNoTracking()
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == match.User.Id
            select role.Name!)
            .ToListAsync(ct);

        return new StaffQuarterlyReviewer(
            match.User,
            match.Employee,
            ResolveReviewerRole(roles, match.LeadershipRoles, match.JobTitle));
    }

    async Task<StaffQuarterlyReviewer?> IStaffQuarterlyReportRepository.FindEligibleReviewerAsync(
        Guid employeeId, Guid instituteId, Guid reviewerUserId, CancellationToken ct)
    {
        var options = await ((IStaffQuarterlyReportRepository)this)
            .ListReviewerOptionsAsync(employeeId, instituteId, ct);
        var preferred = options.SingleOrDefault(option => option.User.Id == reviewerUserId);
        if (preferred is not null)
            return preferred;

        var scientificSecretary = await FindScientificSecretaryReviewerAsync(
            instituteId, employeeId, reviewerUserId, ct);
        if (scientificSecretary is not null)
            return scientificSecretary;

        return await ((IStaffQuarterlyReportRepository)this)
            .FindInstituteStaffReviewerAsync(instituteId, employeeId, reviewerUserId, ct);
    }

    private async Task<StaffQuarterlyReviewer?> FindScientificSecretaryReviewerAsync(
        Guid instituteId, Guid excludeEmployeeId, Guid reviewerUserId, CancellationToken ct)
    {
        var match = await (
            from user in Users.AsNoTracking()
            join employee in Employees.AsNoTracking() on user.EmployeeId equals employee.Id
            join employment in EmploymentRecords.AsNoTracking() on employee.Id equals employment.EmployeeId
            join userRole in UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.Id == reviewerUserId &&
                  user.InstituteId == instituteId &&
                  user.AccountStatus == "active" &&
                  user.EmployeeId != null &&
                  user.EmployeeId != excludeEmployeeId &&
                  employee.ProfileStatus == EmployeeProfileStatuses.Active &&
                  employment.IsCurrent &&
                  employment.InstituteId == instituteId &&
                  role.Name == ScientificSecretaryRole
            select new { User = user, Employee = employee })
            .FirstOrDefaultAsync(ct);

        return match is null
            ? null
            : new StaffQuarterlyReviewer(match.User, match.Employee, ScientificSecretaryRole);
    }

    async Task<IReadOnlyList<StaffQuarterlyReportAggregate>> IStaffQuarterlyReportRepository.ListMineAsync(
        Guid employeeId, CancellationToken ct)
    {
        var ids = await Reports.AsNoTracking()
            .Where(report => report.ReportScope == ReportScopes.EmployeeQuarterly && report.OwnerEmployeeId == employeeId)
            .OrderByDescending(report => report.Id)
            .Select(report => report.Id)
            .Take(100)
            .ToListAsync(ct);
        return await LoadStaffQuarterlyAggregatesAsync(ids, false, ct);
    }

    async Task<IReadOnlyList<StaffQuarterlyReportAggregate>> IStaffQuarterlyReportRepository.ListForReviewerAsync(
        Guid reviewerUserId, Guid instituteId, CancellationToken ct)
    {
        var ids = await Reports.AsNoTracking()
            .Where(report => report.ReportScope == ReportScopes.EmployeeQuarterly &&
                report.ReviewerUserId == reviewerUserId && report.InstituteId == instituteId &&
                report.Status != ReportStatuses.Draft)
            .OrderByDescending(report => report.Id)
            .Select(report => report.Id)
            .Take(100)
            .ToListAsync(ct);
        return await LoadStaffQuarterlyAggregatesAsync(ids, false, ct);
    }

    async Task<IReadOnlyList<StaffQuarterlyReportAggregate>> IStaffQuarterlyReportRepository.ListCollationReportsAsync(
        Guid instituteId, Guid reportingPeriodId, CancellationToken ct)
    {
        var ids = await Reports.AsNoTracking()
            .Where(report => report.ReportScope == ReportScopes.EmployeeQuarterly &&
                             report.ReportingPeriodId == reportingPeriodId &&
                             (report.Status == ReportStatuses.Submitted || report.Status == ReportStatuses.Approved))
            .Join(Employees.AsNoTracking(), report => report.OwnerEmployeeId, employee => employee.Id,
                (report, employee) => new { report.Id, employee.InstituteId })
            .Where(item => item.InstituteId == instituteId)
            .Select(item => item.Id)
            .ToListAsync(ct);
        return await LoadStaffQuarterlyAggregatesAsync(ids, false, ct);
    }

    async Task<StaffQuarterlyReportAggregate?> IStaffQuarterlyReportRepository.FindAggregateAsync(
        Guid reportId, CancellationToken ct)
    {
        var values = await LoadStaffQuarterlyAggregatesAsync([reportId], true, ct);
        return values.SingleOrDefault();
    }

    Task<bool> IStaffQuarterlyReportRepository.StaffReportExistsAsync(
        Guid employeeId, Guid reportingPeriodId, Guid? excludeId, CancellationToken ct) =>
        Reports.AnyAsync(report => report.ReportScope == ReportScopes.EmployeeQuarterly &&
            report.OwnerEmployeeId == employeeId && report.ReportingPeriodId == reportingPeriodId &&
            report.ReportType == ReportTypes.StaffQuarterly &&
            (!excludeId.HasValue || report.Id != excludeId.Value), ct);

    async Task<IReadOnlyList<Project>> IStaffQuarterlyReportRepository.FindProjectsAsync(
        Guid instituteId, IReadOnlyCollection<Guid> projectIds, CancellationToken ct) =>
        await Projects.AsNoTracking().Where(project => project.InstituteId == instituteId &&
            projectIds.Contains(project.Id) && project.Status != ProjectStatuses.Archived &&
            project.Status != ProjectStatuses.Cancelled).ToListAsync(ct);

    async Task<IReadOnlyList<Technology>> IStaffQuarterlyReportRepository.FindTechnologiesAsync(
        Guid instituteId, IReadOnlyCollection<Guid> technologyIds, CancellationToken ct) =>
        await Technologies.AsNoTracking().Where(technology => technology.InstituteId == instituteId &&
            technologyIds.Contains(technology.Id) && technology.Status != TechnologyStatuses.Archived).ToListAsync(ct);

    Task<Project?> IStaffQuarterlyReportRepository.FindProjectForUpdateAsync(
        Guid instituteId, Guid projectId, CancellationToken ct) =>
        Projects.SingleOrDefaultAsync(project => project.InstituteId == instituteId && project.Id == projectId, ct);

    async Task<ProjectInception?> IStaffQuarterlyReportRepository.FindProjectInceptionForUpdateAsync(
        Guid projectId, CancellationToken ct) =>
        await ProjectInceptions.SingleOrDefaultAsync(item => item.ProjectId == projectId, ct);

    Task<FileRecord?> IStaffQuarterlyReportRepository.FindFileRecordAsync(Guid fileId, CancellationToken ct) =>
        FileRecords.AsNoTracking().SingleOrDefaultAsync(file => file.Id == fileId && !file.IsDeleted, ct);

    Task<FileRecord?> IStaffQuarterlyReportRepository.FindFileRecordForUpdateAsync(
        Guid fileId, CancellationToken ct) =>
        FileRecords.SingleOrDefaultAsync(file => file.Id == fileId && !file.IsDeleted, ct);

    async Task<IReadOnlyList<FileRecord>> IStaffQuarterlyReportRepository.FindFileRecordsAsync(
        IReadOnlyCollection<Guid> fileIds, CancellationToken ct) =>
        fileIds.Count == 0
            ? []
            : await FileRecords.AsNoTracking().Where(file => fileIds.Contains(file.Id) && !file.IsDeleted).ToListAsync(ct);

    Task<Project?> IStaffQuarterlyReportRepository.FindProjectByIdAsync(
        Guid instituteId, Guid projectId, CancellationToken ct) => Projects.AsNoTracking()
        .SingleOrDefaultAsync(project => project.InstituteId == instituteId && project.Id == projectId, ct);

    async Task<ProjectInception?> IStaffQuarterlyReportRepository.FindProjectInceptionAsync(
        Guid projectId, CancellationToken ct) =>
        await ProjectInceptions.AsNoTracking().SingleOrDefaultAsync(item => item.ProjectId == projectId, ct);

    async Task<IReadOnlyDictionary<Guid, ProjectInception>> IStaffQuarterlyReportRepository.FindProjectInceptionsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken ct) =>
        await ProjectInceptions.AsNoTracking().Where(item => projectIds.Contains(item.ProjectId))
            .ToDictionaryAsync(item => item.ProjectId, ct);

    Task<StaffQuarterlyReportUploadSession?> IStaffQuarterlyReportRepository.FindUploadSessionAsync(
        Guid sessionId, CancellationToken ct) =>
        StaffQuarterlyReportUploadSessions.SingleOrDefaultAsync(item => item.Id == sessionId, ct);

    Task<int> IStaffQuarterlyReportRepository.CountReportImagesAsync(Guid reportId, CancellationToken ct) =>
        ReportAttachments.CountAsync(item => item.ReportId == reportId &&
            item.AttachmentType == StaffReportAttachmentTypes.ReportImage, ct);

    async Task<bool> IStaffQuarterlyReportRepository.CanReadProjectAsync(
        Guid projectId, Guid employeeId, Guid? reviewerUserId, CancellationToken ct)
    {
        var project = await Projects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == projectId, ct);
        if (project is null)
            return false;

        if (project.LeadEmployeeId == employeeId)
            return true;

        if (reviewerUserId.HasValue && project.CreatedByUserId == reviewerUserId)
            return true;

        if (reviewerUserId.HasValue)
        {
            var isScientificSecretary = await (
                from userRole in UserRoles.AsNoTracking()
                join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == reviewerUserId &&
                      role.Name == ScientificSecretaryRole
                select userRole.UserId).AnyAsync(ct);
            if (isScientificSecretary && await Users.AsNoTracking().AnyAsync(user =>
                user.Id == reviewerUserId && user.InstituteId == project.InstituteId, ct) &&
                await ProjectInceptions.AsNoTracking().AnyAsync(item => item.ProjectId == projectId, ct))
                return true;
        }

        if (await Reports.AnyAsync(report => report.ReportScope == ReportScopes.EmployeeQuarterly &&
            report.OwnerEmployeeId == employeeId && report.InstituteId == project.InstituteId &&
            ReportProjects.Any(link => link.ReportId == report.Id && link.ProjectId == projectId), ct))
            return true;

        if (reviewerUserId.HasValue && await Reports.AnyAsync(report =>
            report.ReportScope == ReportScopes.EmployeeQuarterly &&
            report.ReviewerUserId == reviewerUserId &&
            report.InstituteId == project.InstituteId &&
            report.Status != ReportStatuses.Draft &&
            ReportProjects.Any(link => link.ReportId == report.Id && link.ProjectId == projectId), ct))
            return true;

        return false;
    }

    void IStaffQuarterlyReportRepository.Add(ProjectInception inception) => ProjectInceptions.Add(inception);
    void IStaffQuarterlyReportRepository.Add(ReportAttachment attachment) => ReportAttachments.Add(attachment);
    void IStaffQuarterlyReportRepository.Add(StaffQuarterlyReportUploadSession session) =>
        StaffQuarterlyReportUploadSessions.Add(session);
    void IStaffQuarterlyReportRepository.Add(FileRecord file) => FileRecords.Add(file);
    void IStaffQuarterlyReportRepository.RemoveAttachment(ReportAttachment attachment) =>
        ReportAttachments.Remove(attachment);

    Task<Project?> IStaffQuarterlyReportRepository.FindProjectByCodeOrNameAsync(
        Guid instituteId, string code, string name, CancellationToken ct) => Projects.AsNoTracking()
        .FirstOrDefaultAsync(project => project.InstituteId == instituteId &&
            (project.Code == code || project.Name == name), ct);

    Task<Project?> IStaffQuarterlyReportRepository.FindProjectByNameForEmployeeAsync(
        Guid instituteId, Guid employeeId, Guid userId, string name, CancellationToken ct) =>
        Projects.AsNoTracking()
            .FirstOrDefaultAsync(project => project.InstituteId == instituteId &&
                project.Name == name &&
                (project.LeadEmployeeId == employeeId || project.CreatedByUserId == userId), ct);

    Task<bool> IStaffQuarterlyReportRepository.PinExistsAsync(
        Guid instituteId, string pin, Guid? excludeProjectId, CancellationToken ct) =>
        Projects.AnyAsync(project => project.InstituteId == instituteId &&
            project.Pin == pin &&
            (!excludeProjectId.HasValue || project.Id != excludeProjectId.Value), ct);

    Task<Project?> IStaffQuarterlyReportRepository.FindProjectForPinAssignmentAsync(
        Guid instituteId, Guid projectId, CancellationToken ct) =>
        Projects.SingleOrDefaultAsync(project => project.InstituteId == instituteId && project.Id == projectId, ct);

    Task<Technology?> IStaffQuarterlyReportRepository.FindTechnologyByCodeOrNameAsync(
        Guid instituteId, string code, string name, CancellationToken ct) => Technologies.AsNoTracking()
        .FirstOrDefaultAsync(technology => technology.InstituteId == instituteId &&
            (technology.Code == code || technology.Name == name), ct);

    void IStaffQuarterlyReportRepository.Add(Report report) => Reports.Add(report);
    void IStaffQuarterlyReportRepository.Add(Project project) => Projects.Add(project);
    void IStaffQuarterlyReportRepository.Add(Technology technology) => Technologies.Add(technology);

    void IStaffQuarterlyReportRepository.ReplaceProjects(
        Guid reportId, IReadOnlyCollection<SaveStaffQuarterlyProjectProgressCommand> projectProgress)
    {
        var existing = ReportProjects.Where(link => link.ReportId == reportId).ToList();
        var incomingIds = projectProgress.Select(item => item.ProjectId).Distinct().ToHashSet();
        ReportProjects.RemoveRange(existing.Where(link => !incomingIds.Contains(link.ProjectId)));
        foreach (var progress in projectProgress)
        {
            var link = existing.SingleOrDefault(item => item.ProjectId == progress.ProjectId);
            if (link is null)
            {
                link = new ReportProject(reportId, progress.ProjectId);
                ReportProjects.Add(link);
            }

            link.UpdateProgress(progress.ProgressSummary, progress.ProgressKeyResults, progress.Challenges,
                progress.NextQuarterActivities, progress.WayForward, progress.ConferencePapersProduced,
                progress.IpTechnologiesProtected);
        }
    }

    void IStaffQuarterlyReportRepository.ReplaceTechnologies(Guid reportId, IReadOnlyCollection<Guid> technologyIds)
    {
        ReportTechnologies.RemoveRange(ReportTechnologies.Where(link => link.ReportId == reportId));
        ReportTechnologies.AddRange(technologyIds.Distinct().Select(technologyId => new ReportTechnology(reportId, technologyId)));
    }

    private async Task<IReadOnlyList<StaffQuarterlyReportAggregate>> LoadStaffQuarterlyAggregatesAsync(
        IReadOnlyCollection<Guid> ids, bool trackReport, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];

        IQueryable<Report> reportsQuery = Reports.Where(report => ids.Contains(report.Id) &&
            report.ReportScope == ReportScopes.EmployeeQuarterly);
        if (!trackReport)
            reportsQuery = reportsQuery.AsNoTracking();
        var reports = await reportsQuery.ToListAsync(ct);
        if (reports.Count == 0)
            return [];

        var periodIds = reports.Select(report => report.ReportingPeriodId).Distinct().ToList();
        var employeeIds = reports.SelectMany(report => new[] { report.OwnerEmployeeId, report.ReviewerEmployeeId })
            .OfType<Guid>().Distinct().ToList();
        var reviewerUserIds = reports.Select(report => report.ReviewerUserId).OfType<Guid>().Distinct().ToList();
        var periods = await ReportingPeriods.AsNoTracking().Where(period => periodIds.Contains(period.Id))
            .ToDictionaryAsync(period => period.Id, ct);
        var employees = await Employees.AsNoTracking().Where(employee => employeeIds.Contains(employee.Id))
            .ToDictionaryAsync(employee => employee.Id, ct);
        var users = await Users.AsNoTracking().Where(user => reviewerUserIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, ct);
        var roles = await (
            from userRole in UserRoles.AsNoTracking()
            join role in Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where reviewerUserIds.Contains(userRole.UserId) &&
                (role.Name == "HeadOfSection" || role.Name == "HeadOfDivision" ||
                 role.Name == "Director" || role.Name == "InstituteDirector" ||
                 role.Name == ScientificSecretaryRole)
            select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(ct);
        var roleByUser = roles
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => ResolveReviewerRole(
                group.Select(item => item.Role).ToList(), null, null));
        IQueryable<ReportProject> projectLinksQuery = ReportProjects.Where(link => ids.Contains(link.ReportId));
        IQueryable<ReportTechnology> technologyLinksQuery = ReportTechnologies.Where(link => ids.Contains(link.ReportId));
        if (!trackReport)
        {
            projectLinksQuery = projectLinksQuery.AsNoTracking();
            technologyLinksQuery = technologyLinksQuery.AsNoTracking();
        }
        var projectLinks = await projectLinksQuery.ToListAsync(ct);
        var technologyLinks = await technologyLinksQuery.ToListAsync(ct);
        var projectIds = projectLinks.Select(link => link.ProjectId).Distinct().ToList();
        var technologyIds = technologyLinks.Select(link => link.TechnologyId).Distinct().ToList();
        var projects = await Projects.AsNoTracking().Where(project => projectIds.Contains(project.Id)).ToListAsync(ct);
        var technologies = await Technologies.AsNoTracking().Where(technology => technologyIds.Contains(technology.Id)).ToListAsync(ct);
        var attachments = await ReportAttachments.AsNoTracking().Where(item => ids.Contains(item.ReportId)).ToListAsync(ct);
        var attachmentFileIds = attachments.Select(item => item.FileId).Distinct().ToList();
        var attachmentFiles = attachmentFileIds.Count == 0
            ? []
            : await FileRecords.AsNoTracking().Where(file => attachmentFileIds.Contains(file.Id)).ToListAsync(ct);

        return reports.OrderByDescending(report => report.CreatedAt).Select(report =>
        {
            var owner = employees[report.OwnerEmployeeId!.Value];
            var reviewerEmployee = employees[report.ReviewerEmployeeId!.Value];
            var reviewerUser = users[report.ReviewerUserId!.Value];
            var ownProjectLinks = projectLinks.Where(link => link.ReportId == report.Id).ToList();
            var ownTechnologyLinks = technologyLinks.Where(link => link.ReportId == report.Id).ToList();
            var ownAttachments = attachments.Where(item => item.ReportId == report.Id).ToList();
            var ownAttachmentFiles = attachmentFiles.Where(file =>
                ownAttachments.Any(item => item.FileId == file.Id)).ToList();
            return new StaffQuarterlyReportAggregate(
                report,
                periods[report.ReportingPeriodId],
                owner,
                new StaffQuarterlyReviewer(reviewerUser, reviewerEmployee,
                    roleByUser.GetValueOrDefault(reviewerUser.Id, "Staff")),
                ownProjectLinks,
                projects.Where(project => ownProjectLinks.Any(link => link.ProjectId == project.Id)).ToList(),
                ownTechnologyLinks,
                technologies.Where(technology => ownTechnologyLinks.Any(link => link.TechnologyId == technology.Id)).ToList(),
                ownAttachments,
                ownAttachmentFiles);
        }).ToList();
    }

    private sealed class EfApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
