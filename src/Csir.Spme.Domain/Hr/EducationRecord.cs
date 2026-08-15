using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EducationRecord : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public string InstitutionName { get; private set; } = string.Empty;
    public string CourseStudied { get; private set; } = string.Empty;
    public string CertificateAwarded { get; private set; } = string.Empty;
    public string QualificationLevel { get; private set; } = string.Empty;
    public string? Grade { get; private set; }
    public string? Specialization { get; private set; }
    public string? ProfessionalQualifications { get; private set; }
    public string? Affiliations { get; private set; }
    public string? CertificateNumber { get; private set; }
    public DateTime? DateCommenced { get; private set; }
    public DateTime? DateCompleted { get; private set; }
    public string InstitutionRecognitionStatus { get; private set; } = "pending";
    public Guid? InstitutionRecognitionEvidenceFileId { get; private set; }
    public string RelevantFieldStatus { get; private set; } = "pending";
    public Guid? RelevanceReviewedByUserId { get; private set; }
    public DateTimeOffset? RelevanceReviewedAt { get; private set; }
    public Guid? CertificateFileId { get; private set; }

    private EducationRecord() { }

    public EducationRecord(
        Guid employeeId,
        string institutionName,
        string courseStudied,
        string certificateAwarded,
        string qualificationLevel,
        string? grade,
        string? specialization,
        string? professionalQualifications,
        string? affiliations,
        string? certificateNumber,
        DateTime? dateCommenced,
        DateTime? dateCompleted)
    {
        EmployeeId = employeeId;
        InstitutionName = institutionName.Trim();
        CourseStudied = courseStudied.Trim();
        CertificateAwarded = certificateAwarded.Trim();
        QualificationLevel = qualificationLevel.Trim();
        Grade = NormalizeOptional(grade);
        Specialization = NormalizeOptional(specialization);
        ProfessionalQualifications = NormalizeOptional(professionalQualifications);
        Affiliations = NormalizeOptional(affiliations);
        CertificateNumber = NormalizeOptional(certificateNumber);
        DateCommenced = dateCommenced;
        DateCompleted = dateCompleted;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void SetCertificateFileId(Guid? certificateFileId) => CertificateFileId = certificateFileId;

    public void ResetHrReview()
    {
        InstitutionRecognitionStatus = "pending";
        RelevantFieldStatus = "pending";
        RelevanceReviewedByUserId = null;
        RelevanceReviewedAt = null;
    }

    public void UpdateStaffDetails(
        string institutionName,
        string courseStudied,
        string certificateAwarded,
        string qualificationLevel,
        string? grade,
        string? specialization,
        string? professionalQualifications,
        string? affiliations,
        string? certificateNumber,
        DateTime? dateCommenced,
        DateTime? dateCompleted)
    {
        InstitutionName = institutionName.Trim();
        CourseStudied = courseStudied.Trim();
        CertificateAwarded = certificateAwarded.Trim();
        QualificationLevel = qualificationLevel.Trim();
        Grade = NormalizeOptional(grade);
        Specialization = NormalizeOptional(specialization);
        ProfessionalQualifications = NormalizeOptional(professionalQualifications);
        Affiliations = NormalizeOptional(affiliations);
        CertificateNumber = NormalizeOptional(certificateNumber);
        DateCommenced = dateCommenced;
        DateCompleted = dateCompleted;
    }

    public void SetInstitutionRecognitionStatus(string status)
    {
        InstitutionRecognitionStatus = NormalizeRecognitionStatus(status);
    }

    public void SetRelevantFieldStatus(string status, Guid? reviewedByUserId, DateTimeOffset reviewedAt)
    {
        RelevantFieldStatus = NormalizeRelevantFieldStatus(status);
        if (RelevantFieldStatus is "verified" or "rejected")
        {
            RelevanceReviewedByUserId = reviewedByUserId;
            RelevanceReviewedAt = reviewedAt;
        }
        else
        {
            RelevanceReviewedByUserId = null;
            RelevanceReviewedAt = null;
        }
    }

    private static string NormalizeRecognitionStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pending" or "verified" or "rejected" => normalized,
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Institution recognition status is not supported.")
        };
    }

    private static string NormalizeRelevantFieldStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pending" or "verified" or "rejected" or "not-required" => normalized,
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Relevant field status is not supported.")
        };
    }
}
