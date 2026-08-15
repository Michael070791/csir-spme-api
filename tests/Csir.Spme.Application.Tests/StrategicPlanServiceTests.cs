using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Plan;
using Csir.Spme.Domain.Plan;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Csir.Spme.Application.Tests;

public sealed class StrategicPlanServiceTests
{
    [Fact]
    public async Task Create_Stages_Plan_Audit_And_Unit_Of_Work()
    {
        var instituteId = Guid.NewGuid();
        var plans = new Mock<IStrategicPlanRepository>();
        plans.Setup(repository => repository.CodeExistsAsync(
                instituteId, "SP-2030", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var institutes = new Mock<IInstituteDirectory>();
        institutes.Setup(directory => directory.InstituteExistsAsync(
                instituteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.InstituteId).Returns(instituteId);

        var service = new StrategicPlanService(
            plans.Object,
            institutes.Object,
            unitOfWork.Object,
            audit.Object,
            currentUser.Object,
            Mock.Of<ICursorCodec>(),
            Options.Create(new PaginationOptions()));

        var result = await service.CreateAsync(new CreateStrategicPlanCommand(
            null, "SP-2030", "Strategic Plan 2030", "Definition", "Objective", 2026, 2030),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InstituteId.Should().Be(instituteId);
        plans.Verify(repository => repository.Add(It.Is<StrategicPlan>(
            plan => plan.Code == "SP-2030" && plan.Status == "draft")), Times.Once);
        audit.Verify(service => service.RecordAsync(
            "strategic-plan.created", "StrategicPlan", It.IsAny<string>(), null,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_Uses_Institute_Scoped_Lookup_And_Overlapping_Range_Rule()
    {
        var instituteId = Guid.NewGuid();
        var plan = StrategicPlan.Create(
            instituteId, "SP-2030", "Strategic Plan 2030", "Definition", "Objective", 2026, 2030);
        var plans = new Mock<IStrategicPlanRepository>();
        plans.Setup(repository => repository.FindByIdAsync(
                plan.Id, instituteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        plans.Setup(repository => repository.HasOverlappingActiveAsync(
                instituteId, 2026, 2030, plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var unitOfWork = new Mock<IApplicationDbContext>();
        var audit = new Mock<IAuditService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.InstituteId).Returns(instituteId);
        var service = new StrategicPlanService(
            plans.Object,
            Mock.Of<IInstituteDirectory>(),
            unitOfWork.Object,
            audit.Object,
            currentUser.Object,
            Mock.Of<ICursorCodec>(),
            Options.Create(new PaginationOptions()));

        var result = await service.ActivateAsync(
            plan.Id, Guid.NewGuid().ToByteArray(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("active");
        plans.Verify(repository => repository.FindByIdAsync(
            plan.Id, instituteId, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(service => service.RecordAsync(
            "strategic-plan.activated", "StrategicPlan", plan.Id.ToString(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
