using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Reporting;

public sealed class ReportService
{
    private static readonly string[] AllowedSorts = ["title", "reportType", "status"];

    private readonly IReportRepository _reports;
    private readonly IReportingPeriodRepository _periods;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IWorkflowNotificationOutbox _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public ReportService(
        IReportRepository reports,
        IReportingPeriodRepository periods,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        IWorkflowNotificationOutbox notifications,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _reports = reports;
        _periods = periods;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<ReportDto>>> ListAsync(
        string? instituteFilter, string? reportType, string? status, Guid? reportingPeriodId,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        if (!_currentUser.InstituteId.HasValue && !CanAccessAllInstitutes())
        {
            return Result<ListSlice<ReportDto>>.Failure(Error.Forbidden(
                "An institute scope is required to list reports."));
        }

        Guid? resolvedInstituteId = null;
        if (!string.IsNullOrWhiteSpace(instituteFilter))
        {
            resolvedInstituteId = await _institutes.ResolveInstituteIdAsync(instituteFilter, ct);
            if (!resolvedInstituteId.HasValue)
                return Result<ListSlice<ReportDto>>.Failure(Error.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["instituteId"] = ["Institute filter must identify an active institute."]
                    }));
        }

        var scope = InstituteScope.Resolve(_currentUser.InstituteId, resolvedInstituteId);
        if (scope.IsFailure)
        {
            return Result<ListSlice<ReportDto>>.Failure(scope.Error!);
        }

