using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class SqlServerRowVersionTests
{
    [Theory]
    [InlineData("timestamp", true)]
    [InlineData("rowversion", true)]
    [InlineData("varbinary", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Store_generated_rowversion_is_detected_from_sql_type(string? sqlTypeName, bool expected)
    {
        SqlServerRowVersionDetector.IsStoreGeneratedType(sqlTypeName).Should().Be(expected);
    }

    [Fact(Skip = "Requires SPME_DB_CONNECTION_STRING or SPME_DB_SA_PASSWORD; neither is available in this test environment.")]
    public async Task SqlServer_Generates_Opaque_RowVersion_On_Each_Write()
    {
        var configured = Environment.GetEnvironmentVariable("SPME_DB_CONNECTION_STRING");
        var password = Environment.GetEnvironmentVariable("SPME_DB_SA_PASSWORD");
        var builder = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder
            {
                DataSource = "localhost,15433",
                UserID = "sa",
                Password = password!,
                Encrypt = true,
                TrustServerCertificate = true
            }
            : new SqlConnectionStringBuilder(configured);
        builder.InitialCatalog = $"CsirSpmeRowVersion_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SpmeDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;

        await using var db = new SpmeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var institute = new Institute("ROWVERSION", "RowVersion Test Institute", "Institute");
            var period = ReportingPeriod.Create(
                ScopeTypes.Institute, institute.Id, "RV-2026", "RowVersion period",
                ReportingPeriodTypes.Annual, new DateTime(2026, 1, 1),
                new DateTime(2026, 12, 31), null).Value!;
            db.Institutes.Add(institute);
            db.ReportingPeriods.Add(period);
            await db.SaveChangesAsync();
            var first = period.RowVersion.ToArray();

            period.Open().IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();

            first.Should().HaveCount(8);
            period.RowVersion.Should().HaveCount(8).And.NotEqual(first);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
