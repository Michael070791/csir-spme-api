using Csir.Spme.Domain.Common;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class EndpointProblems
{
    public static ProblemHttpResult FromError(Error error) => error.Type switch
    {
        "not-found" => Create(StatusCodes.Status404NotFound, error),
        "validation" => Create(StatusCodes.Status422UnprocessableEntity, error),
        "forbidden" => Create(StatusCodes.Status403Forbidden, error),
        "conflict" => Create(StatusCodes.Status409Conflict, error),
        "state-transition" => Create(StatusCodes.Status409Conflict, error),
        "precondition-failed" => Create(StatusCodes.Status412PreconditionFailed, error),
        "dependency-unavailable" => Create(StatusCodes.Status503ServiceUnavailable, error),
        _ => Create(StatusCodes.Status500InternalServerError, error)
    };

    public static ProblemHttpResult Unauthorized() => TypedResults.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid credentials.",
        type: "https://api.csir.example/problems/unauthenticated",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "unauthenticated",
            ["errorCode"] = "unauthorized"
        });

    public static ProblemHttpResult PasswordResetRequired(string? email) => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Password reset required.",
        detail: "Reset your password before signing in.",
        type: "https://api.csir.example/problems/password-reset-required",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "password_reset_required",
            ["errorCode"] = "password_reset_required",
            ["email"] = email,
        });

    public static ProblemHttpResult LoginLocked() => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Logins are temporarily locked.",
        detail: "Only platform administrators can sign in while the login lock is active.",
        type: "https://api.csir.example/problems/login-locked",
        extensions: new Dictionary<string, object?> { ["code"] = "login_locked", ["errorCode"] = "login_locked" });

    public static ProblemHttpResult Unprocessable(string message) => TypedResults.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: message,
        type: "https://api.csir.example/problems/validation-failed",
        extensions: new Dictionary<string, object?> { ["code"] = "validation_failed", ["errorCode"] = "validation_failed" });

    public static ProblemHttpResult PayloadTooLarge(string message) => TypedResults.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "The request is too large.",
        detail: message,
        type: "https://api.csir.example/problems/payload-too-large",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "payload_too_large",
            ["errorCode"] = "payload_too_large"
        });

    public static ProblemHttpResult UnsupportedMediaType(string message) => TypedResults.Problem(
        statusCode: StatusCodes.Status415UnsupportedMediaType,
        title: "The media type is unsupported.",
        detail: message,
        type: "https://api.csir.example/problems/unsupported-media-type",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "unsupported_media_type",
            ["errorCode"] = "unsupported_media_type"
        });

    private static ProblemHttpResult Create(int statusCode, Error error)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = error.Code,
            ["errorCode"] = error.Code
        };
        if (error.Fields is not null)
        {
            extensions["errors"] = error.Fields;
        }

        return TypedResults.Problem(
            statusCode: statusCode,
            title: Title(error.Code),
            detail: error.Message,
            type: $"https://api.csir.example/problems/{error.Code.Replace('_', '-')}",
            extensions: extensions);
    }

    private static string Title(string code) => code switch
    {
        "validation_failed" => "One or more fields are invalid.",
        "not_found" => "The requested resource was not found.",
        "forbidden" or "cross_institute_access_denied" => "Access is forbidden.",
        "conflict" => "The request conflicts with the current resource state.",
        "invalid_state_transition" => "The requested state transition is invalid.",
        "concurrency_conflict" => "The resource has changed.",
        "dependency_unavailable" => "A required dependency is temporarily unavailable.",
        _ => "The request could not be completed."
    };
}
