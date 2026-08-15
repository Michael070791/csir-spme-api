namespace Csir.Spme.Application.Common;

/// <summary>Formats and parses ETag values backed by row-version concurrency tokens.</summary>
public static class ConcurrencyToken
{
    public static string Format(byte[] rowVersion) =>
        $"\"{Convert.ToBase64String(rowVersion)}\"";

    public static bool TryParse(string? ifMatch, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return false;
        }

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        value = value.Trim('"');
        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
