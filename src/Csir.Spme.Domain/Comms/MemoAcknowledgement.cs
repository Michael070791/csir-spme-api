using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Comms;

public class MemoAcknowledgement : BaseEntity
{
    public Guid MemoId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTimeOffset AcknowledgedAt { get; private set; }

    private MemoAcknowledgement() { }

    public MemoAcknowledgement(Guid memoId, Guid employeeId)
    {
        MemoId = memoId;
        EmployeeId = employeeId;
        AcknowledgedAt = DateTimeOffset.UtcNow;
    }
}
