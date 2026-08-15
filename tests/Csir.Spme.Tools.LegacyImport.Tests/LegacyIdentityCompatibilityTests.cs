using Csir.Spme.Domain.Iam;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Csir.Spme.Tools.LegacyImport.Tests;

public sealed class LegacyIdentityCompatibilityTests
{
    [Fact]
    public void Compatible_Identity_V3_Hash_Is_Preserved_And_Verifiable()
    {
        var hasher = new PasswordHasher<User>();
        var sourceUser = new User("source", "StaffUser");
        var hash = hasher.HashPassword(sourceUser, "Legacy-Passw0rd!");
        var target = new User("target", "StaffUser");

        var imported = target.ImportCompatibleLegacyCredentials(
            hash,
            emailConfirmed: true,
            phoneNumberConfirmed: false,
            lockoutEnabled: true,
            lockoutEnd: null,
            accessFailedCount: 0);

        imported.Should().BeTrue();
        target.AccountStatus.Should().Be("active");
        hasher.VerifyHashedPassword(target, target.PasswordHash!, "Legacy-Passw0rd!")
            .Should().NotBe(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void Unknown_Hash_Format_Requires_Reset()
    {
        var target = new User("target", "StaffUser");

        target.ImportCompatibleLegacyCredentials(
                "not-an-identity-v3-hash",
                false,
                false,
                true,
                null,
                0)
            .Should().BeFalse();
        target.AccountStatus.Should().Be("password-reset-required");
    }
}
