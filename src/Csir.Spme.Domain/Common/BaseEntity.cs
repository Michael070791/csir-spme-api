namespace Csir.Spme.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public Guid? CreatedByUserId { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }
    public Guid? UpdatedByUserId { get; protected set; }
    public byte[] RowVersion { get; protected set; } = [];

    protected BaseEntity() { Id = Guid.NewGuid(); CreatedAt = DateTimeOffset.UtcNow; UpdatedAt = DateTimeOffset.UtcNow; }
}