        var fields = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(reportType) && !DomainValues.Contains(ReportTypes.InstituteReportTypes, reportType.Trim()))
        {
            fields["filter[reportType]"] = [$"Report type must be one of: {string.Join(", ", ReportTypes.InstituteReportTypes)}."];
        }

        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(ReportStatuses.All, status.Trim()))
        {
            fields["filter[status]"] = [$"Status must be one of: {string.Join(", ", ReportStatuses.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<ListSlice<ReportDto>>.Failure(Error.Validation(fields));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "title", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<ReportDto>>.Failure(page.Error!);
        }

        var slice = await _reports.ListAsync(scope.Value!.EffectiveFilter,
            reportType?.Trim(), status?.Trim(), reportingPeriodId, page.Value!, ct);
        return Result<ListSlice<ReportDto>>.Success(Map(slice));
    }

    public async Task<Result<ReportDto>> GetAsync(Guid id, CancellationToken ct)
    {
        if (!_currentUser.InstituteId.HasValue && !CanAccessAllInstitutes())
            return Result<ReportDto>.Failure(Error.Forbidden(
                "An institute scope is required to access reports."));

        var report = await _reports.FindByIdAsync(id, ct);
        if (!IsAccessible(report))
        {
            return Result<ReportDto>.Failure(Error.NotFound("Report not found."));
        }

        return Result<ReportDto>.Success(Map(report!));
    }

    public async Task<Result<ReportDto>> CreateAsync(CreateReportCommand command, CancellationToken ct)
    {
        var fields = ValidateContent(command.ReportType, command.Title, command.Summary);
        if (fields.Count > 0)
        {
            return Result<ReportDto>.Failure(Error.Validation(fields));
        }

        var period = await _periods.FindByIdAsync(command.ReportingPeriodId, ct);
        if (period is null)
        {
            return Result<ReportDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["reportingPeriodId"] = ["The reporting period does not exist."]
            }));
        }

        // Ownership: the period fixes the owning institute unless it is csir-wide, in which
        // case the caller's scope (or explicit institute) owns the report.
        var callerScope = InstituteScope.Resolve(_currentUser.InstituteId, command.InstituteId);
        if (callerScope.IsFailure)
        {
            return Result<ReportDto>.Failure(callerScope.Error!);
        }

        Guid instituteId;
        if (period.ScopeType == ScopeTypes.CsirWide)
        {
            if (!callerScope.Value!.EffectiveFilter.HasValue)
            {
                return Result<ReportDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["instituteId"] = ["An owning institute is required when the reporting period is csir-wide."]
                }));
            }

            instituteId = callerScope.Value.EffectiveFilter.Value;
        }
        else
        {
            instituteId = period.InstituteId!.Value;
            if (command.InstituteId.HasValue && command.InstituteId.Value != instituteId)
            {
                return Result<ReportDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["instituteId"] = ["The institute must match the reporting period's institute."]
                }));
            }
        }

        if (!callerScope.Value!.CanAccess(instituteId))
        {
            return Result<ReportDto>.Failure(Error.CrossInstitute(
                "You are not authorized to create reports for that institute."));
        }

        var reportType = command.ReportType.Trim();
        if (await _reports.DuplicateExistsAsync(instituteId, period.Id, reportType, null, ct))
        {
            return Result<ReportDto>.Failure(Error.Conflict(
                "A report of this type already exists for the institute and reporting period."));
        }

        var report = Report.Create(instituteId, period.Id, reportType, command.Title.Trim(),
            command.Summary.Trim(), command.Abstract, command.KeyResults, command.Conclusion);
        _reports.Add(report);
        await _audit.RecordAsync("report.created", "Report", report.Id.ToString(), null,
            $"type={report.ReportType};period={report.ReportingPeriodId}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ReportDto>.Success(Map(report));
    }

    public async Task<Result<ReportDto>> UpdateAsync(Guid id, UpdateReportCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var report = await _reports.FindByIdAsync(id, ct);
        if (!IsAccessible(report))
        {
            return Result<ReportDto>.Failure(Error.NotFound("Report not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<ReportDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var fields = ValidateContent(null, command.Title, command.Summary);
        if (fields.Count > 0)
        {
            return Result<ReportDto>.Failure(Error.Validation(fields));
        }

        var before = Snapshot(report!);
        var updated = report!.Update(command.Title.Trim(), command.Summary.Trim(),
            command.Abstract, command.KeyResults, command.Conclusion);
        if (updated.IsFailure)
        {
            return Result<ReportDto>.Failure(updated.Error!);
        }

        return await SaveTrackedAsync(report, before, "report.updated", expectedRowVersion, ct);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var report = await _reports.FindByIdAsync(id, ct);
        if (!IsAccessible(report))
        {
            return Result<bool>.Failure(Error.NotFound("Report not found."));
        }

        if (report!.Status is not (ReportStatuses.Draft or ReportStatuses.Returned))
        {
            return Result<bool>.Failure(Error.Conflict(
                "Only draft or returned reports can be deleted."));
        }

        if (await _reports.HasMetricsAsync(report.Id, ct))
        {
            return Result<bool>.Failure(Error.Conflict(
                "The report has metrics and cannot be deleted."));
        }

        _reports.Remove(report);
        await _audit.RecordAsync("report.deleted", "Report", report.Id.ToString(),
            Snapshot(report), null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ReportDto>> SubmitAsync(Guid id, CancellationToken ct)
    {
        return await TransitionAsync(id, "report.submitted", report =>
            report.Submit(_currentUser.UserId ?? Guid.Empty, DateTimeOffset.UtcNow), ct);
    }

    public async Task<Result<ReportDto>> ApproveAsync(Guid id, CancellationToken ct)
    {
        return await TransitionAsync(id, "report.approved", report =>
            report.Approve(_currentUser.UserId ?? Guid.Empty, DateTimeOffset.UtcNow), ct);
    }

    public async Task<Result<ReportDto>> ReturnAsync(
        Guid id, ReturnReportCommand command, CancellationToken ct)
    {
        return await TransitionAsync(
            id, "report.returned", report => report.Return(command.ReturnReason), ct);
    }

    private async Task<Result<ReportDto>> TransitionAsync(
        Guid id, string auditAction, Func<Report, Result<bool>> transition, CancellationToken ct)
    {
        var report = await _reports.FindByIdAsync(id, ct);
        if (!IsAccessible(report))
        {
            return Result<ReportDto>.Failure(Error.NotFound("Report not found."));
        }

        var before = Snapshot(report!);
        var transitioned = transition(report!);
        if (transitioned.IsFailure)
        {
            return Result<ReportDto>.Failure(transitioned.Error!);
        }

        if (auditAction == "report.submitted")
        {
            await _notifications.StageReportSubmittedAsync(
                report!.Id,
                report.InstituteId,
                report.SubmittedByUserId ?? Guid.Empty,
                report.SubmittedAt!.Value,
                report.Title,
                ct);
        }

        return await SaveTrackedAsync(report!, before, auditAction, null, ct);
    }

    private async Task<Result<ReportDto>> SaveTrackedAsync(
        Report report, string before, string auditAction, byte[]? expectedRowVersion, CancellationToken ct)
    {
        if (expectedRowVersion is not null)
        {
            _unitOfWork.SetOriginalRowVersion(report, expectedRowVersion);
        }

        try
        {
            await _audit.RecordAsync(auditAction, "Report", report.Id.ToString(), before, Snapshot(report), ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<ReportDto>.Failure(Error.PreconditionFailed(
                "The report was modified by another request. Reload it and retry."));
        }

        return Result<ReportDto>.Success(Map(report));
    }

    private bool IsAccessible(Report? report) =>
        report is not null &&
        (_currentUser.InstituteId.HasValue
            ? _currentUser.InstituteId.Value == report.InstituteId
            : CanAccessAllInstitutes());

    private bool CanAccessAllInstitutes() =>
        _currentUser.IsInRole("PlatformAdmin") ||
        string.Equals(_currentUser.IdentityType, "SystemAdmin", StringComparison.OrdinalIgnoreCase);

    private static string Snapshot(Report report) =>
        $"status={report.Status};title={report.Title}";

    private static Dictionary<string, string[]> ValidateContent(string? reportType, string? title, string? summary)
    {
        var fields = new Dictionary<string, string[]>();
        if (reportType is not null && !DomainValues.Contains(ReportTypes.InstituteReportTypes, reportType.Trim()))
        {
            fields["reportType"] = [$"Report type must be one of: {string.Join(", ", ReportTypes.InstituteReportTypes)}."];
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 512)
        {
            fields["title"] = ["A title of at most 512 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            fields["summary"] = ["A summary is required."];
        }

        return fields;
    }

    private ListSlice<ReportDto> Map(ListSlice<Report> slice) =>
        new(slice.Items.Select(Map).ToList(), slice.Next);

    private static ReportDto Map(Report report) => new(
        report.Id, report.InstituteId, report.ReportingPeriodId, report.ReportType, report.Title,
        report.Summary, report.Abstract, report.KeyResults, report.Conclusion, report.Status,
        report.SubmittedAt, report.ApprovedAt, report.ReturnReason,
        ConcurrencyToken.Format(report.RowVersion), report.CreatedAt, report.UpdatedAt);
}
