using Csir.Spme.Domain.Promotions;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class PromotionApplicationWindowTests
{
    [Fact]
    public void PresentGradeStart_Uses_Appointment_When_No_Recorded_Promotion()
    {
        var result = PromotionPresentGradeStart.Resolve(
            new DateTime(2021, 1, 1),
            null,
            new DateTime(2026, 8, 14),
            null);

        result.StartDate.Should().Be(new DateTime(2021, 1, 1));
        result.Source.Should().Be(PromotionPresentGradeStart.SourceFirstAppointment);
    }

    [Fact]
    public void PresentGradeStart_Ignores_EffectiveFrom_Copied_As_Promotion_Date()
    {
        var result = PromotionPresentGradeStart.Resolve(
            new DateTime(2021, 1, 1),
            new DateTime(2026, 8, 14),
            new DateTime(2026, 8, 14),
            null);

        result.StartDate.Should().Be(new DateTime(2021, 1, 1));
        result.Source.Should().Be(PromotionPresentGradeStart.SourceFirstAppointment);
    }

    [Fact]
    public void PresentGradeStart_Prefers_Recorded_Last_Promotion()
    {
        var result = PromotionPresentGradeStart.Resolve(
            new DateTime(2015, 3, 1),
            new DateTime(2022, 1, 15),
            new DateTime(2026, 8, 14),
            null);

        result.StartDate.Should().Be(new DateTime(2022, 1, 15));
        result.Source.Should().Be(PromotionPresentGradeStart.SourceLastPromotion);
    }

    [Fact]
    public void ApplicationWindow_Opens_Five_Months_Before_Four_Year_Due_Date()
    {
        var present = PromotionPresentGradeStart.Resolve(
            new DateTime(2021, 1, 1),
            null,
            new DateTime(2026, 8, 14),
            null);
        var window = PromotionApplicationWindow.Calculate(
            present,
            minimumYearsInSourceGrade: 4,
            evaluationDate: new DateTime(2024, 8, 1),
            hasQualifyingEducationRecord: true,
            isSeniorStaff: true,
            hasActivePath: true,
            pathRequiresPolicyConfirmation: false);

        window.ServiceDueOn.Should().Be(new DateTime(2025, 1, 1));
        window.OpensOn.Should().Be(new DateTime(2024, 8, 1));
        window.IsOpen.Should().BeTrue();
        window.CanPrepareDraft.Should().BeTrue();
    }

    [Fact]
    public void ApplicationWindow_Stays_Closed_Before_Five_Month_Window()
    {
        var present = PromotionPresentGradeStart.Resolve(
            new DateTime(2021, 1, 1),
            null,
            new DateTime(2026, 8, 14),
            null);
        var window = PromotionApplicationWindow.Calculate(
            present,
            minimumYearsInSourceGrade: 4,
            evaluationDate: new DateTime(2024, 7, 31),
            hasQualifyingEducationRecord: true,
            isSeniorStaff: true,
            hasActivePath: true,
            pathRequiresPolicyConfirmation: false);

        window.IsOpen.Should().BeFalse();
        window.CanPrepareDraft.Should().BeFalse();
    }
}
