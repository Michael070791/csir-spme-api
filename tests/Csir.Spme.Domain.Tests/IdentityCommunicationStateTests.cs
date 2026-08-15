using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Iam;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Domain.Tests;

public sealed class IdentityCommunicationStateTests
{
    [Fact]
    public void Activation_Challenge_Is_Single_Use_And_Stops_After_Maximum_Attempts()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new AccountActivationChallenge(
            Guid.NewGuid(), "requested", "email", "destination", "otp", now.AddMinutes(10), 3);

        challenge.RecordFailedAttempt();
        challenge.RecordFailedAttempt();
        challenge.CanVerify(now).Should().BeTrue();
        challenge.RecordFailedAttempt();
        challenge.CanVerify(now).Should().BeFalse();

        var verified = new AccountActivationChallenge(
            Guid.NewGuid(), "requested", "email", "destination", "otp", now.AddMinutes(10), 3);
        verified.Verify("ABCDEF", now);
        verified.CanComplete("ABCDEF", now).Should().BeTrue();
        verified.CanComplete("ABCDE0", now).Should().BeFalse();
        verified.Consume(now);
        verified.CanComplete("ABCDEF", now).Should().BeFalse();
    }

    [Fact]
    public void Refresh_Token_Rotation_And_Revocation_Are_Idempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.NewGuid(), "hash", Guid.NewGuid(), Guid.NewGuid(), "security-stamp", now.AddDays(7));
        var replacementId = Guid.NewGuid();

        token.IsActive(now).Should().BeTrue();
        token.Rotate(replacementId, now);
        token.IsActive(now).Should().BeFalse();
        token.ReplacedByTokenId.Should().Be(replacementId);
        token.RevocationReason.Should().Be("rotated");
        token.Revoke("later-reason", now.AddMinutes(1));
        token.RevocationReason.Should().Be("rotated");
    }

    [Fact]
    public void Communication_Outbox_Tracks_Lease_Retry_And_Delivery()
    {
        var message = new CommunicationOutboxMessage(
            "email", "recipient@csir.test", "Subject", "Body", false, "notification", "event:1");

        message.Lease(DateTimeOffset.UtcNow.AddMinutes(1));
        message.Status.Should().Be("processing");
        message.AttemptCount.Should().Be(1);

        message.Retry("provider_unavailable", DateTimeOffset.UtcNow.AddMinutes(2));
        message.Status.Should().Be("queued");
        message.LastErrorCode.Should().Be("provider_unavailable");

        message.Lease(DateTimeOffset.UtcNow.AddMinutes(1));
        message.MarkDelivered("provider-123", DateTimeOffset.UtcNow);
        message.Status.Should().Be("delivered");
        message.ProviderMessageId.Should().Be("provider-123");
        message.LastErrorCode.Should().BeNull();
    }

    [Fact]
    public void Password_Reset_Request_And_Challenge_Are_One_Time()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var challenge = new VerificationChallenge(
            userId, null, "password-reset", "email", new string('a', 64), new string('b', 64),
            now.AddHours(24), now);
        var request = new PasswordResetRequest(Guid.NewGuid(), userId, challenge.Id, now);

        challenge.IsActive(now.AddHours(23)).Should().BeTrue();
        challenge.VerifyAndConsume(now.AddMinutes(1));
        challenge.IsActive(now.AddMinutes(2)).Should().BeFalse();
        request.Complete(now.AddMinutes(1));
        request.IsActive.Should().BeFalse();
        var replay = () => request.Complete(now.AddMinutes(2));
        replay.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Password_Reset_Challenge_Locks_After_Maximum_Failed_Attempts()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new VerificationChallenge(
            Guid.NewGuid(), null, "password-reset", "email", new string('a', 64), new string('b', 64),
            now.AddHours(24), now);

        for (var attempt = 0; attempt < 4; attempt++)
            challenge.RecordFailedAttempt(now, 5);

        challenge.IsActive(now).Should().BeTrue();
        challenge.RecordFailedAttempt(now, 5);
        challenge.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Unlink_Employee_Clears_Employee_Id_Without_Changing_Identity_Type()
    {
        var instituteId = Guid.NewGuid();
        var user = new User("staff@csir.local", "Employee");
        user.LinkEmployee(Guid.NewGuid(), instituteId);

        user.UnlinkEmployee();

        user.EmployeeId.Should().BeNull();
        user.IdentityType.Should().Be("Employee");
        user.InstituteId.Should().Be(instituteId);
    }
}
