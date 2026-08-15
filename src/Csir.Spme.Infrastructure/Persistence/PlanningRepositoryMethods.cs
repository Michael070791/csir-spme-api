using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Infrastructure.Persistence;

public partial class SpmeDbContext
{
    Task<StrategicPlan?> IStrategicPlanRepository.FindByIdAsync(
        Guid id, Guid? instituteScope, CancellationToken ct) =>
        StrategicPlans.SingleOrDefaultAsync(
            plan => plan.Id == id &&
                    (!instituteScope.HasValue || plan.InstituteId == instituteScope.Value),
            ct);

    Task<bool> IStrategicPlanRepository.CodeExistsAsync(
        Guid instituteId, string code, Guid? excludeId, CancellationToken ct) =>
        StrategicPlans.AnyAsync(plan =>
            plan.InstituteId == instituteId &&
            plan.Code.ToUpper() == code.ToUpper() &&
            (!excludeId.HasValue || plan.Id != excludeId.Value), ct);

    Task<bool> IStrategicPlanRepository.HasOverlappingActiveAsync(
        Guid instituteId, short startYear, short endYear, Guid? excludeId, CancellationToken ct) =>
        StrategicPlans.AnyAsync(plan =>
            plan.InstituteId == instituteId &&
            plan.Status == "active" &&
            plan.StartYear <= endYear &&
            plan.EndYear >= startYear &&
            (!excludeId.HasValue || plan.Id != excludeId.Value), ct);

