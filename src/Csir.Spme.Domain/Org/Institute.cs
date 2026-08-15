using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class Institute : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public Guid? ParentInstituteId { get; private set; }
    public bool IsActive { get; private set; }
    public string? EmailDomain { get; private set; }
    public string? Address { get; private set; }

    private Institute() { }
    public Institute(string code, string name, string kind)
    {
        Id = Guid.NewGuid();
        Code = code; Name = name;
        NormalizedName = name.ToUpperInvariant();
        Kind = kind; IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
