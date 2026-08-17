using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Promotions;

public static class PromotionStatusMessages
{
    public const string SeniorMemberComingSoon =
        "Senior Member promotion review for this job title is coming soon. Assistant Research Scientist (non-PhD) is assessed under the 5-year M.Sc. upgrade to Research Scientist. You cannot start a promotion application for this title yet.";

    public const string JuniorStaffNotInScheme =
        "Junior Staff are not in the current Senior Staff promotion scheme. You cannot start a promotion application from this portal.";

    public const string SeniorStaffOnly =
        "Promotion applications in this portal currently apply to Senior Staff only.";

    public const string NoCycleOpen =
        "HR has not opened a 1 January promotion cycle yet. When the next cycle opens, Senior Staff time in grade is counted to that 1 January.";

    public const string NoCanonicalGrade =
        "HR still needs to assign your Conditions of Service job title from the catalog before a promotion path can be assessed. Staff category and grade step cannot select a path.";

    public const string NoMatchingPath =
        "No approved Senior Staff promotion path matches your job title. HR must select the correct Conditions of Service title before a review can start.";

    public const string EligibleAwaitingAssessment =
        "You meet the documented checks. HR still needs to open an assessment before a submission can start.";

    public const string EligibleStartSubmission =
        "Start a promotion submission for this cycle.";

    public const string EligibleCompleteRequirements =
        "Complete the required reports, documents, and declarations.";

    public const string EligibleWaitForHr =
        "Wait for HR to review the submitted promotion case.";

    public const string NeedsHrReview =
        "HR still needs to verify remaining evidence.";

    public const string NotEligible =
        "This assessment does not currently qualify for review.";

    public const string PolicyAmbiguity =
        "This path needs an approved policy confirmation before a submission can start.";

    public const string AwaitHrAssessment =
        "Await HR assessment for the current promotion cycle.";

    public static string ForStaffCategory(string? staffCategory)
    {
        if (string.Equals(staffCategory, StaffCategories.SeniorMember, StringComparison.OrdinalIgnoreCase))
            return SeniorMemberComingSoon;
        if (string.Equals(staffCategory, StaffCategories.JuniorStaff, StringComparison.OrdinalIgnoreCase))
            return JuniorStaffNotInScheme;
        return SeniorStaffOnly;
    }

    public static string NotYetEligible(short requiredYears, DateTime serviceRequirementMetOn, short cycleYear) =>
        $"{requiredYears} years in your present grade are required. You meet that date on {serviceRequirementMetOn:d MMM yyyy} for the {cycleYear} cycle (effective 1 January {cycleYear}).";
}
