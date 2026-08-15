using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public class Faq : InstituteScopedEntity
{
    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;
    public short DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Faq() { }
}
