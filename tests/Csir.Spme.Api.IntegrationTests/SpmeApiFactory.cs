using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Xunit;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Csir.Spme.Api.IntegrationTests;

public class SpmeApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    public bool PreserveJwtValidation { get; init; }
    public int? MaximumIdempotencyResponseBytes { get; init; }
    public int? AccountActivationMaximumAttempts { get; init; }
    public int? AccountActivationResendLimit { get; init; }
    public AdjustableTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public SpmeApiFactory()
    {
        var connectionString = $"Data Source=spme-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public new HttpClient CreateClient() =>
        CreateDefaultClient(new TestIdempotencyKeyHandler());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseProvider:UseSqlite"] = "true",
                ["DatabaseProvider:SqlitePath"] = "csir-spme-v2-tests.db",
                ["Jwt:Key"] = Convert.ToBase64String(SHA256.HashData("csir-spme-isolated-test-jwt-signing-key"u8.ToArray())),
                ["AccountActivation:HashKey"] = Convert.ToBase64String(SHA256.HashData("csir-spme-test-activation-key"u8.ToArray())),
                ["PasswordReset:HashKey"] = Convert.ToBase64String(SHA256.HashData("csir-spme-test-password-reset-key"u8.ToArray())),
                ["PasswordReset:PermitLimit"] = "100",
                ["AccountActivation:MaximumAttempts"] = AccountActivationMaximumAttempts?.ToString(),
                ["AccountActivation:ResendLimit"] = AccountActivationResendLimit?.ToString(),
                ["Messaging:DispatcherEnabled"] = "false",
                ["Identity:SeedAdmin:UserName"] = "platform.admin",
                ["Identity:SeedAdmin:Email"] = "platform.admin@csir.local",
                ["Identity:SeedAdmin:Password"] = "TestOnly_Admin_2026!"
            });
            if (MaximumIdempotencyResponseBytes.HasValue)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Idempotency:MaximumStoredResponseBytes"] = MaximumIdempotencyResponseBytes.Value.ToString()
                });
            }
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<SpmeDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<SpmeDbContext>));
            services.RemoveAll<IEmailService>();
            services.RemoveAll<IFileStorageService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<IEmailService, CapturingEmailService>();
            services.AddSingleton<IFileStorageService, InMemoryFileStorageService>();
            services.AddSingleton<TimeProvider>(Clock);
            if (!PreserveJwtValidation)
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    options.Events.OnTokenValidated = _ => Task.CompletedTask);
            }
            services.AddDbContext<SpmeDbContext>(options =>
            {
                options.UseSqlite(_connection.ConnectionString);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Close();
        }
    }
}

public sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}

internal sealed class TestIdempotencyKeyHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post &&
            !request.Headers.Contains("Idempotency-Key") &&
            !request.Headers.Contains("X-Test-Skip-Idempotency"))
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Headers.Remove("X-Test-Skip-Idempotency");
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class InMemoryFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public async Task<FileUploadResult> UploadAsync(
        Stream content,
        string storageKey,
        string contentType,
        CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (!_files.TryAdd(storageKey, bytes))
            throw new InvalidOperationException("The test storage key already exists.");
        return new FileUploadResult(
            storageKey,
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public Task<Stream?> DownloadAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult(_files.TryGetValue(storageKey, out var bytes)
            ? (Stream?)new MemoryStream(bytes, writable: false)
            : null);

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        _files.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default) =>
        Task.FromResult(_files.ContainsKey(storageKey));

    public Task<FileReadAccessResult?> CreateReadAccessAsync(string storageKey, CancellationToken ct = default)
    {
        if (!_files.ContainsKey(storageKey))
            return Task.FromResult<FileReadAccessResult?>(null);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var uri = new Uri($"https://private-storage.example.test/read/{Uri.EscapeDataString(storageKey)}?sig=test-only&sp=r&se={Uri.EscapeDataString(expiresAt.ToString("O"))}");
        return Task.FromResult<FileReadAccessResult?>(new FileReadAccessResult(uri, expiresAt));
    }
}

public sealed class CapturingEmailService : IEmailService
{
    private readonly List<(string To, string Subject, string Body, bool IsHtml)> _messages = [];

    public IReadOnlyList<(string To, string Subject, string Body, bool IsHtml)> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToList();
            }
        }
    }

    public Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        lock (_messages)
        {
            _messages.Add((to, subject, body, isHtml));
        }

        return Task.CompletedTask;
    }
}

public class ApiHealthCheckTests : IClassFixture<SpmeApiFactory>
{
    private readonly HttpClient _client;

    public ApiHealthCheckTests(SpmeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }
}
