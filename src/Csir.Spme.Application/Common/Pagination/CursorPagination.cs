using System.Security.Cryptography;
using System.Text;
using Csir.Spme.Domain.Common;

namespace Csir.Spme.Application.Common.Pagination;

/// <summary>Configuration for list limits and cursor signing.</summary>
public sealed class PaginationOptions
{
    public const string SectionName = "Pagination";

    /// <summary>Default list limit. Baseline default 50.</summary>
    public int DefaultLimit { get; set; } = 50;

    /// <summary>Maximum list limit. Baseline default 100.</summary>
    public int MaxLimit { get; set; } = 100;

    /// <summary>HMAC key used to sign cursors. Defaults to the JWT key when unset.</summary>
    public string? CursorSigningKey { get; set; }
}

/// <summary>Position inside a keyset-paginated list.</summary>
public sealed record CursorPosition(string SortValue, Guid Id);

/// <summary>One page slice of a list with an optional continuation position.</summary>
public sealed record ListSlice<T>(IReadOnlyList<T> Items, CursorPosition? Next);

/// <summary>Validated list paging parameters.</summary>
public sealed record PageRequest(int Limit, CursorPosition? After);

public interface ICursorCodec
{
    string Encode(CursorPosition position);
    Result<CursorPosition> Decode(string cursor);
}

/// <summary>HMAC-SHA256 signed opaque cursor. Cursors are never offsets.</summary>
public sealed class HmacCursorCodec : ICursorCodec
{
    private readonly byte[] _key;

    public HmacCursorCodec(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new ArgumentException("A cursor signing key is required.", nameof(signingKey));
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
    }

    public string Encode(CursorPosition position)
    {
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes($"{position.SortValue}|{position.Id:N}"));
        var signature = Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(payload)));
        return $"{payload}.{signature}";
    }

    public Result<CursorPosition> Decode(string cursor)
    {
        var parts = cursor.Split('.', 2);
        if (parts.Length != 2)
        {
            return Result<CursorPosition>.Failure(Error.Validation("The cursor is malformed."));
        }

        var expected = Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(parts[0])));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[1])))
        {
            return Result<CursorPosition>.Failure(Error.Validation("The cursor is invalid."));
        }

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        }
        catch (FormatException)
        {
            return Result<CursorPosition>.Failure(Error.Validation("The cursor is malformed."));
        }

        var separator = payload.LastIndexOf('|');
        if (separator <= 0 || !Guid.TryParseExact(payload[(separator + 1)..], "N", out var id))
        {
            return Result<CursorPosition>.Failure(Error.Validation("The cursor is malformed."));
        }

        return Result<CursorPosition>.Success(new CursorPosition(payload[..separator], id));
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
