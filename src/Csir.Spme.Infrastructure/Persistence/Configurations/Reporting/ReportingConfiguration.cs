using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Reporting;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Reporting;

public class ReportingPeriodConfiguration : IEntityTypeConfiguration<ReportingPeriod>
{
    public void Configure(EntityTypeBuilder<ReportingPeriod> builder) {
        builder.ToTable("ReportingPeriods", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PeriodType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        // SQL Server unique indexes consider NULL values equal. Do not filter this index:
        // a CSIR-wide period has no institute and its code must still be unique.
        builder.HasIndex(x => new { x.ScopeType, x.InstituteId, x.Code }).IsUnique().HasFilter(null);
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder) {
        builder.ToTable("Reports", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportScope).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Abstract).HasMaxLength(4000);
        builder.Property(x => x.KeyResults);
        builder.Property(x => x.Conclusion).HasMaxLength(4000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReturnReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.InstituteId, x.ReportingPeriodId, x.ReportType })
            .IsUnique()
            .HasFilter("[ReportScope] = 'institute'");
        builder.HasIndex(x => new { x.OwnerEmployeeId, x.ReportingPeriodId, x.ReportType })
            .IsUnique()
            .HasFilter("[ReportScope] = 'employee-quarterly'");
        builder.HasIndex(x => new { x.ReviewerUserId, x.Status, x.SubmittedAt });
        builder.HasOne<Csir.Spme.Domain.Hr.Employee>().WithMany()
            .HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Hr.Employee>().WithMany()
            .HasForeignKey(x => x.ReviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Iam.User>().WithMany()
            .HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("Reports", "reporting", table =>
        {
            table.HasCheckConstraint("CK_Reports_ReportScope", "[ReportScope] IN ('institute', 'employee-quarterly')");
            table.HasCheckConstraint("CK_Reports_EmployeeQuarterlyOwnership",
                "([ReportScope] = 'institute' AND [OwnerEmployeeId] IS NULL AND [ReviewerEmployeeId] IS NULL AND [ReviewerUserId] IS NULL) OR " +
                "([ReportScope] = 'employee-quarterly' AND [OwnerEmployeeId] IS NOT NULL AND [ReviewerEmployeeId] IS NOT NULL AND [ReviewerUserId] IS NOT NULL AND [ReportType] = 'staff-quarterly')");
        });
    }
}

public sealed class ReportProjectConfiguration : IEntityTypeConfiguration<ReportProject>
{
    public void Configure(EntityTypeBuilder<ReportProject> builder)
    {
        builder.ToTable("ReportProjects", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProjectCodeSnapshot).HasMaxLength(64);
        builder.Property(x => x.ProjectNameSnapshot).HasMaxLength(256);
        builder.Property(x => x.ProgressSummary);
        builder.Property(x => x.ProgressKeyResults);
        builder.Property(x => x.Challenges);
        builder.Property(x => x.NextQuarterActivities);
        builder.Property(x => x.WayForward);
        builder.Property(x => x.SnapshotLeadName).HasMaxLength(256);
        builder.Property(x => x.SnapshotEstimatedDuration).HasMaxLength(128);
        builder.Property(x => x.SnapshotSponsorName).HasMaxLength(256);
        builder.Property(x => x.SnapshotLocation).HasMaxLength(256);
        builder.Property(x => x.SnapshotCollaboratingInstitute).HasMaxLength(512);
        builder.Property(x => x.SnapshotParticipatingScientists).HasMaxLength(4000);
        builder.Property(x => x.SnapshotObjective).HasMaxLength(4000);
        builder.Property(x => x.SnapshotMethod).HasMaxLength(4000);
        builder.Property(x => x.SnapshotJustification).HasMaxLength(4000);
        builder.Property(x => x.SnapshotExpectedBeneficiaries).HasMaxLength(4000);
        builder.Property(x => x.SnapshotPotentialTechnology).HasMaxLength(4000);
        builder.Property(x => x.SnapshotContributionToKnowledge).HasMaxLength(4000);
        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Projects.Project>().WithMany()
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReportId, x.ProjectId }).IsUnique();
        builder.ToTable("ReportProjects", "reporting", table =>
        {
            table.HasCheckConstraint("CK_ReportProjects_ConferencePapersProduced",
                "[ConferencePapersProduced] >= 0");
            table.HasCheckConstraint("CK_ReportProjects_IpTechnologiesProtected",
                "[IpTechnologiesProtected] >= 0");
        });
    }
}

public sealed class ReportTechnologyConfiguration : IEntityTypeConfiguration<ReportTechnology>
{
    public void Configure(EntityTypeBuilder<ReportTechnology> builder)
    {
        builder.ToTable("ReportTechnologies", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TechnologyCodeSnapshot).HasMaxLength(64);
        builder.Property(x => x.TechnologyNameSnapshot).HasMaxLength(256);
        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Knowledge.Technology>().WithMany()
            .HasForeignKey(x => x.TechnologyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReportId, x.TechnologyId }).IsUnique();
    }
}

public class ReportMetricConfiguration : IEntityTypeConfiguration<ReportMetric>
{
    public void Configure(EntityTypeBuilder<ReportMetric> builder)
    {
        builder.ToTable("ReportMetrics", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MetricCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NumericValue).HasPrecision(19, 4);
        builder.Property(x => x.Unit).HasMaxLength(64);
        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReportId, x.MetricCode }).IsUnique();
    }
}

public sealed class ReportAttachmentConfiguration : IEntityTypeConfiguration<ReportAttachment>
{
    public void Configure(EntityTypeBuilder<ReportAttachment> builder)
    {
        builder.ToTable("ReportAttachments", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttachmentType).HasMaxLength(64).IsRequired();
        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Common.FileRecord>().WithMany()
            .HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReportId, x.FileId }).IsUnique();
    }
}

public sealed class StaffQuarterlyReportUploadSessionConfiguration
    : IEntityTypeConfiguration<StaffQuarterlyReportUploadSession>
{
    public void Configure(EntityTypeBuilder<StaffQuarterlyReportUploadSession> builder)
    {
        builder.ToTable("StaffQuarterlyReportUploadSessions", "reporting");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UploadKind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeclaredSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.ReportId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.UploadKind, x.Status });
    }
}
