using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public sealed class CommunicationOutboxMessage : BaseEntity
{
    public string Channel { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string? TextBody { get; private set; }
    public string? AttachmentsJson { get; private set; }
    public bool IsHtml { get; private set; }
    public string Category { get; private set; } = "notification";
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = "queued";
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? LastErrorCode { get; private set; }

    private CommunicationOutboxMessage() { }

    public CommunicationOutboxMessage(
        string channel,
        string recipient,
        string? subject,
        string body,
        bool isHtml,
        string category,
        string idempotencyKey,
        string? textBody = null,
        string? attachmentsJson = null)
    {
        Channel = channel;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        TextBody = textBody;
        AttachmentsJson = attachmentsJson;
        IsHtml = isHtml;
        Category = category;
        IdempotencyKey = idempotencyKey;
        NextAttemptAt = DateTimeOffset.UtcNow;
    }

    public void Lease(DateTimeOffset lockedUntil)
    {
        Status = "processing";
        LockedUntil = lockedUntil;
        AttemptCount++;
    }

    public void MarkDelivered(string? providerMessageId, DateTimeOffset deliveredAt)
    {
        Status = "delivered";
        ProviderMessageId = providerMessageId;
        DeliveredAt = deliveredAt;
        LockedUntil = null;
        LastErrorCode = null;
    }

    public void Retry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        Status = "queued";
        LastErrorCode = errorCode;
        NextAttemptAt = nextAttemptAt;
        LockedUntil = null;
    }

    public void DeadLetter(string errorCode)
    {
        Status = "dead-letter";
        LastErrorCode = errorCode;
        LockedUntil = null;
    }
}
