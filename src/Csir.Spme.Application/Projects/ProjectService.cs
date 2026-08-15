using System.Text.RegularExpressions;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Projects;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Projects;

public sealed partial class ProjectService
{
    private static readonly string[] AllowedSorts = ["code", "name", "startDate", "status"];

    private readonly IProjectRepository _projects;
    private readonly IInstituteDirectory _institutes;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ICursorCodec _cursorCodec;
    private readonly PaginationOptions _pagination;

    public ProjectService(
        IProjectRepository projects,
        IInstituteDirectory institutes,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser,
        ICursorCodec cursorCodec,
        IOptions<PaginationOptions> pagination)
    {
        _projects = projects;
        _institutes = institutes;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
        _cursorCodec = cursorCodec;
        _pagination = pagination.Value;
    }

    public async Task<Result<ListSlice<ProjectDto>>> ListAsync(
        Guid? instituteFilter, string? status, string? nature, Guid? leadEmployeeId, Guid? thrustId,
        int? limit, string? cursor, string? sort, string? direction, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, instituteFilter);
        if (scope.IsFailure)
        {
            return Result<ListSlice<ProjectDto>>.Failure(scope.Error!);
        }

        var fields = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(status) && !DomainValues.Contains(ProjectStatuses.All, status.Trim()))
        {
            fields["filter[status]"] = [$"Status must be one of: {string.Join(", ", ProjectStatuses.All)}."];
        }

        if (!string.IsNullOrWhiteSpace(nature) && !DomainValues.Contains(ProjectNatures.All, nature.Trim()))
        {
            fields["filter[nature]"] = [$"Nature must be one of: {string.Join(", ", ProjectNatures.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<ListSlice<ProjectDto>>.Failure(Error.Validation(fields));
        }

        var page = ListQueryParser.Parse(_cursorCodec, _pagination.DefaultLimit, _pagination.MaxLimit,
            limit, cursor, sort, direction, "code", false, AllowedSorts);
        if (page.IsFailure)
        {
            return Result<ListSlice<ProjectDto>>.Failure(page.Error!);
        }

        var slice = await _projects.ListAsync(scope.Value!.EffectiveFilter,
            status?.Trim(), nature?.Trim(), leadEmployeeId, thrustId, page.Value!, ct);
        return Result<ListSlice<ProjectDto>>.Success(Map(slice));
    }

    public async Task<Result<ProjectDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var project = await _projects.FindByIdAsync(id, ct);
        if (!IsAccessible(project))
        {
            return Result<ProjectDto>.Failure(Error.NotFound("Project not found."));
        }

        return Result<ProjectDto>.Success(Map(project!));
    }

    public async Task<Result<ProjectDto>> CreateAsync(CreateProjectCommand command, CancellationToken ct)
    {
        var scope = InstituteScope.Resolve(_currentUser.InstituteId, command.InstituteId);
        if (scope.IsFailure)
        {
            return Result<ProjectDto>.Failure(scope.Error!);
        }

        var fields = ValidateContent(command.Code, command.Name, command.Objective, command.Nature,
            command.StartDate, command.EndDate, command.Currency, command.BudgetAmount);
        if (fields.Count > 0)
        {
            return Result<ProjectDto>.Failure(Error.Validation(fields));
        }

        if (!scope.Value!.EffectiveFilter.HasValue)
        {
            return Result<ProjectDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["instituteId"] = ["An owning institute is required."]
            }));
        }

        var instituteId = scope.Value.EffectiveFilter.Value;
        if (!await _institutes.InstituteExistsAsync(instituteId, ct))
        {
            return Result<ProjectDto>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["instituteId"] = ["The institute does not exist."]
            }));
        }

        var references = await ValidateReferencesAsync(instituteId, command.LeadEmployeeId, command.ThrustId, ct);
        if (references is not null)
        {
            return Result<ProjectDto>.Failure(references);
        }

        var code = command.Code.Trim();
        if (await _projects.CodeExistsAsync(instituteId, code, null, ct))
        {
            return Result<ProjectDto>.Failure(Error.Conflict(
                "A project with the same code already exists for this institute."));
        }

        var project = Project.Create(instituteId, code, command.Name.Trim(), command.Objective.Trim(),
            command.Justification, null, command.ExpectedResult, command.Nature?.Trim(), command.StartDate,
            command.EndDate, NormalizeCurrency(command.Currency), command.BudgetAmount,
            command.Innovation, command.Impact, command.LeadEmployeeId, command.ThrustId);
        _projects.Add(project);
        await _audit.RecordAsync("project.created", "Project", project.Id.ToString(), null,
            $"code={project.Code}", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ProjectDto>.Success(Map(project));
    }

    public async Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        var project = await _projects.FindByIdAsync(id, ct);
        if (!IsAccessible(project))
        {
            return Result<ProjectDto>.Failure(Error.NotFound("Project not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<ProjectDto>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        }

        var fields = ValidateContent(null, command.Name, command.Objective, command.Nature,
            command.StartDate, command.EndDate, command.Currency, command.BudgetAmount);
        if (!DomainValues.Contains(ProjectStatuses.All, command.Status?.Trim()))
        {
            fields["status"] = [$"Status must be one of: {string.Join(", ", ProjectStatuses.All)}."];
        }

        if (fields.Count > 0)
        {
            return Result<ProjectDto>.Failure(Error.Validation(fields));
        }

        var references = await ValidateReferencesAsync(project!.InstituteId, command.LeadEmployeeId, command.ThrustId, ct);
        if (references is not null)
        {
            return Result<ProjectDto>.Failure(references);
        }

        var before = $"status={project.Status};code={project.Code}";
        var updated = project.Update(command.Name.Trim(), command.Objective.Trim(), command.Justification,
            null, command.ExpectedResult, command.ActualResult, command.Nature?.Trim(), command.StartDate,
            command.EndDate, NormalizeCurrency(command.Currency), command.BudgetAmount,
            command.Innovation, command.Impact, command.LeadEmployeeId, command.ThrustId);
        if (updated.IsFailure)
        {
            return Result<ProjectDto>.Failure(updated.Error!);
        }

        var transitioned = ApplyStatus(project!, command.Status!.Trim());
        if (transitioned.IsFailure)
        {
            return Result<ProjectDto>.Failure(transitioned.Error!);
        }

        return await SaveTrackedAsync(project, before, "project.updated", expectedRowVersion, ct);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _projects.FindByIdAsync(id, ct);
        if (!IsAccessible(project))
        {
            return Result<bool>.Failure(Error.NotFound("Project not found."));
        }

        if (project!.Status != ProjectStatuses.Draft)
        {
            return Result<bool>.Failure(Error.Conflict(
                "Only draft projects can be deleted. Archive the project instead."));
        }

        if (await _projects.HasDependenciesAsync(project.Id, ct))
        {
            return Result<bool>.Failure(Error.Conflict(
                "The project has milestones, funding, or updates and cannot be deleted."));
        }

        _projects.Remove(project);
        await _audit.RecordAsync("project.deleted", "Project", project.Id.ToString(),
            $"code={project.Code}", null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ProjectDto>> SubmitAsync(Guid id, CancellationToken ct)
    {
        return await TransitionAsync(id, "project.submitted", project => project.Submit(), ct);
    }

    public async Task<Result<ProjectDto>> ArchiveAsync(Guid id, CancellationToken ct)
    {
        return await TransitionAsync(id, "project.archived", project => project.Archive(), ct);
    }

    private async Task<Result<ProjectDto>> TransitionAsync(
        Guid id, string auditAction, Func<Project, Result<bool>> transition, CancellationToken ct)
    {
        var project = await _projects.FindByIdAsync(id, ct);
        if (!IsAccessible(project))
        {
            return Result<ProjectDto>.Failure(Error.NotFound("Project not found."));
        }

        var before = $"status={project!.Status};code={project.Code}";
        var transitioned = transition(project);
        if (transitioned.IsFailure)
        {
            return Result<ProjectDto>.Failure(transitioned.Error!);
        }

        return await SaveTrackedAsync(project, before, auditAction, null, ct);
    }

    private async Task<Result<ProjectDto>> SaveTrackedAsync(
        Project project, string before, string auditAction, byte[]? expectedRowVersion, CancellationToken ct)
    {
        if (expectedRowVersion is not null)
        {
            _unitOfWork.SetOriginalRowVersion(project, expectedRowVersion);
        }

        try
        {
            await _audit.RecordAsync(auditAction, "Project", project.Id.ToString(), before,
                $"status={project.Status};code={project.Code}", ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<ProjectDto>.Failure(Error.PreconditionFailed(
                "The project was modified by another request. Reload it and retry."));
        }

        return Result<ProjectDto>.Success(Map(project));
    }

    private async Task<Error?> ValidateReferencesAsync(Guid instituteId, Guid? leadEmployeeId, Guid? thrustId, CancellationToken ct)
    {
        if (leadEmployeeId.HasValue)
        {
            var lead = await _institutes.GetEmployeeScopeAsync(leadEmployeeId.Value, ct);
            if (lead is null || lead.InstituteId != instituteId)
            {
                return Error.Validation(new Dictionary<string, string[]>
                {
                    ["leadEmployeeId"] = ["The lead employee must belong to the project's institute."]
                });
            }
        }

        if (thrustId.HasValue)
        {
            var thrustInstituteId = await _projects.GetThrustInstituteAsync(thrustId.Value, ct);
            if (thrustInstituteId is null || thrustInstituteId.Value != instituteId)
            {
                return Error.Validation(new Dictionary<string, string[]>
                {
                    ["thrustId"] = ["The thrust must belong to the project's institute."]
                });
            }
        }

        return null;
    }

    private static Result<bool> ApplyStatus(Project project, string requestedStatus)
    {
        if (project.Status == requestedStatus)
        {
            return Result.Success();
        }

        return requestedStatus switch
        {
            ProjectStatuses.Active => project.Submit(),
            ProjectStatuses.Archived => project.Archive(),
            ProjectStatuses.OnHold or ProjectStatuses.Completed or ProjectStatuses.Cancelled
                when project.Status is ProjectStatuses.Active or ProjectStatuses.OnHold => project.MoveLifecycle(requestedStatus),
            _ => Result.Failure(Error.StateTransition(
                $"A project in status '{project.Status}' cannot move to '{requestedStatus}'."))
        };
    }

    private bool IsAccessible(Project? project) =>
        project is not null && InstituteScope.Resolve(_currentUser.InstituteId, null).Value!.CanAccess(project.InstituteId);

    private static string NormalizeCurrency(string currency) => currency.Trim().ToUpperInvariant();

    private static Dictionary<string, string[]> ValidateContent(
        string? code, string? name, string? objective, string? nature,
        DateTime startDate, DateTime? endDate, string? currency, decimal? budgetAmount)
    {
        var fields = new Dictionary<string, string[]>();
        if (code is not null && (string.IsNullOrWhiteSpace(code) || code.Length > 64))
        {
            fields["code"] = ["A code of at most 64 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            fields["name"] = ["A name of at most 256 characters is required."];
        }

        if (string.IsNullOrWhiteSpace(objective))
        {
            fields["objective"] = ["An objective is required."];
        }

        if (nature is not null && !DomainValues.Contains(ProjectNatures.All, nature.Trim()))
        {
            fields["nature"] = [$"Nature must be one of: {string.Join(", ", ProjectNatures.All)}."];
        }

        if (endDate.HasValue && endDate.Value < startDate)
        {
            fields["endDate"] = ["The end date cannot precede the start date."];
        }

        if (currency is not null && !CurrencyCode().IsMatch(currency.Trim()))
        {
            fields["currency"] = ["The currency must be a three-letter code."];
        }

        if (budgetAmount.HasValue && budgetAmount.Value < 0m)
        {
            fields["budgetAmount"] = ["The budget cannot be negative."];
        }

        return fields;
    }

    private ListSlice<ProjectDto> Map(ListSlice<Project> slice) =>
        new(slice.Items.Select(Map).ToList(), slice.Next);

    private static ProjectDto Map(Project project) => new(
        project.Id, project.InstituteId, project.Code, project.Name, project.Objective,
        project.Justification, project.ExpectedResult, project.ActualResult, project.Status,
        project.Nature, project.StartDate, project.EndDate, project.Currency, project.BudgetAmount,
        project.Innovation, project.Impact, project.LeadEmployeeId, project.ThrustId,
        ConcurrencyToken.Format(project.RowVersion), project.CreatedAt, project.UpdatedAt);

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyCode();
}
