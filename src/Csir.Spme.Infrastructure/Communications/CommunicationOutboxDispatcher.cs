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
    private readonly CommunicationDispatchPulse _pulse;
    private readonly ILogger<CommunicationOutboxDispatcher> _logger;

    public CommunicationOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        CommunicationDispatchPulse pulse,
        ILogger<CommunicationOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _pulse = pulse;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
        {
            _logger.LogWarning(
                "Communication outbox dispatcher is disabled. Queued email and SMS will not be sent until Messaging:DispatcherEnabled is true.");
            return;
        }

        _logger.LogInformation("Communication outbox dispatcher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                    await _pulse.WaitAsync(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Communication outbox dispatch batch failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    internal async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidateIds = await LoadCandidateIdsAsync(db, now, ct);

        var processed = 0;
        foreach (var candidateId in candidateIds)
        {
            var lockedUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(_options.LeaseSeconds, 10, 300));
            var message = await TryClaimAsync(db, candidateId, now, lockedUntil, ct);
            if (message is null)
                continue;

            processed++;
            CommunicationTransportResult result;
            try
            {
                result = await SendAsync(message, scope.ServiceProvider, ct);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Communication provider request failed for outbox message {OutboxMessageId}",
                    message.Id);
                result = new(false, message.Channel, null, "provider_unavailable", null, true);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                result = new(false, message.Channel, null, "provider_timeout", null, true);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(
                    exception,
                    "Unexpected communication provider failure for outbox message {OutboxMessageId}",
                    message.Id);
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

            if (!result.Accepted)
            {
                _logger.LogWarning(
                    "Communication delivery failed for outbox message {OutboxMessageId} channel {Channel} category {Category} code {ErrorCode} status {StatusCode} transient {IsTransient}.",
                    message.Id,
                    message.Channel,
                    message.Category,
                    result.ErrorCode,
                    result.HttpStatusCode,
                    result.IsTransient);
            }

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

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Lost the outbox lease for message {OutboxMessageId} while recording delivery.",
                    message.Id);
                db.ChangeTracker.Clear();
            }
        }

        return processed;
    }

    private async Task<List<Guid>> LoadCandidateIdsAsync(
        SpmeDbContext db,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var batchSize = Math.Clamp(_options.WorkerBatchSize, 1, 200);
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var rows = await db.CommunicationOutboxMessages
                .AsNoTracking()
                .Where(message => message.Status == "queued" || message.Status == "processing")
                .Select(message => new { message.Id, message.Status, message.NextAttemptAt, message.LockedUntil, message.CreatedAt })
                .ToListAsync(ct);
            return rows
                .Where(message => IsDue(message.Status, message.NextAttemptAt, message.LockedUntil, now))
                .OrderBy(message => message.NextAttemptAt)
                .ThenBy(message => message.CreatedAt)
                .Take(batchSize)
                .Select(message => message.Id)
                .ToList();
        }

        var queuedIds = await db.CommunicationOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == "queued" && message.NextAttemptAt <= now)
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(batchSize)
            .ToListAsync(ct);
        var remaining = Math.Max(0, batchSize - queuedIds.Count);
        if (remaining == 0)
            return queuedIds;

        var expiredLeaseIds = await db.CommunicationOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == "processing" &&
                message.LockedUntil != null &&
                message.LockedUntil < now)
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(remaining)
            .ToListAsync(ct);
        return queuedIds.Concat(expiredLeaseIds).ToList();
    }

    private static bool IsDue(
        string status,
        DateTimeOffset nextAttemptAt,
        DateTimeOffset? lockedUntil,
        DateTimeOffset now) =>
        (status == "queued" && nextAttemptAt <= now) ||
        (status == "processing" && lockedUntil is { } lockedUntilValue && lockedUntilValue < now);

    private static async Task<CommunicationOutboxMessage?> TryClaimAsync(
        SpmeDbContext db,
        Guid candidateId,
        DateTimeOffset now,
        DateTimeOffset lockedUntil,
        CancellationToken ct)
    {
        var message = await db.CommunicationOutboxMessages
            .SingleOrDefaultAsync(candidate => candidate.Id == candidateId, ct);
        if (message is null)
            return null;

        var claimable = IsDue(message.Status, message.NextAttemptAt, message.LockedUntil, now);
        if (!claimable)
            return null;

        message.Lease(lockedUntil);
        try
        {
            await db.SaveChangesAsync(ct);
            return message;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return null;
        }
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
