using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Reporting;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Csir.Spme.Application.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task Submit_Stages_One_Correlated_Outbox_Event_Audit_And_Save()
    {
        var instituteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = CreateReport(instituteId);
        var reports = new Mock<IReportRepository>();
        reports.Setup(repository => repository.FindByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var outbox = new Mock<IWorkflowNotificationOutbox>();
        var cancellationToken = new CancellationTokenSource().Token;
        var service = CreateService(
            reports.Object, unitOfWork.Object, audit.Object, outbox.Object, instituteId, userId);

        var result = await service.SubmitAsync(report.Id, cancellationToken);

        result.IsSuccess.Should().BeTrue();
        report.Status.Should().Be(ReportStatuses.Submitted);
        outbox.Verify(queue => queue.StageReportSubmittedAsync(
            report.Id,
            instituteId,
            userId,
            It.Is<DateTimeOffset>(value => value == report.SubmittedAt),
            report.Title,
            cancellationToken), Times.Once);
        audit.Verify(writer => writer.RecordAsync(
            "report.submitted", "Report", report.Id.ToString(),
            It.IsAny<string>(), It.IsAny<string>(), cancellationToken), Times.Once);
        unitOfWork.Verify(context => context.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Submit_Invalid_Transition_Does_Not_Stage_Outbox_Audit_Or_Save()
    {
        var instituteId = Guid.NewGuid();
        var report = CreateReport(instituteId);
        report.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        var reports = new Mock<IReportRepository>();
        reports.Setup(repository => repository.FindByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var outbox = new Mock<IWorkflowNotificationOutbox>();
        var service = CreateService(
            reports.Object, unitOfWork.Object, audit.Object, outbox.Object, instituteId, Guid.NewGuid());

        var result = await service.SubmitAsync(report.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        outbox.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
        unitOfWork.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_Cross_Institute_Report_Is_Hidden_Without_Side_Effects()
    {
        var callerInstituteId = Guid.NewGuid();
        var report = CreateReport(Guid.NewGuid());
        var reports = new Mock<IReportRepository>();
        reports.Setup(repository => repository.FindByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var outbox = new Mock<IWorkflowNotificationOutbox>();
        var service = CreateService(
            reports.Object, unitOfWork.Object, audit.Object, outbox.Object, callerInstituteId, Guid.NewGuid());

        var result = await service.SubmitAsync(report.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("not_found");
        outbox.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
        unitOfWork.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_Resolves_Institute_Code_Through_Application_Directory()
    {
        var instituteId = Guid.NewGuid();
        var institutes = new Mock<IInstituteDirectory>();
        institutes.Setup(directory => directory.ResolveInstituteIdAsync("WRI", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instituteId);
        var reports = new Mock<IReportRepository>();
        reports.Setup(repository => repository.ListAsync(
                instituteId, null, null, null, It.IsAny<KeysetPage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListSlice<Report>([], null));
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.IsInRole("PlatformAdmin")).Returns(true);
        var service = new ReportService(
            reports.Object,
            Mock.Of<IReportingPeriodRepository>(),
            institutes.Object,
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IWorkflowNotificationOutbox>(),
            currentUser.Object,
            Mock.Of<ICursorCodec>(),
            Options.Create(new PaginationOptions()));

        var result = await service.ListAsync(
            "WRI", null, null, null, 20, null, null, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        institutes.Verify(directory => directory.ResolveInstituteIdAsync(
            "WRI", It.IsAny<CancellationToken>()), Times.Once);
        reports.Verify(repository => repository.ListAsync(
            instituteId, null, null, null, It.IsAny<KeysetPage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReportService CreateService(
        IReportRepository reports,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        IWorkflowNotificationOutbox outbox,
        Guid instituteId,
        Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.InstituteId).Returns(instituteId);
        currentUser.SetupGet(user => user.UserId).Returns(userId);
        return new ReportService(
            reports,
            Mock.Of<IReportingPeriodRepository>(),
            Mock.Of<IInstituteDirectory>(),
            unitOfWork,
            audit,
            outbox,
            currentUser.Object,
            Mock.Of<ICursorCodec>(),
            Options.Create(new PaginationOptions()));
    }

    private static Report CreateReport(Guid instituteId) => Report.Create(
        instituteId,
        Guid.NewGuid(),
        ReportTypes.Strategic,
        "Strategic delivery report",
        "Delivery summary",
        null,
        "Results",
        "Conclusion");
}
