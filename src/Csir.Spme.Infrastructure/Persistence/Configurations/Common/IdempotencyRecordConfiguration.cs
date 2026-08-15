using Csir.Spme.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Common;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords", "ops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ResponseContentType).HasMaxLength(128);
        builder.Property(x => x.ResponseEtag).HasMaxLength(256);
        builder.Property(x => x.ResponseLocation).HasMaxLength(2048);
        builder.HasIndex(x => new { x.Scope, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
    }
}
