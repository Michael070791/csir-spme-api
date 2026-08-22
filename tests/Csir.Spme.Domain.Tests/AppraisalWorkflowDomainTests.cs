using Csir.Spme.Domain.Hr;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class AppraisalWorkflowDomainTests
{
    [Fact]
    public void Cycle_Requires_Ordered_Contained_Windows_And_Audited_Reopen()
    {
        var instituteId = Guid.NewGuid();
        var invalid = AppraisalCycle.Create(
            instituteId,
            "2026 appraisal",
            2026,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 6, 30),
            new DateTime(2026, 6, 30),
            new DateTime(2026, 8, 31),
            new DateTime(2026, 9, 1),
            new DateTime(2026, 12, 31));

        invalid.IsFailure.Should().BeTrue();

        var cycle = CreateCycle();
        cycle.Open().IsSuccess.Should().BeTrue();
        cycle.Close().IsSuccess.Should().BeTrue();
        cycle.Reopen(" ").IsFailure.Should().BeTrue();
        cycle.Reopen("Approved by HR after documented outage.").IsSuccess.Should().BeTrue();

        cycle.Status.Should().Be(AppraisalCycleStatuses.Open);
        cycle.ReopenReason.Should().Be("Approved by HR after documented outage.");
        cycle.IsStageWindowOpen(AppraisalStatuses.Planning, new DateTime(2027, 1, 2)).Should().BeTrue();
    }

    [Fact]
    public void Appraisal_Requires_Distinct_Resolved_Routing_Before_Planning_Submission()
    {
        var instituteId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var appraisal = PerformanceAppraisal.Assign(
            instituteId,
            employeeId,
            CreateCycle(instituteId),
            reviewerId,
            null,
            new { },
            new { },
            new { },
            "Final approver is not configured.");

        appraisal.SubmitPlanning().IsFailure.Should().BeTrue();
        appraisal.UpdateRouting(reviewerId, reviewerId, new { }, new { }, null).IsFailure.Should().BeTrue();

        var directorId = Guid.NewGuid();
        appraisal.UpdateRouting(reviewerId, directorId, new { }, new { }, null).IsSuccess.Should().BeTrue();
        appraisal.SubmitPlanning().IsSuccess.Should().BeTrue();
        appraisal.Status.Should().Be(AppraisalStatuses.PlanningReview);
    }

    [Fact]
    public void Repeated_Midyear_Refusals_Always_Return_To_Hod_And_Require_A_Response()
    {
        var appraisal = CreateAtMidyearReview();
        var now = DateTimeOffset.UtcNow;

        appraisal.SubmitMidyearReview(null).IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, false, null, "The remarks are incomplete.", now)
            .IsSuccess.Should().BeTrue();
        appraisal.Status.Should().Be(AppraisalStatuses.MidyearReview);
        appraisal.SubmitMidyearReview(null).IsFailure.Should().BeTrue();
        appraisal.SubmitMidyearReview("The missing remarks were added.").IsSuccess.Should().BeTrue();

        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, false, null, "One target is still inaccurate.", now.AddMinutes(1))
            .IsSuccess.Should().BeTrue();
        appraisal.Status.Should().Be(AppraisalStatuses.MidyearReview);
        appraisal.SubmitMidyearReview(" ").IsFailure.Should().BeTrue();
        appraisal.SubmitMidyearReview("The target wording was corrected.").IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, true, null, null, now.AddMinutes(2))
            .IsSuccess.Should().BeTrue();

        appraisal.Status.Should().Be(AppraisalStatuses.MidyearDirectorReview);
    }

    [Fact]
    public void Accepted_Signature_Clears_The_Previous_Decline_Response_Requirement()
    {
        var appraisal = CreateAtMidyearReview();
        var now = DateTimeOffset.UtcNow;

        appraisal.SubmitMidyearReview(null).IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, false, null, "Please revise.", now)
            .IsSuccess.Should().BeTrue();
        appraisal.SubmitMidyearReview("Revised.").IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, true, null, null, now.AddMinutes(1))
            .IsSuccess.Should().BeTrue();
        appraisal.ReturnMidyearByDirector().IsSuccess.Should().BeTrue();

        appraisal.SubmitMidyearReview(null).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void YearEnd_Refusal_Never_Becomes_Consent_And_Final_Approval_Locks_The_Form()
    {
        var directorId = Guid.NewGuid();
        var appraisal = CreateAtYearEndAssessment(directorId);
        var now = DateTimeOffset.UtcNow;

        appraisal.SubmitHodAssessment(null).IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.YearEnd, true, " ", null, now).IsFailure.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.YearEnd, false, null, "I disagree with the score.", now)
            .IsSuccess.Should().BeTrue();
        appraisal.Status.Should().Be(AppraisalStatuses.HodAssessment);
        appraisal.SubmitHodAssessment(null).IsFailure.Should().BeTrue();
        appraisal.SubmitHodAssessment("The evidence and score were reviewed.").IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.YearEnd, true, "No comment", null, now.AddMinutes(1))
            .IsSuccess.Should().BeTrue();

        var fileId = Guid.NewGuid();
        appraisal.ApproveByDirector(new { Comments = "Approved" }, directorId, fileId, now.AddMinutes(2))
            .IsSuccess.Should().BeTrue();

        appraisal.Status.Should().Be(AppraisalStatuses.Approved);
        appraisal.Outcome.Should().Be("satisfactory");
        appraisal.FinalDocumentFileId.Should().Be(fileId);
        appraisal.SaveHodAssessment(new { }, 20m, 20m).IsFailure.Should().BeTrue();
        appraisal.ReturnByDirector().IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(70, "Exceptional/Outstanding")]
    [InlineData(69.99, "Competent/Very Able and Effective")]
    [InlineData(60, "Competent/Very Able and Effective")]
    [InlineData(59.99, "Fair/Average")]
    [InlineData(50, "Fair/Average")]
    [InlineData(49.99, "Below Average")]
    [InlineData(40, "Below Average")]
    [InlineData(39.99, "Poor")]
    [InlineData(0, "Poor")]
    public void Scoring_Uses_The_Official_Band_Boundaries(double score, string expected)
    {
        AppraisalScoring.Band((decimal)score).Should().Be(expected);
    }

    [Fact]
    public void Scoring_Normalizes_Applicable_Ratings_And_Rejects_An_All_Na_Category()
    {
        AppraisalScoring.CategoryScore([5, null, 3]).Should().Be(40m);
        AppraisalScoring.CategoryScore([5, 4, 4]).Should().Be(43.33m);
        AppraisalScoring.CategoryScore([null, null]).Should().BeNull();
        AppraisalScoring.CategoryScore(Enumerable.Repeat<short?>(5, 10)).Should().Be(50m);
    }

    [Fact]
    public void Official_Factor_Inventory_And_Template_Identity_Are_Stable()
    {
        AppraisalFactors.Behavioral.Select(item => item.Label).Should().Equal(
            "Initiative/ Resourcefulness",
            "Time Management",
            "Confidentiality",
            "Co-operativeness/ ability to work effectively in a Team",
            "Leadership qualities",
            "Commitment to own personal development and Training",
            "Wiliness to Learn",
            "Delivering Results/ Adherence to Deadlines",
            "Interpersonal/human relations skills",
            "Ability to keep to laid-down regulations and procedures");
        AppraisalFactors.Core.Select(item => item.Label).Should().Equal(
            "Acceptance of responsibility",
            "Job Knowledge and Technical Skills",
            "Quality of Reports, Minutes, Memos, Letters/General correspondence etc.",
            "Effective Research and Publishing abilities",
            "Commercialization activities and Technology Transfer etc.",
            "Management/Administrative Skills",
            "Communication (oral, written & electronic)",
            "Commitment to CSIR Core Values",
            "Mentoring & Coaching Skills",
            "Innovation and Strategic thinking");
        AppraisalFactors.BehavioralRatingGuidance.Select(item => item.Rating).Should().Equal(5, 4, 3, 2, 1);
        AppraisalFactors.CoreRatingGuidance.Select(item => item.Rating).Should().Equal(5, 4, 3, 2, 1);
        AppraisalScoring.Formula.Should().Be("(total applicable score / total applicable values) * 10");
        AppraisalFormTemplate.Version.Should().Be("csir-performance-management-form-final-2026-08-18");
        AppraisalFormTemplate.SourceDocumentFileName.Should().Be("CSIR_PERFORMANCE_MANAGEMENT_FORM_-final[1] (4) (1).docx");
        AppraisalFormTemplate.CanonicalContentChecksum.Should().Be("4eb827081f3380d5a68fdafadea7b096f59b4e77518b01b6699c43c0819f645c");
        AppraisalFormTemplate.OfficialLogoChecksum.Should().Be("c284f59b831bc74a5299049a368ce2a22567258cd4b28be9c31d60c969e535c4");
        AppraisalFormTemplate.SourceNumberedPageCount.Should().Be(14);
        AppraisalFormTemplate.PhysicalPageCount.Should().Be(15);
        AppraisalFormTemplate.SourceTableCount.Should().Be(10);
    }

    [Theory]
    [InlineData(7, "7-days")]
    [InlineData(3, "3-days")]
    [InlineData(1, "1-day")]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(-1, "overdue-20260822")]
    public void Reminder_Schedule_Uses_Exact_Offsets_And_Daily_Overdue_Deduplication(
        int daysUntilDeadline,
        string? expected)
    {
        var today = new DateTime(2026, 8, 22);

        AppraisalReminderSchedule.OffsetCode(today.AddDays(daysUntilDeadline), today).Should().Be(expected);
    }

    private static AppraisalCycle CreateCycle(Guid? instituteId = null)
    {
        return AppraisalCycle.Create(
            instituteId ?? Guid.NewGuid(),
            "2026 appraisal",
            2026,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            new DateTime(2026, 4, 1),
            new DateTime(2026, 8, 31),
            new DateTime(2026, 9, 1),
            new DateTime(2026, 12, 31)).Value!;
    }

    private static PerformanceAppraisal CreateAtMidyearReview()
    {
        var instituteId = Guid.NewGuid();
        var appraisal = PerformanceAppraisal.Assign(
            instituteId,
            Guid.NewGuid(),
            CreateCycle(instituteId),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new { },
            new { },
            new { },
            null);
        appraisal.SubmitPlanning().IsSuccess.Should().BeTrue();
        appraisal.ConfirmPlanning().IsSuccess.Should().BeTrue();
        appraisal.SubmitMidyear().IsSuccess.Should().BeTrue();
        return appraisal;
    }

    private static PerformanceAppraisal CreateAtYearEndAssessment(Guid directorId)
    {
        var instituteId = Guid.NewGuid();
        var appraisal = PerformanceAppraisal.Assign(
            instituteId,
            Guid.NewGuid(),
            CreateCycle(instituteId),
            Guid.NewGuid(),
            directorId,
            new { },
            new { },
            new { },
            null);
        appraisal.SubmitPlanning().IsSuccess.Should().BeTrue();
        appraisal.ConfirmPlanning().IsSuccess.Should().BeTrue();
        appraisal.SubmitMidyear().IsSuccess.Should().BeTrue();
        appraisal.SubmitMidyearReview(null).IsSuccess.Should().BeTrue();
        appraisal.RecordStaffSignature(AppraisalPhases.Midyear, true, null, null, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();
        appraisal.ApproveMidyearByDirector().IsSuccess.Should().BeTrue();
        appraisal.SubmitYearEnd().IsSuccess.Should().BeTrue();
        appraisal.SaveHodAssessment(new { }, 40m, 40m).IsSuccess.Should().BeTrue();
        return appraisal;
    }
}
