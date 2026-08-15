using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class Employee : BaseEntity
{
    public Guid InstituteId { get; private set; }
    public string StaffId { get; private set; } = string.Empty;
    public string NormalizedStaffId { get; private set; } = string.Empty;
    public string? Prefix { get; private set; }
    public string Surname { get; private set; } = string.Empty;
    public string? OtherNames { get; private set; }
    public string? PreferredName { get; private set; }
    public string Gender { get; private set; } = string.Empty;
    public DateTime? DateOfBirth { get; private set; }
    public string? Nationality { get; private set; }
    public string? Religion { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? PrimaryEmail { get; private set; }
    public string? NormalizedPrimaryEmail { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string ProfileStatus { get; private set; } = "active";
    public bool IsHrApproved { get; private set; }
    public bool IsContactVerified { get; private set; }
    public Guid? ProfileImageFileId { get; private set; }

    private Employee() { }
    public Employee(Guid instituteId, string staffId, string surname, string gender)
    {
        Id = Guid.NewGuid();
        InstituteId = instituteId;
        StaffId = staffId;
        NormalizedStaffId = staffId.ToUpperInvariant();
        Surname = surname;
        Gender = gender;
        ProfileStatus = "active";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateImportedProfile(
        string? prefix,
        string? otherNames,
        DateTime? dateOfBirth,
        string? nationality,
        string? religion,
        string? maritalStatus,
        string? primaryEmail,
        string? phone,
        bool isHrApproved)
    {
        Prefix = NormalizeOptional(prefix);
        OtherNames = NormalizeOptional(otherNames);
        DateOfBirth = dateOfBirth;
        Nationality = NormalizeOptional(nationality);
        Religion = NormalizeOptional(religion);
        MaritalStatus = NormalizeOptional(maritalStatus);
        PrimaryEmail = NormalizeOptional(primaryEmail);
        NormalizedPrimaryEmail = PrimaryEmail?.ToUpperInvariant();
        Phone = NormalizeOptional(phone);
        IsHrApproved = isHrApproved;
    }

    public void UpdateProfile(
        string staffId,
        string? prefix,
        string surname,
        string? otherNames,
        string gender,
        DateTime? dateOfBirth,
        string? nationality,
        string? religion,
        string? maritalStatus,
        string? primaryEmail,
        string? phone,
        string profileStatus,
        bool isHrApproved)
    {
        StaffId = staffId.Trim();
        NormalizedStaffId = StaffId.ToUpperInvariant();
        Prefix = NormalizeOptional(prefix);
        Surname = surname.Trim();
        OtherNames = NormalizeOptional(otherNames);
        Gender = gender.Trim();
        DateOfBirth = dateOfBirth;
        Nationality = NormalizeOptional(nationality);
        Religion = NormalizeOptional(religion);
        MaritalStatus = NormalizeOptional(maritalStatus);
        PrimaryEmail = NormalizeOptional(primaryEmail);
        NormalizedPrimaryEmail = PrimaryEmail?.ToUpperInvariant();
        Phone = NormalizeOptional(phone);
        ProfileStatus = string.IsNullOrWhiteSpace(profileStatus) ? "active" : profileStatus.Trim();
        IsHrApproved = isHrApproved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSelfContact(string? primaryEmail, string? phone, string? address)
    {
        if (primaryEmail is not null)
        {
            PrimaryEmail = NormalizeOptional(primaryEmail);
            NormalizedPrimaryEmail = PrimaryEmail?.ToUpperInvariant();
        }

        if (phone is not null)
            Phone = NormalizeOptional(phone);

        if (address is not null)
            Address = NormalizeOptional(address);

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfileImage(Guid profileImageFileId)
    {
        ProfileImageFileId = profileImageFileId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the employee as HR-approved. Idempotent when already approved.
    /// Does not change <see cref="ProfileStatus"/>.
    /// </summary>
    /// <returns><c>true</c> when approval state changed; <c>false</c> when already approved.</returns>
    public bool ApproveHr()
    {
        if (IsHrApproved)
            return false;

        IsHrApproved = true;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Clears HR approval. Idempotent when already unapproved.
    /// Does not change <see cref="ProfileStatus"/>.
    /// </summary>
    /// <returns><c>true</c> when approval state changed; <c>false</c> when already unapproved.</returns>
    public bool RejectHr()
    {
        if (!IsHrApproved)
            return false;

        IsHrApproved = false;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
