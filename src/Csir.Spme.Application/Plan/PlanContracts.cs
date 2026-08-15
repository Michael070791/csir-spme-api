namespace Csir.Spme.Application.Plan;

public sealed record StrategicPlanDto(
    Guid Id, Guid InstituteId, string Code, string Name, string Definition, string Objective,
    short StartYear, short EndYear, string Status, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateStrategicPlanCommand(
    Guid? InstituteId, string Code, string Name, string Definition, string Objective,
    short StartYear, short EndYear);

public sealed record UpdateStrategicPlanCommand(
    string Name, string Definition, string Objective, short StartYear, short EndYear);

public sealed record ThrustDto(
    Guid Id, Guid StrategicPlanId, Guid InstituteId, string Code, string Title, string Description,
    string Objective, short DisplayOrder, string Status, string Etag,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateThrustCommand(
    string Code, string Title, string Description, string Objective, short DisplayOrder);

public sealed record UpdateThrustCommand(
    string Title, string Description, string Objective, short DisplayOrder, string Status);

public sealed record OutputDto(
    Guid Id, Guid ThrustId, string Code, string Description, Guid? OwnerUserId, DateTime? DueDate,
    string Status, short DisplayOrder, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateOutputCommand(
    Guid ThrustId, string Code, string Description, Guid? OwnerUserId, DateTime? DueDate, short DisplayOrder);

public sealed record UpdateOutputCommand(
    string Description, Guid? OwnerUserId, DateTime? DueDate, short DisplayOrder, string Status);

public sealed record IndicatorDto(
    Guid Id, Guid OutputId, string Code, string Description, string UnitOfMeasure,
    decimal? BaselineValue, decimal? TargetValue, string? VerificationMethod, DateTime? DueDate,
    string Status, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateIndicatorCommand(
    string Code, string Description, string UnitOfMeasure, decimal? BaselineValue,
    decimal? TargetValue, string? VerificationMethod, DateTime? DueDate);

public sealed record UpdateIndicatorCommand(
    string Description, string UnitOfMeasure, decimal? BaselineValue, decimal? TargetValue,
    string? VerificationMethod, DateTime? DueDate, string Status);

public sealed record IndicatorMeasurementDto(
    Guid Id, Guid IndicatorId, Guid ReportingPeriodId, decimal Value, decimal? Variance,
    string? Remarks, Guid? EvidenceFileId, Guid RecordedByUserId, string Etag,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateIndicatorMeasurementCommand(
    Guid ReportingPeriodId, decimal Value, string? Remarks, Guid? EvidenceFileId);

public sealed record UpdateIndicatorMeasurementCommand(
    decimal Value, string? Remarks, Guid? EvidenceFileId);
