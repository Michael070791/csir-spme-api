using System.IO.Compression;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Tools.LegacyImport.Tests;

public sealed class LegacyBacpacPreflightTests
{
    [Fact]
    public void DeriveSourceName_Uses_Supplied_Bacpac_File_Names()
    {
        var sourceName = LegacyImportSourceName.Derive(
            "/backups/2026-07-28/csir-auth-spme-db-2026-7-28-22-32.bacpac",
            "/backups/2026-07-28/csir-spme-db-2026-7-28-22-33.bacpac");

        sourceName.Should().Be("csir-auth-spme-db-2026-7-28-22-32__csir-spme-db-2026-7-28-22-33");
    }

    [Fact]
    public void DeriveSourceName_Remains_Deterministic_Within_The_Schema_Limit()
    {
        var authPath = $"/backups/{new string('a', 100)}.bacpac";
        var spmePath = $"/backups/{new string('b', 100)}.bacpac";

        var sourceName = LegacyImportSourceName.Derive(authPath, spmePath);

        sourceName.Should().HaveLength(128);
        sourceName.Should().EndWith("__" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{new string('a', 100)}__{new string('b', 100)}")))[..16]);
    }

    [Fact]
    public void Validate_Rejects_Four_Byte_Placeholders()
    {
        var directory = Directory.CreateTempSubdirectory("legacy-bacpac-test-");
        try
        {
            var auth = Path.Combine(directory.FullName, "auth.bacpac");
            var spme = Path.Combine(directory.FullName, "spme.bacpac");
            File.WriteAllBytes(auth, [0x50, 0x4B, 0x03, 0x04]);
            File.WriteAllBytes(spme, [0x50, 0x4B, 0x03, 0x04]);

            var result = LegacyBacpacPreflight.Validate(auth, spme);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("only 4 bytes");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Validate_Accepts_Intact_Bacpac_Packages_And_Produces_Combined_Provenance()
    {
        var directory = Directory.CreateTempSubdirectory("legacy-bacpac-test-");
        try
        {
            var auth = CreatePackage(directory.FullName, "auth.bacpac");
            var spme = CreatePackage(directory.FullName, "spme.bacpac");

            var result = LegacyBacpacPreflight.Validate(auth, spme);

            result.IsValid.Should().BeTrue();
            result.AuthSha256.Should().HaveLength(64);
            result.SpmeSha256.Should().HaveLength(64);
            result.CombinedSha256.Should().HaveLength(64);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string CreatePackage(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entryName in new[] { "model.xml", "Origin.xml", "[Content_Types].xml" })
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(new string('x', 512));
        }

        return path;
    }
}
