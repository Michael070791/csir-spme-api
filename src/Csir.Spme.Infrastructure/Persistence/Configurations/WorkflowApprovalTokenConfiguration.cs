using Csir.Spme.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Csir.Spme.Infrastructure.Persistence.Configurations;

public sealed class WorkflowApprovalTokenConfiguration : IEntityTypeConfiguration<WorkflowApprovalToken>
{
    public void Configure(EntityTypeBuilder<WorkflowApprovalToken> builder)
    {
        builder.ToTable("WorkflowApprovalTokens", "leave");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Purpose)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ResourceId)
            .IsRequired();

        builder.Property(x => x.ApproverUserId)
            .IsRequired();

        builder.Property(x => x.Stage)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.ResourceId, x.Purpose, x.Stage, x.ExpiresAt });
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
    }
}
