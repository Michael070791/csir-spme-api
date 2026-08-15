using Csir.Spme.Infrastructure;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructureServices(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var database = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabasePreflight");
var applyMigrations = builder.Configuration.GetValue<bool?>("DatabaseMigration:Apply") ?? false;
var timeoutSeconds = builder.Configuration.GetValue<int?>("DatabaseMigration:ConnectionTimeoutSeconds") ?? 45;
if (timeoutSeconds is < 5 or > 300)
    throw new InvalidOperationException("DatabaseMigration:ConnectionTimeoutSeconds must be between 5 and 300.");

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
Exception? lastError = null;
var connected = false;
while (!timeout.IsCancellationRequested)
{
    try
    {
        if (await database.Database.CanConnectAsync(timeout.Token))
        {
            lastError = null;
            connected = true;
            break;
        }
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        lastError = exception;
    }

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        break;
    }
}

if (!connected && lastError is not null)
    throw new InvalidOperationException(
        $"Database preflight could not authenticate within {timeoutSeconds} seconds. " +
        "Verify the selected server, database, and secret without recreating persistent storage.", lastError);
if (!connected)
    throw new InvalidOperationException(
        $"Database preflight could not connect within {timeoutSeconds} seconds. " +
        "Verify the selected server, database, and secret without recreating persistent storage.");

var appliedCount = (await database.Database.GetAppliedMigrationsAsync()).Count();
var pendingMigrations = (await database.Database.GetPendingMigrationsAsync()).ToArray();
logger.LogInformation(
    "Database preflight passed with {AppliedMigrationCount} applied migration(s) and {PendingMigrationCount} pending migration(s).",
    appliedCount,
    pendingMigrations.Length);

if (!applyMigrations)
{
    if (pendingMigrations.Length > 0)
        logger.LogWarning(
            "Database migration is disabled; {PendingMigrationCount} pending migration(s) were not applied.",
            pendingMigrations.Length);
    return;
}

logger.LogWarning("Database migration was explicitly enabled for this run.");
await database.Database.MigrateAsync();
