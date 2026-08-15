using System.Security.Cryptography;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Csir.Spme.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Storage;

public sealed class AzureBlobStorageService : IFileStorageService, IDirectFileUploadService
{
    private const string CacheControl = "private, max-age=31536000, immutable";
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);
    private UserDelegationKey? _delegationKey;

    public AzureBlobStorageService(
        BlobServiceClient serviceClient,
        IOptions<BlobStorageOptions> options,
        IHostEnvironment environment,
        ILogger<AzureBlobStorageService> logger)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _container = serviceClient.GetBlobContainerClient(_options.ContainerName);

        if (_options.CreateContainer)
        {
            try
            {
                _container.CreateIfNotExists(PublicAccessType.None);
            }
            catch (RequestFailedException exception)
            {
                throw Unavailable("container initialization", exception);
            }
        }
    }

    public static BlobServiceClient CreateClient(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("blobs") ?? configuration.GetConnectionString("BlobStorage");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Storage account connection strings are allowed only in Development or Test.");
            return new BlobServiceClient(connectionString);
        }

        var options = configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>() ?? new BlobStorageOptions();
        if (options.ServiceUri is null)
            throw new InvalidOperationException("Storage:ServiceUri is required when a development Blob connection string is not configured.");

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(options.ManagedIdentityClientId))
            credentialOptions.ManagedIdentityClientId = options.ManagedIdentityClientId;
        return new BlobServiceClient(options.ServiceUri, new DefaultAzureCredential(credentialOptions));
    }

    public async Task<FileUploadResult> UploadAsync(Stream content, string storageKey, string contentType, CancellationToken ct = default)
    {
        try
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            var checksum = Convert.ToHexStringLower(SHA256.HashData(bytes));
            buffer.Position = 0;

            var blob = _container.GetBlobClient(storageKey);
            await blob.UploadAsync(buffer, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentDisposition = "inline",
                    CacheControl = CacheControl
                },
                Metadata = new Dictionary<string, string> { ["sha256"] = checksum }
            }, ct);

            return new FileUploadResult(storageKey, bytes.LongLength, checksum);
        }
        catch (RequestFailedException exception)
        {
            throw Unavailable("upload", exception);
        }
    }

    public async Task<Stream?> DownloadAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _container.GetBlobClient(storageKey).DownloadStreamingAsync(cancellationToken: ct);
            return response.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException exception)
        {
            throw Unavailable("download", exception);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            await _container.GetBlobClient(storageKey).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }
        catch (RequestFailedException exception)
        {
            throw Unavailable("delete", exception);
        }
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            return (await _container.GetBlobClient(storageKey).ExistsAsync(ct)).Value;
        }
        catch (RequestFailedException exception)
        {
            throw Unavailable("existence check", exception);
        }
    }

    public async Task<FileReadAccessResult?> CreateReadAccessAsync(string storageKey, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(storageKey);
        if (!await ExistsAsync(storageKey, ct))
            return null;

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(_options.ReadUrlLifetime);
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = storageKey,
            Resource = "b",
            StartsOn = now.AddMinutes(-1),
            ExpiresOn = expiresAt,
            Protocol = _environment.IsDevelopment() || _environment.IsEnvironment("Test")
                ? SasProtocol.HttpsAndHttp
                : SasProtocol.Https
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        Uri signedUri;
        if (blob.CanGenerateSasUri)
        {
            if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Shared-key SAS generation is forbidden outside Development and Test.");
            signedUri = blob.GenerateSasUri(sas);
        }
        else
        {
            var key = await GetDelegationKeyAsync(now, ct);
            var uri = new BlobUriBuilder(blob.Uri)
            {
                Sas = sas.ToSasQueryParameters(key, _serviceClient.AccountName)
            };
            signedUri = uri.ToUri();
        }

        if (_options.ExternalServiceUri is not null)
        {
            var external = new UriBuilder(signedUri)
            {
                Scheme = _options.ExternalServiceUri.Scheme,
                Host = _options.ExternalServiceUri.Host,
                Port = _options.ExternalServiceUri.IsDefaultPort ? -1 : _options.ExternalServiceUri.Port
            };
            signedUri = external.Uri;
        }

        return new FileReadAccessResult(signedUri, expiresAt);
    }

    public async Task<DirectFileUploadAccess?> CreateWriteAccessAsync(
        string storageKey, string contentType, long sizeBytes, string sha256, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(storageKey);
        var now = DateTimeOffset.UtcNow;
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = storageKey,
            Resource = "b",
            StartsOn = now.AddMinutes(-1),
            ExpiresOn = expiresAt,
            Protocol = _environment.IsDevelopment() || _environment.IsEnvironment("Test") ? SasProtocol.HttpsAndHttp : SasProtocol.Https
        };
        sas.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);
        Uri signedUri;
        if (blob.CanGenerateSasUri)
        {
            if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Shared-key SAS generation is forbidden outside Development and Test.");
            signedUri = blob.GenerateSasUri(sas);
        }
        else
        {
            var key = await GetDelegationKeyAsync(now, ct);
            signedUri = new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(key, _serviceClient.AccountName) }.ToUri();
        }
        return new DirectFileUploadAccess(signedUri, expiresAt,
            new Dictionary<string, string>
            {
                ["x-ms-blob-type"] = "BlockBlob",
                ["x-ms-meta-sha256"] = sha256,
                ["Content-Type"] = contentType
            });
    }

    public async Task<FileUploadInspection?> InspectAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var properties = (await _container.GetBlobClient(storageKey).GetPropertiesAsync(cancellationToken: ct)).Value;
            properties.Metadata.TryGetValue("sha256", out var sha256);
            return new FileUploadInspection(properties.ContentLength, properties.ContentType, sha256);
        }
        catch (RequestFailedException exception) when (exception.Status == 404) { return null; }
        catch (RequestFailedException exception) { throw Unavailable("inspection", exception); }
    }

    private async Task<UserDelegationKey> GetDelegationKeyAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_delegationKey is not null && _delegationKey.SignedExpiresOn > now.AddMinutes(10))
            return _delegationKey;

        await _delegationKeyLock.WaitAsync(ct);
        try
        {
            if (_delegationKey is null || _delegationKey.SignedExpiresOn <= now.AddMinutes(10))
                _delegationKey = (await _serviceClient.GetUserDelegationKeyAsync(now.AddMinutes(-5), now.AddHours(1), ct)).Value;
            return _delegationKey;
        }
        catch (RequestFailedException exception)
        {
            throw Unavailable("read authorization", exception);
        }
        finally
        {
            _delegationKeyLock.Release();
        }
    }

    private FileStorageUnavailableException Unavailable(string operation, RequestFailedException exception)
    {
        _logger.LogError(exception, "Blob storage {Operation} failed with status {Status}", operation, exception.Status);
        return new FileStorageUnavailableException("Blob storage is temporarily unavailable.", exception);
    }
}
