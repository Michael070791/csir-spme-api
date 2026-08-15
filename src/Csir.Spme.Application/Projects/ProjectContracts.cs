namespace Csir.Spme.Application.Projects;

public sealed record ProjectDto(
    Guid Id, Guid InstituteId, string Code, string Name, string Objective, string? Justification,
    string? ExpectedResult, string? ActualResult, string Status, string? Nature,
    DateTime StartDate, DateTime? EndDate, string Currency, decimal? BudgetAmount,
    string? Innovation, string? Impact, Guid? LeadEmployeeId, Guid? ThrustId,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateProjectCommand(
    Guid? InstituteId, string Code, string Name, string Objective, string? Justification,
    string? ExpectedResult, string? Nature, DateTime StartDate, DateTime? EndDate,
    string Currency, decimal? BudgetAmount, string? Innovation, string? Impact,
    Guid? LeadEmployeeId, Guid? ThrustId);

public sealed record UpdateProjectCommand(
    string Name, string Objective, string? Justification, string? ExpectedResult,
    string? ActualResult, string? Nature, DateTime StartDate, DateTime? EndDate,
    string Currency, decimal? BudgetAmount, string? Innovation, string? Impact,
    Guid? LeadEmployeeId, Guid? ThrustId, string Status);
