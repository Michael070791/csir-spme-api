using System.Text;
using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Hr;

public sealed record EducationCertificate(
    string Code,
    string Label,
    string Name,
    string QualificationLevel,
    bool IsOpenAward = false)
{
    public bool AllowsQualificationLevel(string qualificationLevel) =>
        IsOpenAward ||
        string.Equals(QualificationLevel, qualificationLevel, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Canonical academic awards used for staff education records and reporting.
/// Stored <c>CertificateAwarded</c> values are the stable codes (BSc, MPhil, MSc, BE).
/// </summary>
public static class EducationCertificateCatalog
{
    private static readonly Dictionary<string, EducationCertificate> Lookup = new(StringComparer.Ordinal);
    private static readonly EducationCertificate[] Items = Build();

    public static IReadOnlyList<EducationCertificate> All => Items;

    public static IReadOnlyList<EducationCertificate> ForQualificationLevel(string? qualificationLevel)
    {
        if (string.IsNullOrWhiteSpace(qualificationLevel))
            return All;

        var level = qualificationLevel.Trim().ToLowerInvariant();
        return All.Where(item => item.AllowsQualificationLevel(level)).ToArray();
    }

    public static bool TryResolve(string? value, out EducationCertificate certificate)
    {
        certificate = null!;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return Lookup.TryGetValue(NormalizeKey(value), out certificate!);
    }

    public static string Canonicalize(string? value) =>
        TryResolve(value, out var certificate) ? certificate.Code : value?.Trim() ?? string.Empty;

    private static EducationCertificate[] Build()
    {
        var items = new List<EducationCertificate>();

        void Add(string code, string name, string qualificationLevel, bool isOpenAward = false, params string[] aliases)
        {
            var item = new EducationCertificate(code, code, name, qualificationLevel, isOpenAward);
            items.Add(item);
            Register(item, aliases);
        }

        Add("Certificate", "Certificate", QualificationLevels.Certificate, false, "Cert", "Cert.");
        Add("Advanced Certificate", "Advanced Certificate", QualificationLevels.Certificate);
        Add("Professional Certificate", "Professional Certificate", QualificationLevels.Certificate, false, "Prof Cert");

        Add("Diploma", "Diploma", QualificationLevels.Diploma, false, "Dip", "Dip.");
        Add("HND", "Higher National Diploma", QualificationLevels.Diploma, false, "Higher National Diploma");
        Add("OND", "Ordinary National Diploma", QualificationLevels.Diploma, false, "Ordinary National Diploma");
        Add("DipEd", "Diploma in Education", QualificationLevels.Diploma, false, "Diploma in Education");

        Add("BSc", "Bachelor of Science", QualificationLevels.BachelorOrEquivalent, false,
            "B.Sc", "B.Sc.", "BSc.", "Bachelor of Science", "B.Sc. Honours");
        Add("BA", "Bachelor of Arts", QualificationLevels.BachelorOrEquivalent, false,
            "B.A", "B.A.", "BA.", "Bachelor of Arts");
        Add("BEd", "Bachelor of Education", QualificationLevels.BachelorOrEquivalent, false,
            "B.Ed", "B.Ed.", "BEd.", "Bachelor of Education");
        Add("BEng", "Bachelor of Engineering", QualificationLevels.BachelorOrEquivalent, false,
            "B.Eng", "B.Eng.", "BEng.", "Bachelor of Engineering");
        Add("BE", "Bachelor of Engineering", QualificationLevels.BachelorOrEquivalent, false,
            "B.E", "B.E.", "BE.");
        Add("BTech", "Bachelor of Technology", QualificationLevels.BachelorOrEquivalent, false,
            "B.Tech", "B.Tech.", "BTech.", "Bachelor of Technology");
        Add("LLB", "Bachelor of Laws", QualificationLevels.BachelorOrEquivalent, false,
            "LL.B", "LL.B.", "LLB.", "Bachelor of Laws");
        Add("BPharm", "Bachelor of Pharmacy", QualificationLevels.BachelorOrEquivalent, false,
            "B.Pharm", "B.Pharm.", "Bachelor of Pharmacy");
        Add("BCom", "Bachelor of Commerce", QualificationLevels.BachelorOrEquivalent, false,
            "B.Com", "B.Com.", "Bachelor of Commerce");
        Add("BBA", "Bachelor of Business Administration", QualificationLevels.BachelorOrEquivalent, false,
            "Bachelor of Business Administration");
        Add("BAdmin", "Bachelor of Administration", QualificationLevels.BachelorOrEquivalent, false,
            "B.Admin", "Bachelor of Administration");
        Add("BFA", "Bachelor of Fine Arts", QualificationLevels.BachelorOrEquivalent, false,
            "B.F.A", "Bachelor of Fine Arts");
        Add("BArch", "Bachelor of Architecture", QualificationLevels.BachelorOrEquivalent, false,
            "B.Arch", "Bachelor of Architecture");
        Add("BAgric", "Bachelor of Agriculture", QualificationLevels.BachelorOrEquivalent, false,
            "B.Agric", "Bachelor of Agriculture", "BSc Agriculture");
        Add("BNSc", "Bachelor of Nursing Science", QualificationLevels.BachelorOrEquivalent, false,
            "B.NSc", "Bachelor of Nursing Science");
        Add("MBChB", "Bachelor of Medicine and Bachelor of Surgery", QualificationLevels.BachelorOrEquivalent, false,
            "MBChB.", "MB.ChB", "MBBS", "M.B.B.S", "Bachelor of Medicine");

        Add("PGD", "Postgraduate Diploma", QualificationLevels.Other, false,
            "PgD", "PgDip", "Postgraduate Diploma");
        Add("PgCert", "Postgraduate Certificate", QualificationLevels.Other, false,
            "PGC", "Postgraduate Certificate");

        Add("MSc", "Master of Science", QualificationLevels.MastersOrEquivalent, false,
            "M.Sc", "M.Sc.", "MSc.", "Master of Science");
        Add("MA", "Master of Arts", QualificationLevels.MastersOrEquivalent, false,
            "M.A", "M.A.", "MA.", "Master of Arts");
        Add("MPhil", "Master of Philosophy", QualificationLevels.MastersOrEquivalent, false,
            "M.Phil", "M.Phil.", "MPhil.", "Master of Philosophy");
        Add("MEd", "Master of Education", QualificationLevels.MastersOrEquivalent, false,
            "M.Ed", "M.Ed.", "Master of Education");
        Add("MBA", "Master of Business Administration", QualificationLevels.MastersOrEquivalent, false,
            "M.B.A", "Master of Business Administration");
        Add("MEng", "Master of Engineering", QualificationLevels.MastersOrEquivalent, false,
            "M.Eng", "M.Eng.", "Master of Engineering");
        Add("ME", "Master of Engineering", QualificationLevels.MastersOrEquivalent, false, "M.E", "M.E.");
        Add("LLM", "Master of Laws", QualificationLevels.MastersOrEquivalent, false,
            "LL.M", "LL.M.", "Master of Laws");
        Add("MPH", "Master of Public Health", QualificationLevels.MastersOrEquivalent, false,
            "M.P.H", "Master of Public Health");
        Add("MTech", "Master of Technology", QualificationLevels.MastersOrEquivalent, false,
            "M.Tech", "Master of Technology");
        Add("MPA", "Master of Public Administration", QualificationLevels.MastersOrEquivalent, false,
            "M.P.A", "Master of Public Administration");
        Add("MCom", "Master of Commerce", QualificationLevels.MastersOrEquivalent, false,
            "M.Com", "Master of Commerce");
        Add("MFA", "Master of Fine Arts", QualificationLevels.MastersOrEquivalent, false, "Master of Fine Arts");
        Add("MArch", "Master of Architecture", QualificationLevels.MastersOrEquivalent, false,
            "M.Arch", "Master of Architecture");

        Add("PhD", "Doctor of Philosophy", QualificationLevels.DoctorateOrEquivalent, false,
            "Ph.D", "Ph.D.", "PhD.", "Doctor of Philosophy");
        Add("DPhil", "Doctor of Philosophy", QualificationLevels.DoctorateOrEquivalent, false,
            "D.Phil", "D.Phil.");
        Add("DSc", "Doctor of Science", QualificationLevels.DoctorateOrEquivalent, false,
            "D.Sc", "D.Sc.", "Doctor of Science");
        Add("EdD", "Doctor of Education", QualificationLevels.DoctorateOrEquivalent, false,
            "Ed.D", "Ed.D.", "Doctor of Education");
        Add("DBA", "Doctor of Business Administration", QualificationLevels.DoctorateOrEquivalent, false,
            "Doctor of Business Administration");
        Add("MD", "Doctor of Medicine", QualificationLevels.DoctorateOrEquivalent, false,
            "M.D", "M.D.", "Doctor of Medicine");
        Add("DEng", "Doctor of Engineering", QualificationLevels.DoctorateOrEquivalent, false,
            "D.Eng", "Doctor of Engineering");

        Add("Other", "Other award not listed", QualificationLevels.Other, true, "Others");

        return items.ToArray();
    }

    private static void Register(EducationCertificate item, IEnumerable<string> aliases)
    {
        foreach (var candidate in aliases.Prepend(item.Code).Prepend(item.Label).Prepend(item.Name))
        {
            var key = NormalizeKey(candidate);
            if (string.IsNullOrEmpty(key) || Lookup.ContainsKey(key))
                continue;
            Lookup[key] = item;
        }
    }

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
