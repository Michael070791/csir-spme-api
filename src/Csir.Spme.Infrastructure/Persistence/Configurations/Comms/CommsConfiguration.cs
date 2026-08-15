using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Comms;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Comms;

public class MemoConfiguration : IEntityTypeConfiguration<Memo>
{
    public void Configure(EntityTypeBuilder<Memo> builder) {
        builder.ToTable("Memos", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.Status, x.PublishedAt });
    }
}

public class MemoAudienceConfiguration : IEntityTypeConfiguration<MemoAudience>
{
    public void Configure(EntityTypeBuilder<MemoAudience> builder)
    {
        builder.ToTable("MemoAudiences", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AudienceType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RoleCode).HasMaxLength(64);
        builder.HasOne<Memo>().WithMany().HasForeignKey(x => x.MemoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MemoId, x.AudienceType });
        builder.HasIndex(x => x.InstituteId);
        builder.HasIndex(x => x.DivisionId);
        builder.HasIndex(x => x.SectionId);
        builder.HasIndex(x => x.EmployeeId);
    }
}

public class MemoAcknowledgementConfiguration : IEntityTypeConfiguration<MemoAcknowledgement>
{
    public void Configure(EntityTypeBuilder<MemoAcknowledgement> builder)
    {
        builder.ToTable("MemoAcknowledgements", "comms");
        builder.HasKey(x => x.Id);
        builder.HasOne<Memo>().WithMany().HasForeignKey(x => x.MemoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MemoId, x.EmployeeId }).IsUnique();
        builder.HasIndex(x => x.EmployeeId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder) {
        builder.ToTable("Notifications", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ActionLink).HasMaxLength(1024);
        builder.Property(x => x.Channel).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.RecipientUserId, x.IsRead, x.CreatedAt });
    }
}

public class FaqConfiguration : IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Faq> builder) {
        builder.ToTable("Faqs", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Question).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Answer).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.InstituteId, x.DisplayOrder });
    }
}

public sealed class CommunicationOutboxMessageConfiguration : IEntityTypeConfiguration<CommunicationOutboxMessage>
{
    public void Configure(EntityTypeBuilder<CommunicationOutboxMessage> builder)
    {
        builder.ToTable("CommunicationOutboxMessages", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(512);
        builder.Property(x => x.Body).HasMaxLength(16000).IsRequired();
        builder.Property(x => x.TextBody).HasMaxLength(8000);
        builder.Property(x => x.AttachmentsJson);
        builder.Property(x => x.Category).HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
        builder.Property(x => x.LastErrorCode).HasMaxLength(128);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}

public sealed class CommunicationDeliveryAttemptConfiguration : IEntityTypeConfiguration<CommunicationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<CommunicationDeliveryAttempt> builder)
    {
        builder.ToTable("CommunicationDeliveryAttempts", "comms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
        builder.Property(x => x.ErrorCode).HasMaxLength(128);
        builder.HasOne<CommunicationOutboxMessage>().WithMany().HasForeignKey(x => x.OutboxMessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OutboxMessageId, x.AttemptNumber }).IsUnique();
    }
}
