using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Plan;

public class IndicatorMeasurement : BaseEntity
{
    public Guid IndicatorId { get; private set; }
    public Guid ReportingPeriodId { get; private set; }
    public decimal Value { get; private set; }
    public string? Remarks { get; private set; }
    public Guid? EvidenceFileId { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    private IndicatorMeasurement() { }

    public static IndicatorMeasurement Create(
        Guid indicatorId,
        Guid reportingPeriodId,
        decimal value,
        string? remarks,
        Guid? evidenceFileId,
        Guid recordedByUserId)
    {
        return new IndicatorMeasurement
        {
            IndicatorId = indicatorId,
            ReportingPeriodId = reportingPeriodId,
            Value = value,
            Remarks = remarks,
            EvidenceFileId = evidenceFileId,
            RecordedByUserId = recordedByUserId
        };
    }

    public void Update(decimal value, string? remarks, Guid? evidenceFileId)
    {
        Value = value;
        Remarks = remarks;
        EvidenceFileId = evidenceFileId;
    }

    /// <summary>Variance is derived against the indicator target; it is never stored.</summary>
    public static decimal? DeriveVariance(decimal value, decimal? targetValue) =>
        targetValue.HasValue ? value - targetValue.Value : null;
}
