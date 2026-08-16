using Csir.Spme.Domain.Common;

namespace Csir.Spme.Domain.Promotions;

public sealed class PromotionPolicySource : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public Guid? SourceFileId { get; private set; }
    public string DocumentVersion { get; private set; } = string.Empty;
    public string SectionReference { get; private set; } = string.Empty;
    public string PageReference { get; private set; } = string.Empty;
    public string SourceChecksum { get; private set; } = string.Empty;
    public DateTime? EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public string Status { get; private set; } = "active";

    private PromotionPolicySource() { }

    public static PromotionPolicySource Create(
        string title,
        string documentVersion,
        string sectionReference,
        string pageReference,
        string sourceChecksum,
        DateTime effectiveFrom)
    {
        return new PromotionPolicySource
        {
            Title = title.Trim(),
            DocumentVersion = documentVersion.Trim(),
            SectionReference = sectionReference.Trim(),
            PageReference = pageReference.Trim(),
            SourceChecksum = sourceChecksum.Trim(),
            EffectiveFrom = effectiveFrom,
            Status = "active"
        };
    }
}

public sealed class PromotionCycle : BaseEntity
{
    public short CycleYear { get; private set; }
    public DateTime EffectivePromotionDate { get; private set; }
    public string Status { get; private set; } = PromotionConstants.CyclePlanned;
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private PromotionCycle() { }

    public PromotionCycle(short cycleYear)
    {
        CycleYear = cycleYear;
        EffectivePromotionDate = new DateTime(cycleYear, 1, 1);
    }

    public void Open(DateTimeOffset openedAt) { Status = PromotionConstants.CycleOpen; OpenedAt = openedAt; }
}

public sealed class PromotionPath : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public Guid PolicySourceId { get; private set; }
    public string SectionReference { get; private set; } = string.Empty;
    public string StaffCategory { get; private set; } = string.Empty;
    public string PromotionStream { get; private set; } = string.Empty;
    public Guid SourceGradeId { get; private set; }
    public Guid? TargetGradeId { get; private set; }
    public short MinimumYearsInSourceGrade { get; private set; }
    public string RequiredQualificationLevel { get; private set; } = string.Empty;
    public bool RequiresRecognisedInstitution { get; private set; }
    public bool RequiresRelevantField { get; private set; }
    public bool RequiresSatisfactoryAppraisal { get; private set; }
    public string Status { get; private set; } = PromotionConstants.PathActive;
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }

    private PromotionPath() { }

    public static PromotionPath Create(
        string code,
        Guid policySourceId,
        string sectionReference,
        string staffCategory,
        string promotionStream,
        Guid sourceGradeId,
        Guid targetGradeId,
        short minimumYearsInSourceGrade,
        string requiredQualificationLevel,
        DateTime effectiveFrom,
        string status = PromotionConstants.PathActive)
    {
        return new PromotionPath
        {
            Code = code.Trim(),
            PolicySourceId = policySourceId,
            SectionReference = sectionReference.Trim(),
            StaffCategory = staffCategory.Trim(),
            PromotionStream = promotionStream.Trim(),
            SourceGradeId = sourceGradeId,
            TargetGradeId = targetGradeId,
            MinimumYearsInSourceGrade = minimumYearsInSourceGrade,
            RequiredQualificationLevel = requiredQualificationLevel.Trim(),
            RequiresRecognisedInstitution = true,
            RequiresRelevantField = true,
            RequiresSatisfactoryAppraisal = true,
            Status = status,
            EffectiveFrom = effectiveFrom
        };
    }
}

public sealed class PromotionGradeEquivalency : BaseEntity
{
    public string EquivalentTitle { get; private set; } = string.Empty;
    public string NormalizedEquivalentTitle { get; private set; } = string.Empty;
    public Guid CanonicalGradeId { get; private set; }
    public string StaffCategory { get; private set; } = string.Empty;
    public string PromotionStream { get; private set; } = string.Empty;
    public string ApprovalStatus { get; private set; } = "pending";
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public Guid? EvidenceFileId { get; private set; }

