using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Hr;

public class EmployeeImportBatch : BaseEntity
{
    public Guid? InstituteId { get; private set; }
    public Guid SourceFileId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileChecksum { get; private set; } = string.Empty;
    public string Status { get; private set; } = "uploaded";
    public string SourceFormat { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset? ParsedAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public Guid? CommitJobId { get; private set; }
    public int TotalRows { get; private set; }
    public int ReadyRows { get; private set; }
    public int ReviewRows { get; private set; }
    public int ConflictRows { get; private set; }
    public int CreatedRows { get; private set; }
    public int UpdatedRows { get; private set; }
    public int SkippedRows { get; private set; }
    public string WarningsJson { get; private set; } = "[]";

    private EmployeeImportBatch() { }
}

public class EmployeeImportRow : BaseEntity
{
    public Guid BatchId { get; private set; }
    public string? SheetName { get; private set; }
    public int RowNumber { get; private set; }
    public string? SourceInstituteText { get; private set; }
    public Guid? MatchedEmployeeId { get; private set; }
    public string MatchReason { get; private set; } = string.Empty;
    public string ReviewStatus { get; private set; } = "needs-review";
    public string ProposedAction { get; private set; } = "skip";
    public string PayloadJson { get; private set; } = "{}";
    public string FieldDiffsJson { get; private set; } = "{}";
    public string WarningsJson { get; private set; } = "[]";
    public string AppliedResult { get; private set; } = "pending";
    public DateTimeOffset? AppliedAt { get; private set; }
    public string? AppliedMessage { get; private set; }

    private EmployeeImportRow() { }
}

public class EmployeeImportFieldMapping : BaseEntity
{
    public Guid BatchId { get; private set; }
    public string SourceColumn { get; private set; } = string.Empty;
    public string CanonicalField { get; private set; } = string.Empty;
    public string MappingMode { get; private set; } = "explicit";
    public bool IsRequired { get; private set; }

    private EmployeeImportFieldMapping() { }
}
