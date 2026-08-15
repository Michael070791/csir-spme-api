namespace Csir.Spme.Application.Iam;

public interface IAccountActivationService
{
    Task<AccountActivationResult<AccountActivationChallengeData>> CreateChallengeAsync(
        string identifier,
        string? contact,
        CancellationToken cancellationToken);

    Task<AccountActivationResult<AccountActivationVerificationData>> VerifyChallengeAsync(
        Guid challengeId,
        string code,
        CancellationToken cancellationToken);

    Task<AccountActivationResult> CompleteAsync(
        Guid challengeId,
        string verificationToken,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken);
}

public sealed record AccountActivationChallengeData(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    string DeliveryChannel,
    string MaskedDestination);

public sealed record AccountActivationVerificationData(
    string VerificationToken,
    DateTimeOffset ExpiresAt);

public record AccountActivationResult(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? Detail,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AccountActivationResult Success() => new(true, 204, null, null);

    public static AccountActivationResult Failure(
        int statusCode,
        string errorCode,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, statusCode, errorCode, detail, errors);
}

public sealed record AccountActivationResult<T>(
    bool Succeeded,
    int StatusCode,
    T? Value,
    string? ErrorCode,
    string? Detail,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static AccountActivationResult<T> Success(int statusCode, T value) =>
        new(true, statusCode, value, null, null);

    public static AccountActivationResult<T> Failure(
        int statusCode,
        string errorCode,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, statusCode, default, errorCode, detail, errors);
}
