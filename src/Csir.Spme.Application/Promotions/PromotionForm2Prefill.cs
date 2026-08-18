using System.Text.Json;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;

namespace Csir.Spme.Application.Promotions;

public static class PromotionForm2Prefill
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string BuildParticularsContent(
        Employee employee,
        EmploymentRecord employment,
        Grade sourceGrade,
        Grade? targetGrade,
        Institute institute,
        DateTime? recordedLastPromotionDate,
        string presentGradeStartSource)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["formCode"] = "csir-form-2",
            ["part"] = "personal-record",
            ["name"] = FormatEmployeeName(employee),
            ["currentGradeName"] = sourceGrade.Name,
            ["instituteName"] = institute.Name,
            ["station"] = employment.Location ?? string.Empty,
            ["dateOfBirth"] = employee.DateOfBirth?.ToString("yyyy-MM-dd"),
            ["appointmentDate"] = employment.AppointmentDate?.ToString("yyyy-MM-dd"),
            ["lastPromotionDate"] = recordedLastPromotionDate?.ToString("yyyy-MM-dd"),
            ["presentGradeStartSource"] = presentGradeStartSource,
            ["targetGradeName"] = targetGrade?.Name
        };

        return WrapSection("personal-record", "Particulars of applicant", payload);
    }

    public static string BuildQualificationsContent(IReadOnlyList<EducationRecord> records)
    {
        var rows = records
            .OrderByDescending(record => record.DateCompleted ?? DateTime.MinValue)
            .Select(record => new Dictionary<string, object?>
            {
                ["certificateAwarded"] = record.CertificateAwarded,
                ["institutionName"] = record.InstitutionName,
                ["courseStudied"] = record.CourseStudied,
                ["qualificationLevel"] = record.QualificationLevel,
                ["dateObtained"] = record.DateCompleted?.ToString("yyyy-MM-dd")
            })
            .ToList();

        return WrapSection("qualifications", "Qualifications", new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["formCode"] = "csir-form-2",
            ["part"] = "qualifications",
            ["rows"] = rows
        });
    }

    public static string BuildEmptyTrainingContent() =>
        WrapSection("training", "Training", new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["formCode"] = "csir-form-2",
            ["part"] = "training",
            ["rows"] = Array.Empty<object>()
        });

    public static string BuildEmptyServiceDutiesContent() =>
        WrapSection("service-duties", "Service history and present duties", new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["formCode"] = "csir-form-2",
            ["part"] = "service-duties",
            ["presentDuties"] = string.Empty
        });

    public static string BuildEmptyWorkflowContent(string part, string heading) =>
        WrapSection(part, heading, new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["formCode"] = "csir-form-2",
            ["part"] = part
        });

    private static string WrapSection(string code, string heading, object content)
    {
        var document = new
        {
            schemaVersion = 2,
            sections = new[]
            {
                new
                {
                    code,
                    heading,
                    content
                }
            }
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static string FormatEmployeeName(Employee employee) =>
        employee.PreferredName ??
        string.Join(' ', new[] { employee.OtherNames, employee.Surname }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
