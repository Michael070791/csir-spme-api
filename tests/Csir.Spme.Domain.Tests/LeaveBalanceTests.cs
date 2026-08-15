using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public class LeaveBalanceTests
{
    [Fact]
    public void SetEntitlement_Replaces_Total_Days_Without_Changing_Usage()
    {
        var balance = LeaveBalance.CreateImported(
            Guid.NewGuid(), LeaveTypes.Annual, 2026, 20m, 5m, 2m, 1m);

        var result = balance.SetEntitlement(36m);

        result.IsSuccess.Should().BeTrue();
        balance.TotalDays.Should().Be(36m);
        balance.UsedDays.Should().Be(5m);
        balance.PendingDays.Should().Be(2m);
        balance.AdjustedDays.Should().Be(1m);
        balance.RemainingDays.Should().Be(30m);
    }

    [Fact]
    public void SetEntitlement_Rejects_Amount_Below_Used_And_Pending_Leave()
    {
        var balance = LeaveBalance.CreateImported(
            Guid.NewGuid(), LeaveTypes.Annual, 2026, 20m, 8m, 2m, 0m);

        var result = balance.SetEntitlement(9m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(SpmeErrorCodes.Conflict);
        balance.TotalDays.Should().Be(20m);
    }
}
