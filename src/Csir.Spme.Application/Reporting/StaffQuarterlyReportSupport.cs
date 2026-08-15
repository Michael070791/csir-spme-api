using System.Text.RegularExpressions;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Reporting;

internal static partial class StaffQuarterlyReportSupport
{
    private static readonly string[] ConceptNoteContentTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];

    private static readonly string[] ImageContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public static bool IsConceptNoteContentType(string contentType) =>
        ConceptNoteContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static bool IsImageContentType(string contentType) =>
        ImageContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static bool FileNameMatchesContentType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => extension == ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => extension == ".docx",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "image/webp" => extension == ".webp",
            _ => false
        };
    }

    public static bool SignatureMatches(string contentType, ReadOnlySpan<byte> signature) =>
        contentType.ToLowerInvariant() switch
        {
            "application/pdf" => signature.Length >= 4 && signature[0] == 0x25 && signature[1] == 0x50 &&
                                 signature[2] == 0x44 && signature[3] == 0x46,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                signature.Length >= 2 && signature[0] == 0x50 && signature[1] == 0x4B,
            "image/jpeg" => signature.Length >= 3 && signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF,
            "image/png" => signature.Length >= 4 && signature[0] == 0x89 && signature[1] == 0x50 &&
                           signature[2] == 0x4E && signature[3] == 0x47,
            "image/webp" => signature.Length >= 12 && signature[0] == 0x52 && signature[1] == 0x49 &&
                            signature[2] == 0x46 && signature[3] == 0x46 && signature[8] == 0x57 &&
                            signature[9] == 0x45 && signature[10] == 0x42 && signature[11] == 0x50,
            _ => false
        };

    public static Dictionary<string, string[]> ValidateInception(SaveStaffQuarterlyProjectInceptionCommand command)
    {
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64)
            fields["code"] = ["A code of at most 64 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 256)
            fields["name"] = ["A name of at most 256 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Objective) || command.Objective.Length > 4000)
            fields["objective"] = ["An objective of at most 4000 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Length > 4000)
            fields["justification"] = ["Background and justification of at most 4000 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Method) || command.Method.Length > 4000)
            fields["method"] = ["A method of at most 4000 characters is required."];
        if (!DomainValues.Contains(ProjectNatures.All, command.Nature))
            fields["nature"] = [$"Nature must be one of: {string.Join(", ", ProjectNatures.All)}."];
        if (command.EndDate.HasValue && command.EndDate.Value.Date < command.StartDate.Date)
            fields["endDate"] = ["End date cannot precede start date."];
        if (command.BudgetAmount < 0m)
            fields["budgetAmount"] = ["The budget amount cannot be negative."];
        var currency = command.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3 || currency.Any(ch => ch is < 'A' or > 'Z'))
            fields["currency"] = ["The currency must be a three-letter code."];
        if (command.LeadEmployeeId == Guid.Empty)
            fields["leadEmployeeId"] = ["A principal investigator is required."];
        if (string.IsNullOrWhiteSpace(command.EstimatedDuration) || command.EstimatedDuration.Length > 128)
            fields["estimatedDuration"] = ["Estimated duration of at most 128 characters is required."];
        if (string.IsNullOrWhiteSpace(command.SponsorName) || command.SponsorName.Length > 256)
            fields["sponsorName"] = ["Sponsors of at most 256 characters are required."];
        if (string.IsNullOrWhiteSpace(command.Location) || command.Location.Length > 256)
            fields["location"] = ["Location of at most 256 characters is required."];
        if (command.CollaboratingInstitute?.Length > 512)
            fields["collaboratingInstitute"] = ["Collaborating institute cannot exceed 512 characters."];
        if (command.ParticipatingScientists?.Length > 4000)
            fields["participatingScientists"] = ["Participating scientists cannot exceed 4000 characters."];
        if (command.ExpectedBeneficiaries?.Length > 4000)
            fields["expectedBeneficiaries"] = ["Expected beneficiaries cannot exceed 4000 characters."];
        if (command.PotentialTechnology?.Length > 4000)
            fields["potentialTechnology"] = ["Potential technology cannot exceed 4000 characters."];
        if (command.ContributionToKnowledge?.Length > 4000)
            fields["contributionToKnowledge"] = ["Contribution to knowledge cannot exceed 4000 characters."];
        return fields;
    }

    public static string DisplayName(Employee employee) =>
        !string.IsNullOrWhiteSpace(employee.PreferredName) ? employee.PreferredName :
        string.Join(' ', new[] { employee.OtherNames, employee.Surname }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static StaffQuarterlyFileMetadata MapFile(FileRecord file) =>
        new(file.Id, file.OriginalFileName, file.ContentType, file.SizeBytes, file.ScanStatus);

    public static bool IsSha256(string value) => Sha256Regex().IsMatch(value);

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256Regex();
}
