using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Promotions;

public static class PromotionStaffCheck
{
    public const decimal DaysPerYear = 365.2425m;

    public static bool HasQualifyingEducation(IEnumerable<string> qualificationLevels, string requiredLevel) =>
        qualificationLevels.Any(level => MeetsOrExceeds(level, requiredLevel));

    public static bool MeetsOrExceeds(string? recordedLevel, string requiredLevel)
    {
        var recorded = Rank(recordedLevel);
        return recorded > 0 && recorded >= Rank(requiredLevel);
    }

    public static string RequiredQualificationFor(string? staffCategory, string? pathRequiredLevel)
    {
        if (!string.IsNullOrWhiteSpace(pathRequiredLevel))
            return pathRequiredLevel;

        return string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase)
            ? QualificationLevels.MastersOrEquivalent
            : QualificationLevels.BachelorOrEquivalent;
    }

    public static short InferredMinimumYears(string? staffCategory, short? pathYears)
    {
        if (pathYears is > 0)
            return pathYears.Value;

        return string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase)
            ? (short)5
            : (short)4;
    }

    public static decimal CompletedYears(DateTime startDate, DateTime asOf) =>
        Math.Max(0m, (decimal)(asOf.Date - startDate.Date).TotalDays / DaysPerYear);

    public static string FormatYears(decimal years)
    {
        if (years < 0.1m)
            return "Less than 1 year";

        var rounded = Math.Round(years, 1, MidpointRounding.AwayFromZero);
        if (rounded == 0m)
            return "Less than 1 year";

        return rounded == 1m ? "1 year" : $"{rounded:0.#} years";
    }

    public static bool AllowsApplicationDraft(string? staffCategory, bool hasActivePath) =>
        string.Equals(staffCategory, PromotionConstants.SeniorStaff, StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase) && hasActivePath);

    private static int Rank(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        QualificationLevels.Certificate => 1,
        QualificationLevels.Diploma => 2,
        QualificationLevels.BachelorOrEquivalent => 3,
        QualificationLevels.MastersOrEquivalent => 4,
        QualificationLevels.DoctorateOrEquivalent => 5,
        _ => 0
    };
}
