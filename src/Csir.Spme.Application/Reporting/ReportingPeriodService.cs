using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Reporting;

public sealed class ReportingPeriodService
{
    private static readonly string[] AllowedSorts = ["code", "startDate", "endDate"];

    private readonly IReportingPeriodRepository _periods;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public ReportingPeriodService(
        IReportingPeriodRepository periods,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _periods = periods;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<ReportingPeriodDto>>> ListAsync(
        Guid? instituteFilter, string? periodType, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, instituteFilter);
        if (scope.IsFailure)
        {
            return Result<ListSlice<ReportingPeriodDto>>.Failure(scope.Error!);
        }

        var fields = ValidateListFilters(periodType, status);
        if (fields.Count > 0)
        {
            return Result<ListSlice<ReportingPeriodDto>>.Failure(Error.Validation(fields));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "startDate", true, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<ReportingPeriodDto>>.Failure(page.Error!);
        }

        var slice = await _periods.ListAsync(scope.Value!.EffectiveFilter, periodType?.Trim(), status?.Trim(), page.Value!, ct);
        return Result<ListSlice<ReportingPeriodDto>>.Success(Map(slice));
    }

    public async Task<Result<ReportingPeriodDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var period = await _periods.FindByIdAsync(id, ct);
        var callerScope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        var accessible = period is not null &&
            (period.ScopeType == ScopeTypes.CsirWide ||
             (period.InstituteId.HasValue && callerScope.CanAccess(period.InstituteId.Value)));
        if (!accessible)
        {
            return Result<ReportingPeriodDto>.Failure(Error.NotFound("Reporting period not found."));
        }

