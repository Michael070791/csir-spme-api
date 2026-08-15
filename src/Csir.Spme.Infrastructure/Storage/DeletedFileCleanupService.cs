using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Storage;

public sealed class DeletedFileCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 25;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeletedFileCleanupService> _logger;

    public DeletedFileCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeletedFileCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Deleted-file storage cleanup batch failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                return;
        }
    }

    public async Task DeleteBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var query = db.FileRecords
            .AsNoTracking()
            .Where(file => file.IsDeleted && file.StorageDeletedAt == null);
        query = db.Database.IsSqlServer()
            ? query.OrderBy(file => file.DeletedAt)
            : query.OrderBy(file => file.Id);
        var files = await query
            .Select(file => new PendingDeletedFile(file.Id, file.StorageKey))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var file in files)
        {
            try
            {
                await storage.DeleteAsync(file.StorageKey, ct);
                var deletedAt = DateTimeOffset.UtcNow;
                await db.FileRecords
                    .Where(candidate => candidate.Id == file.Id)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(candidate => candidate.StorageDeletedAt, deletedAt)
                            .SetProperty(candidate => candidate.UpdatedAt, deletedAt),
                        ct);
            }
            catch (FileStorageUnavailableException)
            {
                break;
            }
        }
    }

    private sealed record PendingDeletedFile(Guid Id, string StorageKey);
}
