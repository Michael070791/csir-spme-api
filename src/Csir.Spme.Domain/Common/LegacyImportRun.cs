namespace Csir.Spme.Domain.Common;

public class LegacyImportRun : BaseEntity
{
    public string SourceName { get; private set; } = string.Empty;
    public string SourceBackupPath { get; private set; } = string.Empty;
    public string SourceBackupSha256 { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "dry-run";
    public string Status { get; private set; } = "running";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int SourceTableCount { get; private set; }
    public int SourceRowCount { get; private set; }
    public int TargetInsertedCount { get; private set; }
    public int TargetUpdatedCount { get; private set; }
    public int IssueCount { get; private set; }
    public string RowCountsJson { get; private set; } = "{}";
    public string ReconciliationJson { get; private set; } = "{}";
    public string Notes { get; private set; } = string.Empty;

    private LegacyImportRun() { }

    public LegacyImportRun(string sourceName, string sourceBackupPath, string sourceBackupSha256, string mode)
    {
        SourceName = sourceName;
        SourceBackupPath = sourceBackupPath;
        SourceBackupSha256 = sourceBackupSha256;
        Mode = mode;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void RecordSourceSummary(int sourceTableCount, int sourceRowCount, string rowCountsJson)
    {
        SourceTableCount = sourceTableCount;
        SourceRowCount = sourceRowCount;
        RowCountsJson = rowCountsJson;
    }

    public void RecordReconciliation(string reconciliationJson) =>
        ReconciliationJson = reconciliationJson;

    public void AddInserted() => TargetInsertedCount++;

    public void AddUpdated() => TargetUpdatedCount++;

    public void AddIssue() => IssueCount++;

    public void Complete(string notes)
    {
        Status = "completed";
        Notes = notes;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string notes)
    {
        Status = "failed";
        Notes = notes;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
