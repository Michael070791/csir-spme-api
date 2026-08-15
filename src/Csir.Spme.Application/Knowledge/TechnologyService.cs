using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Knowledge;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Knowledge;

public sealed class TechnologyService
{
    private static readonly string[] AllowedSorts = ["name", "code", "status"];
    private static readonly short MinYear = 1950;

    private readonly ITechnologyRepository _technologies;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public TechnologyService(
        ITechnologyRepository technologies,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _technologies = technologies;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<TechnologyDto>>> ListAsync(
        Guid? instituteFilter, string? status, string? technologyType,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, instituteFilter);
        if (scope.IsFailure)
        {
            return Result<ListSlice<TechnologyDto>>.Failure(scope.Error!);
        }

        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(TechnologyStatuses.All, status.Trim()))
        {
            return Result<ListSlice<TechnologyDto>>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["filter[status]"] = [$"Status must be one of: {string.Join(", ", TechnologyStatuses.All)}."]
            }));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "name", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<TechnologyDto>>.Failure(page.Error!);
        }

        var slice = await _technologies.ListAsync(scope.Value!.EffectiveFilter,
            status?.Trim(), technologyType?.Trim(), page.Value!, ct);
        return Result<ListSlice<TechnologyDto>>.Success(Map(slice));
    }

    public async Task<Result<TechnologyDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var technology = await _technologies.FindByIdAsync(id, ct);
        if (!IsAccessible(technology))
        {
            return Result<TechnologyDto>.Failure(Error.NotFound("Technology not found."));
        }

        return Result<TechnologyDto>.Success(Map(technology!));
    }

    public async Task<Result<TechnologyDto>> CreateAsync(CreateTechnologyCommand command, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, command.InstituteId);
        if (scope.IsFailure)
        {
            return Result<TechnologyDto>.Failure(scope.Error!);
        }

        var fields = ValidateContent(command.Code, command.Name, command.Description,
            command.ApplicationArea, command.TechnologyType, command.YearIntroduced);
        if (fields.Count > 0)
        {
            return Result<TechnologyDto>.Failure(Error.Validation(fields));
        }

        if (!scope.Value!.EffectiveFilter.HasValue)
        {
            return Result<TechnologyDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["instituteId"] = ["An owning institute is required."]
            }));
        }

        var instituteId = scope.Value.EffectiveFilter.Value;
        if (!await _institutes.InstituteExistsAsync(instituteId, ct))
        {
            return Result<TechnologyDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["instituteId"] = ["The institute does not exist."]
            }));
        }

        if (command.LeadEmployeeId.HasValue)
        {
            var lead = await _institutes.GetEmployeeScopeAsync(command.LeadEmployeeId.Value, ct);
            if (lead is null || lead.InstituteId != instituteId)
            {
                return Result<TechnologyDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["leadEmployeeId"] = ["The lead employee must belong to the technology's institute."]
                }));
            }
        }

        var code = command.Code.Trim();
        if (await _technologies.CodeExistsAsync(instituteId, code, null, ct))
        {
            return Result<TechnologyDto>.Failure(Error.Conflict(
                "A technology with the same code already exists for this institute."));
        }

        var technology = Technology.Create(instituteId, code, command.Name.Trim(), command.Description.Trim(),
            command.ApplicationArea.Trim(), command.LeadEmployeeId, command.TechnologyType.Trim(),
            command.YearIntroduced, command.HasIntellectualProperty);
        _technologies.Add(technology);
        await _audit.RecordAsync("technology.created", "Technology", technology.Id.ToString(), null,
            $"code={technology.Code}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<TechnologyDto>.Success(Map(technology));
    }

    public async Task<Result<TechnologyDto>> UpdateAsync(Guid id, UpdateTechnologyCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var technology = await _technologies.FindByIdAsync(id, ct);
        if (!IsAccessible(technology))
        {
            return Result<TechnologyDto>.Failure(Error.NotFound("Technology not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<TechnologyDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var fields = ValidateContent(null, command.Name, command.Description,
            command.ApplicationArea, command.TechnologyType, command.YearIntroduced);
        if (!DomainValues.Contains(TechnologyStatuses.All, command.Status?.Trim()))
        {
            fields["status"] = [$"Status must be one of: {string.Join(", ", TechnologyStatuses.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<TechnologyDto>.Failure(Error.Validation(fields));
        }

        if (command.LeadEmployeeId.HasValue)
        {
            var lead = await _institutes.GetEmployeeScopeAsync(command.LeadEmployeeId.Value, ct);
            if (lead is null || lead.InstituteId != technology!.InstituteId)
            {
                return Result<TechnologyDto>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["leadEmployeeId"] = ["The lead employee must belong to the technology's institute."]
                }));
            }
        }

        var before = $"status={technology!.Status};name={technology.Name}";
        var updated = technology.Update(command.Name.Trim(), command.Description.Trim(),
            command.ApplicationArea.Trim(), command.LeadEmployeeId, command.TechnologyType.Trim(),
            command.YearIntroduced, command.HasIntellectualProperty);
        if (updated.IsFailure)
        {
            return Result<TechnologyDto>.Failure(updated.Error!);
        }

        // Status moves through the domain transitions so no endpoint can bypass them.
        var requestedStatus = command.Status!.Trim();
        var transitioned = ApplyStatus(technology!, requestedStatus);
        if (transitioned.IsFailure)
        {
            return Result<TechnologyDto>.Failure(transitioned.Error!);
        }

        return await SaveTrackedAsync(technology, before, "technology.updated", expectedRowVersion, ct);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var technology = await _technologies.FindByIdAsync(id, ct);
        if (!IsAccessible(technology))
        {
            return Result<bool>.Failure(Error.NotFound("Technology not found."));
        }

        // Only unreferenced drafts may be hard-deleted; published records must be archived.
        if (technology!.Status == TechnologyStatuses.Draft &&
            !await _technologies.HasReferencesAsync(technology.Id, ct))
        {
            _technologies.Remove(technology);
            await _audit.RecordAsync("technology.deleted", "Technology", technology.Id.ToString(),
                $"code={technology.Code}", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }

        return Result<bool>.Failure(Error.Conflict(
            "Only unreferenced draft technologies can be deleted. Archive the technology instead."));
    }

    private static Result<bool> ApplyStatus(Technology technology, string requestedStatus)
    {
        if (technology.Status == requestedStatus)
        {
            return Result.Success();
        }

        return requestedStatus switch
        {
            TechnologyStatuses.Published => technology.Publish(),
            TechnologyStatuses.Archived => technology.Archive(),
            _ => Result.Failure(Error.StateTransition(
                $"A technology in status '{technology.Status}' cannot move to '{requestedStatus}'."))
        };
    }

    private async Task<Result<TechnologyDto>> SaveTrackedAsync(
        Technology technology, string before, string auditAction, byte[]? expectedRowVersion, CancellationToken ct)
    {
        if (expectedRowVersion is not null)
        {
            _unitOfWork.SetOriginalRowVersion(technology, expectedRowVersion);
        }

        await _audit.RecordAsync(auditAction, "Technology", technology.Id.ToString(), before,
            $"status={technology.Status};name={technology.Name}", ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<TechnologyDto>.Failure(Error.PreconditionFailed(
                "The technology was modified by another request. Reload it and retry."));
        }

        return Result<TechnologyDto>.Success(Map(technology));
    }

    private bool IsAccessible(Technology? technology) =>
        technology is not null && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(technology.InstituteId);

    private static Dictionary<string, string[]> ValidateContent(
        string? code, string? name, string? description, string? applicationArea,
        string? technologyType, short? yearIntroduced)
    {
        var fields = new Dictionary<string, string[]>();
        if (code is not null && (string.IsNullOrWhiteSpace(code) || code.Length > 64))
        {
            fields["code"] = ["A code of at most 64 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 512)
        {
            fields["name"] = ["A name of at most 512 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            fields["description"] = ["A description is required."];
        }

        if (string.IsNullOrWhiteSpace(applicationArea) || applicationArea.Length > 256)
        {
            fields["applicationArea"] = ["An application area of at most 256 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(technologyType) || technologyType.Length > 64)
        {
            fields["technologyType"] = ["A technology type of at most 64 characters is required."];
        }

        if (yearIntroduced.HasValue && (yearIntroduced.Value < MinYear || yearIntroduced.Value > DateTime.UtcNow.Year + 1))
        {
            fields["yearIntroduced"] = [$"The year introduced must be between {MinYear} and {DateTime.UtcNow.Year + 1}."];
        }

        return fields;
    }

    private ListSlice<TechnologyDto> Map(ListSlice<Technology> slice) =>
        new(slice.Items.Select(Map).ToList(), slice.Next);

    private static TechnologyDto Map(Technology technology) => new(
        technology.Id, technology.InstituteId, technology.Code, technology.Name, technology.Description,
        technology.ApplicationArea, technology.LeadEmployeeId, technology.TechnologyType,
        technology.YearIntroduced, technology.HasIntellectualProperty, technology.Status,
        ConcurrencyToken.Format(technology.RowVersion), technology.CreatedAt, technology.UpdatedAt);
}
