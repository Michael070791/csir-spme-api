using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Infrastructure;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class CommunicationOutboxDispatcherTests
{
    [Fact]
    public async Task Dispatcher_Delivers_Queued_Email_Without_ExecuteUpdate()
    {
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"spme-outbox-{Guid.NewGuid():N}.db");
        var transport = new RecordingEmailTransport();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider:UseSqlite"] = "true",
                ["DatabaseProvider:SqlitePath"] = sqlitePath,
                ["Storage:Provider"] = "local",
                ["Storage:ContainerName"] = "spme-private",
                ["Storage:ReadUrlLifetime"] = "00:05:00",
                ["Messaging:DispatcherEnabled"] = "true",
                ["Messaging:WorkerBatchSize"] = "10",
                ["Messaging:MaximumAttempts"] = "3",
                ["Messaging:LeaseSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);
        services.RemoveAll<IEmailTransport>();
        services.AddSingleton<IEmailTransport>(transport);
        await using (var provider = services.BuildServiceProvider())
        {
            try
            {
                await using (var setup = provider.CreateAsyncScope())
                {
                    var db = setup.ServiceProvider.GetRequiredService<SpmeDbContext>();
                    await db.Database.EnsureCreatedAsync();
                    db.CommunicationOutboxMessages.Add(new CommunicationOutboxMessage(
                        "email",
                        "staff@csir.test",
                        "Activation",
                        "Use this code",
                        false,
                        "authentication",
                        Guid.NewGuid().ToString("N")));
                    await db.SaveChangesAsync();
                }

                var dispatcher = provider.GetServices<IHostedService>()
                    .OfType<CommunicationOutboxDispatcher>()
                    .Single();
                var processed = await dispatcher.DispatchBatchAsync(CancellationToken.None);
                processed.Should().Be(1);
                transport.Sent.Should().Be(1);

                await using var verify = provider.CreateAsyncScope();
                var delivered = await verify.ServiceProvider.GetRequiredService<SpmeDbContext>()
                    .CommunicationOutboxMessages
                    .AsNoTracking()
                    .SingleAsync();
                delivered.Status.Should().Be("delivered");
                delivered.ProviderMessageId.Should().Be("stub-message");
                delivered.LastErrorCode.Should().BeNull();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(sqlitePath))
                    File.Delete(sqlitePath);
            }
        }
    }

    private sealed class RecordingEmailTransport : IEmailTransport
    {
        public int Sent { get; private set; }

        public Task<CommunicationTransportResult> SendAsync(
            string to,
            string subject,
            string body,
            bool isHtml,
            string? textBody,
            string category,
            CancellationToken ct = default,
            IReadOnlyList<EmailAttachment>? attachments = null)
        {
            Sent++;
            to.Should().Be("staff@csir.test");
            category.Should().Be("authentication");
            return Task.FromResult(new CommunicationTransportResult(
                true, "stub", "stub-message", null, 200, false));
        }
    }
}
