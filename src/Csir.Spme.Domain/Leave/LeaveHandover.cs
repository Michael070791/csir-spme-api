using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class LeaveHandover : BaseEntity
{
    public Guid LeaveRequestId { get; private set; }
    public Guid? DelegateEmployeeId { get; private set; }
    public string? Notes { get; private set; }
    public Guid? FileId { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }

    private LeaveHandover() { }

    public static LeaveHandover CreateImported(
        Guid leaveRequestId,
        Guid? delegateEmployeeId,
        string? notes)
    {
        return new LeaveHandover
        {
            LeaveRequestId = leaveRequestId,
            DelegateEmployeeId = delegateEmployeeId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }
}
