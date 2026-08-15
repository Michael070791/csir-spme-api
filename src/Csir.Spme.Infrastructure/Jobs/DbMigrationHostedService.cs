using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Jobs;

public class DbMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbMigrationHostedService> _logger;

    public DbMigrationHostedService(IServiceProvider serviceProvider, ILogger<DbMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying database migrations...");
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
