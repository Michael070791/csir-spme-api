using Aspire.Hosting.Testing;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.AppHost.Tests;

public sealed class AppHostModelTests
{
    [Fact]
    public async Task PersistentMode_DefaultsToPreflightWithoutApplyingMigrations()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Csir_Spme_AppHost>(
            [
                "--ASPIRE_DATABASE_MODE=persistent",
                "--ASPIRE_SQL_DATA_VOLUME=csir-spme-v2-sql-data-recovered",
                "--ASPIRE_SQL_HOST_PORT=15434",
                "--Parameters:jwt-key=test-jwt-key-with-enough-local-entropy-for-model-tests",
                "--Parameters:sql-password=LocalOnly!23456789"
            ],
            timeout.Token);

        var resourceNames = builder.Resources.Select(resource => resource.Name);

        resourceNames.Should().Contain(["storage", "blobs", "spme-sql", "spme-database", "db-preflight", "spme-api"]);
        resourceNames.Should().NotContain("db-migrator");
    }

    [Fact]
    public async Task LegacyContainerMode_CanExplicitlyEnableMigrationThroughTheGuardedPreflight()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Csir_Spme_AppHost>(
            [
                "--ASPIRE_DATABASE_MODE=container",
                "--ASPIRE_APPLY_DATABASE_MIGRATIONS=true",
                "--ASPIRE_SQL_HOST_PORT=15434",
                "--Parameters:jwt-key=test-jwt-key-with-enough-local-entropy-for-model-tests",
                "--Parameters:sql-password=LocalOnly!23456789"
            ],
            timeout.Token);

        builder.Resources.Select(resource => resource.Name)
            .Should().Contain(["spme-sql", "spme-database", "db-preflight", "spme-api"]);
    }

    [Fact]
    public async Task ExternalDatabaseMode_UsesNamedConnectionStringWithoutManagedSql()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Csir_Spme_AppHost>(
            [
                "--ASPIRE_DATABASE_MODE=external",
                "--ConnectionStrings:DefaultConnection=Server=localhost,1433;Database=CsirSpmeV2;User Id=sa;Password=LocalOnly!23456789;TrustServerCertificate=True;Encrypt=False",
                "--Parameters:jwt-key=test-jwt-key-with-enough-local-entropy-for-model-tests"
            ],
            timeout.Token);

        var resourceNames = builder.Resources.Select(resource => resource.Name).ToArray();

        resourceNames.Should().Contain(["storage", "blobs", "DefaultConnection", "db-preflight", "spme-api"]);
        resourceNames.Should().NotContain(["spme-sql", "spme-database", "db-migrator"]);
    }
}
