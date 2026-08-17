using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class PlanWorkflowDomainTests
{
    [Fact]
    public void Project_Inception_Requires_Core_Fields_And_Locks_After_Completion()
    {
        var inception = ProjectInception.Create(Guid.NewGuid());

        inception.Complete(DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
        inception.UpdateDraft("12 months", "CSIR", "Accra", null, null, "Beneficiaries", "Technology", "Commercialization", "Contribution")
            .IsSuccess.Should().BeTrue();
        inception.Complete(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        inception.UpdateDraft("24 months", "Other", "Kumasi", null, null, "Beneficiaries", "Technology", "Commercialization", "Contribution")
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Report_Requires_The_Review_Sequence_And_Approved_Content_Is_Immutable()
    {
        var report = Report.Create(Guid.NewGuid(), Guid.NewGuid(), ReportTypes.Strategic,
            "Title", "Summary", null, null, null);
        var actor = Guid.NewGuid();

        report.Submit(actor, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        report.Update("Changed", "Changed", null, null, null).IsFailure.Should().BeTrue();
        report.Return(" ").IsFailure.Should().BeTrue();
        report.Return("Add the missing evidence.").IsSuccess.Should().BeTrue();
        report.Update("Corrected", "Corrected summary", null, null, null).IsSuccess.Should().BeTrue();
        report.Submit(actor, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        report.Approve(actor, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        report.Status.Should().Be(ReportStatuses.Approved);
        report.Update("Changed again", "Changed", null, null, null).IsFailure.Should().BeTrue();
        report.Return("Too late").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Project_Submit_And_Archive_Transitions_Are_One_Way()
    {
        var project = Project.Create(Guid.NewGuid(), "P-1", "Project", "Objective", null, null, null,
            ProjectNatures.Research, new DateTime(2026, 1, 1), null, "GHS", 10m,
            null, null, null, null);

        project.Submit().IsSuccess.Should().BeTrue();
        project.Submit().IsFailure.Should().BeTrue();
        project.Archive().IsSuccess.Should().BeTrue();

        project.Status.Should().Be(ProjectStatuses.Archived);
        project.Archive().IsFailure.Should().BeTrue();
        project.Update("Changed", "Objective", null, null, null, null, ProjectNatures.Research,
            new DateTime(2026, 1, 1), null, "GHS", 10m, null, null, null, null)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Reporting_Period_Controls_Measurement_Mutability()
    {
        var period = ReportingPeriod.Create(ScopeTypes.Institute, Guid.NewGuid(), "Q1", "Quarter 1",
            ReportingPeriodTypes.Quarterly, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null).Value!;

        period.AllowsMeasurementChanges.Should().BeTrue();
        period.Open().IsSuccess.Should().BeTrue();
        period.AllowsMeasurementChanges.Should().BeTrue();
        period.Close().IsSuccess.Should().BeTrue();
        period.AllowsMeasurementChanges.Should().BeFalse();
        period.Finalize().IsSuccess.Should().BeTrue();
        period.AllowsMeasurementChanges.Should().BeFalse();
    }

    [Fact]
    public void Strategic_Plan_Only_Allows_Draft_Updates_And_One_Way_Activation()
    {
        var plan = StrategicPlan.Create(
            Guid.NewGuid(), "SP-2030", "Strategic Plan", "Definition", "Objective", 2026, 2030);

        plan.Update("Invalid plan", "Definition", "Objective", 1999, 2030)
            .IsFailure.Should().BeTrue();
        plan.Update("Updated plan", "Updated definition", "Updated objective", 2027, 2031)
            .IsSuccess.Should().BeTrue();
        plan.Activate().IsSuccess.Should().BeTrue();
        plan.Status.Should().Be(StrategicPlanStatuses.Active);

        plan.Update("Too late", "Definition", "Objective", 2027, 2031)
            .IsFailure.Should().BeTrue();
        plan.Activate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Reporting_Period_Rejects_Out_Of_Order_Lifecycle_Commands()
    {
        var period = ReportingPeriod.Create(ScopeTypes.Institute, Guid.NewGuid(), "Q2", "Quarter 2",
            ReportingPeriodTypes.Quarterly, new DateTime(2026, 4, 1), new DateTime(2026, 6, 30), null).Value!;

        period.Close().IsFailure.Should().BeTrue();
        period.Finalize().IsFailure.Should().BeTrue();
        period.Open().IsSuccess.Should().BeTrue();
        period.Finalize().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Indicator_Measurement_Derives_Variance_Without_Persisted_State()
    {
        IndicatorMeasurement.DeriveVariance(82.5m, 80m).Should().Be(2.5m);
        IndicatorMeasurement.DeriveVariance(82.5m, null).Should().BeNull();
    }

    [Fact]
    public void Leave_Approval_Requires_The_Current_Stage_And_Completes_The_Chain()
    {
        var leave = LeaveRequest.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), LeaveTypes.Annual,
            new DateTime(2026, 8, 3), new DateTime(2026, 8, 4), 2,
            null, null, null, null, null, null);

        leave.Submit("section-head", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        leave.Approve("division-head", "director").IsFailure.Should().BeTrue();
        leave.Approve("section-head", "director").IsSuccess.Should().BeTrue();
        leave.Approve("director", null).IsSuccess.Should().BeTrue();
        leave.Status.Should().Be(LeaveRequestStatuses.Approved);
        leave.Reject("director", "Too late").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Leave_Rejection_Requires_A_Reason_And_Is_Terminal()
    {
        var leave = LeaveRequest.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), LeaveTypes.Annual,
            new DateTime(2026, 8, 3), new DateTime(2026, 8, 4), 2,
            null, null, null, null, null, null);

        leave.Submit("section-head", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        leave.Reject("section-head", " ").IsFailure.Should().BeTrue();
        leave.Reject("section-head", "Insufficient coverage.").IsSuccess.Should().BeTrue();
        leave.Status.Should().Be(LeaveRequestStatuses.Rejected);
        leave.Cancel(DateTimeOffset.UtcNow).IsFailure.Should().BeTrue();
    }
}
