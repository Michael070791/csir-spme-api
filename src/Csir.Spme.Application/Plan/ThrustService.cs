using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Plan;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Plan;

public sealed class ThrustService
{
    private static readonly string[] AllowedSorts = ["displayOrder", "code", "title", "status"];

    private readonly IThrustRepository _thrusts;
    private readonly IStrategicPlanRepository _plans;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public ThrustService(
        IThrustRepository thrusts,
        IStrategicPlanRepository plans,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _thrusts = thrusts;
        _plans = plans;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<ThrustDto>>> ListAsync(
        Guid? strategicPlanId, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(PlanItemStatuses.All, status.Trim()))
        {
            return Result<ListSlice<ThrustDto>>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["filter[status]"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."]
            }));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "displayOrder", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<ThrustDto>>.Failure(page.Error!);
        }

        var slice = await _thrusts.ListAsync(
            _currentUser.InstituteId, strategicPlanId, status?.Trim(), page.Value!, ct);
        return Result<ListSlice<ThrustDto>>.Success(new ListSlice<ThrustDto>(
            slice.Items.Select(Map).ToList(), slice.Next));
    }

    public async Task<Result<ThrustDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var thrust = await _thrusts.FindByIdAsync(id, ct);
        if (thrust is null || !IsAccessible(thrust.InstituteId))
        {
            return Result<ThrustDto>.Failure(Error.NotFound("Thrust not found."));
        }

        return Result<ThrustDto>.Success(Map(thrust));
    }

    public async Task<Result<ThrustDto>> CreateAsync(Guid strategicPlanId, CreateThrustCommand command, CancellationToken ct)
    {
        var plan = await _plans.FindByIdAsync(strategicPlanId, _currentUser.InstituteId, ct);
        if (plan is null)
        {
            return Result<ThrustDto>.Failure(Error.NotFound("Strategic plan not found."));
        }

        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64)
        {
            fields["code"] = ["A code of at most 64 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 512)
        {
            fields["title"] = ["A title of at most 512 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            fields["description"] = ["A description is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Objective))
        {
            fields["objective"] = ["An objective is required."];
        }

        if (fields.Count > 0)
        {
            return Result<ThrustDto>.Failure(Error.Validation(fields));
        }

        var code = command.Code.Trim();
        if (await _thrusts.CodeExistsAsync(strategicPlanId, code, null, ct))
        {
            return Result<ThrustDto>.Failure(Error.Conflict(
                "A thrust with the same code already exists in this strategic plan."));
        }

        var thrust = Thrust.Create(strategicPlanId, plan!.InstituteId, code, command.Title.Trim(),
            command.Description.Trim(), command.Objective.Trim(), command.DisplayOrder);
        _thrusts.Add(thrust);
        await _audit.RecordAsync("thrust.created", "Thrust", thrust.Id.ToString(), null,
            $"plan={strategicPlanId};code={thrust.Code}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ThrustDto>.Success(Map(thrust));
    }

    public async Task<Result<ThrustDto>> UpdateAsync(Guid id, UpdateThrustCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var thrust = await _thrusts.FindByIdAsync(id, ct);
        if (thrust is null || !IsAccessible(thrust.InstituteId))
        {
            return Result<ThrustDto>.Failure(Error.NotFound("Thrust not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<ThrustDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 512)
        {
            fields["title"] = ["A title of at most 512 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            fields["description"] = ["A description is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Objective))
        {
            fields["objective"] = ["An objective is required."];
        }

        if (!DomainValues.Contains(PlanItemStatuses.All, command.Status?.Trim()))
        {
            fields["status"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<ThrustDto>.Failure(Error.Validation(fields));
        }

        var before = $"status={thrust.Status};code={thrust.Code}";
        var updated = thrust.Update(command.Title.Trim(), command.Description.Trim(),
            command.Objective!.Trim(), command.DisplayOrder, command.Status!.Trim());
        if (updated.IsFailure)
        {
            return Result<ThrustDto>.Failure(updated.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(thrust, expectedRowVersion);
        try
        {
            await _audit.RecordAsync("thrust.updated", "Thrust", thrust.Id.ToString(), before,
                $"status={thrust.Status};code={thrust.Code}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<ThrustDto>.Failure(Error.PreconditionFailed(
                "The thrust was modified by another request. Reload it and retry."));
        }

        return Result<ThrustDto>.Success(Map(thrust));
    }

    private bool IsAccessible(Guid? instituteId) =>
        instituteId.HasValue && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(instituteId.Value);

    private static ThrustDto Map(Thrust thrust) => new(
        thrust.Id, thrust.StrategicPlanId, thrust.InstituteId, thrust.Code, thrust.Title,
        thrust.Description, thrust.Objective, thrust.DisplayOrder, thrust.Status,
        ConcurrencyToken.Format(thrust.RowVersion), thrust.CreatedAt, thrust.UpdatedAt);
}
