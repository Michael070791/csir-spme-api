using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Projects;

public sealed class ProjectInception : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public string EstimatedDuration { get; private set; } = string.Empty;
    public string SponsorName { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string? CollaboratingInstitute { get; private set; }
    public string? ParticipatingScientists { get; private set; }
    public string? ExpectedBeneficiaries { get; private set; }
    public string? PotentialTechnology { get; private set; }
    public string? ContributionToKnowledge { get; private set; }
    public Guid? ConceptNoteFileId { get; private set; }
    public DateTimeOffset? InceptionCompletedAt { get; private set; }

    private ProjectInception() { }

    public static ProjectInception Create(Guid projectId) => new() { ProjectId = projectId };

    public bool IsComplete => InceptionCompletedAt.HasValue;

    public Result<bool> UpdateDraft(
        string estimatedDuration,
        string sponsorName,
        string location,
        string? collaboratingInstitute,
        string? participatingScientists,
        string? expectedBeneficiaries,
        string? potentialTechnology,
        string? contributionToKnowledge)
    {
        if (IsComplete)
            return Result.Failure(Error.StateTransition("Form 1 is locked after inception is completed."));

        EstimatedDuration = estimatedDuration.Trim();
        SponsorName = sponsorName.Trim();
        Location = location.Trim();
        CollaboratingInstitute = NormalizeOptional(collaboratingInstitute);
        ParticipatingScientists = NormalizeOptional(participatingScientists);
        ExpectedBeneficiaries = NormalizeOptional(expectedBeneficiaries);
        PotentialTechnology = NormalizeOptional(potentialTechnology);
        ContributionToKnowledge = NormalizeOptional(contributionToKnowledge);
        return Result.Success();
    }

    public Result<bool> Complete(DateTimeOffset completedAt)
    {
        if (IsComplete)
            return Result.Failure(Error.StateTransition("Form 1 is already completed."));
        if (string.IsNullOrWhiteSpace(EstimatedDuration) ||
            string.IsNullOrWhiteSpace(SponsorName) ||
            string.IsNullOrWhiteSpace(Location))
            return Result.Failure(Error.Validation(
                "Estimated duration, sponsors, and location are required before Form 1 can be completed."));

        InceptionCompletedAt = completedAt;
        return Result.Success();
    }

    public Result<bool> AttachConceptNote(Guid fileId)
    {
        if (IsComplete)
            return Result.Failure(Error.StateTransition("Form 1 is locked after inception is completed."));

        ConceptNoteFileId = fileId;
        return Result.Success();
    }

    public Result<bool> RemoveConceptNote()
    {
        if (IsComplete)
            return Result.Failure(Error.StateTransition("Form 1 is locked after inception is completed."));

        ConceptNoteFileId = null;
        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
