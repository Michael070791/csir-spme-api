using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Common;

public class Result<TValue>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public TValue? Value { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, TValue? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<TValue> Success(TValue value) => new(true, value, null);
    public static Result<TValue> Failure(Error error) => new(false, default, error);

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

public static class Result
{
    public static Result<bool> Success() => Result<bool>.Success(true);
    public static Result<bool> Failure(Error error) => Result<bool>.Failure(error);
}

public class Error
{
    public string Code { get; }
    public string Message { get; }
    public string Type { get; }
    public IReadOnlyDictionary<string, string[]>? Fields { get; }

    private Error(string code, string message, string type, IReadOnlyDictionary<string, string[]>? fields = null)
    {
        Code = code; Message = message; Type = type; Fields = fields;
    }

    public static Error Validation(string message) => new(SpmeErrorCodes.ValidationFailed, message, "validation");

    public static Error Validation(IReadOnlyDictionary<string, string[]> fields) =>
        new(SpmeErrorCodes.ValidationFailed, "One or more fields are invalid.", "validation", fields);

    public static Error NotFound(string message) => new(SpmeErrorCodes.NotFound, message, "not-found");
    public static Error Forbidden(string message) => new(SpmeErrorCodes.Forbidden, message, "forbidden");
    public static Error CrossInstitute(string message) => new(SpmeErrorCodes.CrossInstituteAccessDenied, message, "forbidden");
    public static Error Conflict(string message) => new(SpmeErrorCodes.Conflict, message, "conflict");
    public static Error PreconditionFailed(string message) => new(SpmeErrorCodes.ConcurrencyConflict, message, "precondition-failed");
    public static Error StateTransition(string message) => new(SpmeErrorCodes.InvalidStateTransition, message, "state-transition");
    public static Error InsufficientLeaveBalance(string message) => new(SpmeErrorCodes.InsufficientLeaveBalance, message, "conflict");
    public static Error DependencyUnavailable(string message) => new(SpmeErrorCodes.DependencyUnavailable, message, "dependency-unavailable");
}
