using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Csir.Spme.Tools.LegacyImport.Tests;

public class LegacyImportSchemaTests
{
    [Fact]
    public void SpmeDbContext_Model_Contains_Legacy_Reconciliation_And_Import_Tables()
    {
        var options = new DbContextOptionsBuilder<SpmeDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new SpmeDbContext(options);

        AssertTable(context, typeof(LegacyImportRun), "ops", "LegacyImportRuns");
        AssertTable(context, typeof(LegacyIdMapping), "ops", "LegacyIdMappings");
        AssertTable(context, typeof(LegacyImportIssue), "ops", "LegacyImportIssues");
        AssertTable(context, typeof(InstituteAlias), "org", "InstituteAliases");
        AssertTable(context, typeof(EmployeeImportBatch), "hr", "EmployeeImportBatches");
        AssertTable(context, typeof(EmployeeImportRow), "hr", "EmployeeImportRows");
        AssertTable(context, typeof(EmployeeImportFieldMapping), "hr", "EmployeeImportFieldMappings");
        AssertTable(context, typeof(EmployeeSpouse), "hr", "EmployeeSpouses");
        AssertTable(context, typeof(EmployeeChild), "hr", "EmployeeChildren");
        AssertTable(context, typeof(LeaveRequestApproval), "leave", "LeaveRequestApprovals");
        AssertTable(context, typeof(LeaveHandover), "leave", "LeaveHandovers");
        AssertTable(context, typeof(LeaveResumption), "leave", "LeaveResumptions");
        AssertTable(context, typeof(CompassionateLeaveType), "leave", "CompassionateLeaveTypes");
        AssertTable(context, typeof(ProjectSponsor), "projects", "ProjectSponsors");
        AssertTable(context, typeof(ReportMetric), "reporting", "ReportMetrics");
    }

    [Fact]
    public void SpmeDbContext_Model_Enforces_Legacy_Reconciliation_Uniqueness()
    {
        var options = new DbContextOptionsBuilder<SpmeDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new SpmeDbContext(options);

        var mapping = context.Model.FindEntityType(typeof(LegacyIdMapping));
        mapping.Should().NotBeNull();
        var mappingIndexProperties = new[]
        {
            nameof(LegacyIdMapping.LegacyImportRunId),
            nameof(LegacyIdMapping.SourceDatabase),
            nameof(LegacyIdMapping.SourceTable),
            nameof(LegacyIdMapping.SourceKey)
        };
        mapping!.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(mappingIndexProperties)).Should().BeTrue();

        var alias = context.Model.FindEntityType(typeof(InstituteAlias));
        alias.Should().NotBeNull();
        alias!.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(InstituteAlias.NormalizedAlias) })).Should().BeTrue();

        var report = context.Model.FindEntityType(typeof(Report));
        report.Should().NotBeNull();
        report!.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(Report.InstituteId),
                nameof(Report.ReportingPeriodId),
                nameof(Report.ReportType)
            })).Should().BeTrue();
    }

    private static void AssertTable(DbContext context, Type entityType, string schema, string table)
    {
        var entity = context.Model.FindEntityType(entityType);
        if (entity is null)
            throw new InvalidOperationException($"Entity '{entityType.Name}' is not configured in the DbContext model.");

        entity.GetSchema().Should().Be(schema);
        entity.GetTableName().Should().Be(table);
    }
}
