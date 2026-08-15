using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Jobs;

public class AuditService : IAuditService
{
    private readonly SpmeDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(SpmeDbContext db, ICurrentUserService currentUser, ILogger<AuditService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task RecordAsync(string action, string targetType, string? targetId = null,
        string? before = null, string? after = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var record = new AuditRecord(
            _currentUser.UserId ?? Guid.Empty,
            action,
            targetType,
            Guid.NewGuid().ToString("N"))
        {
            TargetId = targetId,
            BeforeSummary = before,
            AfterSummary = after,
            ClientIp = _currentUser.IpAddress
        };

        _db.AuditRecords.Add(record);
        _logger.LogInformation("Audit staged: {Action} on {TargetType} {TargetId}", action, targetType, targetId);
        return Task.CompletedTask;
    }

    public async Task RecordAndSaveAsync(string action, string targetType, string? targetId = null,
        string? before = null, string? after = null, CancellationToken ct = default)
    {
        await RecordAsync(action, targetType, targetId, before, after, ct);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Standalone audit committed: {Action} on {TargetType} {TargetId}",
            action, targetType, targetId);
    }
}
