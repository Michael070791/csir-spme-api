using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

/// <summary>Append-only approval decision history for a leave request.</summary>
public class LeaveRequestApproval : BaseEntity
{
    public Guid LeaveRequestId { get; private set; }
    public Guid ApproverUserId { get; private set; }
    public string ApprovalStage { get; private set; } = string.Empty;
    public string Decision { get; private set; } = string.Empty;
    public string? Comments { get; private set; }
    public string? SignatureName { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public short Sequence { get; private set; }

    private LeaveRequestApproval() { }

    public static LeaveRequestApproval Create(
        Guid leaveRequestId,
        Guid approverUserId,
        string approvalStage,
        string decision,
        string? comments,
        string? signatureName,
        short sequence)
    {
        return new LeaveRequestApproval
        {
            LeaveRequestId = leaveRequestId,
            ApproverUserId = approverUserId,
            ApprovalStage = approvalStage,
            Decision = decision,
            Comments = comments,
            SignatureName = signatureName,
            DecidedAt = DateTimeOffset.UtcNow,
            Sequence = sequence
        };
    }
}
