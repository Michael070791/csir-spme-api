using Csir.Spme.Domain.Common;

namespace Csir.Spme.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Overrides the original concurrency token of a tracked entity so the next save enforces
    /// the caller-supplied ETag.
    /// </summary>
    void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : BaseEntity;
}
