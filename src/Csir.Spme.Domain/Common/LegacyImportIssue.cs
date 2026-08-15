namespace Csir.Spme.Domain.Common;

public class LegacyImportIssue : BaseEntity
{
    public Guid LegacyImportRunId { get; private set; }
    public string SourceDatabase { get; private set; } = string.Empty;
    public string SourceTable { get; private set; } = string.Empty;
    public string SourceKey { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "warning";
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string ResolutionStatus { get; private set; } = "open";
    public string PayloadJson { get; private set; } = "{}";

    private LegacyImportIssue() { }

    public LegacyImportIssue(
        Guid legacyImportRunId,
        string sourceDatabase,
        string sourceTable,
        string sourceKey,
        string severity,
        string code,
        string message,
        string payloadJson)
    {
        LegacyImportRunId = legacyImportRunId;
        SourceDatabase = sourceDatabase;
        SourceTable = sourceTable;
        SourceKey = sourceKey;
        Severity = severity;
        Code = code;
        Message = message;
        PayloadJson = payloadJson;
    }
}
