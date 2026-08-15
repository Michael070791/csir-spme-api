namespace Csir.Spme.Domain.Common;

public class FileRecord : BaseEntity
{
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Checksum { get; private set; } = string.Empty;
    public string? ResourceType { get; private set; }
    public Guid? InstituteId { get; private set; }
    public string? Classification { get; private set; }
    public string ScanStatus { get; private set; } = "pending";
    public string? RetentionRule { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? StorageDeletedAt { get; private set; }

    private FileRecord() { }
    public FileRecord(
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string checksum,
        string? resourceType = null,
        Guid? instituteId = null,
        string? classification = null,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        StorageKey = storageKey;
        OriginalFileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Checksum = checksum;
        ResourceType = resourceType;
        InstituteId = instituteId;
        Classification = classification;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = deletedAt;
        UpdatedAt = deletedAt;
    }

    public void MarkStorageDeleted(DateTimeOffset deletedAt)
    {
        StorageDeletedAt = deletedAt;
        UpdatedAt = deletedAt;
    }

    public void MarkScanStatus(string status) => ScanStatus = status;
}
