using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class Division : InstituteScopedEntity
{
    public string? Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Division() { }
    public Division(Guid instituteId, string name)
    {
        Id = Guid.NewGuid(); InstituteId = instituteId;
        Name = name; IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow; UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Result<bool> Update(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Division name is required."));

        Name = name.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
