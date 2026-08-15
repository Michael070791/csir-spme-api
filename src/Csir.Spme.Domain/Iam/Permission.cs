using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Iam;

public class Permission : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission() { }
    public Permission(string code, string module, string description)
    {
        Id = Guid.NewGuid();
        Code = code;
        Module = module;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
