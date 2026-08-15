namespace Csir.Spme.Application.Common;

/// <summary>
/// Raised by the persistence layer when a save fails the optimistic concurrency check.
/// Application services translate it into a typed precondition-failed result.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
