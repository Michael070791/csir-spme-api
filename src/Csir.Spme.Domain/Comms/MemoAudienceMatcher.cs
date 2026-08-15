using Csir.Spme.Domain.Constants;

namespace Csir.Spme.Domain.Comms;

public static class MemoAudienceMatcher
{
    public const int SmsSynopsisMaxLength = 140;
    public const int InAppBodyMaxLength = 400;
    public const int EmailBodyMaxLength = 8000;

    public static bool Matches(
        IEnumerable<MemoAudience> audiences,
        Guid employeeId,
        Guid employeeInstituteId,
        Guid? divisionId,
        Guid? sectionId,
        IReadOnlySet<string> roleNames)
    {
        return audiences.Any(audience => audience.AudienceType switch
        {
            MemoAudienceTypes.AllEmployees => !audience.InstituteId.HasValue || audience.InstituteId == employeeInstituteId,
            MemoAudienceTypes.Institute => audience.InstituteId == employeeInstituteId,
            MemoAudienceTypes.Division => audience.DivisionId.HasValue && audience.DivisionId == divisionId,
            MemoAudienceTypes.Section => audience.SectionId.HasValue && audience.SectionId == sectionId,
            MemoAudienceTypes.Employee => audience.EmployeeId == employeeId,
            MemoAudienceTypes.Role => audience.RoleCode is not null &&
                roleNames.Contains(audience.RoleCode),
            _ => false
        });
    }

    public static string SmsSynopsis(string title, string body)
    {
        var headline = Compact(title);
        var remainder = Compact(body);
        var text = string.IsNullOrWhiteSpace(remainder) ? headline : $"{headline}: {remainder}";
        return Truncate(text, SmsSynopsisMaxLength);
    }

    public static string InAppBody(string body) => Truncate(Compact(body), InAppBodyMaxLength);

    public static string EmailBody(string body) => Truncate(body.Trim(), EmailBodyMaxLength);

    private static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        if (maxLength <= 3)
            return value[..maxLength];
        return value[..(maxLength - 3)] + "...";
    }
}
