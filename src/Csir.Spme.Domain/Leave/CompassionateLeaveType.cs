using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class CompassionateLeaveType : BaseEntity
{
    public string ScopeType { get; private set; } = "csir-wide";
    public Guid? InstituteId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Days { get; private set; }
    public bool DoesNotDeductFromBalance { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CompassionateLeaveType() { }

    public CompassionateLeaveType(
        string code,
        string name,
        decimal days,
        bool doesNotDeductFromBalance)
    {
        Code = code.Trim();
        Name = name.Trim();
        Days = days;
        DoesNotDeductFromBalance = doesNotDeductFromBalance;
    }
}
