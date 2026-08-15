using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class LeaveBalance : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public string LeaveType { get; private set; } = string.Empty;
    public short LeaveYear { get; private set; }
    public decimal TotalDays { get; private set; }
    public decimal UsedDays { get; private set; }
    public decimal PendingDays { get; private set; }
    public decimal AdjustedDays { get; private set; }

    private LeaveBalance() { }

    public static LeaveBalance Create(Guid employeeId, string leaveType, short leaveYear, decimal totalDays)
    {
        return new LeaveBalance
        {
            EmployeeId = employeeId,
            LeaveType = leaveType,
            LeaveYear = leaveYear,
            TotalDays = totalDays,
            UsedDays = 0m,
            PendingDays = 0m,
            AdjustedDays = 0m
        };
    }

    public static LeaveBalance CreateImported(
        Guid employeeId,
        string leaveType,
        short leaveYear,
        decimal totalDays,
        decimal usedDays,
        decimal pendingDays,
        decimal adjustedDays)
    {
        return new LeaveBalance
        {
            EmployeeId = employeeId,
            LeaveType = leaveType,
            LeaveYear = leaveYear,
            TotalDays = totalDays,
            UsedDays = usedDays,
            PendingDays = pendingDays,
            AdjustedDays = adjustedDays
        };
    }

    /// <summary>Days currently available for a new request.</summary>
    public decimal RemainingDays => TotalDays + AdjustedDays - UsedDays - PendingDays;

    /// <summary>Reserves days while a request is awaiting a decision.</summary>
    public Result<bool> Reserve(decimal days)
    {
        if (days <= 0m)
        {
            return Result.Failure(Error.Validation("Reserved days must be positive."));
        }

        if (RemainingDays < days)
        {
            return Result.Failure(Error.InsufficientLeaveBalance(
                $"The remaining {LeaveType} balance of {RemainingDays} days cannot cover {days} requested days."));
        }

        PendingDays += days;
        return Result.Success();
    }

    /// <summary>Releases a reservation without consuming it (reject/cancel of a pending request).</summary>
    public Result<bool> Release(decimal days)
    {
        if (days <= 0m || PendingDays < days)
        {
            return Result.Failure(Error.Conflict("The pending leave balance cannot be released."));
        }

        PendingDays -= days;
        return Result.Success();
    }

    /// <summary>Converts a reservation into used days when a request is approved.</summary>
    public Result<bool> Consume(decimal days)
    {
        if (days <= 0m || PendingDays < days)
        {
            return Result.Failure(Error.Conflict("The pending leave balance cannot be consumed."));
        }

        PendingDays -= days;
        UsedDays += days;
        return Result.Success();
    }

    /// <summary>Returns used days to the balance when an approved request is cancelled.</summary>
    public Result<bool> Credit(decimal days)
    {
        if (days <= 0m || UsedDays < days)
        {
            return Result.Failure(Error.Conflict("The used leave balance cannot be credited."));
        }

        UsedDays -= days;
        return Result.Success();
    }

    public Result<bool> AddAdjustment(decimal days)
    {
        if (days <= 0m)
        {
            return Result.Failure(Error.Validation("Leave balance adjustments must be positive."));
        }

        AdjustedDays += days;
        return Result.Success();
    }

    /// <summary>Sets the annual entitlement for this year without changing used, pending, or adjusted days.</summary>
    public Result<bool> SetEntitlement(decimal totalDays)
    {
        if (totalDays < 0m)
        {
            return Result.Failure(Error.Validation("Annual leave days cannot be negative."));
        }

        if (totalDays > 366m)
        {
            return Result.Failure(Error.Validation("Annual leave days cannot exceed 366."));
        }

        if (decimal.Round(totalDays, 2) != totalDays)
        {
            return Result.Failure(Error.Validation("Annual leave days can include at most two decimal places."));
        }

        if (totalDays + AdjustedDays < UsedDays + PendingDays)
        {
            return Result.Failure(Error.Conflict(
                "The assigned entitlement cannot be less than used and pending leave for this year."));
        }

        TotalDays = totalDays;
        return Result.Success();
    }
}
