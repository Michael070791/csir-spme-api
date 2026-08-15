using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

/// <summary>Append-only approval decision history for a leave resumption.</summary>
public class LeaveResumptionApproval : BaseEntity
{
    public Guid ResumptionId { get; private set; }
    public Guid ApproverUserId { get; private set; }
    public string ApprovalStage { get; private set; } = string.Empty;
    public string Decision { get; private set; } = string.Empty;
    public string? Comments { get; private set; }
    public string? SignatureName { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public short Sequence { get; private set; }

    private LeaveResumptionApproval() { }

    public static LeaveResumptionApproval Create(
        Guid resumptionId,
        Guid approverUserId,
        string approvalStage,
        string decision,
        string? comments,
        string? signatureName,
        short sequence)
    {
        return new LeaveResumptionApproval
        {
            ResumptionId = resumptionId,
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
