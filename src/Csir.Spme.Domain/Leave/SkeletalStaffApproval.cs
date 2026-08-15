using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Leave;

public class SkeletalStaffApproval : BaseEntity
{
    public Guid SkeletalStaffRequestId { get; private set; }
    public Guid? ApproverUserId { get; private set; }
    public string ApprovalStage { get; private set; } = string.Empty;
    public string Decision { get; private set; } = string.Empty;
    public string? Comments { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    public short Sequence { get; private set; }

    private SkeletalStaffApproval() { }

    public static SkeletalStaffApproval Create(
        Guid requestId,
        Guid? approverUserId,
        string approvalStage,
        string decision,
        string? comments,
        short sequence) =>
        new()
        {
            SkeletalStaffRequestId = requestId,
            ApproverUserId = approverUserId,
            ApprovalStage = approvalStage,
            Decision = decision,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            DecidedAt = DateTimeOffset.UtcNow,
            Sequence = sequence
        };
}
