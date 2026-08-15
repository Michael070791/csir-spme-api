using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Knowledge;

public class Technology : InstituteScopedEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string ApplicationArea { get; private set; } = string.Empty;
    public Guid? LeadEmployeeId { get; private set; }
    public string TechnologyType { get; private set; } = string.Empty;
    public short? YearIntroduced { get; private set; }
    public bool HasIntellectualProperty { get; private set; }
    public string Status { get; private set; } = TechnologyStatuses.Draft;

    private Technology() { }

    public static Technology Create(
        Guid instituteId,
        string code,
        string name,
        string description,
        string applicationArea,
        Guid? leadEmployeeId,
        string technologyType,
        short? yearIntroduced,
        bool hasIntellectualProperty)
    {
        return new Technology
        {
            InstituteId = instituteId,
            Code = code,
            Name = name,
            Description = description,
            ApplicationArea = applicationArea,
            LeadEmployeeId = leadEmployeeId,
            TechnologyType = technologyType,
            YearIntroduced = yearIntroduced,
            HasIntellectualProperty = hasIntellectualProperty,
            Status = TechnologyStatuses.Draft
        };
    }

    public bool IsEditable => Status is not TechnologyStatuses.Archived;

    /// <summary>Updates technology content. Archived technologies are immutable.</summary>
    public Result<bool> Update(
        string name,
        string description,
        string applicationArea,
        Guid? leadEmployeeId,
        string technologyType,
        short? yearIntroduced,
        bool hasIntellectualProperty)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition(
                $"An archived technology cannot be edited."));
        }

        Name = name;
        Description = description;
        ApplicationArea = applicationArea;
        LeadEmployeeId = leadEmployeeId;
        TechnologyType = technologyType;
        YearIntroduced = yearIntroduced;
        HasIntellectualProperty = hasIntellectualProperty;
        return Result.Success();
    }

    /// <summary>draft -> published.</summary>
    public Result<bool> Publish()
    {
        if (Status is not TechnologyStatuses.Draft)
        {
            return Result.Failure(Error.StateTransition(
                $"A technology in status '{Status}' cannot be published."));
        }

        Status = TechnologyStatuses.Published;
        return Result.Success();
    }

    /// <summary>draft | published -> archived.</summary>
    public Result<bool> Archive()
    {
        if (Status is TechnologyStatuses.Archived)
        {
            return Result.Failure(Error.StateTransition("The technology is already archived."));
        }

        Status = TechnologyStatuses.Archived;
        return Result.Success();
    }
}
