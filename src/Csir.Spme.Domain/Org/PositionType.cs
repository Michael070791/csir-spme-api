using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class PositionType : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public short AnnualLeaveDays { get; private set; }
    public bool IsActive { get; private set; }

    private PositionType() { }

    public PositionType(string code, string name, short annualLeaveDays)
    {
        Code = code.Trim();
        Name = name.Trim();
        AnnualLeaveDays = annualLeaveDays;
        IsActive = true;
    }
}
