using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class CommunicationOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagingOptions _options;
    private readonly ILogger<CommunicationOutboxDispatcher> _logger;

    public CommunicationOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<CommunicationOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await DispatchBatchAsync(stoppingToken);
            if (processed == 0)
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    internal async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidateIds = await db.CommunicationOutboxMessages
            .AsNoTracking()
            .Where(message =>
                (message.Status == "queued" && message.NextAttemptAt <= now) ||
                (message.Status == "processing" && message.LockedUntil < now))
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(Math.Clamp(_options.WorkerBatchSize, 1, 200))
            .ToListAsync(ct);

        var processed = 0;
        foreach (var candidateId in candidateIds)
        {
            var lockedUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(_options.LeaseSeconds, 10, 300));
            var claimed = await db.CommunicationOutboxMessages
                .Where(message => message.Id == candidateId &&
                    ((message.Status == "queued" && message.NextAttemptAt <= now) ||
                     (message.Status == "processing" && message.LockedUntil < now)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.Status, "processing")
                    .SetProperty(message => message.LockedUntil, lockedUntil)
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1), ct);
            if (claimed != 1)
                continue;

            var message = await db.CommunicationOutboxMessages
                .SingleAsync(candidate => candidate.Id == candidateId, ct);
            processed++;

            CommunicationTransportResult result;
            try
            {
                result = await SendAsync(message, scope.ServiceProvider, ct);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    "Communication provider request failed for outbox message {OutboxMessageId}: {ErrorClass}",
                    message.Id,
                    exception.GetType().Name);
                result = new(false, message.Channel, null, "provider_unavailable", null, true);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                result = new(false, message.Channel, null, "provider_timeout", null, true);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(
                    "Unexpected communication provider failure for outbox message {OutboxMessageId}: {ErrorClass}",
                    message.Id,
                    exception.GetType().Name);
                result = new(false, message.Channel, null, "provider_unavailable", null, true);
            }

            db.CommunicationDeliveryAttempts.Add(new CommunicationDeliveryAttempt(
                message.Id,
                message.AttemptCount,
                result.Provider,
                result.Accepted ? "accepted" : "failed",
                result.ProviderMessageId,
                result.ErrorCode,
                result.HttpStatusCode));

            if (result.Accepted)
            {
                message.MarkDelivered(result.ProviderMessageId, DateTimeOffset.UtcNow);
            }
            else if (result.IsTransient && message.AttemptCount < Math.Clamp(_options.MaximumAttempts, 1, 20))
            {
                var baseDelaySeconds = Math.Min(3600, Math.Pow(2, message.AttemptCount - 1) * 60);
                var jitterCeiling = Math.Max(1, (int)Math.Ceiling(baseDelaySeconds * 0.25));
                var jitterSeconds = Random.Shared.Next(0, jitterCeiling);
                var delaySeconds = Math.Min(3600, baseDelaySeconds + jitterSeconds);
                message.Retry(result.ErrorCode ?? "provider_unavailable", DateTimeOffset.UtcNow.AddSeconds(delaySeconds));
            }
            else
            {
                message.DeadLetter(result.ErrorCode ?? "provider_rejected_message");
            }

            await db.SaveChangesAsync(ct);
        }

        return processed;
    }

    private static Task<CommunicationTransportResult> SendAsync(
        CommunicationOutboxMessage message,
        IServiceProvider services,
        CancellationToken ct) => message.Channel switch
    {
        "email" => services.GetRequiredService<IEmailTransport>().SendAsync(
            message.Recipient,
            message.Subject ?? string.Empty,
            message.Body,
            message.IsHtml,
            message.TextBody,
            message.Category,
            ct,
            ParseAttachments(message.AttachmentsJson)),
        "sms" => services.GetRequiredService<ISmsTransport>().SendAsync(
            message.Recipient,
            message.Body,
            ct),
        "event" => Task.FromResult(new CommunicationTransportResult(
            true, "internal-outbox", message.Id.ToString("N"), null, null, false)),
        _ => Task.FromResult(new CommunicationTransportResult(
            false, "unknown", null, "unsupported_channel", null, false))
    };

    private static IReadOnlyList<EmailAttachment> ParseAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<EmailAttachment>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
