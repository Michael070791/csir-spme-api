namespace Csir.Spme.Domain.Iam;

public class NotificationPreference
{
    public Guid UserId { get; private set; }
    public bool EmailAlerts { get; private set; } = true;
    public bool LeaveReminders { get; private set; } = true;
    public bool PromotionUpdates { get; private set; } = true;
    public bool SystemAnnouncements { get; private set; } = true;

    private NotificationPreference() { }

    public NotificationPreference(Guid userId) => UserId = userId;

    public void Update(bool emailAlerts, bool leaveReminders, bool promotionUpdates, bool systemAnnouncements)
    {
        EmailAlerts = emailAlerts;
        LeaveReminders = leaveReminders;
        PromotionUpdates = promotionUpdates;
        SystemAnnouncements = systemAnnouncements;
    }
}
