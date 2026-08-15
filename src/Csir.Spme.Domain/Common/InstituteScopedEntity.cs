using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Common;

public abstract class InstituteScopedEntity : BaseEntity
{
    public Guid InstituteId { get; protected set; }
}
