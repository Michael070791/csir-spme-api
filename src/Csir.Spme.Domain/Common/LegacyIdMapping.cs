namespace Csir.Spme.Domain.Common;

public class LegacyIdMapping : BaseEntity
{
    public Guid LegacyImportRunId { get; private set; }
    public string SourceDatabase { get; private set; } = string.Empty;
    public string SourceTable { get; private set; } = string.Empty;
    public string SourceKey { get; private set; } = string.Empty;
    public string TargetSchema { get; private set; } = string.Empty;
    public string TargetTable { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string MatchKey { get; private set; } = string.Empty;
    public string MatchStrategy { get; private set; } = string.Empty;
    public string RowChecksum { get; private set; } = string.Empty;

    private LegacyIdMapping() { }

    public LegacyIdMapping(
        Guid legacyImportRunId,
        string sourceDatabase,
        string sourceTable,
        string sourceKey,
        string targetSchema,
        string targetTable,
        Guid targetId,
        string matchKey,
        string matchStrategy,
        string rowChecksum)
    {
        LegacyImportRunId = legacyImportRunId;
        SourceDatabase = sourceDatabase;
        SourceTable = sourceTable;
        SourceKey = sourceKey;
        TargetSchema = targetSchema;
        TargetTable = targetTable;
        TargetId = targetId;
        MatchKey = matchKey;
        MatchStrategy = matchStrategy;
        RowChecksum = rowChecksum;
    }
}
