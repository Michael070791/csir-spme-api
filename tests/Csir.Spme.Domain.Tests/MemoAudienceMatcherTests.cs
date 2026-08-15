using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Constants;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class MemoAudienceMatcherTests
{
    [Fact]
    public void Institute_Audience_Matches_Employees_In_That_Institute_Only()
    {
        var instituteId = Guid.NewGuid();
        var audiences = new[] { new MemoAudience(Guid.NewGuid(), MemoAudienceTypes.Institute, instituteId) };

        MemoAudienceMatcher.Matches(audiences, Guid.NewGuid(), instituteId, null, null, new HashSet<string>())
            .Should().BeTrue();
        MemoAudienceMatcher.Matches(audiences, Guid.NewGuid(), Guid.NewGuid(), null, null, new HashSet<string>())
            .Should().BeFalse();
    }

    [Fact]
    public void Selected_Employees_Match_Any_Listed_Person()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var memoId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var audiences = new[]
        {
            new MemoAudience(memoId, MemoAudienceTypes.Employee, instituteId, employeeId: first),
            new MemoAudience(memoId, MemoAudienceTypes.Employee, instituteId, employeeId: second)
        };

        MemoAudienceMatcher.Matches(audiences, first, instituteId, null, null, new HashSet<string>()).Should().BeTrue();
        MemoAudienceMatcher.Matches(audiences, second, instituteId, null, null, new HashSet<string>()).Should().BeTrue();
        MemoAudienceMatcher.Matches(audiences, Guid.NewGuid(), instituteId, null, null, new HashSet<string>())
            .Should().BeFalse();
    }

    [Fact]
    public void Sms_Synopsis_Stays_Within_The_Documented_Length()
    {
        var synopsis = MemoAudienceMatcher.SmsSynopsis(
            "Council circular",
            string.Join(' ', Enumerable.Repeat("Please acknowledge this memorandum immediately.", 12)));

        synopsis.Should().StartWith("Council circular:");
        synopsis.Length.Should().BeLessThanOrEqualTo(MemoAudienceMatcher.SmsSynopsisMaxLength);
        synopsis.Should().EndWith("...");
    }
}
