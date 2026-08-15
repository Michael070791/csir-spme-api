namespace Csir.Spme.Application.Reporting;

public sealed record ReportingPeriodDto(
    Guid Id, string ScopeType, Guid? InstituteId, string Code, string Name, string PeriodType,
    DateTime StartDate, DateTime EndDate, DateTime? DueDate, string Status,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateReportingPeriodCommand(
    string ScopeType, Guid? InstituteId, string Code, string Name, string PeriodType,
    DateTime StartDate, DateTime EndDate, DateTime? DueDate);

public sealed record ReportDto(
    Guid Id, Guid InstituteId, Guid ReportingPeriodId, string ReportType, string Title, string Summary,
    string? Abstract, string? KeyResults, string? Conclusion, string Status,
    DateTimeOffset? SubmittedAt, DateTimeOffset? ApprovedAt, string? ReturnReason,
    string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateReportCommand(
    Guid? InstituteId, Guid ReportingPeriodId, string ReportType, string Title, string Summary,
    string? Abstract, string? KeyResults, string? Conclusion);

public sealed record UpdateReportCommand(
    string Title, string Summary, string? Abstract, string? KeyResults, string? Conclusion);

public sealed record ReturnReportCommand(string ReturnReason);
