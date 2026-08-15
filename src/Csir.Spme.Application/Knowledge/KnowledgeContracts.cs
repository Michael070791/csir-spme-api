namespace Csir.Spme.Application.Knowledge;

public sealed record TechnologyDto(
    Guid Id, Guid InstituteId, string Code, string Name, string Description, string ApplicationArea,
    Guid? LeadEmployeeId, string TechnologyType, short? YearIntroduced, bool HasIntellectualProperty,
    string Status, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateTechnologyCommand(
    Guid? InstituteId, string Code, string Name, string Description, string ApplicationArea,
    Guid? LeadEmployeeId, string TechnologyType, short? YearIntroduced, bool HasIntellectualProperty);

public sealed record UpdateTechnologyCommand(
    string Name, string Description, string ApplicationArea, Guid? LeadEmployeeId,
    string TechnologyType, short? YearIntroduced, bool HasIntellectualProperty, string Status);
