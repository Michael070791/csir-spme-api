using Microsoft.AspNetCore.Identity;

namespace Csir.Spme.Domain.Iam;

public class Role : IdentityRole<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystemRole { get; private set; }

    private Role() { }

    public Role(string code, string name, string description, bool isSystemRole = false)
    {
        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
        IsSystemRole = isSystemRole;
    }
}
