using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Plan;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Plan;

public sealed class StrategicPlanService
{
    private static readonly string[] AllowedSorts = ["code", "startYear", "endYear", "status"];

    private readonly IStrategicPlanRepository _plans;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public StrategicPlanService(
        IStrategicPlanRepository plans,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _plans = plans;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<StrategicPlanDto>>> ListAsync(
        Guid? instituteFilter, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, instituteFilter);
        if (scope.IsFailure)
        {
            return Result<ListSlice<StrategicPlanDto>>.Failure(scope.Error!);
        }

        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(StrategicPlanStatuses.All, status.Trim()))
        {
            return Result<ListSlice<StrategicPlanDto>>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["filter[status]"] = [$"Status must be one of: {string.Join(", ", StrategicPlanStatuses.All)}."]
            }));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "endYear", true, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<StrategicPlanDto>>.Failure(page.Error!);
        }

        var slice = await _plans.ListAsync(scope.Value!.EffectiveFilter, status?.Trim(), page.Value!, ct);
        return Result<ListSlice<StrategicPlanDto>>.Success(new ListSlice<StrategicPlanDto>(
            slice.Items.Select(Map).ToList(), slice.Next));
    }

    public async Task<Result<StrategicPlanDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        var plan = await _plans.FindByIdAsync(id, scope.EffectiveFilter, ct);
        if (plan is null)
        {
            return Result<StrategicPlanDto>.Failure(Error.NotFound("Strategic plan not found."));
        }

        return Result<StrategicPlanDto>.Success(Map(plan));
    }

    public async Task<Result<StrategicPlanDto>> CreateAsync(
        CreateStrategicPlanCommand command, CancellationToken ct)
    {
        var instituteId = _currentUser.InstituteId ?? command.InstituteId;
        if (!instituteId.HasValue)
            return Result<StrategicPlanDto>.Failure(Error.Validation(
                new Dictionary<string, string[]> { ["instituteId"] = ["An institute is required."] }));
        if (_currentUser.InstituteId.HasValue && command.InstituteId.HasValue &&
            command.InstituteId != _currentUser.InstituteId)
            return Result<StrategicPlanDto>.Failure(Error.CrossInstitute(
                "You are not authorized to create a strategic plan for that institute."));
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64)
            fields["code"] = ["A code of at most 64 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 256)
            fields["name"] = ["A name of at most 256 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Definition))
            fields["definition"] = ["A definition is required."];
        if (string.IsNullOrWhiteSpace(command.Objective))
            fields["objective"] = ["An objective is required."];
        if (command.StartYear is < 2000 or > 3000)
            fields["startYear"] = ["The start year must be between 2000 and 3000."];
        if (command.EndYear is < 2000 or > 3000)
            fields["endYear"] = ["The end year must be between 2000 and 3000."];
        if (command.EndYear < command.StartYear)
            fields["endYear"] = ["The end year cannot precede the start year."];
        if (fields.Count > 0)
            return Result<StrategicPlanDto>.Failure(Error.Validation(fields));
        if (!await _institutes.InstituteExistsAsync(instituteId.Value, ct))
            return Result<StrategicPlanDto>.Failure(Error.Validation(
                new Dictionary<string, string[]> { ["instituteId"] = ["The institute does not exist."] }));
        if (await _plans.CodeExistsAsync(instituteId.Value, command.Code.Trim(), null, ct))
            return Result<StrategicPlanDto>.Failure(Error.Conflict(
                "A strategic plan with this code already exists for the institute."));

        var plan = StrategicPlan.Create(instituteId.Value, command.Code, command.Name,
            command.Definition, command.Objective, command.StartYear, command.EndYear);
        _plans.Add(plan);
        await _audit.RecordAsync("strategic-plan.created", "StrategicPlan", plan.Id.ToString(),
            null, Snapshot(plan), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<StrategicPlanDto>.Success(Map(plan));
    }

    public async Task<Result<StrategicPlanDto>> UpdateAsync(
        Guid id, UpdateStrategicPlanCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        var plan = await _plans.FindByIdAsync(id, scope.EffectiveFilter, ct);
        if (plan is null)
            return Result<StrategicPlanDto>.Failure(Error.NotFound("Strategic plan not found."));
        if (expectedRowVersion is null)
            return Result<StrategicPlanDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        var before = Snapshot(plan);
        var updated = plan.Update(command.Name, command.Definition, command.Objective,
            command.StartYear, command.EndYear);
        if (updated.IsFailure)
            return Result<StrategicPlanDto>.Failure(updated.Error!);
        return await SaveAsync(plan, before, "strategic-plan.updated", expectedRowVersion, ct);
    }

    public async Task<Result<StrategicPlanDto>> ActivateAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, null).Value!;
        var plan = await _plans.FindByIdAsync(id, scope.EffectiveFilter, ct);
        if (plan is null)
            return Result<StrategicPlanDto>.Failure(Error.NotFound("Strategic plan not found."));
        if (expectedRowVersion is null)
            return Result<StrategicPlanDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));

        var before = Snapshot(plan);
        var activated = plan.Activate();
        if (activated.IsFailure)
            return Result<StrategicPlanDto>.Failure(activated.Error!);
        if (await _plans.HasOverlappingActiveAsync(
                plan.InstituteId, plan.StartYear, plan.EndYear, plan.Id, ct))
            return Result<StrategicPlanDto>.Failure(Error.Conflict(
                "The institute already has an active strategic plan for an overlapping planning range."));
        return await SaveAsync(plan, before, "strategic-plan.activated", expectedRowVersion, ct);
    }

    private async Task<Result<StrategicPlanDto>> SaveAsync(
        StrategicPlan plan, string before, string action, byte[] expectedRowVersion, CancellationToken ct)
    {
        _unitOfWork.SetOriginalRowVersion(plan, expectedRowVersion);
        try
        {
            await _audit.RecordAsync(action, "StrategicPlan", plan.Id.ToString(), before, Snapshot(plan), ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<StrategicPlanDto>.Failure(Error.PreconditionFailed(
                "The strategic plan was modified by another request. Reload it and retry."));
        }
        return Result<StrategicPlanDto>.Success(Map(plan));
    }

    private static string Snapshot(StrategicPlan plan) =>
        $"code={plan.Code};status={plan.Status};years={plan.StartYear}-{plan.EndYear}";

    private static StrategicPlanDto Map(StrategicPlan plan) => new(
        plan.Id, plan.InstituteId, plan.Code, plan.Name, plan.Definition, plan.Objective,
        plan.StartYear, plan.EndYear, plan.Status,
        ConcurrencyToken.Format(plan.RowVersion), plan.CreatedAt, plan.UpdatedAt);
}
