using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AuditPersistenceTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public AuditPersistenceTests(SpmeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RecordAsync_Stages_Aggregate_And_Audit_Until_The_Owning_Save()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var settingKey = $"audit-transaction-{suffix}";
        var auditTargetId = Guid.NewGuid().ToString();

        using var ownerScope = _factory.Services.CreateScope();
        var ownerDb = ownerScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var audit = ownerScope.ServiceProvider.GetRequiredService<IAuditService>();

        ownerDb.AppSettings.Add(new AppSetting(settingKey, "pending"));
        await audit.RecordAsync(
            "test.aggregate-created",
            "AppSetting",
            auditTargetId,
            after: $"key={settingKey}");

        using (var observerScope = _factory.Services.CreateScope())
        {
            var observerDb = observerScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            (await observerDb.AppSettings.AsNoTracking().AnyAsync(setting => setting.Key == settingKey))
                .Should().BeFalse();
            (await observerDb.AuditRecords.AsNoTracking().AnyAsync(record => record.TargetId == auditTargetId))
                .Should().BeFalse();
        }

        await ownerDb.SaveChangesAsync();

        using var committedScope = _factory.Services.CreateScope();
        var committedDb = committedScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await committedDb.AppSettings.AsNoTracking().AnyAsync(setting => setting.Key == settingKey))
            .Should().BeTrue();
        (await committedDb.AuditRecords.AsNoTracking().AnyAsync(record =>
                record.Action == "test.aggregate-created" && record.TargetId == auditTargetId))
            .Should().BeTrue();
    }
}
