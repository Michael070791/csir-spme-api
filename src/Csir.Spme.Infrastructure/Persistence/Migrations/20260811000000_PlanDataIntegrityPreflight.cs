using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

/// <summary>
/// Read-only release gate for legacy planning, project, and reporting data. Every check stops the
/// migration with a targeted message; this migration never repairs, rewrites, or deletes data.
/// </summary>
[DbContext(typeof(SpmeDbContext))]
[Migration("20260811000000_PlanDataIntegrityPreflight")]
public partial class PlanDataIntegrityPreflight : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [plan].[StrategicPlans] WHERE [Status] NOT IN ('draft','active','closed','archived'))
                THROW 51101, 'PLAN preflight failed: StrategicPlans contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Thrusts] WHERE [Status] NOT IN ('draft','active','on-track','at-risk','completed','archived'))
                THROW 51102, 'PLAN preflight failed: Thrusts contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Outputs] WHERE [Status] NOT IN ('draft','active','on-track','at-risk','completed','archived'))
                THROW 51103, 'PLAN preflight failed: Outputs contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Indicators] WHERE [Status] NOT IN ('draft','active','on-track','at-risk','completed','archived'))
                THROW 51104, 'PLAN preflight failed: Indicators contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] WHERE [Status] NOT IN ('draft','active','on-hold','completed','cancelled','archived'))
                THROW 51105, 'PLAN preflight failed: Projects contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[ReportingPeriods] WHERE [Status] NOT IN ('draft','open','closed','finalized'))
                THROW 51106, 'PLAN preflight failed: ReportingPeriods contains an invalid status.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[Reports] WHERE [Status] NOT IN ('draft','submitted','under-review','returned','approved','archived'))
                THROW 51107, 'PLAN preflight failed: Reports contains an invalid status.', 1;

            IF EXISTS (SELECT 1 FROM [plan].[StrategicPlans] WHERE [StartYear] > [EndYear])
                THROW 51108, 'PLAN preflight failed: StrategicPlans contains an invalid year range.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] WHERE [EndDate] IS NOT NULL AND [EndDate] < [StartDate])
                THROW 51109, 'PLAN preflight failed: Projects contains an invalid date range.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[ReportingPeriods] WHERE [EndDate] < [StartDate])
                THROW 51110, 'PLAN preflight failed: ReportingPeriods contains an invalid date range.', 1;

            IF EXISTS (SELECT 1 FROM [plan].[StrategicPlans] p LEFT JOIN [org].[Institutes] i ON i.[Id] = p.[InstituteId] WHERE i.[Id] IS NULL)
                THROW 51111, 'PLAN preflight failed: StrategicPlans contains an orphan institute.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Thrusts] t LEFT JOIN [plan].[StrategicPlans] p ON p.[Id] = t.[StrategicPlanId] WHERE p.[Id] IS NULL)
                THROW 51112, 'PLAN preflight failed: Thrusts contains an orphan strategic plan.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Outputs] o LEFT JOIN [plan].[Thrusts] t ON t.[Id] = o.[ThrustId] WHERE t.[Id] IS NULL)
                THROW 51113, 'PLAN preflight failed: Outputs contains an orphan thrust.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Indicators] i LEFT JOIN [plan].[Outputs] o ON o.[Id] = i.[OutputId] WHERE o.[Id] IS NULL)
                THROW 51114, 'PLAN preflight failed: Indicators contains an orphan output.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[IndicatorMeasurements] m LEFT JOIN [plan].[Indicators] i ON i.[Id] = m.[IndicatorId] WHERE i.[Id] IS NULL)
                THROW 51115, 'PLAN preflight failed: IndicatorMeasurements contains an orphan indicator.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[IndicatorMeasurements] m LEFT JOIN [reporting].[ReportingPeriods] p ON p.[Id] = m.[ReportingPeriodId] WHERE p.[Id] IS NULL)
                THROW 51116, 'PLAN preflight failed: IndicatorMeasurements contains an orphan reporting period.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] p LEFT JOIN [org].[Institutes] i ON i.[Id] = p.[InstituteId] WHERE i.[Id] IS NULL)
                THROW 51117, 'PLAN preflight failed: Projects contains an orphan institute.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] p LEFT JOIN [plan].[Thrusts] t ON t.[Id] = p.[ThrustId] WHERE p.[ThrustId] IS NOT NULL AND t.[Id] IS NULL)
                THROW 51133, 'PLAN preflight failed: Projects contains an orphan thrust.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] p LEFT JOIN [hr].[Employees] e ON e.[Id] = p.[LeadEmployeeId] WHERE p.[LeadEmployeeId] IS NOT NULL AND e.[Id] IS NULL)
                THROW 51134, 'PLAN preflight failed: Projects contains an orphan lead employee.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[ReportingPeriods] p LEFT JOIN [org].[Institutes] i ON i.[Id] = p.[InstituteId] WHERE p.[InstituteId] IS NOT NULL AND i.[Id] IS NULL)
                THROW 51118, 'PLAN preflight failed: ReportingPeriods contains an orphan institute.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[Reports] r LEFT JOIN [org].[Institutes] i ON i.[Id] = r.[InstituteId] WHERE i.[Id] IS NULL)
                THROW 51119, 'PLAN preflight failed: Reports contains an orphan institute.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[Reports] r LEFT JOIN [reporting].[ReportingPeriods] p ON p.[Id] = r.[ReportingPeriodId] WHERE p.[Id] IS NULL)
                THROW 51120, 'PLAN preflight failed: Reports contains an orphan reporting period.', 1;

            IF EXISTS (
                SELECT 1 FROM [plan].[Thrusts] t
                INNER JOIN [plan].[StrategicPlans] p ON p.[Id] = t.[StrategicPlanId]
                WHERE t.[InstituteId] <> p.[InstituteId])
                THROW 51121, 'PLAN preflight failed: a thrust and its strategic plan belong to different institutes.', 1;
            IF EXISTS (
                SELECT 1 FROM [projects].[Projects] p
                INNER JOIN [plan].[Thrusts] t ON t.[Id] = p.[ThrustId]
                WHERE p.[ThrustId] IS NOT NULL AND p.[InstituteId] <> t.[InstituteId])
                THROW 51122, 'PLAN preflight failed: a project and its thrust belong to different institutes.', 1;
            IF EXISTS (
                SELECT 1 FROM [projects].[Projects] p
                INNER JOIN [hr].[Employees] e ON e.[Id] = p.[LeadEmployeeId]
                WHERE p.[LeadEmployeeId] IS NOT NULL AND p.[InstituteId] <> e.[InstituteId])
                THROW 51123, 'PLAN preflight failed: a project and its lead employee belong to different institutes.', 1;
            IF EXISTS (
                SELECT 1 FROM [reporting].[Reports] r
                INNER JOIN [reporting].[ReportingPeriods] p ON p.[Id] = r.[ReportingPeriodId]
                WHERE p.[ScopeType] = 'institute' AND r.[InstituteId] <> p.[InstituteId])
                THROW 51124, 'PLAN preflight failed: a report and its institute reporting period belong to different institutes.', 1;
            IF EXISTS (
                SELECT 1 FROM [plan].[IndicatorMeasurements] m
                INNER JOIN [plan].[Indicators] i ON i.[Id] = m.[IndicatorId]
                INNER JOIN [plan].[Outputs] o ON o.[Id] = i.[OutputId]
                INNER JOIN [plan].[Thrusts] t ON t.[Id] = o.[ThrustId]
                INNER JOIN [reporting].[ReportingPeriods] p ON p.[Id] = m.[ReportingPeriodId]
                WHERE p.[ScopeType] = 'institute' AND t.[InstituteId] <> p.[InstituteId])
                THROW 51135, 'PLAN preflight failed: an indicator measurement uses a reporting period from another institute.', 1;

            IF EXISTS (SELECT 1 FROM [plan].[StrategicPlans] GROUP BY [InstituteId], [Code] HAVING COUNT(*) > 1)
                THROW 51125, 'PLAN preflight failed: duplicate StrategicPlans institute/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Thrusts] GROUP BY [StrategicPlanId], [Code] HAVING COUNT(*) > 1)
                THROW 51126, 'PLAN preflight failed: duplicate Thrusts strategic-plan/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Outputs] GROUP BY [ThrustId], [Code] HAVING COUNT(*) > 1)
                THROW 51127, 'PLAN preflight failed: duplicate Outputs thrust/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[Indicators] GROUP BY [OutputId], [Code] HAVING COUNT(*) > 1)
                THROW 51128, 'PLAN preflight failed: duplicate Indicators output/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [plan].[IndicatorMeasurements] GROUP BY [IndicatorId], [ReportingPeriodId] HAVING COUNT(*) > 1)
                THROW 51129, 'PLAN preflight failed: duplicate IndicatorMeasurements indicator/reporting-period natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [projects].[Projects] GROUP BY [InstituteId], [Code] HAVING COUNT(*) > 1)
                THROW 51130, 'PLAN preflight failed: duplicate Projects institute/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[ReportingPeriods] GROUP BY [ScopeType], [InstituteId], [Code] HAVING COUNT(*) > 1)
                THROW 51131, 'PLAN preflight failed: duplicate ReportingPeriods scope/institute/code natural keys exist.', 1;
            IF EXISTS (SELECT 1 FROM [reporting].[Reports] GROUP BY [InstituteId], [ReportingPeriodId], [ReportType] HAVING COUNT(*) > 1)
                THROW 51132, 'PLAN preflight failed: duplicate Reports institute/period/type natural keys exist.', 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
            schema: "reporting",
            table: "Reports");

        migrationBuilder.CreateIndex(
            name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
            schema: "reporting",
            table: "Reports",
            columns: new[] { "InstituteId", "ReportingPeriodId", "ReportType" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
            schema: "reporting",
            table: "Reports");

        migrationBuilder.CreateIndex(
            name: "IX_Reports_InstituteId_ReportingPeriodId_ReportType",
            schema: "reporting",
            table: "Reports",
            columns: new[] { "InstituteId", "ReportingPeriodId", "ReportType" });
    }
}
