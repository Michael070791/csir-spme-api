using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Org;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Org;

public class InstituteConfiguration : IEntityTypeConfiguration<Institute>
{
    public void Configure(EntityTypeBuilder<Institute> builder)
    {
        builder.ToTable("Institutes", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.EmailDomain).HasMaxLength(256);
        builder.Property(x => x.Address).HasMaxLength(512);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

public class InstituteAliasConfiguration : IEntityTypeConfiguration<InstituteAlias>
{
    public void Configure(EntityTypeBuilder<InstituteAlias> builder)
    {
        builder.ToTable("InstituteAliases", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Alias).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedAlias).HasMaxLength(256).IsRequired();
        builder.HasOne<Institute>().WithMany().HasForeignKey(x => x.InstituteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.NormalizedAlias).IsUnique();
        builder.HasIndex(x => x.InstituteId);
    }
}

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("Divisions", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.InstituteId, x.IsActive });
    }
}

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.DivisionId, x.Name }).IsUnique();
        builder.HasOne<Division>().WithMany().HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.DivisionId);
    }
}

public class PositionTypeConfiguration : IEntityTypeConfiguration<PositionType>
{
    public void Configure(EntityTypeBuilder<PositionType> builder)
    {
        builder.ToTable("PositionTypes", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades", "org");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.StaffCategory).HasMaxLength(64);
        builder.Property(x => x.PromotionStream).HasMaxLength(32);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
