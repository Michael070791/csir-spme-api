namespace Csir.Spme.Domain.Constants;

/// <summary>Stable, lowercase error codes emitted in <c>ProblemDetails.code</c>.</summary>
public static class SpmeErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string MalformedRequest = "malformed_request";
    public const string Unauthenticated = "unauthenticated";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string IdempotencyKeyReused = "idempotency_key_reused";
    public const string RateLimited = "rate_limited";
    public const string PayloadTooLarge = "payload_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string InvalidStateTransition = "invalid_state_transition";
    public const string InsufficientLeaveBalance = "insufficient_leave_balance";
    public const string CrossInstituteAccessDenied = "cross_institute_access_denied";
    public const string DependencyUnavailable = "dependency_unavailable";
    public const string InternalError = "internal_error";

    /// <summary>Stable public documentation URL prefix for problem types.</summary>
    public const string ProblemTypePrefix = "https://api.csir.example/problems/";
}
