using System.IO.Compression;
using System.Security.Cryptography;

namespace Csir.Spme.Tools.LegacyImport;

public sealed record LegacyBacpacValidationResult(
    bool IsValid,
    string Message,
    string? AuthSha256 = null,
    string? SpmeSha256 = null,
    string? CombinedSha256 = null);

public static class LegacyBacpacPreflight
{
    private const long MinimumPlausibleBacpacBytes = 1024;

    public static LegacyBacpacValidationResult Validate(string authPath, string spmePath)
    {
        var auth = ValidateOne(authPath, "authentication");
        if (!auth.IsValid)
            return auth;

        var spme = ValidateOne(spmePath, "SPME");
        if (!spme.IsValid)
            return spme;

        var combined = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{auth.AuthSha256}|{spme.SpmeSha256}")));
        return new LegacyBacpacValidationResult(
            true,
            "Both BACPAC archives passed size, ZIP, and package-entry validation.",
            auth.AuthSha256,
            spme.SpmeSha256,
            combined);
    }

    private static LegacyBacpacValidationResult ValidateOne(string path, string source)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new LegacyBacpacValidationResult(false, $"{source} BACPAC was not found: {path}");

        var file = new FileInfo(path);
        if (file.Length < MinimumPlausibleBacpacBytes)
        {
            return new LegacyBacpacValidationResult(
                false,
                $"{source} BACPAC is only {file.Length} bytes and is not a valid database export: {path}");
        }

        try
        {
            using var archive = ZipFile.OpenRead(path);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!entryNames.Contains("model.xml") ||
                !entryNames.Contains("Origin.xml") ||
                !entryNames.Contains("[Content_Types].xml"))
            {
                return new LegacyBacpacValidationResult(
                    false,
                    $"{source} BACPAC is missing required package entries: {path}");
            }

            foreach (var entry in archive.Entries)
            {
                using var stream = entry.Open();
                stream.CopyTo(Stream.Null);
            }
        }
        catch (InvalidDataException exception)
        {
            return new LegacyBacpacValidationResult(
                false,
                $"{source} BACPAC failed ZIP integrity validation: {exception.Message}");
        }

        using var sourceStream = File.OpenRead(path);
        var checksum = Convert.ToHexString(SHA256.HashData(sourceStream));
        return source.Equals("authentication", StringComparison.Ordinal)
            ? new LegacyBacpacValidationResult(true, "Valid.", AuthSha256: checksum)
            : new LegacyBacpacValidationResult(true, "Valid.", SpmeSha256: checksum);
    }
}