    private PromotionGradeEquivalency() { }
}

public sealed class PromotionAssessment : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid PromotionCycleId { get; private set; }
    public Guid PromotionPathId { get; private set; }
    public Guid SourceEmploymentRecordId { get; private set; }
    public Guid SourceGradeId { get; private set; }
    public Guid? TargetGradeId { get; private set; }
    public DateTime AssessmentDate { get; private set; }
    public DateTime EffectivePromotionDate { get; private set; }
    public DateTime SourceGradeEffectiveDate { get; private set; }
    public DateTime? ServiceRequirementMetOn { get; private set; }
    public decimal CompletedSourceGradeYears { get; private set; }
    public string EligibilityState { get; private set; } = PromotionConstants.EligibilityNeedsHrReview;
    public string BlockingReasonsJson { get; private set; } = "[]";
    public string PendingHrChecksJson { get; private set; } = "[]";
    public string EligibilitySnapshotJson { get; private set; } = "{}";
    public Guid? AssessedByUserId { get; private set; }
    public DateTimeOffset? AssessedAt { get; private set; }

    private PromotionAssessment() { }

    public static PromotionAssessment Create(
        Guid employeeId, Guid instituteId, Guid promotionCycleId, Guid promotionPathId,
        Guid sourceEmploymentRecordId, Guid sourceGradeId, Guid? targetGradeId,
        DateTime assessmentDate, DateTime effectivePromotionDate, DateTime sourceGradeEffectiveDate,
        DateTime? serviceRequirementMetOn, decimal completedSourceGradeYears, string eligibilityState,
        string blockingReasonsJson, string pendingHrChecksJson, string eligibilitySnapshotJson, Guid? assessedByUserId)
    {
        return new PromotionAssessment
        {
            EmployeeId = employeeId,
            InstituteId = instituteId,
            PromotionCycleId = promotionCycleId,
            PromotionPathId = promotionPathId,
            SourceEmploymentRecordId = sourceEmploymentRecordId,
            SourceGradeId = sourceGradeId,
            TargetGradeId = targetGradeId,
            AssessmentDate = assessmentDate,
            EffectivePromotionDate = effectivePromotionDate,
            SourceGradeEffectiveDate = sourceGradeEffectiveDate,
            ServiceRequirementMetOn = serviceRequirementMetOn,
            CompletedSourceGradeYears = completedSourceGradeYears,
            EligibilityState = eligibilityState,
            BlockingReasonsJson = blockingReasonsJson,
            PendingHrChecksJson = pendingHrChecksJson,
            EligibilitySnapshotJson = eligibilitySnapshotJson,
            AssessedByUserId = assessedByUserId,
            AssessedAt = DateTimeOffset.UtcNow
        };
    }
}

