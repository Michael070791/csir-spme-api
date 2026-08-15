namespace Csir.Spme.Domain.Iam;

public sealed class UserSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string? DeviceName { get; private set; }
    public string? Platform { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private UserSession() { }

    public UserSession(Guid id, Guid userId, string? deviceName, string? platform, DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        DeviceName = Normalize(deviceName, 128);
        Platform = Normalize(platform, 32);
        StartedAt = startedAt;
        LastSeenAt = startedAt;
    }

    public void Touch(DateTimeOffset at) => LastSeenAt = at;
    public void Revoke(DateTimeOffset at) => RevokedAt ??= at;

    private static string? Normalize(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized[..Math.Min(normalized.Length, maximumLength)];
    }
}
