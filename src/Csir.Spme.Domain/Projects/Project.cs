using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Projects;

public class Project : InstituteScopedEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? LeadEmployeeId { get; private set; }
    public string Objective { get; private set; } = string.Empty;
    public string? Justification { get; private set; }
    public string? Method { get; private set; }
    public string? ExpectedResult { get; private set; }
    public string? ActualResult { get; private set; }
    public string Status { get; private set; } = ProjectStatuses.Draft;
    public string? Nature { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string Currency { get; private set; } = "GHS";
    public decimal? BudgetAmount { get; private set; }
    public string? Innovation { get; private set; }
    public string? Impact { get; private set; }
    public Guid? ThrustId { get; private set; }

    private Project() { }

    public static Project Create(
        Guid instituteId,
        string code,
        string name,
        string objective,
        string? justification,
        string? method,
        string? expectedResult,
        string? nature,
        DateTime startDate,
        DateTime? endDate,
        string currency,
        decimal? budgetAmount,
        string? innovation,
        string? impact,
        Guid? leadEmployeeId,
        Guid? thrustId)
    {
        return new Project
        {
            InstituteId = instituteId,
            Code = code,
            Name = name,
            Objective = objective,
            Justification = justification,
            Method = method,
            ExpectedResult = expectedResult,
            Nature = nature,
            StartDate = startDate.Date,
            EndDate = endDate?.Date,
            Currency = currency,
            BudgetAmount = budgetAmount,
            Innovation = innovation,
            Impact = impact,
            LeadEmployeeId = leadEmployeeId,
            ThrustId = thrustId,
            Status = ProjectStatuses.Draft
        };
    }

    public bool IsEditable => Status is not (ProjectStatuses.Archived or ProjectStatuses.Cancelled);

    /// <summary>Updates project content. Archived or cancelled projects are immutable.</summary>
    public Result<bool> Update(
        string name,
        string objective,
        string? justification,
        string? method,
        string? expectedResult,
        string? actualResult,
        string? nature,
        DateTime startDate,
        DateTime? endDate,
        string currency,
        decimal? budgetAmount,
        string? innovation,
        string? impact,
        Guid? leadEmployeeId,
        Guid? thrustId)
    {
        if (!IsEditable)
        {
            return Result.Failure(Error.StateTransition(
                $"A project in status '{Status}' cannot be edited."));
        }

        Name = name;
        Objective = objective;
        Justification = justification;
        Method = method;
        ExpectedResult = expectedResult;
        ActualResult = actualResult;
        Nature = nature;
        StartDate = startDate.Date;
        EndDate = endDate?.Date;
        Currency = currency;
        BudgetAmount = budgetAmount;
        Innovation = innovation;
        Impact = impact;
        LeadEmployeeId = leadEmployeeId;
        ThrustId = thrustId;
        return Result.Success();
    }

    /// <summary>draft -> active.</summary>
    public Result<bool> Submit()
    {
        if (Status is not ProjectStatuses.Draft)
        {
            return Result.Failure(Error.StateTransition(
                $"A project in status '{Status}' cannot be submitted."));
        }

        Status = ProjectStatuses.Active;
        return Result.Success();
    }

    /// <summary>any non-archived status -> archived.</summary>
    public Result<bool> Archive()
    {
        if (Status is ProjectStatuses.Archived)
        {
            return Result.Failure(Error.StateTransition("The project is already archived."));
        }

        Status = ProjectStatuses.Archived;
        return Result.Success();
    }

    /// <summary>active | on-hold -> on-hold | completed | cancelled lifecycle moves.</summary>
    public Result<bool> MoveLifecycle(string targetStatus)
    {
        if (Status is not (ProjectStatuses.Active or ProjectStatuses.OnHold))
        {
            return Result.Failure(Error.StateTransition(
                $"A project in status '{Status}' cannot move to '{targetStatus}'."));
        }

        if (targetStatus is not (ProjectStatuses.OnHold or ProjectStatuses.Completed or ProjectStatuses.Cancelled))
        {
            return Result.Failure(Error.StateTransition(
                $"A project in status '{Status}' cannot move to '{targetStatus}'."));
        }

        Status = targetStatus;
        return Result.Success();
    }
}
