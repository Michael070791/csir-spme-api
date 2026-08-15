using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Plan;

public class StrategicPlan : InstituteScopedEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Definition { get; private set; } = string.Empty;
    public string Objective { get; private set; } = string.Empty;
    public short StartYear { get; private set; }
    public short EndYear { get; private set; }
    public string Status { get; private set; } = "draft";

    private StrategicPlan() { }

    public static StrategicPlan Create(
        Guid instituteId,
        string code,
        string name,
        string definition,
        string objective,
        short startYear,
        short endYear)
    {
        return new StrategicPlan
        {
            InstituteId = instituteId,
            Code = code.Trim(),
            Name = name.Trim(),
            Definition = definition.Trim(),
            Objective = objective.Trim(),
            StartYear = startYear,
            EndYear = endYear,
            Status = "draft"
        };
    }

    public Result<bool> Update(
        string name,
        string definition,
        string objective,
        short startYear,
        short endYear)
    {
        if (Status != "draft")
            return Result.Failure(Error.StateTransition("Only draft strategic plans can be updated."));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 ||
            string.IsNullOrWhiteSpace(definition) ||
            string.IsNullOrWhiteSpace(objective) ||
            startYear is < 2000 or > 3000 ||
            endYear is < 2000 or > 3000 ||
            endYear < startYear)
            return Result.Failure(Error.Validation("Valid name, definition, objective, and year range are required."));

        Name = name.Trim();
        Definition = definition.Trim();
        Objective = objective.Trim();
        StartYear = startYear;
        EndYear = endYear;
        return Result.Success();
    }

    public Result<bool> Activate()
    {
        if (Status != "draft")
            return Result.Failure(Error.StateTransition(
                $"A strategic plan in status '{Status}' cannot be activated."));
        Status = "active";
        return Result.Success();
    }
}
