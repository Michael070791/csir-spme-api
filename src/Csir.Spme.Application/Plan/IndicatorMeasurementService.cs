using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Plan;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Plan;

public sealed class IndicatorMeasurementService
{
    private static readonly string[] AllowedSorts = ["id"];

    private readonly IIndicatorMeasurementRepository _measurements;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public IndicatorMeasurementService(
        IIndicatorMeasurementRepository measurements,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _measurements = measurements;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<IndicatorMeasurementDto>>> ListByIndicatorAsync(
        Guid indicatorId, int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var indicator = await _measurements.GetIndicatorAsync(indicatorId, ct);
        var indicatorInstituteId = indicator is null
            ? null
            : await _measurements.GetIndicatorInstituteIdAsync(indicator.Id, ct);
        if (indicator is null || !IsAccessible(indicatorInstituteId))
        {
            return Result<ListSlice<IndicatorMeasurementDto>>.Failure(Error.NotFound("Indicator not found."));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "id", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<IndicatorMeasurementDto>>.Failure(page.Error!);
        }

        var slice = await _measurements.ListByIndicatorAsync(indicatorId, page.Value!, ct);
        return Result<ListSlice<IndicatorMeasurementDto>>.Success(new ListSlice<IndicatorMeasurementDto>(
            slice.Items.Select(item => Map(item, indicator.TargetValue)).ToList(), slice.Next));
    }

    public async Task<Result<IndicatorMeasurementDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var measurement = await _measurements.FindByIdAsync(id, ct);
        if (measurement is null || !await MeasurementAccessibleAsync(measurement.Id, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.NotFound("Indicator measurement not found."));
        }

        var indicator = await _measurements.GetIndicatorAsync(measurement.IndicatorId, ct);
        return Result<IndicatorMeasurementDto>.Success(Map(measurement, indicator?.TargetValue));
    }

    public async Task<Result<IndicatorMeasurementDto>> CreateAsync(
        Guid indicatorId, CreateIndicatorMeasurementCommand command, CancellationToken ct)
    {
        var indicator = await _measurements.GetIndicatorAsync(indicatorId, ct);
        if (indicator is null || !await IndicatorAccessibleAsync(indicator.Id, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.NotFound("Indicator not found."));
        }

        var indicatorInstituteId = await _measurements.GetIndicatorInstituteIdAsync(indicator.Id, ct);

        var period = await _measurements.GetReportingPeriodAsync(command.ReportingPeriodId, ct);
        if (period is null)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["reportingPeriodId"] = ["The reporting period does not exist."]
            }));
        }

        if (period.ScopeType != Csir.Spme.Domain.Constants.ScopeTypes.CsirWide &&
            period.InstituteId != indicatorInstituteId)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["reportingPeriodId"] = ["The reporting period does not exist."]
            }));
        }

        if (!period.AllowsMeasurementChanges)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Conflict(
                "Measurements can only be recorded while the reporting period is draft or open."));
        }

        if (command.EvidenceFileId.HasValue && !await _institutes.FileExistsAsync(command.EvidenceFileId.Value, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["evidenceFileId"] = ["The referenced file does not exist."]
            }));
        }

        if (await _measurements.ExistsAsync(indicatorId, command.ReportingPeriodId, null, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Conflict(
                "A measurement already exists for this indicator and reporting period."));
        }

        var measurement = IndicatorMeasurement.Create(indicatorId, command.ReportingPeriodId,
            command.Value, command.Remarks, command.EvidenceFileId, _currentUser.UserId ?? Guid.Empty);
        _measurements.Add(measurement);
        await _audit.RecordAsync("indicator-measurement.created", "IndicatorMeasurement",
            measurement.Id.ToString(), null, $"indicator={indicatorId};period={command.ReportingPeriodId}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<IndicatorMeasurementDto>.Success(Map(measurement, indicator.TargetValue));
    }

    public async Task<Result<IndicatorMeasurementDto>> UpdateAsync(
        Guid id, UpdateIndicatorMeasurementCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var measurement = await _measurements.FindByIdAsync(id, ct);
        if (measurement is null || !await MeasurementAccessibleAsync(measurement.Id, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.NotFound("Indicator measurement not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var period = await _measurements.GetReportingPeriodAsync(measurement.ReportingPeriodId, ct);
        if (period is null || !period.AllowsMeasurementChanges)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Conflict(
                "Measurements can only change while the reporting period is draft or open."));
        }

        if (command.EvidenceFileId.HasValue && !await _institutes.FileExistsAsync(command.EvidenceFileId.Value, ct))
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["evidenceFileId"] = ["The referenced file does not exist."]
            }));
        }

        measurement.Update(command.Value, command.Remarks, command.EvidenceFileId);
        _unitOfWork.SetOriginalRowVersion(measurement, expectedRowVersion);
        try
        {
            await _audit.RecordAsync("indicator-measurement.updated", "IndicatorMeasurement",
                measurement.Id.ToString(), null, $"value={measurement.Value}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<IndicatorMeasurementDto>.Failure(Error.PreconditionFailed(
                "The measurement was modified by another request. Reload it and retry."));
        }

        var indicator = await _measurements.GetIndicatorAsync(measurement.IndicatorId, ct);
        return Result<IndicatorMeasurementDto>.Success(Map(measurement, indicator?.TargetValue));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var measurement = await _measurements.FindByIdAsync(id, ct);
        if (measurement is null || !await MeasurementAccessibleAsync(measurement.Id, ct))
        {
            return Result<bool>.Failure(Error.NotFound("Indicator measurement not found."));
        }

        var period = await _measurements.GetReportingPeriodAsync(measurement.ReportingPeriodId, ct);
        if (period is null || !period.AllowsMeasurementChanges)
        {
            return Result<bool>.Failure(Error.Conflict(
                "Measurements can only be removed while the reporting period is draft or open."));
        }

        _measurements.Remove(measurement);
        await _audit.RecordAsync("indicator-measurement.deleted", "IndicatorMeasurement",
            measurement.Id.ToString(), $"value={measurement.Value}", null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> IndicatorAccessibleAsync(Guid indicatorId, CancellationToken ct)
    {
        var instituteId = await _measurements.GetIndicatorInstituteIdAsync(indicatorId, ct);
        return IsAccessible(instituteId);
    }

    private async Task<bool> MeasurementAccessibleAsync(Guid measurementId, CancellationToken ct)
    {
        var instituteId = await _measurements.GetInstituteIdAsync(measurementId, ct);
        return IsAccessible(instituteId);
    }

    private bool IsAccessible(Guid? instituteId) =>
        instituteId.HasValue && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(instituteId.Value);

    private static IndicatorMeasurementDto Map(IndicatorMeasurement measurement, decimal? targetValue) => new(
        measurement.Id, measurement.IndicatorId, measurement.ReportingPeriodId, measurement.Value,
        IndicatorMeasurement.DeriveVariance(measurement.Value, targetValue), measurement.Remarks,
        measurement.EvidenceFileId, measurement.RecordedByUserId,
        ConcurrencyToken.Format(measurement.RowVersion), measurement.CreatedAt, measurement.UpdatedAt);
}
