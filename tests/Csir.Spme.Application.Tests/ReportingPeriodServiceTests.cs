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

public sealed class ReportingPeriodServiceTests
{
    [Fact]
    public async Task Open_Uses_Domain_Transition_And_Saves_Audit_In_Unit_Of_Work()
    {
        var instituteId = Guid.NewGuid();
        var period = ReportingPeriod.Create(
            ScopeTypes.Institute,
            instituteId,
            "Q1-2026",
            "Quarter one",
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            null).Value!;
        var periods = new Mock<IReportingPeriodRepository>();
        periods.Setup(repository => repository.FindByIdAsync(
                period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.InstituteId).Returns(instituteId);
        var service = CreateService(
            periods.Object, unitOfWork.Object, audit.Object, currentUser.Object);
        var expectedRowVersion = new byte[] { 1, 2, 3 };

        var result = await service.OpenAsync(
            period.Id, expectedRowVersion, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ReportingPeriodStatuses.Open);
        unitOfWork.Verify(context => context.SetOriginalRowVersion(period, expectedRowVersion), Times.Once);
        audit.Verify(service => service.RecordAsync(
            "reporting-period.opened",
            "ReportingPeriod",
            period.Id.ToString(),
            "status=draft",
            "status=open",
            It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(context => context.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Open_Hides_A_Period_Outside_The_Callers_Institute()
    {
        var period = ReportingPeriod.Create(
            ScopeTypes.Institute,
            Guid.NewGuid(),
            "Q2-2026",
            "Quarter two",
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 6, 30),
            null).Value!;
        var periods = new Mock<IReportingPeriodRepository>();
        periods.Setup(repository => repository.FindByIdAsync(
                period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.InstituteId).Returns(Guid.NewGuid());
        var service = CreateService(
            periods.Object,
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IAuditService>(),
            currentUser.Object);

        var result = await service.OpenAsync(
            period.Id, new byte[] { 1 }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("not_found");
    }

    private static ReportingPeriodService CreateService(
        IReportingPeriodRepository periods,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser) =>
        new(
            periods,
            Mock.Of<IInstituteDirectory>(),
            unitOfWork,
            audit,
            currentUser,
            Mock.Of<ICursorCodec>(),
            Options.Create(new PaginationOptions()));
}
