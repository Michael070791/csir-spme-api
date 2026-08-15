using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmploymentRecord : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid? DivisionId { get; private set; }
    public Guid? SectionId { get; private set; }
    public Guid? PositionTypeId { get; private set; }
    public Guid? GradeId { get; private set; }
    public string? JobTitle { get; private set; }
    public string? LeadershipRoles { get; private set; }
    public string? StaffCategory { get; private set; }
    public string? GradeStep { get; private set; }
    public string? AreaOfSpecialization { get; private set; }
    public string ServiceStatus { get; private set; } = "active";
    public DateTime? AppointmentDate { get; private set; }
    public DateTime? PromotionDate { get; private set; }
    public DateTime? RetirementDate { get; private set; }
    public string? Organization { get; private set; }
    public string? Location { get; private set; }
    public string? Region { get; private set; }
    public string? District { get; private set; }
    public string? PensionType { get; private set; }
    public string? PensionId { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsCurrent { get; private set; }

    private EmploymentRecord() { }

    public EmploymentRecord(
        Guid employeeId,
        Guid instituteId,
        Guid? divisionId,
        Guid? sectionId,
        Guid? positionTypeId,
        string? jobTitle,
        string? leadershipRoles,
        string? staffCategory,
        string serviceStatus,
        string? areaOfSpecialization,
        DateTime? appointmentDate,
        DateTime? promotionDate,
        string? pensionType,
        string? pensionId,
        DateTime effectiveFrom,
        bool isCurrent)
        : this(
            employeeId,
            instituteId,
            divisionId,
            sectionId,
            positionTypeId,
            null,
            jobTitle,
            leadershipRoles,
            staffCategory,
            null,
            areaOfSpecialization,
            serviceStatus,
            null,
            null,
            null,
            null,
            appointmentDate,
            promotionDate,
            pensionType,
            pensionId,
            effectiveFrom,
            isCurrent)
    {
    }

    public EmploymentRecord(
        Guid employeeId,
        Guid instituteId,
        Guid? divisionId,
        Guid? sectionId,
        Guid? positionTypeId,
        Guid? gradeId,
        string? jobTitle,
        string? leadershipRoles,
        string? staffCategory,
        string? gradeStep,
        string? areaOfSpecialization,
        string serviceStatus,
        string? organization,
        string? location,
        string? region,
        string? district,
        DateTime? appointmentDate,
        DateTime? promotionDate,
        string? pensionType,
        string? pensionId,
        DateTime effectiveFrom,
        bool isCurrent)
    {
        EmployeeId = employeeId;
        InstituteId = instituteId;
        DivisionId = divisionId;
        SectionId = sectionId;
        PositionTypeId = positionTypeId;
        GradeId = gradeId;
        JobTitle = NormalizeOptional(jobTitle);
        LeadershipRoles = NormalizeOptional(leadershipRoles);
        StaffCategory = NormalizeOptional(staffCategory);
        GradeStep = NormalizeOptional(gradeStep);
        AreaOfSpecialization = NormalizeOptional(areaOfSpecialization);
        ServiceStatus = string.IsNullOrWhiteSpace(serviceStatus) ? "active" : serviceStatus.Trim();
        Organization = NormalizeOptional(organization);
        Location = NormalizeOptional(location);
        Region = NormalizeOptional(region);
        District = NormalizeOptional(district);
        AppointmentDate = appointmentDate;
        PromotionDate = promotionDate;
        PensionType = NormalizeOptional(pensionType);
        PensionId = NormalizeOptional(pensionId);
        EffectiveFrom = effectiveFrom;
        IsCurrent = isCurrent;
    }

    public void UpdateCurrent(
        Guid? divisionId,
        Guid? sectionId,
        Guid? positionTypeId,
        Guid? gradeId,
        string? jobTitle,
        string? leadershipRoles,
        string? staffCategory,
        string? gradeStep,
        string? areaOfSpecialization,
        string serviceStatus,
        string? organization,
        string? location,
        string? region,
        string? district,
        DateTime? appointmentDate,
        DateTime? promotionDate,
        string? pensionType,
        string? pensionId)
    {
        DivisionId = divisionId;
        SectionId = sectionId;
        PositionTypeId = positionTypeId;
        GradeId = gradeId;
        JobTitle = NormalizeOptional(jobTitle);
        LeadershipRoles = NormalizeOptional(leadershipRoles);
        StaffCategory = NormalizeOptional(staffCategory);
        GradeStep = NormalizeOptional(gradeStep);
        AreaOfSpecialization = NormalizeOptional(areaOfSpecialization);
        ServiceStatus = string.IsNullOrWhiteSpace(serviceStatus) ? "active" : serviceStatus.Trim();
        Organization = NormalizeOptional(organization);
        Location = NormalizeOptional(location);
        Region = NormalizeOptional(region);
        District = NormalizeOptional(district);
        AppointmentDate = appointmentDate;
        PromotionDate = promotionDate;
        PensionType = NormalizeOptional(pensionType);
        PensionId = NormalizeOptional(pensionId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