public sealed class PromotionSubmission : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid ApplicantUserId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid PromotionAssessmentId { get; private set; }
    public Guid PromotionCycleId { get; private set; }
    public Guid PromotionPathId { get; private set; }
    public Guid SourceGradeId { get; private set; }
    public Guid TargetGradeId { get; private set; }
    public string? RequestedTargetJobTitle { get; private set; }
    public string? EmployeeNote { get; private set; }
    public DateTimeOffset? ApplicantDeclarationAcceptedAt { get; private set; }
    public DateTimeOffset RequirementsLockedAt { get; private set; }
    public string Status { get; private set; } = PromotionConstants.SubmissionDraft;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private PromotionSubmission() { }

    public static PromotionSubmission Create(
        Guid employeeId, Guid applicantUserId, Guid instituteId, PromotionAssessment assessment,
        DateTimeOffset lockedAt) => new()
    {
        EmployeeId = employeeId,
        ApplicantUserId = applicantUserId,
        InstituteId = instituteId,
        PromotionAssessmentId = assessment.Id,
        PromotionCycleId = assessment.PromotionCycleId,
        PromotionPathId = assessment.PromotionPathId,
        SourceGradeId = assessment.SourceGradeId,
        TargetGradeId = assessment.TargetGradeId!.Value,
        RequirementsLockedAt = lockedAt,
        Status = PromotionConstants.SubmissionDraft
    };

    public Result<bool> UpdateEmployeeNote(string? note)
    {
        if (!IsStaffEditable) return Result.Failure(Error.Conflict("Only draft or returned submissions are editable."));
        if (note?.Length > 2000) return Result.Failure(Error.Validation("Employee note cannot exceed 2000 characters."));
        EmployeeNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }

    public bool IsStaffEditable => Status is PromotionConstants.SubmissionDraft or PromotionConstants.SubmissionReturned;
    public void MarkApplicantDeclarationAccepted(DateTimeOffset acceptedAt) => ApplicantDeclarationAcceptedAt = acceptedAt;
    public Result<bool> Submit(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionDraft, PromotionConstants.SubmissionReturned], PromotionConstants.SubmissionSubmitted, now);
    public Result<bool> Withdraw(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionDraft, PromotionConstants.SubmissionSubmitted, PromotionConstants.SubmissionUnderReview,
         PromotionConstants.SubmissionReturned, PromotionConstants.SubmissionAcknowledged], PromotionConstants.SubmissionWithdrawn, now);
    public Result<bool> BeginReview(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionSubmitted], PromotionConstants.SubmissionUnderReview, now);
    public Result<bool> Return(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionSubmitted, PromotionConstants.SubmissionUnderReview, PromotionConstants.SubmissionAcknowledged],
        PromotionConstants.SubmissionReturned, now);
    public Result<bool> Acknowledge(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionUnderReview], PromotionConstants.SubmissionAcknowledged, now);
    public Result<bool> Approve(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionUnderReview, PromotionConstants.SubmissionAcknowledged], PromotionConstants.SubmissionApproved, now);
    public Result<bool> Reject(DateTimeOffset now) => Move(
        [PromotionConstants.SubmissionUnderReview, PromotionConstants.SubmissionAcknowledged], PromotionConstants.SubmissionRejected, now);

    private Result<bool> Move(IReadOnlyCollection<string> allowed, string target, DateTimeOffset now)
    {
        if (!allowed.Contains(Status)) return Result.Failure(Error.StateTransition($"A promotion submission in status '{Status}' cannot move to '{target}'."));
        Status = target;
        if (target == PromotionConstants.SubmissionSubmitted) SubmittedAt = now;
        if (target == PromotionConstants.SubmissionReturned) ReturnedAt = now;
        if (target is PromotionConstants.SubmissionWithdrawn or PromotionConstants.SubmissionApproved or PromotionConstants.SubmissionRejected) ClosedAt = now;
        return Result.Success();
    }
}

public sealed class PromotionQualificationAssessment : BaseEntity
{
    public Guid PromotionAssessmentId { get; private set; }
    public Guid EducationRecordId { get; private set; }
    public bool QualificationRequirementMet { get; private set; }
    public bool InstitutionRecognitionVerified { get; private set; }
    public bool RelevantFieldVerified { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? Notes { get; private set; }

    private PromotionQualificationAssessment() { }
}

public sealed class PromotionAppraisalAssessment : BaseEntity
{
    public Guid PromotionAssessmentId { get; private set; }
    public Guid PerformanceAppraisalId { get; private set; }
    public bool SatisfactoryRequirementMet { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? Notes { get; private set; }

    private PromotionAppraisalAssessment() { }
}

public sealed class PromotionSubmissionRequirementTemplate : BaseEntity
{
    public Guid PromotionCycleId { get; private set; }
    public Guid PromotionPathId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string RequirementType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? DeclarationText { get; private set; }
    public bool IsRequired { get; private set; }
    public short DisplayOrder { get; private set; }
    public string? ReportTemplateCode { get; private set; }
    public string? AcceptedContentTypesJson { get; private set; }
    public long? MaximumFileBytes { get; private set; }
    public short? MaximumDocumentCount { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }

