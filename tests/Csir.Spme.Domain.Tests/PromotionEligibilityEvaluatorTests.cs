using Csir.Spme.Domain.Promotions;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class PromotionEligibilityEvaluatorTests
{
    [Fact]
    public void Evaluate_Requires_Four_Complete_Years_On_The_January_Cycle_Date()
    {
        var beforeThreshold = Evaluate(new DateTime(2022, 1, 2), new DateTime(2026, 1, 1), 4);
        var atThreshold = Evaluate(new DateTime(2022, 1, 1), new DateTime(2026, 1, 1), 4);

        beforeThreshold.EligibilityState.Should().Be(PromotionConstants.EligibilityNotYetEligible);
        beforeThreshold.ServiceRequirementMetOn.Should().Be(new DateTime(2026, 1, 2));
        atThreshold.EligibilityState.Should().Be(PromotionConstants.EligibilityEligibleForReview);
    }

    [Fact]
    public void Evaluate_Uses_Five_Years_For_Section_TwentyTwo_Paths()
    {
        var result = Evaluate(new DateTime(2021, 1, 2), new DateTime(2026, 1, 1), 5);

        result.EligibilityState.Should().Be(PromotionConstants.EligibilityNotYetEligible);
        result.ServiceRequirementMetOn.Should().Be(new DateTime(2026, 1, 2));
    }

    [Fact]
    public void Evaluate_Rejects_NonSeniorStaff_And_Blocks_Unresolved_Policy()
    {
        var outsideScope = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            "senior-member", PromotionConstants.PathActive, new DateTime(2020, 1, 1), 4, new DateTime(2026, 1, 1), true, false, true, false));
        var ambiguity = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            PromotionConstants.SeniorStaff, PromotionConstants.PathRequiresPolicyConfirmation, new DateTime(2020, 1, 1), 5, new DateTime(2026, 1, 1), true, false, true, false));

        outsideScope.EligibilityState.Should().Be(PromotionConstants.EligibilityNotApplicable);
        ambiguity.EligibilityState.Should().Be(PromotionConstants.EligibilityPolicyAmbiguity);
    }

    [Fact]
    public void Evaluate_Treats_Verified_Degree_Or_Satisfactory_Appraisal_As_Evidence()
    {
        var degreeOnly = PromotionEligibilityEvaluator.Evaluate(Facts(qualificationSatisfied: true, appraisalSatisfied: false));
        var appraisalOnly = PromotionEligibilityEvaluator.Evaluate(Facts(qualificationSatisfied: false, appraisalSatisfied: true));
        var pending = PromotionEligibilityEvaluator.Evaluate(Facts(qualificationSatisfied: false, appraisalSatisfied: false));
        var rejected = PromotionEligibilityEvaluator.Evaluate(Facts(
            qualificationSatisfied: false, qualificationRejected: true, appraisalSatisfied: false, appraisalRejected: true));

        degreeOnly.EligibilityState.Should().Be(PromotionConstants.EligibilityEligibleForReview);
        appraisalOnly.EligibilityState.Should().Be(PromotionConstants.EligibilityEligibleForReview);
        pending.EligibilityState.Should().Be(PromotionConstants.EligibilityNeedsHrReview);
        rejected.EligibilityState.Should().Be(PromotionConstants.EligibilityNotEligible);
    }

    private static PromotionEligibilityEvaluation Evaluate(DateTime sourceGradeDate, DateTime cycleDate, short requiredYears) =>
        PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
            PromotionConstants.SeniorStaff, PromotionConstants.PathActive, sourceGradeDate, requiredYears, cycleDate,
            true, false, false, false));

    private static PromotionEligibilityFacts Facts(
        bool qualificationSatisfied,
        bool appraisalSatisfied,
        bool qualificationRejected = false,
        bool appraisalRejected = false) =>
        new(PromotionConstants.SeniorStaff, PromotionConstants.PathActive, new DateTime(2020, 1, 1), 4, new DateTime(2026, 1, 1),
            qualificationSatisfied, qualificationRejected, appraisalSatisfied, appraisalRejected);
}
