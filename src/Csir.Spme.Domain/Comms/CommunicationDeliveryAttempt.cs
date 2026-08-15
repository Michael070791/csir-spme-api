using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public sealed class CommunicationDeliveryAttempt : BaseEntity
{
    public Guid OutboxMessageId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? ProviderMessageId { get; private set; }
    public string? ErrorCode { get; private set; }
    public int? HttpStatusCode { get; private set; }

    private CommunicationDeliveryAttempt() { }

    public CommunicationDeliveryAttempt(
        Guid outboxMessageId,
        int attemptNumber,
        string provider,
        string outcome,
        string? providerMessageId,
        string? errorCode,
        int? httpStatusCode)
    {
        OutboxMessageId = outboxMessageId;
        AttemptNumber = attemptNumber;
        Provider = provider;
        Outcome = outcome;
        ProviderMessageId = providerMessageId;
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}
