using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class InstituteAlias : BaseEntity
{
    public Guid InstituteId { get; private set; }
    public string Alias { get; private set; } = string.Empty;
    public string NormalizedAlias { get; private set; } = string.Empty;

    private InstituteAlias() { }

    public InstituteAlias(Guid instituteId, string alias)
    {
        InstituteId = instituteId;
        Alias = alias.Trim();
        NormalizedAlias = Alias.ToUpperInvariant();
    }
}
