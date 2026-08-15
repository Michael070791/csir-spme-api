using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Common;

namespace Csir.Spme.Application.Common;

/// <summary>Validates limit/cursor/sort/direction list parameters against a whitelist.</summary>
public static class ListQueryParser
{
    public static Result<KeysetPage> Parse(
        ICursorCodec cursorCodec,
        int defaultLimit,
        int maxLimit,
        int? limit,
        string? cursor,
        string? sort,
        string? direction,
        string defaultSort,
        bool defaultDescending,
        string[] allowedSorts)
    {
        var effectiveLimit = limit ?? defaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > maxLimit)
        {
            return Result<KeysetPage>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["limit"] = [$"The limit must be between 1 and {maxLimit}."]
            }));
        }

        var effectiveSort = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort.Trim();
        if (!allowedSorts.Contains(effectiveSort, StringComparer.Ordinal))
        {
            return Result<KeysetPage>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["sort"] = [$"The sort field '{effectiveSort}' is not supported. Allowed values: {string.Join(", ", allowedSorts)}."]
            }));
        }

        var descending = defaultDescending;
        if (!string.IsNullOrWhiteSpace(direction))
        {
            if (string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase))
            {
                descending = false;
            }
            else if (string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase))
            {
                descending = true;
            }
            else
            {
                return Result<KeysetPage>.Failure(Error.Validation(new Dictionary<string, string[]>
                {
                    ["direction"] = ["The direction must be 'asc' or 'desc'."]
                }));
            }
        }

        CursorPosition? after = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var decoded = cursorCodec.Decode(cursor.Trim());
            if (decoded.IsFailure)
            {
                return Result<KeysetPage>.Failure(decoded.Error!);
            }

            after = decoded.Value;
        }

        return Result<KeysetPage>.Success(new KeysetPage(effectiveSort, descending, after, effectiveLimit));
    }
}
