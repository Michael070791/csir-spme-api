namespace Csir.Spme.Application.Iam;

public interface IPasswordResetService
{
    Task RequestAsync(string email, CancellationToken ct = default);

    Task<PasswordResetConfirmationResult> ConfirmAsync(
        Guid requestId,
        string token,
        string newPassword,
        string confirmNewPassword,
        CancellationToken ct = default);
}

public sealed record PasswordResetConfirmationResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static PasswordResetConfirmationResult Success() => new(true);

    public static PasswordResetConfirmationResult Failure(
        string errorCode,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, errorCode, detail, errors);
}
