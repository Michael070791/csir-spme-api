using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmployeeChild : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public string Gender { get; private set; } = string.Empty;
    public string? BirthCertificateNumber { get; private set; }
    public Guid? BirthCertificateFileId { get; private set; }

    private EmployeeChild() { }

    public EmployeeChild(
        Guid employeeId,
        string name,
        DateTime dateOfBirth,
        string gender,
        string? birthCertificateNumber,
        Guid? birthCertificateFileId)
    {
        EmployeeId = employeeId;
        Update(name, dateOfBirth, gender, birthCertificateNumber, birthCertificateFileId);
    }

    public void Update(
        string name,
        DateTime dateOfBirth,
        string gender,
        string? birthCertificateNumber,
        Guid? birthCertificateFileId)
    {
        Name = name.Trim();
        DateOfBirth = dateOfBirth;
        Gender = gender.Trim();
        BirthCertificateNumber = string.IsNullOrWhiteSpace(birthCertificateNumber)
            ? null
            : birthCertificateNumber.Trim();
        BirthCertificateFileId = birthCertificateFileId;
    }
}
