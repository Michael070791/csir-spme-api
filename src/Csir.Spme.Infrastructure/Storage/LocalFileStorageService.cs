using Csir.Spme.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Csir.Spme.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService, IDirectFileUploadService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger)
    {
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), "AppData", "Storage");
        _logger = logger;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<FileUploadResult> UploadAsync(Stream content, string storageKey, string contentType, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var stream = new FileStream(fullPath, FileMode.Create);
        await content.CopyToAsync(stream, ct);

        var checksum = await ComputeSha256Async(fullPath);
        var fileInfo = new FileInfo(fullPath);

        _logger.LogInformation("Uploaded file {StorageKey} ({Size} bytes)", storageKey, fileInfo.Length);
        return new FileUploadResult(storageKey, fileInfo.Length, checksum);
    }

    public Task<Stream?> DownloadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(Path.Combine(_basePath, storageKey)));
    }

    public Task<FileReadAccessResult?> CreateReadAccessAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult<FileReadAccessResult?>(null);

    public Task<DirectFileUploadAccess?> CreateWriteAccessAsync(
        string storageKey, string contentType, long sizeBytes, string sha256, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        Task.FromResult<DirectFileUploadAccess?>(null);

    public async Task<FileUploadInspection?> InspectAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storageKey);
        if (!File.Exists(fullPath)) return null;
        await using var stream = File.OpenRead(fullPath);
        var checksum = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        return new FileUploadInspection(stream.Length, null, checksum);
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}
