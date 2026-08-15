using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public class EducationCertificateCatalogTests
{
    [Theory]
    [InlineData("BSc", "BSc")]
    [InlineData("B.Sc.", "BSc")]
    [InlineData("Bachelor of Science", "BSc")]
    [InlineData("BE", "BE")]
    [InlineData("B.E.", "BE")]
    [InlineData("MPhil", "MPhil")]
    [InlineData("M.Phil.", "MPhil")]
    [InlineData("MSc", "MSc")]
    [InlineData("PhD", "PhD")]
    [InlineData("Ph.D.", "PhD")]
    public void Resolves_Common_Award_Aliases_To_Canonical_Codes(string input, string expectedCode)
    {
        EducationCertificateCatalog.TryResolve(input, out var certificate).Should().BeTrue();
        certificate.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Rejects_Free_Text_Awards()
    {
        EducationCertificateCatalog.TryResolve("My Custom Degree", out _).Should().BeFalse();
    }

    [Fact]
    public void Bachelor_Awards_Do_Not_Match_Masters_Level()
    {
        EducationCertificateCatalog.TryResolve("BSc", out var certificate).Should().BeTrue();
        certificate.AllowsQualificationLevel(QualificationLevels.BachelorOrEquivalent).Should().BeTrue();
        certificate.AllowsQualificationLevel(QualificationLevels.MastersOrEquivalent).Should().BeFalse();
    }

    [Fact]
    public void Other_Award_Is_Available_At_Every_Qualification_Level()
    {
        EducationCertificateCatalog.TryResolve("Other", out var certificate).Should().BeTrue();
        certificate.IsOpenAward.Should().BeTrue();
        foreach (var level in QualificationLevels.All)
            certificate.AllowsQualificationLevel(level).Should().BeTrue();
    }

    [Fact]
    public void Catalog_Includes_Requested_Analytics_Awards()
    {
        EducationCertificateCatalog.All.Select(item => item.Code).Should().Contain(["BSc", "BE", "BEng", "MSc", "MPhil", "PhD"]);
    }
}
