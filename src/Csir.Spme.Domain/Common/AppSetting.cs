namespace Csir.Spme.Domain.Common;

public class AppSetting : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private AppSetting() { }

    public AppSetting(string key, string value)
    {
        Key = key.Trim();
        Value = value.Trim();
    }

    public void Update(string value)
    {
        Value = value.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
