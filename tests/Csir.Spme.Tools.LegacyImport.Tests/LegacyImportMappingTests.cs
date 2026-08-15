using Csir.Spme.Tools.LegacyImport;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Tools.LegacyImport.Tests;

public class LegacyImportMappingTests
{
    [Theory]
    [InlineData("Senior Staff", "senior-staff")]
    [InlineData("senior-staff", "senior-staff")]
    [InlineData("Junior Staff", "junior-staff")]
    [InlineData("Senior Member", "senior-member")]
    public void StaffCategoryMapper_Maps_Legacy_Designation_To_Controlled_Value(string source, string expected)
    {
        LegacyStaffCategoryMapper.Map(source).Should().Be(expected);
    }

    [Fact]
    public void StaffCategoryMapper_Does_Not_Map_Unknown_Designation()
    {
        LegacyStaffCategoryMapper.Map("Chief Research Scientist").Should().BeNull();
    }

    [Theory]
    [InlineData("2026-06-22", 2026, 6, 22)]
    [InlineData("22/06/2026", 2026, 6, 22)]
    [InlineData("6/22/2026", 2026, 6, 22)]
    public void DateParser_Parses_Known_Legacy_Date_Formats(string source, int year, int month, int day)
    {
        LegacyValueParser.ParseDate(source).Should().Be(new DateTime(year, month, day));
    }

    [Fact]
    public void DateParser_Returns_Null_For_Invalid_Date()
    {
        LegacyValueParser.ParseDate("not-a-date").Should().BeNull();
    }

    [Fact]
    public void DateTimeOffsetParser_Normalizes_Legacy_Values_To_Utc()
    {
        LegacyValueParser.ParseDateTimeOffset("2026-06-22T10:14:00Z")
            .Should().Be(new DateTimeOffset(2026, 6, 22, 10, 14, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RowPrecedence_Prefers_UpdatedAt_Then_Highest_SourceId()
    {
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var older = LegacyRowPrecedence.From("2026-06-22T10:00:00Z", null, higherId);
        var newer = LegacyRowPrecedence.From("2026-06-22T10:00:00Z", "2026-06-23T10:00:00Z", lowerId);
        var sameTimestampHigherId = LegacyRowPrecedence.From("2026-06-23T10:00:00Z", null, higherId);

        newer.CompareTo(older).Should().BePositive();
        sameTimestampHigherId.CompareTo(newer).Should().BePositive();
    }

    [Fact]
    public void RowPrecedence_Uses_Minimum_Timestamp_For_Unparseable_Legacy_Dates()
    {
        var precedence = LegacyRowPrecedence.From("not-a-date", "also-not-a-date", Guid.Empty);

        precedence.Timestamp.Should().Be(DateTimeOffset.MinValue);
    }
}
