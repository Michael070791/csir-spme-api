using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Azure.Storage.Blobs;
using Csir.Spme.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AzuriteBlobStorageIntegrationTests
{
    [Fact]
    [Trait("Category", "Azurite")]
    public async Task Blob_Lifecycle_Sas_Range_Expiry_And_Tampering_Work()
    {
        var connectionString = Environment.GetEnvironmentVariable("SPME_TEST_AZURITE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var serviceClient = new BlobServiceClient(connectionString);
        var containerName = $"spme-test-{Guid.NewGuid():N}"[..28];
        var options = Options.Create(new BlobStorageOptions
        {
            ContainerName = containerName,
            CreateContainer = true,
            ReadUrlLifetime = TimeSpan.FromSeconds(2)
        });
        var storage = new AzureBlobStorageService(
            serviceClient,
            options,
            new TestHostEnvironment(),
            NullLogger<AzureBlobStorageService>.Instance);
        const string key = "employee-profile-images/11111111111111111111111111111111/2026/08/22222222222222222222222222222222.webp";
        var bytes = Encoding.ASCII.GetBytes("RIFF1234WEBPnormalized-profile-image");

        try
        {
            await using var upload = new MemoryStream(bytes, writable: false);
            var result = await storage.UploadAsync(upload, key, "image/webp");
            result.StorageKey.Should().Be(key);
            (await storage.ExistsAsync(key)).Should().BeTrue();

            var access = await storage.CreateReadAccessAsync(key);
            access.Should().NotBeNull();
            access!.Uri.Query.Should().Contain("sp=r");

            using var client = new HttpClient();
            using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, access.Uri);
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 3);
            var rangeResponse = await client.SendAsync(rangeRequest);
            rangeResponse.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            (await rangeResponse.Content.ReadAsByteArrayAsync()).Should().Equal(bytes[..4]);

            var tamperedUri = TamperSignature(access.Uri);
            (await client.GetAsync(tamperedUri)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await Task.Delay(TimeSpan.FromSeconds(3));
            (await client.GetAsync(access.Uri)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await storage.DeleteAsync(key);
            (await storage.ExistsAsync(key)).Should().BeFalse();
        }
        finally
        {
            await serviceClient.DeleteBlobContainerAsync(containerName);
        }
    }

    private static Uri TamperSignature(Uri uri)
    {
        var parts = uri.Query.TrimStart('?').Split('&');
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].StartsWith("sig=", StringComparison.Ordinal))
                parts[index] = $"{parts[index]}x";
        }

        return new UriBuilder(uri) { Query = string.Join('&', parts) }.Uri;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Csir.Spme.Api.IntegrationTests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
