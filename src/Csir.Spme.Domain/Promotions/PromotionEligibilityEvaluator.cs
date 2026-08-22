namespace Csir.Spme.Domain.Promotions;

public sealed record PromotionEligibilityFacts(
    string? StaffCategory,
    string PathStatus,
    DateTime SourceGradeEffectiveDate,
    short MinimumYearsInSourceGrade,
    DateTime EffectivePromotionDate,
    bool QualificationSatisfied,
    bool QualificationRejected,
    bool AppraisalSatisfied,
    bool AppraisalRejected);

public sealed record PromotionEligibilityEvaluation(
    string EligibilityState,
    DateTime? ServiceRequirementMetOn,
    decimal CompletedSourceGradeYears,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> PendingHrChecks,
    bool QualificationSatisfied = false,
    bool AppraisalSatisfied = false,
    bool QualificationRejected = false,
    bool AppraisalRejected = false);

public static class PromotionEligibilityEvaluator
{
    public static PromotionEligibilityEvaluation Evaluate(PromotionEligibilityFacts facts)
    {
        var serviceRequirementMetOn = facts.SourceGradeEffectiveDate.AddYears(facts.MinimumYearsInSourceGrade);
        var completedYears = Math.Max(0, (decimal)(facts.EffectivePromotionDate - facts.SourceGradeEffectiveDate).TotalDays / 365.2425m);
        var blocking = new List<string>();
        var pending = new List<string>();

        if (!IsAssessableStaffCategory(facts.StaffCategory))
            return Result(PromotionConstants.EligibilityNotApplicable, null, completedYears, blocking, pending, facts);
        if (facts.PathStatus == PromotionConstants.PathRequiresPolicyConfirmation)
            return Result(PromotionConstants.EligibilityPolicyAmbiguity, null, completedYears, ["policy-confirmation-required"], pending, facts);
        if (facts.EffectivePromotionDate < serviceRequirementMetOn)
            return Result(PromotionConstants.EligibilityNotYetEligible, serviceRequirementMetOn, completedYears, ["source-grade-service"], pending, facts);

        if (!facts.QualificationSatisfied)
        {
            if (facts.QualificationRejected) blocking.Add("qualification");
            else pending.Add("qualification");
        }

        if (!facts.AppraisalSatisfied)
        {
            if (facts.AppraisalRejected) blocking.Add("satisfactory-appraisal");
            else pending.Add("satisfactory-appraisal");
        }

        var state = blocking.Count > 0 ? PromotionConstants.EligibilityNotEligible :
            pending.Count > 0 ? PromotionConstants.EligibilityNeedsHrReview :
            PromotionConstants.EligibilityEligibleForReview;
        return Result(state, serviceRequirementMetOn, completedYears, blocking, pending, facts);
    }

    public static bool IsAssessableStaffCategory(string? staffCategory) =>
        string.Equals(staffCategory, PromotionConstants.SeniorStaff, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(staffCategory, PromotionConstants.SeniorMember, StringComparison.OrdinalIgnoreCase);

    public static string EvidenceCriterionStatus(bool satisfied, bool rejected) =>
        satisfied ? "satisfied" : rejected ? "not-met" : "pending-hr-review";

    private static PromotionEligibilityEvaluation Result(
        string state,
        DateTime? serviceRequirementMetOn,
        decimal completedYears,
        IReadOnlyList<string> blocking,
        IReadOnlyList<string> pending,
        PromotionEligibilityFacts facts) =>
        new(state, serviceRequirementMetOn, completedYears, blocking, pending,
            facts.QualificationSatisfied, facts.AppraisalSatisfied, facts.QualificationRejected, facts.AppraisalRejected);
}
