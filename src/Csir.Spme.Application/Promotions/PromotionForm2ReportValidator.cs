using System.Text.Json;
using Csir.Spme.Application.Promotions;

namespace Csir.Spme.Application.Promotions;

public static class PromotionForm2ReportValidator
{
    private static readonly HashSet<string> StaffLockedParticularsFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "currentGradeName", "instituteName", "dateOfBirth", "appointmentDate",
        "targetGradeName", "formCode", "part", "schemaVersion", "presentGradeStartSource"
    };

    public static Dictionary<string, string[]> Validate(
        string reportType,
        int schemaVersion,
        IReadOnlyList<PromotionReportSectionDto> sections)
    {
        if (schemaVersion == 1)
            return [];

        if (schemaVersion != 2)
        {
            return new Dictionary<string, string[]>
            {
                ["content.schemaVersion"] = ["Schema version must be 1 or 2."]
            };
        }

        if (sections.Count != 1)
        {
            return new Dictionary<string, string[]>
            {
                ["content.sections"] = ["Schema version 2 reports must contain exactly one structured section."]
            };
        }

        var section = sections[0];
        if (section.Content.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string[]>
            {
                ["content.sections[0].content"] = ["Section content must be a structured JSON object."]
            };
        }

        return reportType switch
        {
            "particulars" => ValidateParticulars(section.Content),
            "qualifications" => ValidateQualifications(section.Content),
            "training" => ValidateTraining(section.Content),
            "service-duties" => ValidateServiceDuties(section.Content),
            "hod-assessment" => ValidateHodAssessment(section.Content),
            "applicant-hod-response" => ValidateApplicantHodResponse(section.Content),
            "director-assessment" => ValidateDirectorAssessment(section.Content),
            _ => []
        };
    }

    public static Dictionary<string, string[]> ValidateStaffParticularsOverrides(
        JsonElement submitted,
        JsonElement? serverBaseline)
    {
        if (serverBaseline is null || serverBaseline.Value.ValueKind != JsonValueKind.Object)
            return [];

        var fields = new Dictionary<string, string[]>();
        foreach (var property in submitted.EnumerateObject())
        {
            if (!StaffLockedParticularsFields.Contains(property.Name))
                continue;

            if (!serverBaseline.Value.TryGetProperty(property.Name, out var baseline) ||
                !JsonElement.DeepEquals(property.Value, baseline))
            {
                fields[$"content.sections[0].content.{property.Name}"] =
                    ["This field is prefilled by HR and cannot be changed."];
            }
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateParticulars(JsonElement content)
    {
        var fields = RequireFormHeader(content, "personal-record");
        if (!content.TryGetProperty("station", out var station) || station.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(station.GetString()))
        {
            fields["content.sections[0].content.station"] = ["Station is required."];
        }

        if (content.TryGetProperty("lastPromotionDate", out var lastPromotion) &&
            lastPromotion.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
        {
            fields["content.sections[0].content.lastPromotionDate"] =
                ["Last promotion date must be null or an ISO date string."];
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateQualifications(JsonElement content)
    {
        var fields = RequireFormHeader(content, "qualifications");
        if (!content.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            fields["content.sections[0].content.rows"] = ["Qualification rows are required."];
            return fields;
        }

        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("dateObtained", out var date) ||
                date.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(date.GetString()))
            {
                fields[$"content.sections[0].content.rows[{index}].dateObtained"] =
                    ["Date obtained is required for each qualification row."];
            }

            index++;
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateTraining(JsonElement content)
    {
        var fields = RequireFormHeader(content, "training");
        if (!content.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            fields["content.sections[0].content.rows"] = ["Training rows are required."];
            return fields;
        }

        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("courseName", out var course) ||
                course.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(course.GetString()))
            {
                fields[$"content.sections[0].content.rows[{index}].courseName"] =
                    ["Course name is required for each training row."];
            }

            index++;
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateServiceDuties(JsonElement content)
    {
        var fields = RequireFormHeader(content, "service-duties");
        if (!content.TryGetProperty("presentDuties", out var duties) ||
            duties.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(duties.GetString()))
        {
            fields["content.sections[0].content.presentDuties"] = ["Present duties are required."];
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateHodAssessment(JsonElement content)
    {
        var fields = RequireFormHeader(content, "hod-assessment");
        RequireRecommendation(content, fields);
        return fields;
    }

    private static Dictionary<string, string[]> ValidateApplicantHodResponse(JsonElement content)
    {
        return RequireFormHeader(content, "applicant-hod-response");
    }

    private static Dictionary<string, string[]> ValidateDirectorAssessment(JsonElement content)
    {
        var fields = RequireFormHeader(content, "director-assessment");
        RequireRecommendation(content, fields);
        return fields;
    }

    private static Dictionary<string, string[]> RequireFormHeader(JsonElement content, string part)
    {
        var fields = new Dictionary<string, string[]>();
        if (!content.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != 2)
            fields["content.sections[0].content.schemaVersion"] = ["schemaVersion must be 2."];
        if (!content.TryGetProperty("formCode", out var formCode) ||
            !string.Equals(formCode.GetString(), "csir-form-2", StringComparison.Ordinal))
            fields["content.sections[0].content.formCode"] = ["formCode must be csir-form-2."];
        if (!content.TryGetProperty("part", out var partElement) ||
            !string.Equals(partElement.GetString(), part, StringComparison.Ordinal))
            fields["content.sections[0].content.part"] = [$"part must be {part}."];
        return fields;
    }

    private static void RequireRecommendation(JsonElement content, Dictionary<string, string[]> fields)
    {
        if (!content.TryGetProperty("recommendation", out var recommendation) ||
            recommendation.ValueKind != JsonValueKind.String ||
            recommendation.GetString() is not ("recommended" or "not-recommended"))
        {
            fields["content.sections[0].content.recommendation"] =
                ["Recommendation must be recommended or not-recommended."];
        }
    }
}
