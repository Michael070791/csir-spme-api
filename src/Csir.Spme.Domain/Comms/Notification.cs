using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? ActionLink { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public string Channel { get; private set; } = "in-app";

    private Notification() { }
    public Notification(Guid recipientUserId, string title, string body, string? actionLink = null)
    {
        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        Title = title;
        Body = body;
        ActionLink = actionLink;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkRead(DateTimeOffset readAt)
    {
        IsRead = true;
        ReadAt = readAt;
    }
}
