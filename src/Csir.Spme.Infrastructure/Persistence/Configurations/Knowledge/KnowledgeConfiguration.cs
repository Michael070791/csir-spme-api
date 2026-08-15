using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Knowledge;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Knowledge;

public class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> builder) {
        builder.ToTable("Technologies", "knowledge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ApplicationArea).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TechnologyType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Code }).IsUnique();
    }
}

public class PublicationConfiguration : IEntityTypeConfiguration<Publication>
{
    public void Configure(EntityTypeBuilder<Publication> builder) {
        builder.ToTable("Publications", "knowledge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Abstract).HasMaxLength(4000);
        builder.Property(x => x.PublicationType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Citation).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Title });
    }
}

public class SuccessStoryConfiguration : IEntityTypeConfiguration<SuccessStory>
{
    public void Configure(EntityTypeBuilder<SuccessStory> builder) {
        builder.ToTable("SuccessStories", "knowledge");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.ProjectId });
    }
}
