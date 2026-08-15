using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Plan;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Plan;

public sealed class OutputService
{
    private static readonly string[] AllowedSorts = ["displayOrder", "code", "status"];

    private readonly IOutputRepository _outputs;
    private readonly IThrustRepository _thrusts;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public OutputService(
        IOutputRepository outputs,
        IThrustRepository thrusts,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _outputs = outputs;
        _thrusts = thrusts;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<OutputDto>>> ListAsync(
        Guid? thrustId, string? status,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        if (thrustId.HasValue)
        {
            var thrust = await _thrusts.FindByIdAsync(thrustId.Value, ct);
            if (thrust is null || !IsAccessible(thrust.InstituteId))
            {
                return Result<ListSlice<OutputDto>>.Failure(Error.NotFound("Thrust not found."));
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(PlanItemStatuses.All, status.Trim()))
        {
            return Result<ListSlice<OutputDto>>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["filter[status]"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."]
            }));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "displayOrder", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<OutputDto>>.Failure(page.Error!);
        }

        var slice = await _outputs.ListAsync(_currentUser.InstituteId, thrustId, status?.Trim(), page.Value!, ct);
        return Result<ListSlice<OutputDto>>.Success(new ListSlice<OutputDto>(
            slice.Items.Select(Map).ToList(), slice.Next));
    }

    public async Task<Result<OutputDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var output = await _outputs.FindByIdAsync(id, ct);
        var instituteId = output is null ? null : await _outputs.GetInstituteIdAsync(output.Id, ct);
        if (output is null || !IsAccessible(instituteId))
        {
            return Result<OutputDto>.Failure(Error.NotFound("Output not found."));
        }

        return Result<OutputDto>.Success(Map(output));
    }

    public async Task<Result<OutputDto>> CreateAsync(CreateOutputCommand command, CancellationToken ct)
    {
        var thrust = await _thrusts.FindByIdAsync(command.ThrustId, ct);
        if (thrust is null || !IsAccessible(thrust.InstituteId))
        {
            return Result<OutputDto>.Failure(Error.NotFound("Thrust not found."));
        }

        var fields = Validate(command.Code, command.Description);
        if (fields.Count > 0)
        {
            return Result<OutputDto>.Failure(Error.Validation(fields));
        }

        var code = command.Code.Trim();
        if (await _outputs.CodeExistsAsync(command.ThrustId, code, null, ct))
        {
            return Result<OutputDto>.Failure(Error.Conflict(
                "An output with the same code already exists for this thrust."));
        }

        var output = Output.Create(command.ThrustId, code, command.Description.Trim(),
            command.OwnerUserId, command.DueDate, command.DisplayOrder);
        _outputs.Add(output);
        await _audit.RecordAsync("output.created", "Output", output.Id.ToString(), null,
            $"thrust={command.ThrustId};code={output.Code}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<OutputDto>.Success(Map(output));
    }

    public async Task<Result<OutputDto>> UpdateAsync(Guid id, UpdateOutputCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var output = await _outputs.FindByIdAsync(id, ct);
        var instituteId = output is null ? null : await _outputs.GetInstituteIdAsync(output.Id, ct);
        if (output is null || !IsAccessible(instituteId))
        {
            return Result<OutputDto>.Failure(Error.NotFound("Output not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<OutputDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            return Result<OutputDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["description"] = ["A description is required."]
            }));
        }

        if (!DomainValues.Contains(PlanItemStatuses.All, command.Status?.Trim()))
        {
            return Result<OutputDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["status"] = [$"Status must be one of: {string.Join(", ", PlanItemStatuses.All)}."]
            }));
        }

        var before = $"status={output.Status};code={output.Code}";
        var updated = output.Update(command.Description.Trim(), command.OwnerUserId, command.DueDate,
            command.DisplayOrder, command.Status!.Trim());
        if (updated.IsFailure)
        {
            return Result<OutputDto>.Failure(updated.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(output, expectedRowVersion);
        try
        {
            await _audit.RecordAsync("output.updated", "Output", output.Id.ToString(), before,
                $"status={output.Status};code={output.Code}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<OutputDto>.Failure(Error.PreconditionFailed(
                "The output was modified by another request. Reload it and retry."));
        }

        return Result<OutputDto>.Success(Map(output));
    }

    private bool IsAccessible(Guid? instituteId) =>
        instituteId.HasValue && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(instituteId.Value);

    private static Dictionary<string, string[]> Validate(string? code, string? description)
    {
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 32)
        {
            fields["code"] = ["A code of at most 32 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            fields["description"] = ["A description is required."];
        }

        return fields;
    }

    private static OutputDto Map(Output output) => new(
        output.Id, output.ThrustId, output.Code, output.Description, output.OwnerUserId,
        output.DueDate, output.Status, output.DisplayOrder,
        ConcurrencyToken.Format(output.RowVersion), output.CreatedAt, output.UpdatedAt);
}
