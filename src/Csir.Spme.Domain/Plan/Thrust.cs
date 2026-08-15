using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Plan;

public class Thrust : InstituteScopedEntity
{
    public Guid StrategicPlanId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Objective { get; private set; } = string.Empty;
    public short DisplayOrder { get; private set; }
    public string Status { get; private set; } = PlanItemStatuses.Draft;

    private Thrust() { }

    public static Thrust Create(
        Guid strategicPlanId,
        Guid instituteId,
        string code,
        string title,
        string description,
        string objective,
        short displayOrder)
    {
        return new Thrust
        {
            StrategicPlanId = strategicPlanId,
            InstituteId = instituteId,
            Code = code,
            Title = title,
            Description = description,
            Objective = objective,
            DisplayOrder = displayOrder,
            Status = PlanItemStatuses.Draft
        };
    }

    public Result<bool> Update(string title, string description, string objective, short displayOrder, string status)
    {
        if (Status is PlanItemStatuses.Archived)
        {
            return Result.Failure(Error.StateTransition("An archived thrust cannot be edited."));
        }

        Title = title;
        Description = description;
        Objective = objective;
        DisplayOrder = displayOrder;
        Status = status;
        return Result.Success();
    }
}
