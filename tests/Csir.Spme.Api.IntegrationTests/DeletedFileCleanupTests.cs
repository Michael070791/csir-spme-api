using System.Data;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Infrastructure.Persistence;
using Csir.Spme.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class DeletedFileCleanupTests
{
    [Fact]
    public async Task Cleanup_Marks_Storage_Deleted_Without_Reading_ScanStatus()
    {
        await using var factory = new SpmeApiFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var storageKey = $"cleanup/{Guid.NewGuid():N}";
        await using (var content = new MemoryStream("x"u8.ToArray()))
            await storage.UploadAsync(content, storageKey, "text/plain");

        var file = new FileRecord(storageKey, "gone.txt", "text/plain", 1, new string('a', 64));
        file.MarkDeleted(DateTimeOffset.UtcNow);
        db.FileRecords.Add(file);
        await db.SaveChangesAsync();
        await DropScanStatusColumnAsync(db);

        var cleanup = ActivatorUtilities.CreateInstance<DeletedFileCleanupService>(factory.Services);
        var act = () => cleanup.DeleteBatchAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        (await storage.ExistsAsync(storageKey)).Should().BeFalse();
        var storageDeletedAt = await db.FileRecords.AsNoTracking()
            .Where(candidate => candidate.Id == file.Id)
            .Select(candidate => candidate.StorageDeletedAt)
            .SingleAsync();
        storageDeletedAt.Should().NotBeNull();
    }

    private static async Task DropScanStatusColumnAsync(SpmeDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE '%FileRecord%' LIMIT 1";
        var tableName = (string?)await command.ExecuteScalarAsync();
        tableName.Should().NotBeNullOrWhiteSpace();
        tableName.Should().MatchRegex("^[A-Za-z0-9._]+$");
        var sql = "ALTER TABLE \"" + tableName + "\" DROP COLUMN \"ScanStatus\"";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
