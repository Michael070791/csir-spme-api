using System.Net.Mail;
using System.Text;

namespace Csir.Spme.Infrastructure.Communications;

public static class LoginIdentifierNormalizer
{
    public static (string Type, string Value)? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            try
            {
                var email = new MailAddress(trimmed).Address;
                return ("email", email.ToUpperInvariant());
            }
            catch (FormatException)
            {
                return null;
            }
        }

        var phone = NormalizeGhanaPhone(trimmed);
        if (phone is not null)
            return ("phone", phone);

        var staffId = StripLegacyCsirPrefix(
            new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray())
                .ToUpperInvariant());
        return staffId.Length is > 0 and <= 64 ? ("staff-id", staffId) : null;
    }

    private static string StripLegacyCsirPrefix(string staffId)
    {
        if (staffId.StartsWith("CSIR-", StringComparison.Ordinal))
            return staffId["CSIR-".Length..];
        if (staffId.StartsWith("CSIR", StringComparison.Ordinal))
            return staffId["CSIR".Length..];
        return staffId;
    }

    public static string? NormalizeGhanaPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.Any(character => !char.IsAsciiDigit(character) &&
            character is not '+' and not '-' and not '(' and not ')' && !char.IsWhiteSpace(character)))
            return null;

        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
                builder.Append(character);
        }

        var digits = builder.ToString();
        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 10)
            digits = "233" + digits[1..];
        if (digits.Length == 9)
            digits = "233" + digits;

        return digits.Length == 12 && digits.StartsWith("233", StringComparison.Ordinal)
            ? digits
            : null;
    }
}
