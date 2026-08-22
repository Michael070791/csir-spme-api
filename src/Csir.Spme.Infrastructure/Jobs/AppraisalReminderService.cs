using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Infrastructure.Jobs;

public sealed class AppraisalReminderOptions
{
    public const string SectionName = "AppraisalReminders";
    public bool Enabled { get; set; }
    public int InitialDelaySeconds { get; set; } = 60;
    public int IntervalMinutes { get; set; } = 60;
}

public sealed record AppraisalReminderRunResult(Guid CycleId, int Processed, int Staged);

public sealed class AppraisalReminderService(
    SpmeDbContext db,
    IWorkflowNotificationOutbox outbox,
    IAuditService audit)
{
    public async Task<AppraisalReminderRunResult> RunCycleAsync(
        AppraisalCycle cycle,
        DateTime today,
        string source,
        CancellationToken ct)
    {
        var items = await db.PerformanceAppraisals
            .Where(x => x.AppraisalCycleId == cycle.Id && x.InstituteId == cycle.InstituteId &&
                x.Status != AppraisalStatuses.Approved)
            .ToListAsync(ct);
        var staged = 0;
        foreach (var appraisal in items)
        {
            var offset = AppraisalReminderSchedule.OffsetCode(cycle.DeadlineFor(appraisal.Status), today);
            if (offset is null || await db.AppraisalReminderRecords.AnyAsync(x =>
                    x.PerformanceAppraisalId == appraisal.Id && x.Stage == appraisal.Status &&
                    x.OffsetCode == offset, ct))
                continue;

            var recipient = await RecipientForStageAsync(appraisal, ct);
            if (!recipient.HasValue) continue;
            db.AppraisalReminderRecords.Add(new AppraisalReminderRecord(
                appraisal.Id, appraisal.Status, offset, DateTimeOffset.UtcNow));
            await outbox.StageAppraisalNoticeAsync(
                appraisal.Id,
                recipient.Value,
                "deadline-reminder",
                "Confidential appraisal deadline reminder",
                "An appraisal action is due. Open the secure portal to review it.",
                $"{appraisal.Status}:{offset}",
                ct);
            staged++;
        }

        await audit.RecordAsync(
            "appraisal-cycle.reminders-run",
            "AppraisalCycle",
            cycle.Id.ToString(),
            null,
            $"source={source};processed={items.Count};staged={staged}",
            ct);
        await db.SaveChangesAsync(ct);
        return new AppraisalReminderRunResult(cycle.Id, items.Count, staged);
    }

    public async Task<int> RunOpenCyclesAsync(DateTime today, CancellationToken ct)
    {
        var cycles = await db.AppraisalCycles.AsNoTracking()
            .Where(x => x.Status == AppraisalCycleStatuses.Open)
            .ToListAsync(ct);
        foreach (var cycle in cycles)
            await RunCycleAsync(cycle, today, "scheduled", ct);
        return cycles.Count;
    }

    private async Task<Guid?> RecipientForStageAsync(PerformanceAppraisal appraisal, CancellationToken ct) =>
        appraisal.Status switch
        {
            AppraisalStatuses.Planning or AppraisalStatuses.Midyear or AppraisalStatuses.MidyearStaffSignature
                or AppraisalStatuses.YearEnd or AppraisalStatuses.StaffSignature =>
                await db.Users.Where(x => x.EmployeeId == appraisal.EmployeeId && x.AccountStatus == "active")
                    .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct),
            AppraisalStatuses.PlanningReview or AppraisalStatuses.MidyearReview or AppraisalStatuses.HodAssessment =>
                appraisal.HodUserId,
            AppraisalStatuses.MidyearDirectorReview or AppraisalStatuses.DirectorReview => appraisal.DirectorUserId,
            _ => null
        };
}

public sealed class AppraisalReminderHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<AppraisalReminderOptions> options,
    ILogger<AppraisalReminderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), timeProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AppraisalReminderService>();
                await service.RunOpenCyclesAsync(timeProvider.GetUtcNow().UtcDateTime.Date, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled appraisal reminder run failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
        }
    }
}