        return Result<ReportingPeriodDto>.Success(Map(period!));
    }

    public async Task<Result<ReportingPeriodDto>> CreateAsync(CreateReportingPeriodCommand command, CancellationToken ct)
    {
        var scopeType = command.ScopeType?.Trim() ?? string.Empty;
        var fields = new Dictionary<string, string[]>();

        if (scopeType is not ScopeTypes.Institute and not ScopeTypes.CsirWide)
        {
            fields["scopeType"] = ["Scope type must be institute or csir-wide."];
        }

        if (!DomainValues.Contains(ReportingPeriodTypes.All, command.PeriodType?.Trim()))
        {
            fields["periodType"] = [$"Period type must be one of: {string.Join(", ", ReportingPeriodTypes.All)}."];
        }

        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64)
        {
            fields["code"] = ["A code of at most 64 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 256)
        {
            fields["name"] = ["A name of at most 256 characters is required."];
        }

        if (command.EndDate < command.StartDate)
        {
            fields["endDate"] = ["The end date cannot precede the start date."];
        }

        // Institute ownership: institute callers create in their own institute; CSIR-wide
        // callers may target an explicit institute or create a csir-wide period.
        Guid? instituteId;
        if (_currentUser.InstituteId.HasValue)
        {
            instituteId = _currentUser.InstituteId.Value;
            if (command.InstituteId.HasValue && command.InstituteId.Value != instituteId.Value)
            {
                return Result<ReportingPeriodDto>.Failure(Error.CrossInstitute(
                    "You are not authorized to create reporting periods for that institute."));
            }
        }
        else
        {
            instituteId = command.InstituteId;
        }

        var effectiveScope = _currentUser.InstituteId.HasValue ? ScopeTypes.Institute : scopeType;
        if (effectiveScope == ScopeTypes.CsirWide)
        {
            instituteId = null;
        }
        else if (!instituteId.HasValue)
        {
            fields["instituteId"] = ["An institute is required for an institute-scoped reporting period."];
        }

        if (fields.Count > 0)
        {
            return Result<ReportingPeriodDto>.Failure(Error.Validation(fields));
        }

        if (instituteId.HasValue && !await _institutes.InstituteExistsAsync(instituteId.Value, ct))
        {
            return Result<ReportingPeriodDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["instituteId"] = ["The institute does not exist."]
            }));
        }

        var code = command.Code.Trim();
        if (await _periods.CodeExistsAsync(effectiveScope, instituteId, code, null, ct))
        {
            return Result<ReportingPeriodDto>.Failure(Error.Conflict(
                "A reporting period with the same code already exists for this scope."));
        }

        var created = ReportingPeriod.Create(effectiveScope, instituteId, code, command.Name.Trim(),
            command.PeriodType!.Trim(), command.StartDate, command.EndDate, command.DueDate);
        if (created.IsFailure)
        {
            return Result<ReportingPeriodDto>.Failure(created.Error!);
        }

        var period = created.Value!;
        _periods.Add(period);
        await _audit.RecordAsync("reporting-period.created", "ReportingPeriod", period.Id.ToString(), null,
            $"code={period.Code};scope={period.ScopeType}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ReportingPeriodDto>.Success(Map(period));
    }

    public Task<Result<ReportingPeriodDto>> OpenAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct) =>
        TransitionAsync(id, "reporting-period.opened", expectedRowVersion, period => period.Open(), ct);

    public Task<Result<ReportingPeriodDto>> CloseAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct) =>
        TransitionAsync(id, "reporting-period.closed", expectedRowVersion, period => period.Close(), ct);

    public Task<Result<ReportingPeriodDto>> FinalizeAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct) =>
        TransitionAsync(id, "reporting-period.finalized", expectedRowVersion, period => period.Finalize(), ct);

    private async Task<Result<ReportingPeriodDto>> TransitionAsync(
        Guid id,
        string auditAction,
        byte[]? expectedRowVersion,
        Func<ReportingPeriod, Result<bool>> transition,
        CancellationToken ct)
    {
        var period = await _periods.FindByIdAsync(id, ct);
        var callerScope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        if (period is null ||
            (period.ScopeType != ScopeTypes.CsirWide &&
             (!period.InstituteId.HasValue || !callerScope.CanAccess(period.InstituteId.Value))))
            return Result<ReportingPeriodDto>.Failure(Error.NotFound("Reporting period not found."));
        if (expectedRowVersion is null)
            return Result<ReportingPeriodDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        var before = $"status={period.Status}";
        var changed = transition(period);
        if (changed.IsFailure)
            return Result<ReportingPeriodDto>.Failure(changed.Error!);

        _unitOfWork.SetOriginalRowVersion(period, expectedRowVersion);
        try
        {
            await _audit.RecordAsync(auditAction, "ReportingPeriod", period.Id.ToString(),
                before, $"status={period.Status}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<ReportingPeriodDto>.Failure(Error.PreconditionFailed(
                "The reporting period was modified by another request. Reload it and retry."));
        }
        return Result<ReportingPeriodDto>.Success(Map(period));
    }

    private ListSlice<ReportingPeriodDto> Map(ListSlice<ReportingPeriod> slice) =>
        new(slice.Items.Select(Map).ToList(), slice.Next);

    private static ReportingPeriodDto Map(ReportingPeriod period) => new(
        period.Id, period.ScopeType, period.InstituteId, period.Code, period.Name, period.PeriodType,
        period.StartDate, period.EndDate, period.DueDate, period.Status,
        ConcurrencyToken.Format(period.RowVersion), period.CreatedAt, period.UpdatedAt);

    private static Dictionary<string, string[]> ValidateListFilters(string? periodType, string? status)
    {
        var fields = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(periodType) && !DomainValues.Contains(ReportingPeriodTypes.All, periodType.Trim()))
        {
            fields["filter[periodType]"] = [$"Period type must be one of: {string.Join(", ", ReportingPeriodTypes.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(ReportingPeriodStatuses.All, status.Trim()))
        {
            fields["filter[status]"] = [$"Status must be one of: {string.Join(", ", ReportingPeriodStatuses.All)}."];
        }

        return fields;
    }
}
