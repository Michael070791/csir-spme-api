using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

/// <summary>
/// Makes the reporting-period natural key apply to CSIR-wide rows, where InstituteId is null.
/// The preflight stops rather than changing existing data when legacy duplicates need review.
/// </summary>
[DbContext(typeof(SpmeDbContext))]
[Migration("20260809000000_EnforceReportingPeriodCsirWideCodeUniqueness")]
public partial class EnforceReportingPeriodCsirWideCodeUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM [reporting].[ReportingPeriods]
                WHERE [InstituteId] IS NULL
                GROUP BY [ScopeType], [Code]
                HAVING COUNT(*) > 1
            )
                THROW 51000, 'Cannot enforce reporting-period code uniqueness for rows without an institute because duplicate legacy scope/code values exist. Reconcile them before retrying this migration.', 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_ReportingPeriods_ScopeType_InstituteId_Code",
            schema: "reporting",
            table: "ReportingPeriods");

        migrationBuilder.CreateIndex(
            name: "IX_ReportingPeriods_ScopeType_InstituteId_Code",
            schema: "reporting",
            table: "ReportingPeriods",
            columns: new[] { "ScopeType", "InstituteId", "Code" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReportingPeriods_ScopeType_InstituteId_Code",
            schema: "reporting",
            table: "ReportingPeriods");

        migrationBuilder.CreateIndex(
            name: "IX_ReportingPeriods_ScopeType_InstituteId_Code",
            schema: "reporting",
            table: "ReportingPeriods",
            columns: new[] { "ScopeType", "InstituteId", "Code" },
            unique: true,
            filter: "[InstituteId] IS NOT NULL");
    }
}