    async Task<ListSlice<StrategicPlan>> IStrategicPlanRepository.ListAsync(
        Guid? instituteScope, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = StrategicPlans.AsNoTracking();
        if (instituteScope.HasValue)
        {
            query = query.Where(plan => plan.InstituteId == instituteScope.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(plan => plan.Status == status);
        }

        var ordered = page.Sort switch
        {
            "code" => page.Descending
                ? query.OrderByDescending(plan => plan.Code).ThenByDescending(plan => plan.Id)
                : query.OrderBy(plan => plan.Code).ThenBy(plan => plan.Id),
            "name" => page.Descending
                ? query.OrderByDescending(plan => plan.Name).ThenByDescending(plan => plan.Id)
                : query.OrderBy(plan => plan.Name).ThenBy(plan => plan.Id),
            "status" => page.Descending
                ? query.OrderByDescending(plan => plan.Status).ThenByDescending(plan => plan.Id)
                : query.OrderBy(plan => plan.Status).ThenBy(plan => plan.Id),
            _ => page.Descending
                ? query.OrderByDescending(plan => plan.EndYear).ThenByDescending(plan => plan.Id)
                : query.OrderBy(plan => plan.EndYear).ThenBy(plan => plan.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, plan => page.Sort switch
        {
            "code" => plan.Code,
            "name" => plan.Name,
            "status" => plan.Status,
            _ => plan.EndYear.ToString()
        });
    }

    void IStrategicPlanRepository.Add(StrategicPlan plan) => StrategicPlans.Add(plan);

    Task<Thrust?> IThrustRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Thrusts.FindAsync([id], ct).AsTask();

    Task<bool> IThrustRepository.CodeExistsAsync(Guid strategicPlanId, string code, Guid? excludeId, CancellationToken ct) =>
        Thrusts.AnyAsync(thrust =>
            thrust.StrategicPlanId == strategicPlanId &&
            thrust.Code == code &&
            (!excludeId.HasValue || thrust.Id != excludeId.Value), ct);

    async Task<ListSlice<Thrust>> IThrustRepository.ListAsync(
        Guid? instituteScope, Guid? strategicPlanId, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = Thrusts.AsNoTracking().AsQueryable();
        if (instituteScope.HasValue)
        {
            query = query.Where(thrust => thrust.InstituteId == instituteScope.Value);
        }

        if (strategicPlanId.HasValue)
        {
            query = query.Where(thrust => thrust.StrategicPlanId == strategicPlanId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(thrust => thrust.Status == status);
        }

        var ordered = page.Sort switch
        {
            "code" => page.Descending
                ? query.OrderByDescending(thrust => thrust.Code).ThenByDescending(thrust => thrust.Id)
                : query.OrderBy(thrust => thrust.Code).ThenBy(thrust => thrust.Id),
            "title" => page.Descending
                ? query.OrderByDescending(thrust => thrust.Title).ThenByDescending(thrust => thrust.Id)
                : query.OrderBy(thrust => thrust.Title).ThenBy(thrust => thrust.Id),
            "status" => page.Descending
                ? query.OrderByDescending(thrust => thrust.Status).ThenByDescending(thrust => thrust.Id)
                : query.OrderBy(thrust => thrust.Status).ThenBy(thrust => thrust.Id),
            _ => page.Descending
                ? query.OrderByDescending(thrust => thrust.DisplayOrder).ThenByDescending(thrust => thrust.Id)
                : query.OrderBy(thrust => thrust.DisplayOrder).ThenBy(thrust => thrust.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, thrust => page.Sort switch
        {
            "code" => thrust.Code,
            "title" => thrust.Title,
            "status" => thrust.Status,
            _ => thrust.DisplayOrder.ToString()
        });
    }

    void IThrustRepository.Add(Thrust thrust) => Thrusts.Add(thrust);

    Task<Output?> IOutputRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Outputs.FindAsync([id], ct).AsTask();

    async Task<Guid?> IOutputRepository.GetInstituteIdAsync(Guid outputId, CancellationToken ct) =>
        await Outputs.AsNoTracking()
            .Where(output => output.Id == outputId)
            .Join(Thrusts.AsNoTracking(), output => output.ThrustId, thrust => thrust.Id,
                (output, thrust) => (Guid?)thrust.InstituteId)
            .FirstOrDefaultAsync(ct);

    Task<bool> IOutputRepository.CodeExistsAsync(Guid thrustId, string code, Guid? excludeId, CancellationToken ct) =>
        Outputs.AnyAsync(output =>
            output.ThrustId == thrustId &&
            output.Code == code &&
            (!excludeId.HasValue || output.Id != excludeId.Value), ct);

    async Task<ListSlice<Output>> IOutputRepository.ListAsync(
        Guid? instituteScope, Guid? thrustId, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = Outputs.AsNoTracking().AsQueryable();
        if (thrustId.HasValue)
        {
            query = query.Where(output => output.ThrustId == thrustId.Value);
        }

        if (instituteScope.HasValue)
        {
            query = query.Join(Thrusts.AsNoTracking().Where(thrust => thrust.InstituteId == instituteScope.Value),
                output => output.ThrustId, thrust => thrust.Id, (output, _) => output);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(output => output.Status == status);
        }

        var ordered = page.Sort switch
        {
            "code" => page.Descending
                ? query.OrderByDescending(output => output.Code).ThenByDescending(output => output.Id)
                : query.OrderBy(output => output.Code).ThenBy(output => output.Id),
            "status" => page.Descending
                ? query.OrderByDescending(output => output.Status).ThenByDescending(output => output.Id)
                : query.OrderBy(output => output.Status).ThenBy(output => output.Id),
            _ => page.Descending
                ? query.OrderByDescending(output => output.DisplayOrder).ThenByDescending(output => output.Id)
                : query.OrderBy(output => output.DisplayOrder).ThenBy(output => output.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, output => page.Sort switch
        {
            "code" => output.Code,
            "status" => output.Status,
            _ => output.DisplayOrder.ToString()
        });
    }

    void IOutputRepository.Add(Output output) => Outputs.Add(output);

    Task<Indicator?> IIndicatorRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        Indicators.FindAsync([id], ct).AsTask();

    async Task<Guid?> IIndicatorRepository.GetInstituteIdAsync(Guid indicatorId, CancellationToken ct) =>
        await Indicators.AsNoTracking()
            .Where(indicator => indicator.Id == indicatorId)
            .Join(Outputs.AsNoTracking(), indicator => indicator.OutputId, output => output.Id,
                (indicator, output) => output)
            .Join(Thrusts.AsNoTracking(), output => output.ThrustId, thrust => thrust.Id,
                (output, thrust) => (Guid?)thrust.InstituteId)
            .FirstOrDefaultAsync(ct);

    async Task<Guid?> IIndicatorRepository.GetThrustIdAsync(Guid indicatorId, CancellationToken ct) =>
        await Indicators.AsNoTracking()
            .Where(indicator => indicator.Id == indicatorId)
            .Join(Outputs.AsNoTracking(), indicator => indicator.OutputId, output => output.Id,
                (indicator, output) => (Guid?)output.ThrustId)
            .FirstOrDefaultAsync(ct);

    Task<bool> IIndicatorRepository.CodeExistsAsync(Guid outputId, string code, Guid? excludeId, CancellationToken ct) =>
        Indicators.AnyAsync(indicator =>
            indicator.OutputId == outputId &&
            indicator.Code == code &&
            (!excludeId.HasValue || indicator.Id != excludeId.Value), ct);

    async Task<ListSlice<Indicator>> IIndicatorRepository.ListByOutputAsync(
        Guid? instituteScope, Guid outputId, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = Indicators.AsNoTracking().Where(indicator => indicator.OutputId == outputId);
        if (instituteScope.HasValue)
        {
            query = query
                .Join(Outputs.AsNoTracking(), indicator => indicator.OutputId, output => output.Id,
                    (indicator, output) => new { indicator, output })
                .Join(Thrusts.AsNoTracking().Where(thrust => thrust.InstituteId == instituteScope.Value),
                    item => item.output.ThrustId, thrust => thrust.Id, (item, _) => item.indicator);
        }

        return await ListIndicatorsAsync(query, status, page, ct);
    }

    async Task<ListSlice<Indicator>> IIndicatorRepository.ListByThrustAsync(
        Guid thrustId, string? status, KeysetPage page, CancellationToken ct)
    {
        var query = Indicators.AsNoTracking()
            .Join(Outputs.AsNoTracking().Where(output => output.ThrustId == thrustId),
                indicator => indicator.OutputId, output => output.Id, (indicator, _) => indicator);
        return await ListIndicatorsAsync(query, status, page, ct);
    }

    void IIndicatorRepository.Add(Indicator indicator) => Indicators.Add(indicator);

    Task<IndicatorMeasurement?> IIndicatorMeasurementRepository.FindByIdAsync(Guid id, CancellationToken ct) =>
        IndicatorMeasurements.FindAsync([id], ct).AsTask();

    async Task<Guid?> IIndicatorMeasurementRepository.GetInstituteIdAsync(Guid measurementId, CancellationToken ct) =>
        await IndicatorMeasurements.AsNoTracking()
            .Where(measurement => measurement.Id == measurementId)
            .Join(Indicators.AsNoTracking(), measurement => measurement.IndicatorId, indicator => indicator.Id,
                (measurement, indicator) => indicator)
            .Join(Outputs.AsNoTracking(), indicator => indicator.OutputId, output => output.Id,
                (indicator, output) => output)
            .Join(Thrusts.AsNoTracking(), output => output.ThrustId, thrust => thrust.Id,
                (output, thrust) => (Guid?)thrust.InstituteId)
            .FirstOrDefaultAsync(ct);

    async Task<Guid?> IIndicatorMeasurementRepository.GetIndicatorInstituteIdAsync(Guid indicatorId, CancellationToken ct) =>
        await ((IIndicatorRepository)this).GetInstituteIdAsync(indicatorId, ct);

    Task<ReportingPeriod?> IIndicatorMeasurementRepository.GetReportingPeriodAsync(Guid reportingPeriodId, CancellationToken ct) =>
        ReportingPeriods.FindAsync([reportingPeriodId], ct).AsTask();

    Task<Indicator?> IIndicatorMeasurementRepository.GetIndicatorAsync(Guid indicatorId, CancellationToken ct) =>
        Indicators.FindAsync([indicatorId], ct).AsTask();

    Task<bool> IIndicatorMeasurementRepository.ExistsAsync(
        Guid indicatorId, Guid reportingPeriodId, Guid? excludeId, CancellationToken ct) =>
        IndicatorMeasurements.AnyAsync(measurement =>
            measurement.IndicatorId == indicatorId &&
            measurement.ReportingPeriodId == reportingPeriodId &&
            (!excludeId.HasValue || measurement.Id != excludeId.Value), ct);

    async Task<ListSlice<IndicatorMeasurement>> IIndicatorMeasurementRepository.ListByIndicatorAsync(
        Guid indicatorId, KeysetPage page, CancellationToken ct)
    {
        var query = IndicatorMeasurements.AsNoTracking()
            .Where(measurement => measurement.IndicatorId == indicatorId)
            .OrderBy(measurement => measurement.Id);

        return ToSlice(await query.ToListAsync(ct), page, measurement => measurement.Id.ToString("N"));
    }

    void IIndicatorMeasurementRepository.Add(IndicatorMeasurement measurement) =>
        IndicatorMeasurements.Add(measurement);

    void IIndicatorMeasurementRepository.Remove(IndicatorMeasurement measurement) =>
        IndicatorMeasurements.Remove(measurement);

    private async Task<ListSlice<Indicator>> ListIndicatorsAsync(
        IQueryable<Indicator> query, string? status, KeysetPage page, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(indicator => indicator.Status == status);
        }

        var ordered = page.Sort switch
        {
            "status" => page.Descending
                ? query.OrderByDescending(indicator => indicator.Status).ThenByDescending(indicator => indicator.Id)
                : query.OrderBy(indicator => indicator.Status).ThenBy(indicator => indicator.Id),
            _ => page.Descending
                ? query.OrderByDescending(indicator => indicator.Code).ThenByDescending(indicator => indicator.Id)
                : query.OrderBy(indicator => indicator.Code).ThenBy(indicator => indicator.Id)
        };

        return ToSlice(await ordered.ToListAsync(ct), page, indicator => page.Sort switch
        {
            "status" => indicator.Status,
            _ => indicator.Code
        });
    }

    private static ListSlice<T> ToSlice<T>(
        IReadOnlyList<T> orderedItems, KeysetPage page, Func<T, string> sortValue)
        where T : class
    {
        IEnumerable<T> items = orderedItems;
        if (page.After is not null)
        {
            var afterSeen = false;
            items = items.SkipWhile(item =>
            {
                if (afterSeen)
                {
                    return false;
                }

                afterSeen = sortValue(item) == page.After.SortValue && EntityId(item) == page.After.Id;
                return true;
            });
        }

        var pageItems = items.Take(page.Limit + 1).ToList();
        var hasNext = pageItems.Count > page.Limit;
        if (hasNext)
        {
            pageItems.RemoveAt(pageItems.Count - 1);
        }

        var next = hasNext && pageItems.Count > 0
            ? new CursorPosition(sortValue(pageItems[^1]), EntityId(pageItems[^1]))
            : null;

        return new ListSlice<T>(pageItems, next);
    }

    private static Guid EntityId<T>(T item)
        where T : class
    {
        var property = typeof(T).GetProperty("Id")
            ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} does not expose Id.");
        return (Guid)(property.GetValue(item) ?? Guid.Empty);
    }
}
