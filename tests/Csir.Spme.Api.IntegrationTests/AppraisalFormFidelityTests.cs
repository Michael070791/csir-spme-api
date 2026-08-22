using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Infrastructure.Communications;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AppraisalFormFidelityTests
{
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    [Fact]
    public void Canonical_Docx_Matches_The_Frozen_Template_Identity_And_Official_Inventory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", AppraisalFormTemplate.SourceDocumentFileName);
        File.Exists(path).Should().BeTrue("the authoritative appraisal DOCX must accompany fidelity tests");

        var sourceBytes = File.ReadAllBytes(path);
        Convert.ToHexStringLower(SHA256.HashData(sourceBytes))
            .Should().Be(AppraisalFormTemplate.CanonicalContentChecksum);

        using var archive = ZipFile.OpenRead(path);
        var applicationProperties = ReadXml(archive, "docProps/app.xml");
        XNamespace properties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        int.Parse(applicationProperties.Root!.Element(properties + "Pages")!.Value)
            .Should().Be(AppraisalFormTemplate.SourceNumberedPageCount);

        var document = ReadXml(archive, "word/document.xml");
        document.Descendants(Word + "tbl").Should().HaveCount(AppraisalFormTemplate.SourceTableCount);
        var pageSize = document.Descendants(Word + "pgSz").Should().ContainSingle().Subject;
        pageSize.Attribute(Word + "w")!.Value.Should().Be("12240");
        pageSize.Attribute(Word + "h")!.Value.Should().Be("15840");

        var sourceText = Normalize(string.Join(' ', document.Descendants(Word + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(Word + "t").Select(node => node.Value)))));
        AssertOrdered(sourceText,
            "COUNCIL FOR SCIENTIFIC AND INDUSTRIAL RESEARCH",
            "PERFORMANCE APPRAISAL",
            "MANAGEMENT FORM",
            "STRICTLY CONFIDENTIAL",
            "CSIR PERFORMANCE MANAGEMENT",
            "STAFF PERFORMANCE PLANNING, REVIEW AND APPRAISAL FORM",
            "PART I",
            "SECTION A: APPRAISEE PERSONAL DATA",
            "SECTION B: APPRAISER (HEAD) INFORMATION",
            "PART II",
            "PERFORMANCE PLANNING STAGE",
            "PERFORMANCE /MID-YEAR PROGRESS REVIEW",
            "PART III",
            "END OF YEAR ASSESSMENT",
            "PART IV",
            "PERFORMANCE STANDARD",
            "PART V",
            "OVERALL ASSESSMENT: (REFER TO PART III)",
            "PART VI",
            "APPENDIX");

        foreach (var factor in AppraisalFactors.Behavioral.Concat(AppraisalFactors.Core))
            sourceText.Should().Contain(Normalize(factor.Label));
        foreach (var guidance in AppraisalFactors.BehavioralRatingGuidance.Concat(AppraisalFactors.CoreRatingGuidance))
            sourceText.Should().Contain(Normalize(guidance.Explanation));
        sourceText.Should().Contain("Consideration for promotion to")
            .And.Contain("Performance bonus")
            .And.Contain("Training in")
            .And.Contain("Reassignment")
            .And.Contain("Reprimand/caution")
            .And.Contain("Termination of appointment");

        var logo = ReadEntry(archive, "word/media/image1.jpeg");
        Convert.ToHexStringLower(SHA256.HashData(logo)).Should().Be(AppraisalFormTemplate.OfficialLogoChecksum);
    }

    [Fact]
    public void Pdf_Renderer_Embeds_The_Exact_Logo_From_The_Authoritative_Docx()
    {
        const string resourceName =
            "Csir.Spme.Infrastructure.Communications.Templates.CSIR-performance-appraisal-logo.jpeg";
        using var stream = typeof(AppraisalPdf).Assembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull();
        using var memory = new MemoryStream();
        stream!.CopyTo(memory);
        Convert.ToHexStringLower(SHA256.HashData(memory.ToArray()))
            .Should().Be(AppraisalFormTemplate.OfficialLogoChecksum);
        AppraisalPdf.PhysicalPageCount.Should().Be(AppraisalFormTemplate.PhysicalPageCount);
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        using var stream = archive.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }

    private static byte[] ReadEntry(ZipArchive archive, string path)
    {
        using var stream = archive.GetEntry(path)!.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static void AssertOrdered(string content, params string[] markers)
    {
        var previous = -1;
        foreach (var marker in markers)
        {
            var current = content.IndexOf(marker, StringComparison.Ordinal);
            current.Should().BeGreaterThan(previous, $"because '{marker}' must retain its official position");
            previous = current;
        }
    }
}
