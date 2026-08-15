using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Reporting;

public class ReportMetric : BaseEntity
{
    public Guid ReportId { get; private set; }
    public string MetricCode { get; private set; } = string.Empty;
    public decimal? NumericValue { get; private set; }
    public string? TextValue { get; private set; }
    public string? Unit { get; private set; }

    private ReportMetric() { }

    public ReportMetric(
        Guid reportId,
        string metricCode,
        decimal? numericValue,
        string? textValue,
        string? unit)
    {
        ReportId = reportId;
        MetricCode = metricCode.Trim();
        NumericValue = numericValue;
        TextValue = string.IsNullOrWhiteSpace(textValue) ? null : textValue.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }
}
