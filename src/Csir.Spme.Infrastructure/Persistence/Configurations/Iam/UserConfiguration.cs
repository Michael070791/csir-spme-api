using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Csir.Spme.Domain.Iam;

namespace Csir.Spme.Infrastructure.Persistence.Configurations.Iam;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
        builder.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PendingEmail).HasMaxLength(320);
        builder.Property(x => x.AccountStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdentityType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.InstituteId);
        builder.HasIndex(x => x.NormalizedUserName).IsUnique().HasFilter("[NormalizedUserName] IS NOT NULL");
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        builder.HasIndex(x => x.PendingEmail).IsUnique().HasFilter("[PendingEmail] IS NOT NULL");
        builder.HasIndex(x => new { x.InstituteId, x.AccountStatus });
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SecurityStamp).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
        builder.HasIndex(x => new { x.FamilyId, x.RevokedAt });
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceName).HasMaxLength(128);
        builder.Property(x => x.Platform).HasMaxLength(32);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.RevokedAt, x.LastSeenAt });
    }
}

public sealed class UserLoginIdentifierConfiguration : IEntityTypeConfiguration<UserLoginIdentifier>
{
    public void Configure(EntityTypeBuilder<UserLoginIdentifier> builder)
    {
        builder.ToTable("UserLoginIdentifiers", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdentifierType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.NormalizedValue).HasMaxLength(320).IsRequired();
        builder.Property(x => x.VerificationSource).HasMaxLength(64).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.IdentifierType, x.NormalizedValue })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasIndex(x => new { x.EmployeeId, x.IsActive });
    }
}

public sealed class AccountActivationChallengeConfiguration : IEntityTypeConfiguration<AccountActivationChallenge>
{
    public void Configure(EntityTypeBuilder<AccountActivationChallenge> builder)
    {
        builder.ToTable("AccountActivationChallenges", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedIdentifierHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeliveryChannel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.DestinationHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OtpHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.VerificationTokenHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.RequestedIdentifierHash, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });
    }
}

public sealed class VerificationChallengeConfiguration : IEntityTypeConfiguration<VerificationChallenge>
{
    public void Configure(EntityTypeBuilder<VerificationChallenge> builder)
    {
        builder.ToTable("VerificationChallenges", "iam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.DestinationHash).HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.CodeHash).HasColumnType("char(64)").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAt });
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.Channel, x.ConsumedAt });
    }
}

public sealed class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.ToTable("PasswordResetRequests", "iam");
        builder.HasKey(x => x.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<VerificationChallenge>().WithMany()
            .HasForeignKey(x => x.VerificationChallengeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.VerificationChallengeId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.CompletedAt, x.SupersededAt });
        builder.HasIndex(x => x.UserId).IsUnique()
            .HasFilter("[CompletedAt] IS NULL AND [SupersededAt] IS NULL");
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "iam");
        builder.HasKey(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
