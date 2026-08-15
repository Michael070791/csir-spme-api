using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Plan;

public class Indicator : BaseEntity
{
    public Guid OutputId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal? BaselineValue { get; private set; }
    public decimal? TargetValue { get; private set; }
    public string? VerificationMethod { get; private set; }
    public DateTime? DueDate { get; private set; }
    public string Status { get; private set; } = PlanItemStatuses.Draft;

    private Indicator() { }

    public static Indicator Create(
        Guid outputId,
        string code,
        string description,
        string unitOfMeasure,
        decimal? baselineValue,
        decimal? targetValue,
        string? verificationMethod,
        DateTime? dueDate)
    {
        return new Indicator
        {
            OutputId = outputId,
            Code = code,
            Description = description,
            UnitOfMeasure = unitOfMeasure,
            BaselineValue = baselineValue,
            TargetValue = targetValue,
            VerificationMethod = verificationMethod,
            DueDate = dueDate?.Date,
            Status = PlanItemStatuses.Draft
        };
    }

    public Result<bool> Update(
        string description,
        string unitOfMeasure,
        decimal? baselineValue,
        decimal? targetValue,
        string? verificationMethod,
        DateTime? dueDate,
        string status)
    {
        if (Status is PlanItemStatuses.Archived)
        {
            return Result.Failure(Error.StateTransition("An archived indicator cannot be edited."));
        }

        Description = description;
        UnitOfMeasure = unitOfMeasure;
        BaselineValue = baselineValue;
        TargetValue = targetValue;
        VerificationMethod = verificationMethod;
        DueDate = dueDate?.Date;
        Status = status;
        return Result.Success();
    }
}
