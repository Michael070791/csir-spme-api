using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Plan;

public class Output : BaseEntity
{
    public Guid ThrustId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? OwnerUserId { get; private set; }
    public DateTime? DueDate { get; private set; }
    public string Status { get; private set; } = PlanItemStatuses.Draft;
    public short DisplayOrder { get; private set; }

    private Output() { }

    public static Output Create(
        Guid thrustId,
        string code,
        string description,
        Guid? ownerUserId,
        DateTime? dueDate,
        short displayOrder)
    {
        return new Output
        {
            ThrustId = thrustId,
            Code = code,
            Description = description,
            OwnerUserId = ownerUserId,
            DueDate = dueDate?.Date,
            DisplayOrder = displayOrder,
            Status = PlanItemStatuses.Draft
        };
    }

    public Result<bool> Update(string description, Guid? ownerUserId, DateTime? dueDate, short displayOrder, string status)
    {
        if (Status is PlanItemStatuses.Archived)
        {
            return Result.Failure(Error.StateTransition("An archived output cannot be edited."));
        }

        Description = description;
        OwnerUserId = ownerUserId;
        DueDate = dueDate?.Date;
        DisplayOrder = displayOrder;
        Status = status;
        return Result.Success();
    }
}
