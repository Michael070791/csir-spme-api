namespace Csir.Spme.Domain.Hr;

public static class AppraisalCycleStatuses
{
    public const string Draft = "draft";
    public const string Open = "open";
    public const string Closed = "closed";
    public static readonly string[] All = [Draft, Open, Closed];
}

public static class AppraisalStatuses
{
    public const string Planning = "planning";
    public const string PlanningReview = "planning-review";
    public const string Midyear = "midyear";
    public const string MidyearReview = "midyear-review";
    public const string MidyearStaffSignature = "midyear-staff-signature";
    public const string MidyearDirectorReview = "midyear-director-review";
    public const string YearEnd = "year-end";
    public const string HodAssessment = "hod-assessment";
    public const string StaffSignature = "staff-signature";
    public const string DirectorReview = "director-review";
    public const string Approved = "approved";
    public static readonly string[] All =
    [Planning, PlanningReview, Midyear, MidyearReview, MidyearStaffSignature, MidyearDirectorReview,
        YearEnd, HodAssessment, StaffSignature, DirectorReview, Approved];
}

public static class AppraisalFactors
{
    public static readonly IReadOnlyList<AppraisalFactor> Behavioral =
    [
        new("initiative-resourcefulness", "Initiative/ Resourcefulness"),
        new("time-management", "Time Management"),
        new("confidentiality", "Confidentiality"),
        new("co-operativeness-teamwork", "Co-operativeness/ ability to work effectively in a Team"),
        new("leadership-qualities", "Leadership qualities"),
        new("personal-development-training", "Commitment to own personal development and Training"),
        new("willingness-to-learn", "Wiliness to Learn"),
        new("delivering-results-deadlines", "Delivering Results/ Adherence to Deadlines"),
        new("interpersonal-human-relations", "Interpersonal/human relations skills"),
        new("regulations-procedures", "Ability to keep to laid-down regulations and procedures")
    ];

    public static readonly IReadOnlyList<AppraisalFactor> Core =
    [
        new("acceptance-responsibility", "Acceptance of responsibility"),
        new("job-knowledge-technical-skills", "Job Knowledge and Technical Skills"),
        new("quality-correspondence", "Quality of Reports, Minutes, Memos, Letters/General correspondence etc."),
        new("research-publishing", "Effective Research and Publishing abilities"),
        new("commercialization-technology-transfer", "Commercialization activities and Technology Transfer etc."),
        new("management-administrative-skills", "Management/Administrative Skills"),
        new("communication", "Communication (oral, written & electronic)"),
        new("csir-core-values", "Commitment to CSIR Core Values"),
        new("mentoring-coaching", "Mentoring & Coaching Skills"),
        new("innovation-strategic-thinking", "Innovation and Strategic thinking")
    ];

    public static readonly IReadOnlyList<AppraisalRatingGuidance> BehavioralRatingGuidance =
    [
        new(5, "Exceptional", "Consistently demonstrates outstanding performance in all aspects and proactively goes above and beyond to contribute significantly to the team and organization. His/her demonstration of competencies is truly recognized by others."),
        new(4, "Exceeded Expectations", "Demonstrates performance above the expected level. Consistently goes the extra mile in tasks and interactions, contributing positively to the team. Displays a high level of proficiency and commitment."),
        new(3, "Met all Expectations", "Meets the expected standards consistently. Performs duties effectively and efficiently, contributing positively to the team and achieving the required outcomes. A solid and dependable performer."),
        new(2, "Below Expectation", "Falls short of meeting some expectations. Performance is inconsistent, and improvements are needed in specific areas. Requires additional support or development in certain competencies."),
        new(1, "Unacceptable", "Fails to meet the minimum performance expectations. Serious deficiencies in competencies require immediate attention. Significant improvement is necessary for the employee to meet the basic requirements of the role")
    ];

    public static readonly IReadOnlyList<AppraisalRatingGuidance> CoreRatingGuidance =
    [
        new(5, "Exceptional", "Displays an exceptional level of competence, surpassing expectations. Demonstrates an extraordinary depth of knowledge and proficiency in the core responsibilities of the role. The employee truly stands out clearly and consistently demonstrates exceptional accomplishments in terms of quality and quantity of work."),
        new(4, "Exceeded Expectations", "Demonstrates a high level of competency and exceeds the standard expectations. Consistently performs at a level that contributes significantly to the success of the team and organization."),
        new(3, "Met all Expectations", "Meets the expected standards consistently in core competencies. Performs duties effectively and efficiently, contributing positively to the team and achieving the required outcomes"),
        new(2, "Below Expectation", "Falls short of meeting some expectations in core competencies. Performance is inconsistent, and improvements are needed in specific areas. Requires additional support or development."),
        new(1, "Unacceptable", "Fails to meet the minimum performance expectations in core competencies. Serious deficiencies require immediate attention. Significant improvement is necessary for the employee to meet the basic requirements of the role.")
    ];
}

public sealed record AppraisalFactor(string Code, string Label);
public sealed record AppraisalRatingGuidance(short Rating, string Label, string Explanation);

public static class AppraisalScoring
{
    public const string Formula = "(total applicable score / total applicable values) * 10";

    public static decimal? CategoryScore(IEnumerable<short?> ratings)
    {
        var applicable = ratings.Where(rating => rating.HasValue).Select(rating => (decimal)rating!.Value).ToList();
        return applicable.Count == 0 ? null : decimal.Round(applicable.Average() * 10m, 2);
    }

    public static string? Band(decimal? score) => score switch
    {
        >= 70m => "Exceptional/Outstanding",
        >= 60m => "Competent/Very Able and Effective",
        >= 50m => "Fair/Average",
        >= 40m => "Below Average",
        not null => "Poor",
        _ => null
    };
}

public static class AppraisalFormTemplate
{
    public const string Version = "csir-performance-management-form-final-2026-08-18";
    public const string SourceDocumentFileName = "CSIR_PERFORMANCE_MANAGEMENT_FORM_-final[1] (4) (1).docx";
    public const string CanonicalContentChecksum = "4eb827081f3380d5a68fdafadea7b096f59b4e77518b01b6699c43c0819f645c";
    public const string OfficialLogoChecksum = "c284f59b831bc74a5299049a368ce2a22567258cd4b28be9c31d60c969e535c4";
    public const int SourceNumberedPageCount = 14;
    public const int PhysicalPageCount = 15;
    public const int SourceTableCount = 10;
}

public static class AppraisalReminderSchedule
{
    public static string? OffsetCode(DateTime deadline, DateTime today)
    {
        var days = (deadline.Date - today.Date).Days;
        return days switch
        {
            7 => "7-days",
            3 => "3-days",
            1 => "1-day",
            < 0 => $"overdue-{today:yyyyMMdd}",
            _ => null
        };
    }
}
