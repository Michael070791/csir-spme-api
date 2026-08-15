using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Plan;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Plan;

public sealed class IndicatorService
{
    private static readonly string[] AllowedSorts = ["code", "status"];

    private readonly IIndicatorRepository _indicators;
    private readonly IOutputRepository _outputs;
    private readonly IThrustRepository _thrusts;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public IndicatorService(
        IIndicatorRepository indicators,
        IOutputRepository outputs,
        IThrustRepository thrusts,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _indicators = indicators;
        _outputs = outputs;
        _thrusts = thrusts;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<IndicatorDto>>> ListByOutputAsync(
        Guid outputId, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var instituteId = await _outputs.GetInstituteIdAsync(outputId, ct);
        if (!IsAccessible(instituteId))
        {
            return Result<ListSlice<IndicatorDto>>.Failure(Error.NotFound("Output not found."));
        }

        var page = ParsePage(status, limit, cursor, sort, direction);
        if (page.IsFailure)
        {
            return Result<ListSlice<IndicatorDto>>.Failure(page.Error!);
        }

        var slice = await _indicators.ListByOutputAsync(_currentUser.InstituteId, outputId, status?.Trim(), page.Value!, ct);
        return Result<ListSlice<IndicatorDto>>.Success(Map(slice));
    }

    /// <summary>Read-only aggregate view of all indicators under a thrust.</summary>
    public async Task<Result<ListSlice<IndicatorDto>>> ListByThrustAsync(
        Guid thrustId, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var thrust = await _thrusts.FindByIdAsync(thrustId, ct);
        if (thrust is null || !IsAccessible(thrust.InstituteId))
        {
            return Result<ListSlice<IndicatorDto>>.Failure(Error.NotFound("Thrust not found."));
        }

        var page = ParsePage(status, limit, cursor, sort, direction);
        if (page.IsFailure)
        {
            return Result<ListSlice<IndicatorDto>>.Failure(page.Error!);
        }

        var slice = await _indicators.ListByThrustAsync(thrustId, status?.Trim(), page.Value!, ct);
        return Result<ListSlice<IndicatorDto>>.Success(Map(slice));
    }

    public async Task<Result<IndicatorDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var indicator = await _indicators.FindByIdAsync(id, ct);
        var instituteId = indicator is null ? null : await _indicators.GetInstituteIdAsync(indicator.Id, ct);
        if (indicator is null || !IsAccessible(instituteId))
        {
            return Result<IndicatorDto>.Failure(Error.NotFound("Indicator not found."));
        }

        return Result<IndicatorDto>.Success(Map(indicator));
    }

    public async Task<Result<IndicatorDto>> CreateAsync(Guid outputId, CreateIndicatorCommand command, CancellationToken ct)
    {
        var instituteId = await _outputs.GetInstituteIdAsync(outputId, ct);
        if (!IsAccessible(instituteId))
        {
            return Result<IndicatorDto>.Failure(Error.NotFound("Output not found."));
        }

        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 32)
        {
            fields["code"] = ["A code of at most 32 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            fields["description"] = ["A description is required."];
        }

        if (string.IsNullOrWhiteSpace(command.UnitOfMeasure) || command.UnitOfMeasure.Length > 64)
        {
            fields["unitOfMeasure"] = ["A unit of measure of at most 64 characters is required."];
        }

        if (fields.Count > 0)
        {
            return Result<IndicatorDto>.Failure(Error.Validation(fields));
        }

        var code = command.Code.Trim();
        if (await _indicators.CodeExistsAsync(outputId, code, null, ct))
        {
            return Result<IndicatorDto>.Failure(Error.Conflict(
                "An indicator with the same code already exists for this output."));
        }

        var indicator = Indicator.Create(outputId, code, command.Description.Trim(),
            command.UnitOfMeasure.Trim(), command.BaselineValue, command.TargetValue,
            command.VerificationMethod, command.DueDate);
        _indicators.Add(indicator);
        await _audit.RecordAsync("indicator.created", "Indicator", indicator.Id.ToString(), null,
            $"output={outputId};code={indicator.Code}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<IndicatorDto>.Success(Map(indicator));
    }

    public async Task<Result<IndicatorDto>> UpdateAsync(Guid id, UpdateIndicatorCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var indicator = await _indicators.FindByIdAsync(id, ct);
        var instituteId = indicator is null ? null : await _indicators.GetInstituteIdAsync(indicator.Id, ct);
        if (indicator is null || !IsAccessible(instituteId))
        {
            return Result<IndicatorDto>.Failure(Error.NotFound("Indicator not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<IndicatorDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Description))
        {
            fields["description"] = ["A description is required."];
        }

        if (string.IsNullOrWhiteSpace(command.UnitOfMeasure) || command.UnitOfMeasure.Length > 64)
        {
            fields["unitOfMeasure"] = ["A unit of measure of at most 64 characters is required."];
        }

        if (!DomainValues.Contains(PlanItemStatuses.All, command.Status?.Trim()))
        {
            fields["status"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<IndicatorDto>.Failure(Error.Validation(fields));
        }

        var before = $"status={indicator.Status};code={indicator.Code}";
        var updated = indicator.Update(command.Description.Trim(), command.UnitOfMeasure.Trim(),
            command.BaselineValue, command.TargetValue, command.VerificationMethod,
            command.DueDate, command.Status!.Trim());
        if (updated.IsFailure)
        {
            return Result<IndicatorDto>.Failure(updated.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(indicator, expectedRowVersion);
        try
        {
            await _audit.RecordAsync("indicator.updated", "Indicator", indicator.Id.ToString(), before,
                $"status={indicator.Status};code={indicator.Code}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<IndicatorDto>.Failure(Error.PreconditionFailed(
                "The indicator was modified by another request. Reload it and retry."));
        }

        return Result<IndicatorDto>.Success(Map(indicator));
    }

    private Result<KeysetPage> ParsePage(string? status, int? limit, string? cursor, string? sort, string? direction)
    {
        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(PlanItemStatuses.All, status.Trim()))
        {
            return Result<KeysetPage>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["filter[status]"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."]
            }));
        }

        return ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "code", false, AllowedSorts);
    }

    private bool IsAccessible(Guid? instituteId) =>
        instituteId.HasValue && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(instituteId.Value);

    private static ListSlice<IndicatorDto> Map(ListSlice<Indicator> slice) =>
        new(slice.Items.Select(Map).ToList(), slice.Next);

    private static IndicatorDto Map(Indicator indicator) => new(
        indicator.Id, indicator.OutputId, indicator.Code, indicator.Description, indicator.UnitOfMeasure,
        indicator.BaselineValue, indicator.TargetValue, indicator.VerificationMethod, indicator.DueDate,
        indicator.Status, ConcurrencyToken.Format(indicator.RowVersion), indicator.CreatedAt, indicator.UpdatedAt);
}
