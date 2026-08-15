using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Projects;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder) {
        builder.ToTable("Projects", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Objective).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Justification).HasMaxLength(4000);
        builder.Property(x => x.Method);
        builder.Property(x => x.ExpectedResult).HasMaxLength(4000);
        builder.Property(x => x.ActualResult).HasMaxLength(4000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Nature).HasMaxLength(64);
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Innovation).HasMaxLength(4000);
        builder.Property(x => x.Impact).HasMaxLength(4000);
        builder.HasIndex(x => new { x.InstituteId, x.Code }).IsUnique();
        builder.HasIndex(x => x.LeadEmployeeId);
    }
}

public class ProjectSponsorConfiguration : IEntityTypeConfiguration<ProjectSponsor>
{
    public void Configure(EntityTypeBuilder<ProjectSponsor> builder)
    {
        builder.ToTable("ProjectSponsors", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContactDetails).HasMaxLength(512);
        builder.Property(x => x.CommittedAmount).HasColumnType("decimal(19,4)");
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
    }
}

public class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder) {
        builder.ToTable("ProjectMilestones", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.DisplayOrder });
    }
}

public class ProjectFundingConfiguration : IEntityTypeConfiguration<ProjectFunding>
{
    public void Configure(EntityTypeBuilder<ProjectFunding> builder) {
        builder.ToTable("ProjectFundings", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FundingType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(128);
    }
}

public class ProjectUpdateConfiguration : IEntityTypeConfiguration<ProjectUpdate>
{
    public void Configure(EntityTypeBuilder<ProjectUpdate> builder) {
        builder.ToTable("ProjectUpdates", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProgressPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Risks).HasMaxLength(4000);
        builder.Property(x => x.NextSteps).HasMaxLength(4000);
        builder.HasIndex(x => x.ReportingPeriodId);
    }
}

public sealed class ProjectInceptionConfiguration : IEntityTypeConfiguration<ProjectInception>
{
    public void Configure(EntityTypeBuilder<ProjectInception> builder)
    {
        builder.ToTable("ProjectInceptions", "projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EstimatedDuration).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SponsorName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CollaboratingInstitute).HasMaxLength(512);
        builder.Property(x => x.ParticipatingScientists);
        builder.Property(x => x.ExpectedBeneficiaries);
        builder.Property(x => x.PotentialTechnology);
        builder.Property(x => x.ContributionToKnowledge);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Csir.Spme.Domain.Common.FileRecord>().WithMany()
            .HasForeignKey(x => x.ConceptNoteFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ProjectId).IsUnique();
    }
}
