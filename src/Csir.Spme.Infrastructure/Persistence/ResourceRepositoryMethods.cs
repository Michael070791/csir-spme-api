using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext
{
    Task<ReportingPeriod?> IReportingPeriodRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        ReportingPeriods.FindAsync([id], ct).AsTask();

    Task<bool> IReportingPeriodRepository.CodeExistsAsync(
        string scopeType, Guid? instituteId, string code, Guid? excludeId, CancellationToken ct) =>
        ReportingPeriods.AnyAsync(period =>
            period.ScopeType == scopeType &&
            period.InstituteId == instituteId &&
            period.Code == code &&
            (!excludeId.HasValue || period.Id != excludeId.Value), ct);

    async Task<ListSlice<ReportingPeriod>> IReportingPeriodRepository.ListAsync(
        Guid? instituteScope, string? periodType, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = ReportingPeriods.AsNoTracking().AsQueryable();
        if (instituteScope.HasValue)
        {
            query = query.Where(period =>
                period.ScopeType == Csir.Spme.Domain.Constants.ScopeTypes.CsirWide ||
                period.InstituteId == instituteScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(periodType))
        {
            query = query.Where(period => period.PeriodType == periodType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(period => period.Status == status);
        }

        var ordered = page.Sort switch
        {
            "code" => page.Descending
                ? query.OrderByDescending(period => period.Code).ThenByDescending(period => period.Id)
                : query.OrderBy(period => period.Code).ThenBy(period => period.Id),
            "status" => page.Descending
                ? query.OrderByDescending(period => period.Status).ThenByDescending(period => period.Id)
                : query.OrderBy(period => period.Status).ThenBy(period => period.Id),
            _ => page.Descending
                ? query.OrderByDescending(period => period.StartDate).ThenByDescending(period => period.Id)
                : query.OrderBy(period => period.StartDate).ThenBy(period => period.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, period => page.Sort switch
        {
            "code" => period.Code,
            "status" => period.Status,
            _ => period.StartDate.ToString("O")
        });
    }

    void IReportingPeriodRepository.Add(ReportingPeriod period) => ReportingPeriods.Add(period);

    Task<Report?> IReportRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Reports.SingleOrDefaultAsync(report =>
            report.Id == id && report.ReportScope == ReportScopes.Institute, ct);

    Task<bool> IReportRepository.DuplicateExistsAsync(
        Guid instituteId, Guid reportingPeriodId, string reportType, Guid? excludeId, CancellationToken ct) =>
        Reports.AnyAsync(report =>
            report.ReportScope == ReportScopes.Institute &&
            report.InstituteId == instituteId &&
            report.ReportingPeriodId == reportingPeriodId &&
            report.ReportType == reportType &&
            (!excludeId.HasValue || report.Id != excludeId.Value), ct);

    Task<bool> IReportRepository.HasMetricsAsync(Guid reportId, CancellationToken ct) =>
        ReportMetrics.AnyAsync(metric => metric.ReportId == reportId, ct);

    async Task<ListSlice<Report>> IReportRepository.ListAsync(
        Guid? instituteScope, string? reportType, string? status, Guid? reportingPeriodId, KeysetPage page, CancellationToken ct)
    {
        var query = Reports.AsNoTracking()
            .Where(report => report.ReportScope == ReportScopes.Institute);
        if (instituteScope.HasValue)
        {
            query = query.Where(report => report.InstituteId == instituteScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(report => report.ReportType == reportType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(report => report.Status == status);
        }

        if (reportingPeriodId.HasValue)
        {
            query = query.Where(report => report.ReportingPeriodId == reportingPeriodId.Value);
        }

        var ordered = page.Sort switch
        {
            "reportType" => page.Descending
                ? query.OrderByDescending(report => report.ReportType).ThenByDescending(report => report.Id)
                : query.OrderBy(report => report.ReportType).ThenBy(report => report.Id),
            "status" => page.Descending
                ? query.OrderByDescending(report => report.Status).ThenByDescending(report => report.Id)
                : query.OrderBy(report => report.Status).ThenBy(report => report.Id),
            _ => page.Descending
                ? query.OrderByDescending(report => report.Title).ThenByDescending(report => report.Id)
                : query.OrderBy(report => report.Title).ThenBy(report => report.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, report => page.Sort switch
        {
            "reportType" => report.ReportType,
            "status" => report.Status,
            _ => report.Title
        });
    }

    void IReportRepository.Add(Report report) => Reports.Add(report);

    void IReportRepository.Remove(Report report) => Reports.Remove(report);

    Task<Technology?> ITechnologyRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Technologies.FindAsync([id], ct).AsTask();

    Task<bool> ITechnologyRepository.CodeExistsAsync(Guid instituteId, string code, Guid? excludeId, CancellationToken ct) =>
        Technologies.AnyAsync(technology =>
            technology.InstituteId == instituteId &&
            technology.Code == code &&
            (!excludeId.HasValue || technology.Id != excludeId.Value), ct);

    Task<bool> ITechnologyRepository.HasReferencesAsync(Guid technologyId, CancellationToken ct) =>
        Publications.AnyAsync(publication => publication.TechnologyId == technologyId, ct);

    async Task<ListSlice<Technology>> ITechnologyRepository.ListAsync(
        Guid? instituteScope, string? status, string? technologyType, KeysetPage page, CancellationToken ct)
    {
        var query = Technologies.AsNoTracking().AsQueryable();
        if (instituteScope.HasValue)
        {
            query = query.Where(technology => technology.InstituteId == instituteScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(technology => technology.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(technologyType))
        {
            query = query.Where(technology => technology.TechnologyType == technologyType);
        }

        var ordered = page.Sort switch
        {
            "code" => page.Descending
                ? query.OrderByDescending(technology => technology.Code).ThenByDescending(technology => technology.Id)
                : query.OrderBy(technology => technology.Code).ThenBy(technology => technology.Id),
            "status" => page.Descending
                ? query.OrderByDescending(technology => technology.Status).ThenByDescending(technology => technology.Id)
                : query.OrderBy(technology => technology.Status).ThenBy(technology => technology.Id),
            _ => page.Descending
                ? query.OrderByDescending(technology => technology.Name).ThenByDescending(technology => technology.Id)
                : query.OrderBy(technology => technology.Name).ThenBy(technology => technology.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, technology => page.Sort switch
        {
            "code" => technology.Code,
            "status" => technology.Status,
            _ => technology.Name
        });
    }

    void ITechnologyRepository.Add(Technology technology) => Technologies.Add(technology);

    void ITechnologyRepository.Remove(Technology technology) => Technologies.Remove(technology);

    Task<Project?> IProjectRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Projects.FindAsync([id], ct).AsTask();

    Task<bool> IProjectRepository.CodeExistsAsync(Guid instituteId, string code, Guid? excludeId, CancellationToken ct) =>
        Projects.AnyAsync(project =>
            project.InstituteId == instituteId &&
            project.Code == code &&
            (!excludeId.HasValue || project.Id != excludeId.Value), ct);

    async Task<bool> IProjectRepository.HasDependenciesAsync(Guid projectId, CancellationToken ct) =>
        await ProjectMilestones.AnyAsync(milestone => milestone.ProjectId == projectId, ct) ||
        await ProjectFundings.AnyAsync(funding => funding.ProjectId == projectId, ct) ||
        await ProjectUpdates.AnyAsync(update => update.ProjectId == projectId, ct) ||
        await ProjectSponsors.AnyAsync(sponsor => sponsor.ProjectId == projectId, ct);

    async Task<Guid?> IProjectRepository.GetThrustInstituteAsync(Guid thrustId, CancellationToken ct) =>
        await Thrusts.AsNoTracking()
            .Where(thrust => thrust.Id == thrustId)
            .Select(thrust => (Guid?)thrust.InstituteId)
            .FirstOrDefaultAsync(ct);

    async Task<ListSlice<Project>> IProjectRepository.ListAsync(
        Guid? instituteScope, string? status, string? nature, Guid? leadEmployeeId, Guid? thrustId, KeysetPage page, CancellationToken ct)
    {
        var query = Projects.AsNoTracking().AsQueryable();
        if (instituteScope.HasValue)
        {
            query = query.Where(project => project.InstituteId == instituteScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(project => project.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(nature))
        {
            query = query.Where(project => project.Nature == nature);
        }

        if (leadEmployeeId.HasValue)
        {
            query = query.Where(project => project.LeadEmployeeId == leadEmployeeId.Value);
        }

        if (thrustId.HasValue)
        {
            query = query.Where(project => project.ThrustId == thrustId.Value);
        }

        var ordered = page.Sort switch
        {
            "name" => page.Descending
                ? query.OrderByDescending(project => project.Name).ThenByDescending(project => project.Id)
                : query.OrderBy(project => project.Name).ThenBy(project => project.Id),
            "startDate" => page.Descending
                ? query.OrderByDescending(project => project.StartDate).ThenByDescending(project => project.Id)
                : query.OrderBy(project => project.StartDate).ThenBy(project => project.Id),
            "status" => page.Descending
                ? query.OrderByDescending(project => project.Status).ThenByDescending(project => project.Id)
                : query.OrderBy(project => project.Status).ThenBy(project => project.Id),
            _ => page.Descending
                ? query.OrderByDescending(project => project.Code).ThenByDescending(project => project.Id)
                : query.OrderBy(project => project.Code).ThenBy(project => project.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, project => page.Sort switch
        {
            "name" => project.Name,
            "startDate" => project.StartDate.ToString("O"),
            "status" => project.Status,
            _ => project.Code
        });
    }

    void IProjectRepository.Add(Project project) => Projects.Add(project);

    void IProjectRepository.Remove(Project project) => Projects.Remove(project);
}
