using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Common;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Common;

public class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> builder) {
        builder.ToTable("FileRecords", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(64);
        builder.Property(x => x.Classification).HasMaxLength(32);
        builder.Property(x => x.ScanStatus).HasMaxLength(32).HasDefaultValue("pending").IsRequired();
        builder.Property(x => x.RetentionRule).HasMaxLength(32);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => new { x.IsDeleted, x.StorageDeletedAt });
    }
}

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder) {
        builder.ToTable("AuditRecords", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorScope).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetId).HasMaxLength(128);
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ClientIp).HasMaxLength(48);
        builder.Property(x => x.BeforeSummary);
        builder.Property(x => x.AfterSummary);
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.OccurredAt });
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        builder.HasIndex(x => x.CorrelationId);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public class LegacyImportRunConfiguration : IEntityTypeConfiguration<LegacyImportRun>
{
    public void Configure(EntityTypeBuilder<LegacyImportRun> builder)
    {
        builder.ToTable("LegacyImportRuns", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceBackupPath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.SourceBackupSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RowCountsJson).IsRequired();
        builder.Property(x => x.ReconciliationJson).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.SourceName, x.StartedAt });
        builder.HasIndex(x => x.Status);
    }
}

public class LegacyIdMappingConfiguration : IEntityTypeConfiguration<LegacyIdMapping>
{
    public void Configure(EntityTypeBuilder<LegacyIdMapping> builder)
    {
        builder.ToTable("LegacyIdMappings", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceDatabase).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceTable).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TargetSchema).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetTable).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MatchKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.MatchStrategy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RowChecksum).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.LegacyImportRunId, x.SourceDatabase, x.SourceTable, x.SourceKey }).IsUnique();
        builder.HasIndex(x => new { x.TargetSchema, x.TargetTable, x.TargetId });
    }
}

public class LegacyImportIssueConfiguration : IEntityTypeConfiguration<LegacyImportIssue>
{
    public void Configure(EntityTypeBuilder<LegacyImportIssue> builder)
    {
        builder.ToTable("LegacyImportIssues", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceDatabase).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceTable).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ResolutionStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => new { x.LegacyImportRunId, x.Severity, x.ResolutionStatus });
        builder.HasIndex(x => new { x.SourceDatabase, x.SourceTable, x.SourceKey });
    }
}
