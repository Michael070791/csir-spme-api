using Csir.Spme.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Promotions;

public sealed class PromotionPolicySourceConfiguration : IEntityTypeConfiguration<PromotionPolicySource>
{
    public void Configure(EntityTypeBuilder<PromotionPolicySource> builder)
    {
        builder.ToTable("PolicySources", "promotions");
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.DocumentVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SectionReference).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PageReference).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceChecksum).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SourceChecksum, x.SectionReference }).IsUnique();
    }
}

public sealed class PromotionGradeEquivalencyConfiguration : IEntityTypeConfiguration<PromotionGradeEquivalency>
{
    public void Configure(EntityTypeBuilder<PromotionGradeEquivalency> builder)
    {
        builder.ToTable("GradeEquivalencies", "promotions");
        builder.Property(x => x.EquivalentTitle).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedEquivalentTitle).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StaffCategory).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PromotionStream).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ApprovalStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.NormalizedEquivalentTitle, x.StaffCategory, x.PromotionStream }).IsUnique();
    }
}

public sealed class PromotionCycleConfiguration : IEntityTypeConfiguration<PromotionCycle>
{
    public void Configure(EntityTypeBuilder<PromotionCycle> builder)
    {
        builder.ToTable("Cycles", "promotions");
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.CycleYear).IsUnique();
    }
}

public sealed class PromotionPathConfiguration : IEntityTypeConfiguration<PromotionPath>
{
    public void Configure(EntityTypeBuilder<PromotionPath> builder)
    {
        builder.ToTable("Paths", "promotions");
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SectionReference).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StaffCategory).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PromotionStream).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RequiredQualificationLevel).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.PolicySourceId, x.StaffCategory, x.PromotionStream, x.SourceGradeId }).IsUnique();
    }
}

public sealed class PromotionAssessmentConfiguration : IEntityTypeConfiguration<PromotionAssessment>
{
    public void Configure(EntityTypeBuilder<PromotionAssessment> builder)
    {
        builder.ToTable("Assessments", "promotions");
        builder.Property(x => x.CompletedSourceGradeYears).HasPrecision(5, 2);
        builder.Property(x => x.EligibilityState).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BlockingReasonsJson).IsRequired();
        builder.Property(x => x.PendingHrChecksJson).IsRequired();
        builder.Property(x => x.EligibilitySnapshotJson).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.PromotionCycleId, x.PromotionPathId }).IsUnique();
    }
}

public sealed class PromotionStatusSnapshotConfiguration : IEntityTypeConfiguration<PromotionStatusSnapshot>
{
    public void Configure(EntityTypeBuilder<PromotionStatusSnapshot> builder)
    {
        builder.ToTable("StatusSnapshots", "promotions");
        builder.Property(x => x.StaffCategory).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssessmentState).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EligibilityState).HasMaxLength(64);
        builder.Property(x => x.PromotionSubmissionStatus).HasMaxLength(32);
        builder.HasIndex(x => new { x.EmployeeId, x.PromotionCycleId }).IsUnique();
    }
}

public sealed class PromotionQualificationAssessmentConfiguration : IEntityTypeConfiguration<PromotionQualificationAssessment>
{
    public void Configure(EntityTypeBuilder<PromotionQualificationAssessment> builder)
    {
        builder.ToTable("QualificationAssessments", "promotions");
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.PromotionAssessmentId, x.EducationRecordId }).IsUnique();
    }
}

public sealed class PromotionAppraisalAssessmentConfiguration : IEntityTypeConfiguration<PromotionAppraisalAssessment>
{
    public void Configure(EntityTypeBuilder<PromotionAppraisalAssessment> builder)
    {
        builder.ToTable("AppraisalAssessments", "promotions");
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.PromotionAssessmentId, x.PerformanceAppraisalId }).IsUnique();
    }
}

