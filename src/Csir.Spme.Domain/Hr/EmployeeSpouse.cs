using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmployeeSpouse : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime? DateOfBirth { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Occupation { get; private set; }
    public string? Employer { get; private set; }

    private EmployeeSpouse() { }

    public EmployeeSpouse(
        Guid employeeId,
        string name,
        DateTime? dateOfBirth,
        string? phone,
        string? email,
        string? occupation,
        string? employer)
    {
        EmployeeId = employeeId;
        Update(name, dateOfBirth, phone, email, occupation, employer);
    }

    public void Update(
        string name,
        DateTime? dateOfBirth,
        string? phone,
        string? email,
        string? occupation,
        string? employer)
    {
        Name = name.Trim();
        DateOfBirth = dateOfBirth;
        Phone = NormalizeOptional(phone);
        Email = NormalizeOptional(email);
        Occupation = NormalizeOptional(occupation);
        Employer = NormalizeOptional(employer);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
