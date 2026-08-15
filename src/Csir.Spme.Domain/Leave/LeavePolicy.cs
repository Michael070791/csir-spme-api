using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class LeavePolicy : BaseEntity
{
    public string ScopeType { get; private set; } = "institute";
    public Guid? InstituteId { get; private set; }
    public string LeaveType { get; private set; } = string.Empty;
    public Guid? PositionTypeId { get; private set; }
    public short AnnualEntitlementDays { get; private set; }
    public short? MaxConsecutiveDays { get; private set; }
    public bool RequiresDocument { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public string? RulesJson { get; private set; }

    private LeavePolicy() { }
}
