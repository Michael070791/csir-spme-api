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
    IReadOnlyList<string> PendingHrChecks);

public static class PromotionEligibilityEvaluator
{
    public static PromotionEligibilityEvaluation Evaluate(PromotionEligibilityFacts facts)
    {
        var serviceRequirementMetOn = facts.SourceGradeEffectiveDate.AddYears(facts.MinimumYearsInSourceGrade);
        var completedYears = Math.Max(0, (decimal)(facts.EffectivePromotionDate - facts.SourceGradeEffectiveDate).TotalDays / 365.2425m);
        var blocking = new List<string>();
        var pending = new List<string>();

        if (!string.Equals(facts.StaffCategory, PromotionConstants.SeniorStaff, StringComparison.OrdinalIgnoreCase))
            return new(PromotionConstants.EligibilityNotApplicable, null, completedYears, blocking, pending);
        if (facts.PathStatus == PromotionConstants.PathRequiresPolicyConfirmation)
            return new(PromotionConstants.EligibilityPolicyAmbiguity, null, completedYears, ["policy-confirmation-required"], pending);
        if (facts.EffectivePromotionDate < serviceRequirementMetOn)
            return new(PromotionConstants.EligibilityNotYetEligible, serviceRequirementMetOn, completedYears, ["source-grade-service"], pending);
        if (facts.QualificationRejected)
            blocking.Add("qualification");
        else if (!facts.QualificationSatisfied)
            pending.Add("qualification");
        if (facts.AppraisalRejected)
            blocking.Add("satisfactory-appraisal");
        else if (!facts.AppraisalSatisfied)
            pending.Add("satisfactory-appraisal");

        var state = blocking.Count > 0 ? PromotionConstants.EligibilityNotEligible :
            pending.Count > 0 ? PromotionConstants.EligibilityNeedsHrReview :
            PromotionConstants.EligibilityEligibleForReview;
        return new(state, serviceRequirementMetOn, completedYears, blocking, pending);
    }
}
