using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Communications;

public sealed class DurableCommunicationOutbox : ICommunicationOutbox
{
    private readonly SpmeDbContext _db;

    public DurableCommunicationOutbox(SpmeDbContext db) => _db = db;

    public Task EnqueueEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml,
        string category,
        string idempotencyKey,
        CancellationToken ct = default) =>
        EnqueueAsync(new CommunicationOutboxMessage(
            "email", to.Trim(), subject.Trim(), body, isHtml, category, idempotencyKey), ct);

    public Task EnqueueSmsAsync(
        string to,
        string body,
        string category,
        string idempotencyKey,
        CancellationToken ct = default) =>
        EnqueueAsync(new CommunicationOutboxMessage(
            "sms", to.Trim(), null, body, false, category, idempotencyKey), ct);

    private async Task EnqueueAsync(CommunicationOutboxMessage message, CancellationToken ct)
    {
        if (await _db.CommunicationOutboxMessages.AnyAsync(
                candidate => candidate.IdempotencyKey == message.IdempotencyKey, ct))
            return;

        _db.CommunicationOutboxMessages.Add(message);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Entry(message).State = EntityState.Detached;
            if (!await _db.CommunicationOutboxMessages.AsNoTracking()
                .AnyAsync(candidate => candidate.IdempotencyKey == message.IdempotencyKey, ct))
                throw;

            await _db.SaveChangesAsync(ct);
        }
    }
}

public sealed class DurableEmailService : IEmailService
{
    private readonly ICommunicationOutbox _outbox;
    public DurableEmailService(ICommunicationOutbox outbox) => _outbox = outbox;

    public Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default) =>
        _outbox.EnqueueEmailAsync(to, subject, body, isHtml, "notification", Guid.NewGuid().ToString("N"), ct);
}

public sealed class DurableSmsService : ISmsService
{
    private readonly ICommunicationOutbox _outbox;
    public DurableSmsService(ICommunicationOutbox outbox) => _outbox = outbox;

    public Task SendAsync(string to, string body, CancellationToken ct = default) =>
        _outbox.EnqueueSmsAsync(to, body, "notification", Guid.NewGuid().ToString("N"), ct);
}
