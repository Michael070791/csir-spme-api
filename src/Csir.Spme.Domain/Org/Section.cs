using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Org;

public class Section : BaseEntity
{
    public Guid DivisionId { get; private set; }
    public string? Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Section() { }
    public Section(Guid divisionId, string name)
    {
        Id = Guid.NewGuid(); DivisionId = divisionId;
        Name = name; IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow; UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Result<bool> Update(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Section name is required."));

        Name = name.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