    private PromotionSubmissionRequirementTemplate() { }

    public PromotionSubmissionRequirementTemplate(
        Guid cycleId, Guid pathId, string code, string type, string title, bool required, short displayOrder,
        string? description = null, string? declarationText = null, string? reportTemplateCode = null,
        string? acceptedContentTypesJson = null, long? maximumFileBytes = null, short? maximumDocumentCount = null)
    {
        PromotionCycleId = cycleId; PromotionPathId = pathId; Code = code; RequirementType = type;
        Title = title; IsRequired = required; DisplayOrder = displayOrder; Description = description;
        DeclarationText = declarationText; ReportTemplateCode = reportTemplateCode;
        AcceptedContentTypesJson = acceptedContentTypesJson; MaximumFileBytes = maximumFileBytes;
        MaximumDocumentCount = maximumDocumentCount; EffectiveFrom = DateTimeOffset.UtcNow;
    }
}

public sealed class PromotionSubmissionRequirementSnapshot : BaseEntity
{
    public Guid PromotionSubmissionId { get; private set; }
    public Guid RequirementTemplateId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string RequirementType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? DeclarationText { get; private set; }
    public bool IsRequired { get; private set; }
    public short DisplayOrder { get; private set; }
    public string? ReportTemplateCode { get; private set; }
    public string? AcceptedContentTypesJson { get; private set; }
    public long? MaximumFileBytes { get; private set; }
    public short? MaximumDocumentCount { get; private set; }

    private PromotionSubmissionRequirementSnapshot() { }

    public PromotionSubmissionRequirementSnapshot(Guid submissionId, PromotionSubmissionRequirementTemplate template)
    {
        PromotionSubmissionId = submissionId; RequirementTemplateId = template.Id; Code = template.Code;
        RequirementType = template.RequirementType; Title = template.Title; Description = template.Description;
        DeclarationText = template.DeclarationText; IsRequired = template.IsRequired; DisplayOrder = template.DisplayOrder;
        ReportTemplateCode = template.ReportTemplateCode; AcceptedContentTypesJson = template.AcceptedContentTypesJson;
        MaximumFileBytes = template.MaximumFileBytes; MaximumDocumentCount = template.MaximumDocumentCount;
    }
}

public sealed class PromotionSubmissionReport : BaseEntity
{
    private const string EmptyStructuredContent = """{"schemaVersion":1,"sections":[]}""";

    public Guid PromotionSubmissionId { get; private set; }
    public Guid RequirementSnapshotId { get; private set; }
    public string ReportType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string ContentJson { get; private set; } = EmptyStructuredContent;
    public string Status { get; private set; } = PromotionConstants.SubmissionReportDraft;
    public Guid? RenderedFileId { get; private set; }
    public DateTimeOffset LastSavedAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }

    private PromotionSubmissionReport() { }

