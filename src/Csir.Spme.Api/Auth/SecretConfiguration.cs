using System.Text;

namespace Csir.Spme.Api.Auth;

internal static class SecretConfiguration
{
    private const int MinimumSecretBytes = 32;

    public static string RequireStrongSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        if (!IsStrongSecret(value))
        {
            throw new InvalidOperationException(
                $"{key} must be supplied by .NET User Secrets, an environment variable, or an Azure Key Vault reference and must contain at least {MinimumSecretBytes} UTF-8 bytes. Placeholder values are rejected.");
        }

        return value!;
    }

    public static bool IsStrongSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !IsPlaceholder(value) &&
        Encoding.UTF8.GetByteCount(value.Trim()) >= MinimumSecretBytes;

    private static bool IsPlaceholder(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("TODO", StringComparison.OrdinalIgnoreCase) ||
            (trimmed.StartsWith('<') && trimmed.EndsWith('>'));
    }
}

internal sealed class JwtSecretOptions
{
    public string Key { get; set; } = string.Empty;
}

internal sealed class AccountActivationSecretOptions
{
    public string HashKey { get; set; } = string.Empty;
}
