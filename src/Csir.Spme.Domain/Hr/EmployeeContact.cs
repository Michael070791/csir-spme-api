using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmployeeContact : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public string ContactType { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Relationship { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsPrimary { get; private set; }

    private EmployeeContact() { }
}