    public static Result<PromotionSubmissionReport> CreateDraft(
        Guid promotionSubmissionId,
        Guid requirementSnapshotId,
        string reportType,
        string title,
        DateTimeOffset createdAt)
    {
        if (promotionSubmissionId == Guid.Empty || requirementSnapshotId == Guid.Empty)
        {
            return Result<PromotionSubmissionReport>.Failure(
                Error.Validation("A promotion submission and locked report requirement are required."));
        }

        if (string.IsNullOrWhiteSpace(reportType) || reportType.Length > 64)
        {
            return Result<PromotionSubmissionReport>.Failure(
                Error.Validation("A report type of at most 64 characters is required."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 512)
        {
            return Result<PromotionSubmissionReport>.Failure(
                Error.Validation("A title of at most 512 characters is required."));
        }

        return Result<PromotionSubmissionReport>.Success(new PromotionSubmissionReport
        {
            PromotionSubmissionId = promotionSubmissionId,
            RequirementSnapshotId = requirementSnapshotId,
            ReportType = reportType.Trim(),
            Title = title.Trim(),
            ContentJson = EmptyStructuredContent,
            Status = PromotionConstants.SubmissionReportDraft,
            LastSavedAt = createdAt
        });
    }

    public Result<bool> ReplaceDraft(
        string title,
        string contentJson,
        string submissionStatus,
        DateTimeOffset savedAt)
    {
        if (submissionStatus is not (PromotionConstants.SubmissionDraft or PromotionConstants.SubmissionReturned))
        {
            return Result<bool>.Failure(Error.Conflict(
                "Promotion reports can be edited only while the submission is draft or returned."));
        }

        if (Status is not (
            PromotionConstants.SubmissionReportDraft or
            PromotionConstants.SubmissionReportReady or
            PromotionConstants.SubmissionReportFinalized))
        {
            return Result<bool>.Failure(Error.Conflict(
                "The promotion report is not in an editable workflow state."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 512)
        {
            return Result<bool>.Failure(Error.Validation(
                "A title of at most 512 characters is required."));
        }

        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return Result<bool>.Failure(Error.Validation(
                "Structured report content is required."));
        }

        Title = title.Trim();
        ContentJson = contentJson;
        Status = PromotionConstants.SubmissionReportReady;
        LastSavedAt = savedAt;
        FinalizedAt = null;
        return Result.Success();
    }

    public bool HasMeaningfulContent => Status is PromotionConstants.SubmissionReportReady or PromotionConstants.SubmissionReportFinalized;

    public Result<bool> Finalize(Guid renderedFileId, DateTimeOffset finalizedAt)
    {
        if (!HasMeaningfulContent)
            return Result.Failure(Error.Conflict("The structured promotion report is incomplete."));
        RenderedFileId = renderedFileId;
        Status = PromotionConstants.SubmissionReportFinalized;
        FinalizedAt = finalizedAt;
        return Result.Success();
    }
}

public sealed class PromotionSubmissionDeclaration : BaseEntity
{
    public Guid PromotionSubmissionId { get; private set; }
    public Guid RequirementSnapshotId { get; private set; }
    public Guid AcceptedByUserId { get; private set; }
    public DateTimeOffset AcceptedAt { get; private set; }
    public string DeclarationTextSnapshot { get; private set; } = string.Empty;

    private PromotionSubmissionDeclaration() { }
    public PromotionSubmissionDeclaration(Guid submissionId, Guid requirementSnapshotId, Guid userId,
        DateTimeOffset acceptedAt, string declarationText)
    {
        PromotionSubmissionId = submissionId; RequirementSnapshotId = requirementSnapshotId;
        AcceptedByUserId = userId; AcceptedAt = acceptedAt; DeclarationTextSnapshot = declarationText;
    }
}

public sealed class PromotionSubmissionDocument : BaseEntity
{
    public Guid PromotionSubmissionId { get; private set; }
    public Guid RequirementSnapshotId { get; private set; }
    public Guid FileId { get; private set; }
    public string DocumentStatus { get; private set; } = "uploading";
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? EmployeeVisibleReviewNote { get; private set; }

    private PromotionSubmissionDocument() { }

    public PromotionSubmissionDocument(Guid submissionId, Guid requirementSnapshotId, Guid fileId, Guid userId)
    {
        PromotionSubmissionId = submissionId; RequirementSnapshotId = requirementSnapshotId;
        FileId = fileId; UploadedByUserId = userId; DocumentStatus = PromotionConstants.DocumentScanPending;
    }

    public void MarkAvailable(DateTimeOffset submittedAt) { DocumentStatus = PromotionConstants.DocumentAvailable; SubmittedAt = submittedAt; }
    public void MarkInfected() => DocumentStatus = PromotionConstants.DocumentInfected;
    public void MarkScanFailed() => DocumentStatus = PromotionConstants.DocumentScanFailed;
    public void MarkRemoved() => DocumentStatus = PromotionConstants.DocumentRemoved;

}

public sealed class PromotionDecision : BaseEntity
{
    public Guid PromotionSubmissionId { get; private set; }
    public Guid DecidedByUserId { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public string? InternalDecisionNote { get; private set; }
    public string? EmployeeVisibleNote { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    private PromotionDecision() { }
    public PromotionDecision(Guid submissionId, Guid userId, string decision, DateTimeOffset decidedAt,
        string? employeeVisibleNote, string? internalNote)
    {
        PromotionSubmissionId = submissionId; DecidedByUserId = userId; Decision = decision;
        DecidedAt = decidedAt; EmployeeVisibleNote = string.IsNullOrWhiteSpace(employeeVisibleNote) ? null : employeeVisibleNote.Trim();
        InternalDecisionNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim();
    }
}

public sealed class PromotionStatusSnapshot : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid PromotionCycleId { get; private set; }
    public string StaffCategory { get; private set; } = string.Empty;
    public Guid? LatestAssessmentId { get; private set; }
    public Guid? LatestPromotionSubmissionId { get; private set; }
    public Guid? SourceGradeId { get; private set; }
    public Guid? TargetGradeId { get; private set; }
    public string AssessmentState { get; private set; } = PromotionConstants.AssessmentNotAssessed;
    public string? EligibilityState { get; private set; }
    public string? PromotionSubmissionStatus { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; }
    public int? SourceAssessmentVersion { get; private set; }

    private PromotionStatusSnapshot() { }

    public static PromotionStatusSnapshot FromAssessment(PromotionAssessment assessment, string staffCategory)
    {
        return new PromotionStatusSnapshot
        {
            EmployeeId = assessment.EmployeeId,
            InstituteId = assessment.InstituteId,
            PromotionCycleId = assessment.PromotionCycleId,
            StaffCategory = staffCategory,
            LatestAssessmentId = assessment.Id,
            SourceGradeId = assessment.SourceGradeId,
            TargetGradeId = assessment.TargetGradeId,
            AssessmentState = PromotionConstants.AssessmentAssessed,
            EligibilityState = assessment.EligibilityState,
            CalculatedAt = DateTimeOffset.UtcNow,
            SourceAssessmentVersion = 1
        };
    }

    public void SyncSubmission(PromotionSubmission submission)
    {
        LatestPromotionSubmissionId = submission.Id;
        PromotionSubmissionStatus = submission.Status;
        CalculatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class PromotionDocumentUploadSession : BaseEntity
{
    public Guid PromotionSubmissionId { get; private set; }
    public Guid RequirementSnapshotId { get; private set; }
    public Guid InstituteId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public string DeclaredSha256 { get; private set; } = string.Empty;
    public string Status { get; private set; } = PromotionConstants.UploadPending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? FileId { get; private set; }

    private PromotionDocumentUploadSession() { }

    public PromotionDocumentUploadSession(Guid submissionId, Guid requirementId, Guid instituteId,
        Guid employeeId, Guid userId, string storageKey, string fileName, string contentType,
        long sizeBytes, string sha256, DateTimeOffset expiresAt)
    {
        PromotionSubmissionId = submissionId; RequirementSnapshotId = requirementId;
        InstituteId = instituteId; EmployeeId = employeeId; InitiatedByUserId = userId;
        StorageKey = storageKey; FileName = fileName; ContentType = contentType;
        DeclaredSizeBytes = sizeBytes; DeclaredSha256 = sha256; ExpiresAt = expiresAt;
    }

    public Result<bool> Complete(Guid fileId, DateTimeOffset now)
    {
        if (Status != PromotionConstants.UploadPending)
            return Result.Failure(Error.Conflict("The upload session is no longer pending."));
        if (ExpiresAt <= now)
        {
            Status = PromotionConstants.UploadExpired;
            return Result.Failure(Error.Conflict("The upload session has expired."));
        }
        FileId = fileId; Status = PromotionConstants.UploadCompleted;
        return Result.Success();
    }
}
