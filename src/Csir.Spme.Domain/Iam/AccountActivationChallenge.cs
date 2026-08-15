using Csir.Spme.Domain.Common;

using System.Security.Cryptography;

namespace Csir.Spme.Domain.Iam;

public sealed class AccountActivationChallenge : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string RequestedIdentifierHash { get; private set; } = string.Empty;
    public string DeliveryChannel { get; private set; } = string.Empty;
    public string DestinationHash { get; private set; } = string.Empty;
    public string OtpHash { get; private set; } = string.Empty;
    public string? VerificationTokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaximumAttempts { get; private set; }

    private AccountActivationChallenge() { }

    public AccountActivationChallenge(
        Guid? userId,
        string requestedIdentifierHash,
        string deliveryChannel,
        string destinationHash,
        string otpHash,
        DateTimeOffset expiresAt,
        int maximumAttempts)
    {
        UserId = userId;
        RequestedIdentifierHash = requestedIdentifierHash;
        DeliveryChannel = deliveryChannel;
        DestinationHash = destinationHash;
        OtpHash = otpHash;
        ExpiresAt = expiresAt;
        MaximumAttempts = maximumAttempts;
    }

    public bool CanVerify(DateTimeOffset now) =>
        ConsumedAt is null && VerifiedAt is null && now < ExpiresAt && AttemptCount < MaximumAttempts;

    public void RecordFailedAttempt() => AttemptCount++;

    public void Verify(string verificationTokenHash, DateTimeOffset verifiedAt)
    {
        VerificationTokenHash = verificationTokenHash;
        VerifiedAt = verifiedAt;
    }

    public bool CanComplete(string verificationTokenHash, DateTimeOffset now) =>
        UserId.HasValue && ConsumedAt is null && VerifiedAt.HasValue && now < ExpiresAt &&
        VerificationTokenHash is not null && FixedTimeEquals(VerificationTokenHash, verificationTokenHash);

    public void Consume(DateTimeOffset consumedAt) => ConsumedAt = consumedAt;

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(left),
            System.Text.Encoding.ASCII.GetBytes(right));
    }
}