public sealed class PromotionSubmissionRequirementTemplateConfiguration : IEntityTypeConfiguration<PromotionSubmissionRequirementTemplate>
{
    public void Configure(EntityTypeBuilder<PromotionSubmissionRequirementTemplate> builder)
    {
        builder.ToTable("SubmissionRequirementTemplates", "promotions");
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequirementType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.DeclarationText);
        builder.Property(x => x.ReportTemplateCode).HasMaxLength(64);
        builder.Property(x => x.AcceptedContentTypesJson);
        builder.HasIndex(x => new { x.PromotionCycleId, x.PromotionPathId, x.Code }).IsUnique();
    }
}

public sealed class PromotionSubmissionRequirementSnapshotConfiguration : IEntityTypeConfiguration<PromotionSubmissionRequirementSnapshot>
{
    public void Configure(EntityTypeBuilder<PromotionSubmissionRequirementSnapshot> builder)
    {
        builder.ToTable("SubmissionRequirementSnapshots", "promotions");
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequirementType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.DeclarationText);
        builder.Property(x => x.ReportTemplateCode).HasMaxLength(64);
        builder.Property(x => x.AcceptedContentTypesJson);
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.Code }).IsUnique();
    }
}

public sealed class PromotionSubmissionConfiguration : IEntityTypeConfiguration<PromotionSubmission>
{
    public void Configure(EntityTypeBuilder<PromotionSubmission> builder)
    {
        builder.ToTable("Submissions", "promotions");
        builder.Property(x => x.RequestedTargetJobTitle).HasMaxLength(256);
        builder.Property(x => x.EmployeeNote).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.SubmittedAt });
        builder.HasIndex(x => new { x.EmployeeId, x.Status });
        builder.HasIndex(x => x.PromotionAssessmentId).IsUnique();
    }
}

public sealed class PromotionSubmissionReportConfiguration : IEntityTypeConfiguration<PromotionSubmissionReport>
{
    public void Configure(EntityTypeBuilder<PromotionSubmissionReport> builder)
    {
        builder.ToTable("SubmissionReports", "promotions");
        builder.Property(x => x.ReportType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentJson).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.RequirementSnapshotId }).IsUnique();
    }
}

public sealed class PromotionSubmissionDeclarationConfiguration : IEntityTypeConfiguration<PromotionSubmissionDeclaration>
{
    public void Configure(EntityTypeBuilder<PromotionSubmissionDeclaration> builder)
    {
        builder.ToTable("SubmissionDeclarations", "promotions");
        builder.Property(x => x.DeclarationTextSnapshot).IsRequired();
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.RequirementSnapshotId }).IsUnique();
    }
}

public sealed class PromotionSubmissionDocumentConfiguration : IEntityTypeConfiguration<PromotionSubmissionDocument>
{
    public void Configure(EntityTypeBuilder<PromotionSubmissionDocument> builder)
    {
        builder.ToTable("SubmissionDocuments", "promotions");
        builder.Property(x => x.DocumentStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EmployeeVisibleReviewNote).HasMaxLength(2000);
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.RequirementSnapshotId, x.FileId }).IsUnique();
    }
}

public sealed class PromotionDecisionConfiguration : IEntityTypeConfiguration<PromotionDecision>
{
    public void Configure(EntityTypeBuilder<PromotionDecision> builder)
    {
        builder.ToTable("Decisions", "promotions");
        builder.Property(x => x.Decision).HasMaxLength(32).IsRequired();
        builder.Property(x => x.InternalDecisionNote).HasMaxLength(4000);
        builder.Property(x => x.EmployeeVisibleNote).HasMaxLength(2000);
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.DecidedAt });
    }
}

public sealed class PromotionDocumentUploadSessionConfiguration : IEntityTypeConfiguration<PromotionDocumentUploadSession>
{
    public void Configure(EntityTypeBuilder<PromotionDocumentUploadSession> builder)
    {
        builder.ToTable("DocumentUploadSessions", "promotions");
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeclaredSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.PromotionSubmissionId, x.RequirementSnapshotId, x.Status });
        builder.HasOne<PromotionSubmission>().WithMany().HasForeignKey(x => x.PromotionSubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromotionSubmissionRequirementSnapshot>().WithMany().HasForeignKey(x => x.RequirementSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Common.FileRecord>().WithMany().HasForeignKey(x => x.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
