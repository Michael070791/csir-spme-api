using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Csir.Spme.Api.Auth;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Hr;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class PromotionSubmissionEndpoints
{
    public static void MapPromotionSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var self = endpoints.MapGroup("/api/v2/promotion-submissions")
            .WithGroupName("v2").WithTags("Promotion submissions")
            .RequireAuthorization(AuthorizationPolicies.ReadOwnPromotionStatus);
        self.MapGet("/me", ListMineAsync)
            .WithName("PromotionSubmissions_ListMine")
            .WithSummary("List the authenticated employee's promotion submissions.")
            .WithDescription("Returns only promotion submissions owned by the authenticated employee, ordered by creation time; an unlinked employee identity receives a non-disclosing not-found response.");
        self.MapPost("", CreateAsync)
            .WithName("PromotionSubmissions_Create")
            .WithSummary("Create the authenticated employee's promotion submission.")
            .WithDescription("Creates a draft from the authenticated employee's eligible assessment while the cycle is open, snapshots applicable requirements, and idempotently returns the existing submission for the same assessment.");
        self.MapPatch("/{id:guid}", PatchAsync)
            .WithName("PromotionSubmissions_UpdateMine")
            .WithSummary("Update an owned promotion submission.")
            .WithDescription("Updates the authenticated employee's note on an editable draft or returned submission and requires the current If-Match ETag to prevent lost concurrent changes.");
        self.MapGet("/{id:guid}/requirements", RequirementsAsync)
            .WithName("PromotionSubmissions_Requirements")
            .WithSummary("List promotion submission requirements.")
            .WithDescription("Returns immutable requirement snapshots and computed completion states for an owner-visible submission or an institute-scoped HR or platform review submission.");
        self.MapPut("/{id:guid}/declarations/{code}", AcceptDeclarationAsync)
            .WithName("PromotionSubmissions_AcceptDeclaration")
            .WithSummary("Accept a promotion submission declaration.")
            .WithDescription("Records the authenticated employee's acceptance of a snapshotted declaration on an editable owned submission, using If-Match concurrency and preserving repeat acceptance as a single record.");
        self.MapGet("/{id:guid}/documents", DocumentsAsync)
            .WithName("PromotionSubmissions_Documents")
            .WithSummary("List promotion submission documents.")
            .WithDescription("Returns non-removed document metadata and malware scan state for an owner-visible submission or an institute-scoped HR or platform review submission, without exposing storage locations.");
        self.MapPost("/{id:guid}/document-upload-sessions", CreateUploadSessionAsync)
            .WithName("PromotionSubmissions_CreateUploadSession")
            .WithSummary("Create a promotion document upload session.")
            .WithDescription("Creates expiring direct-to-storage upload access for an editable owned submission after validating requirement ownership, file name, declared type, configured size limit, checksum, and document quota.");
        self.MapDelete("/{id:guid}/documents/{documentId:guid}", RemoveDocumentAsync)
            .WithName("PromotionSubmissions_RemoveDocument")
            .WithSummary("Remove an owned promotion submission document.")
            .WithDescription("Marks a document and its file as removed only when the authenticated employee uploaded it to an editable owned submission, and requires the current submission If-Match ETag.");
        self.MapPost("/{id:guid}/submit", SubmitAsync)
            .WithName("PromotionSubmissions_Submit")
            .WithSummary("Submit an owned promotion case for HR review.")
            .WithDescription("Submits an editable owned case only while eligibility and the cycle remain valid and all required items are complete, finalizing structured reports as immutable PDFs and notifying institute HR; the current If-Match ETag is required.");
        self.MapPost("/{id:guid}/withdraw", WithdrawAsync)
            .WithName("PromotionSubmissions_Withdraw")
            .WithSummary("Withdraw an owned promotion submission.")
            .WithDescription("Withdraws the authenticated employee's submission from an allowed lifecycle state, synchronizes promotion status, records an audit event, and requires the current If-Match ETag.");

        endpoints.MapPost("/api/v2/files/upload-sessions/{uploadSessionId:guid}/complete", CompleteUploadAsync)
            .WithGroupName("v2").WithTags("Files")
            .RequireAuthorization()
            .WithName("Files_CompletePromotionUploadSession")
            .WithSummary("Complete a direct file upload session.")
            .WithDescription("Completes an authenticated employee's promotion-document or profile-document upload session after verifying declared size, content type, SHA-256 checksum, file signature, and malware scan result.");

        var visible = endpoints.MapGroup("/api/v2/promotion-submissions")
            .WithGroupName("v2").WithTags("Promotion submissions")
            .RequireAuthorization(AuthorizationPolicies.ReadVisiblePromotionSubmission);
        visible.MapGet("/{id:guid}", GetAsync)
            .WithName("PromotionSubmissions_Get")
            .WithSummary("Get a promotion submission.")
            .WithDescription("Returns an ETag-bearing promotion submission for the authenticated owner or for institute-scoped HR and platform reviewers. Unknown, other employees', and cross-institute identifiers share the same non-disclosing not-found response.");
        visible.MapGet("/{id:guid}/documents/{documentId:guid}/content", DownloadDocumentAsync)
            .WithName("PromotionSubmissions_DownloadDocument")
            .WithSummary("Download a promotion submission document.")
            .WithDescription("Streams a clean promotion document for the submission owner or an authorized institute-scoped reviewer without exposing storage locations.");

        var review = endpoints.MapGroup("/api/v2/promotion-submissions")
            .WithGroupName("v2").WithTags("Promotion submissions")
            .RequireAuthorization(AuthorizationPolicies.ReadPromotions);
        review.MapGet("/", ListForReviewAsync)
            .WithName("PromotionSubmissions_ListForReview")
            .WithSummary("List promotion submissions for review.")
            .WithDescription("Returns promotion submissions for the caller's institute, optionally filtered by lifecycle status; platform administrators can review submissions across institutes.");
        review.MapPost("/{id:guid}/begin-review", BeginReviewAsync).RequireAuthorization(AuthorizationPolicies.WritePromotions)
            .WithName("PromotionSubmissions_BeginReview")
            .WithSummary("Begin HR review of a promotion submission.")
            .WithDescription("Moves an institute-scoped submission from submitted to under review, records employee-visible and internal decision notes, emits status events, and requires the current If-Match ETag.");
        review.MapPost("/{id:guid}/return", ReturnAsync).RequireAuthorization(AuthorizationPolicies.WritePromotions)
            .WithName("PromotionSubmissions_Return")
            .WithSummary("Return a promotion submission for correction.")
            .WithDescription("Returns an institute-scoped submission to an employee-editable lifecycle state, stores employee-visible and internal decision notes separately, notifies the owner, and requires the current If-Match ETag.");
        review.MapPost("/{id:guid}/acknowledge", AcknowledgeAsync).RequireAuthorization(AuthorizationPolicies.ApprovePromotions)
            .WithName("PromotionSubmissions_Acknowledge")
            .WithSummary("Acknowledge a reviewed promotion submission.")
            .WithDescription("Moves an institute-scoped reviewed submission to acknowledged, records the approver's employee-visible and internal notes, synchronizes status, notifies the owner, and requires the current If-Match ETag.");
        review.MapPost("/{id:guid}/approve", ApproveAsync).RequireAuthorization(AuthorizationPolicies.ApprovePromotions)
            .WithName("PromotionSubmissions_Approve")
            .WithSummary("Approve a promotion submission.")
            .WithDescription("Moves an institute-scoped submission through the authorized approval transition, records separate employee-visible and internal notes, synchronizes status, notifies the owner, and requires the current If-Match ETag.");
        review.MapPost("/{id:guid}/reject", RejectAsync).RequireAuthorization(AuthorizationPolicies.ApprovePromotions)
            .WithName("PromotionSubmissions_Reject")
            .WithSummary("Reject a promotion submission.")
            .WithDescription("Moves an institute-scoped submission through the authorized rejection transition, records separate employee-visible and internal notes, synchronizes status, notifies the owner, and requires the current If-Match ETag.");
    }

    private static async Task<IResult> ListMineAsync(SpmeDbContext db, HttpContext context, CancellationToken ct)
    {
        if (!TrySelf(context, out var employeeId)) return NotFound();
        var items = await db.PromotionSubmissions.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<PromotionSubmissionResponse>(
            await MapManyAsync(db, items, ct), items.Count));
    }

    private static async Task<IResult> ListForReviewAsync(SpmeDbContext db, HttpContext context, string? status, CancellationToken ct)
    {
        var query = db.PromotionSubmissions.AsNoTracking().AsQueryable();
        if (!context.User.IsInRole(SpmeRoles.PlatformAdmin))
        {
            var institute = Institute(context);
            if (!institute.HasValue) return TypedResults.Ok(new CollectionResponse<PromotionSubmissionResponse>([], 0));
            query = query.Where(x => x.InstituteId == institute.Value);
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var items = (await query.ToListAsync(ct)).OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).ToList();
        return TypedResults.Ok(new CollectionResponse<PromotionSubmissionResponse>(await MapManyAsync(db, items, ct), items.Count));
    }

    private static async Task<IResult> CreateAsync(CreatePromotionSubmissionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        if (!TryIdentity(context, out var userId, out var employeeId)) return NotFound();
        var assessment = await db.PromotionAssessments.SingleOrDefaultAsync(x =>
            x.Id == request.PromotionAssessmentId && x.EmployeeId == employeeId, ct);
        if (assessment is null) return NotFound();
        var existing = await db.PromotionSubmissions.SingleOrDefaultAsync(x => x.PromotionAssessmentId == assessment.Id, ct);
        if (existing is not null) return await ResourceAsync(db, existing, context, ct);
        if (assessment.EligibilityState != PromotionConstants.EligibilityEligibleForReview || !assessment.TargetGradeId.HasValue)
            return EndpointProblems.FromError(Error.Conflict("Only an eligible-for-review assessment can create a promotion submission."));
        var cycle = await db.PromotionCycles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assessment.PromotionCycleId, ct);
        if (cycle?.Status != PromotionConstants.CycleOpen)
            return EndpointProblems.FromError(Error.Conflict("The promotion cycle is not open."));

        var now = DateTimeOffset.UtcNow;
        var submission = PromotionSubmission.Create(employeeId, userId, assessment.InstituteId, assessment, now);
        db.PromotionSubmissions.Add(submission);
        var templateCandidates = await db.PromotionSubmissionRequirementTemplates.AsNoTracking()
            .Where(x => x.PromotionCycleId == assessment.PromotionCycleId && x.PromotionPathId == assessment.PromotionPathId)
            .ToListAsync(ct);
        var templates = templateCandidates.Where(x => x.EffectiveFrom <= now &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo > now))
            .OrderBy(x => x.DisplayOrder).ToList();
        foreach (var template in templates)
        {
            var snapshot = new PromotionSubmissionRequirementSnapshot(submission.Id, template);
            db.PromotionSubmissionRequirementSnapshots.Add(snapshot);
            if (template.RequirementType == PromotionConstants.RequirementReport)
            {
                var report = PromotionSubmissionReport.CreateDraft(submission.Id, snapshot.Id,
                    template.ReportTemplateCode ?? template.Code, template.Title, now);
                if (report.IsFailure) return EndpointProblems.FromError(report.Error!);
                db.PromotionSubmissionReports.Add(report.Value!);
            }
        }
        var status = await db.PromotionStatusSnapshots.SingleOrDefaultAsync(x =>
            x.EmployeeId == employeeId && x.PromotionCycleId == assessment.PromotionCycleId, ct);
        status?.SyncSubmission(submission);
        await audit.RecordAsync("promotion-submission.created", "PromotionSubmission", submission.Id.ToString(), null,
            $"assessment={assessment.Id};requirements={templates.Count}", ct);
        await db.SaveChangesAsync(ct);
        context.Response.Headers.Location = $"/api/v2/promotion-submissions/{submission.Id:D}";
        var response = await MapAsync(db, submission, ct);
        SetEtag(context, response.Etag);
        return TypedResults.Created(context.Response.Headers.Location.ToString(), response);
    }

    private static async Task<IResult> GetAsync(Guid id, SpmeDbContext db, HttpContext context, CancellationToken ct)
    {
        var submission = await FindReadableAsync(db, context, id, ct);
        return submission is null ? NotFound() : await ResourceAsync(db, submission, context, ct);
    }

    private static async Task<IResult> PatchAsync(Guid id, UpdatePromotionSubmissionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!SetExpected(db, submission, context)) return Precondition();
        var changed = submission.UpdateEmployeeNote(request.EmployeeNote);
        if (changed.IsFailure) return EndpointProblems.FromError(changed.Error!);
        await audit.RecordAsync("promotion-submission.updated", "PromotionSubmission", id.ToString(), null, "employee-note", ct);
        return await SaveResourceAsync(db, submission, context, ct);
    }

    private static async Task<IResult> RequirementsAsync(Guid id, SpmeDbContext db, HttpContext context, CancellationToken ct)
    {
        var submission = await FindReadableAsync(db, context, id, ct);
        if (submission is null) return NotFound();
        var requirements = await BuildRequirementsAsync(db, submission.Id, ct);
        return TypedResults.Ok(new CollectionResponse<PromotionRequirementResponse>(requirements, requirements.Count));
    }

    private static async Task<IResult> AcceptDeclarationAsync(Guid id, string code, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!submission.IsStaffEditable) return EndpointProblems.FromError(Error.Conflict("The submission is not editable."));
        if (!SetExpected(db, submission, context)) return Precondition();
        var requirement = await db.PromotionSubmissionRequirementSnapshots.SingleOrDefaultAsync(x =>
            x.PromotionSubmissionId == id && x.Code == code && x.RequirementType == PromotionConstants.RequirementDeclaration, ct);
        if (requirement is null) return NotFound();
        var existing = await db.PromotionSubmissionDeclarations.SingleOrDefaultAsync(x =>
            x.PromotionSubmissionId == id && x.RequirementSnapshotId == requirement.Id, ct);
        if (existing is null)
        {
            var now = DateTimeOffset.UtcNow;
            db.PromotionSubmissionDeclarations.Add(new PromotionSubmissionDeclaration(id, requirement.Id,
                UserId(context)!.Value, now, requirement.DeclarationText ?? string.Empty));
            submission.MarkApplicantDeclarationAccepted(now);
        }
        await audit.RecordAsync("promotion-submission.declaration-accepted", "PromotionSubmission", id.ToString(), null, code, ct);
        return await SaveResourceAsync(db, submission, context, ct);
    }

    private static async Task<IResult> DocumentsAsync(Guid id, SpmeDbContext db, HttpContext context, CancellationToken ct)
    {
        if (await FindReadableAsync(db, context, id, ct) is null) return NotFound();
        var documents = await (from document in db.PromotionSubmissionDocuments.AsNoTracking()
                               join requirement in db.PromotionSubmissionRequirementSnapshots.AsNoTracking()
                                   on document.RequirementSnapshotId equals requirement.Id
                               join file in db.FileRecords.AsNoTracking() on document.FileId equals file.Id
                               where document.PromotionSubmissionId == id && document.DocumentStatus != PromotionConstants.DocumentRemoved
                               select new PromotionDocumentResponse(document.Id, requirement.Code, requirement.Title,
                                   file.OriginalFileName, file.ContentType, file.SizeBytes, document.DocumentStatus,
                                   file.ScanStatus, document.EmployeeVisibleReviewNote, document.CreatedAt)).ToListAsync(ct);
        return TypedResults.Ok(new CollectionResponse<PromotionDocumentResponse>(documents, documents.Count));
    }

    private static async Task<IResult> DownloadDocumentAsync(Guid id, Guid documentId, SpmeDbContext db,
        IFileStorageService storage, HttpContext context, CancellationToken ct)
    {
        if (await FindReadableAsync(db, context, id, ct) is null) return NotFound();
        var document = await (from item in db.PromotionSubmissionDocuments.AsNoTracking()
                              join file in db.FileRecords.AsNoTracking() on item.FileId equals file.Id
                              where item.PromotionSubmissionId == id && item.Id == documentId &&
                                    item.DocumentStatus != PromotionConstants.DocumentRemoved
                              select new { item.DocumentStatus, file.StorageKey, file.ContentType, file.OriginalFileName, file.ScanStatus })
            .SingleOrDefaultAsync(ct);
        if (document is null) return NotFound();
        if (document.DocumentStatus != PromotionConstants.DocumentAvailable || document.ScanStatus != "clean")
            return EndpointProblems.FromError(Error.Forbidden("The document is not available for download."));
        var stream = await storage.DownloadAsync(document.StorageKey, ct);
        if (stream is null) return NotFound();
        return Results.File(stream, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> CreateUploadSessionAsync(Guid id, CreatePromotionDocumentUploadRequest request,
        SpmeDbContext db, IDirectFileUploadService uploads, IOptions<PromotionUploadOptions> optionsAccessor,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!submission.IsStaffEditable) return EndpointProblems.FromError(Error.Conflict("The submission is not editable."));
        var requirement = await db.PromotionSubmissionRequirementSnapshots.SingleOrDefaultAsync(x =>
            x.PromotionSubmissionId == id && x.Code == request.RequirementCode &&
            x.RequirementType == PromotionConstants.RequirementDocument, ct);
        if (requirement is null) return NotFound();
        var options = optionsAccessor.Value;
        var effectiveMax = Math.Min(options.MaximumFileBytes, requirement.MaximumFileBytes ?? options.MaximumFileBytes);
        var fileName = Path.GetFileName(request.FileName ?? string.Empty);
        var allowed = ParseContentTypes(requirement.AcceptedContentTypesJson);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 512 || request.ByteLength <= 0 ||
            request.ByteLength > effectiveMax || !Regex.IsMatch(request.Sha256Checksum ?? string.Empty, "^[a-fA-F0-9]{64}$") ||
            allowed.Count > 0 && !allowed.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            return EndpointProblems.FromError(Error.Validation("The document metadata, type, size, or SHA-256 checksum is invalid."));
        var count = await db.PromotionSubmissionDocuments.CountAsync(x => x.PromotionSubmissionId == id &&
            x.RequirementSnapshotId == requirement.Id && x.DocumentStatus != PromotionConstants.DocumentRemoved, ct);
        if (count >= (requirement.MaximumDocumentCount ?? 1))
            return EndpointProblems.FromError(Error.Conflict("The document count limit has been reached."));
        var userId = UserId(context)!.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(options.UploadSessionMinutes, 5, 1440));
        var storageKey = $"promotions/{submission.InstituteId:N}/{submission.Id:N}/{Guid.NewGuid():N}/{fileName}";
        var access = await uploads.CreateWriteAccessAsync(storageKey, request.ContentType, request.ByteLength,
            request.Sha256Checksum!, expires, ct);
        if (access is null)
            return EndpointProblems.FromError(Error.DependencyUnavailable("Direct upload storage is not configured."));
        var session = new PromotionDocumentUploadSession(id, requirement.Id, submission.InstituteId,
            submission.EmployeeId, userId, storageKey, fileName, request.ContentType, request.ByteLength,
            request.Sha256Checksum!.ToLowerInvariant(), expires);
        db.PromotionDocumentUploadSessions.Add(session);
        await audit.RecordAsync("promotion-document.upload-session-created", "PromotionDocumentUploadSession",
            session.Id.ToString(), null, $"submission={id};requirement={requirement.Code};bytes={request.ByteLength}", ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/v2/files/upload-sessions/{session.Id:D}",
            new PromotionUploadSessionResponse(session.Id, access.UploadUri, access.ExpiresAt, access.RequiredHeaders));
    }

    private static async Task<IResult> CompleteUploadAsync(Guid uploadSessionId, SpmeDbContext db,
        IDirectFileUploadService uploads, IFileStorageService storage, IPromotionMalwareScanner scanner,
        IOptions<PromotionUploadOptions> promotionOptions, IOptions<ProfileDocumentOptions> profileOptions,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var profileResult = await EmployeeProfileEndpoints.TryCompleteProfileDocumentUploadAsync(
            uploadSessionId, db, uploads, storage, scanner, profileOptions, audit, context, ct);
        if (profileResult is not IStatusCodeHttpResult { StatusCode: StatusCodes.Status404NotFound })
            return profileResult;

        if (!TryIdentity(context, out var userId, out var employeeId)) return NotFound();
        var session = await db.PromotionDocumentUploadSessions.SingleOrDefaultAsync(x =>
            x.Id == uploadSessionId && x.EmployeeId == employeeId && x.InitiatedByUserId == userId, ct);
        if (session is null) return NotFound();
        var submission = await db.PromotionSubmissions.SingleAsync(x => x.Id == session.PromotionSubmissionId, ct);
        if (!submission.IsStaffEditable) return EndpointProblems.FromError(Error.Conflict("The submission is not editable."));
        var inspected = await uploads.InspectAsync(session.StorageKey, ct);
        if (inspected is null || inspected.SizeBytes != session.DeclaredSizeBytes ||
            inspected.ContentType is not null && !string.Equals(inspected.ContentType, session.ContentType, StringComparison.OrdinalIgnoreCase))
            return EndpointProblems.FromError(Error.Validation("Uploaded content does not match the declared size and SHA-256 checksum."));
        await using var uploadedStream = await storage.DownloadAsync(session.StorageKey, ct);
        if (uploadedStream is null)
            return EndpointProblems.FromError(Error.Validation("The uploaded content could not be verified."));
        var signature = new byte[8];
        var signatureLength = await uploadedStream.ReadAsync(signature, ct);
        if (uploadedStream.CanSeek) uploadedStream.Position = 0;
        else return EndpointProblems.FromError(Error.Validation("The uploaded content cannot be securely inspected."));
        var actualSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(uploadedStream, ct));
        if (!string.Equals(actualSha256, session.DeclaredSha256, StringComparison.OrdinalIgnoreCase) ||
            !SignatureMatches(session.ContentType, signature.AsSpan(0, signatureLength)))
            return EndpointProblems.FromError(Error.Validation("Uploaded content does not match the declared type or SHA-256 checksum."));
        var file = new FileRecord(session.StorageKey, session.FileName, session.ContentType,
            inspected.SizeBytes, session.DeclaredSha256, "promotion-document", session.InstituteId, "confidential");
        db.FileRecords.Add(file);
        var document = new PromotionSubmissionDocument(submission.Id, session.RequirementSnapshotId, file.Id, userId);
        var scan = await scanner.ScanAsync(session.StorageKey, ct);
        if (string.Equals(promotionOptions.Value.DevelopmentScanResult, "clean", StringComparison.OrdinalIgnoreCase))
            scan = "clean";
        file.MarkScanStatus(scan);
        if (scan == "clean") document.MarkAvailable(DateTimeOffset.UtcNow);
        else if (scan == "infected") document.MarkInfected();
        else if (scan == "failed") document.MarkScanFailed();
        db.PromotionSubmissionDocuments.Add(document);
        var completed = session.Complete(file.Id, DateTimeOffset.UtcNow);
        if (completed.IsFailure) return EndpointProblems.FromError(completed.Error!);
        await audit.RecordAsync("promotion-document.upload-completed", "PromotionSubmissionDocument",
            document.Id.ToString(), null, $"scan={document.DocumentStatus}", ct);
        await db.SaveChangesAsync(ct);
        var requirement = await db.PromotionSubmissionRequirementSnapshots.AsNoTracking()
            .SingleAsync(x => x.Id == session.RequirementSnapshotId, ct);
        return TypedResults.Ok(new PromotionDocumentResponse(document.Id, requirement.Code, requirement.Title,
            file.OriginalFileName, file.ContentType, file.SizeBytes, document.DocumentStatus, file.ScanStatus,
            null, document.CreatedAt));
    }

    private static async Task<IResult> RemoveDocumentAsync(Guid id, Guid documentId, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!submission.IsStaffEditable) return EndpointProblems.FromError(Error.Conflict("The submission is not editable."));
        if (!SetExpected(db, submission, context)) return Precondition();
        var document = await db.PromotionSubmissionDocuments.SingleOrDefaultAsync(x =>
            x.Id == documentId && x.PromotionSubmissionId == id && x.UploadedByUserId == UserId(context), ct);
        if (document is null) return NotFound();
        document.MarkRemoved();
        var file = await db.FileRecords.SingleAsync(x => x.Id == document.FileId, ct);
        file.MarkDeleted(DateTimeOffset.UtcNow);
        await audit.RecordAsync("promotion-document.removed", "PromotionSubmissionDocument", documentId.ToString(), null, null, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> SubmitAsync(Guid id, SpmeDbContext db, IFileStorageService storage,
        IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!SetExpected(db, submission, context)) return Precondition();
        var assessment = await db.PromotionAssessments.AsNoTracking().SingleAsync(x => x.Id == submission.PromotionAssessmentId, ct);
        var cycleOpen = await db.PromotionCycles.AsNoTracking().AnyAsync(x => x.Id == submission.PromotionCycleId && x.Status == PromotionConstants.CycleOpen, ct);
        if (assessment.EligibilityState != PromotionConstants.EligibilityEligibleForReview || !cycleOpen)
            return EndpointProblems.FromError(Error.Conflict("Promotion eligibility or the cycle is no longer valid."));
        var requirements = await BuildRequirementsAsync(db, id, ct);
        var incomplete = requirements.Where(x => x.IsRequired && x.CompletionState != "complete").Select(x => x.Code).ToList();
        if (incomplete.Count > 0)
            return EndpointProblems.FromError(Error.Conflict($"Required promotion items are incomplete: {string.Join(", ", incomplete)}."));

        var now = DateTimeOffset.UtcNow;
        var reports = await db.PromotionSubmissionReports.Where(x => x.PromotionSubmissionId == id).ToListAsync(ct);
        foreach (var report in reports.Where(x => x.Status != PromotionConstants.SubmissionReportFinalized))
        {
            if (!report.HasMeaningfulContent)
                return EndpointProblems.FromError(Error.Conflict("A structured promotion report is incomplete."));
            var pdf = BuildPdf(report.Title, report.ContentJson);
            var storageKey = $"promotions/{submission.InstituteId:N}/{id:N}/rendered/{report.Id:N}.pdf";
            await using var stream = new MemoryStream(pdf);
            var uploaded = await storage.UploadAsync(stream, storageKey, "application/pdf", ct);
            var file = new FileRecord(storageKey, $"{report.ReportType}.pdf", "application/pdf",
                uploaded.SizeBytes, uploaded.Checksum, "promotion-rendered-report", submission.InstituteId, "confidential");
            file.MarkScanStatus("clean");
            db.FileRecords.Add(file);
            var finalized = report.Finalize(file.Id, now);
            if (finalized.IsFailure) return EndpointProblems.FromError(finalized.Error!);
        }
        var moved = submission.Submit(now);
        if (moved.IsFailure) return EndpointProblems.FromError(moved.Error!);
        await SyncStatusAsync(db, submission, ct);
        await StagePromotionEventAsync(db, submission, "submitted", UserId(context)!.Value, null, ct);
        await audit.RecordAsync("promotion-submission.submitted", "PromotionSubmission", id.ToString(), null, "status=submitted", ct);
        return await SaveResourceAsync(db, submission, context, ct);
    }

    private static async Task<IResult> WithdrawAsync(Guid id, SpmeDbContext db, IAuditService audit,
        HttpContext context, CancellationToken ct)
    {
        var submission = await FindMineAsync(db, context, id, true, ct);
        if (submission is null) return NotFound();
        if (!SetExpected(db, submission, context)) return Precondition();
        var result = submission.Withdraw(DateTimeOffset.UtcNow);
        if (result.IsFailure) return EndpointProblems.FromError(result.Error!);
        await SyncStatusAsync(db, submission, ct);
        await audit.RecordAsync("promotion-submission.withdrawn", "PromotionSubmission", id.ToString(), null, "status=withdrawn", ct);
        return await SaveResourceAsync(db, submission, context, ct);
    }

    private static Task<IResult> ReturnAsync(Guid id, PromotionDecisionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct) =>
        ReviewTransitionAsync(id, request, "returned", db, audit, context, ct);
    private static Task<IResult> BeginReviewAsync(Guid id, PromotionDecisionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct) =>
        ReviewTransitionAsync(id, request, "under-review", db, audit, context, ct);
    private static Task<IResult> AcknowledgeAsync(Guid id, PromotionDecisionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct) =>
        ReviewTransitionAsync(id, request, "acknowledged", db, audit, context, ct);
    private static Task<IResult> ApproveAsync(Guid id, PromotionDecisionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct) =>
        ReviewTransitionAsync(id, request, "approved", db, audit, context, ct);
    private static Task<IResult> RejectAsync(Guid id, PromotionDecisionRequest request, SpmeDbContext db,
        IAuditService audit, HttpContext context, CancellationToken ct) =>
        ReviewTransitionAsync(id, request, "rejected", db, audit, context, ct);

    private static async Task<IResult> ReviewTransitionAsync(Guid id, PromotionDecisionRequest request, string decision,
        SpmeDbContext db, IAuditService audit, HttpContext context, CancellationToken ct)
    {
        var query = db.PromotionSubmissions.AsQueryable();
        if (!context.User.IsInRole(SpmeRoles.PlatformAdmin))
        {
            var institute = Institute(context);
            if (!institute.HasValue) return NotFound();
            query = query.Where(x => x.InstituteId == institute.Value);
        }
        var submission = await query.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (submission is null) return NotFound();
        if (!SetExpected(db, submission, context)) return Precondition();
        var now = DateTimeOffset.UtcNow;
        Result<bool> moved = decision switch
        {
            "under-review" => submission.BeginReview(now),
            "returned" => submission.Return(now), "acknowledged" => submission.Acknowledge(now),
            "approved" => submission.Approve(now), "rejected" => submission.Reject(now),
            _ => Result.Failure(Error.Validation("Unsupported promotion decision."))
        };
        if (moved.IsFailure) return EndpointProblems.FromError(moved.Error!);
        db.PromotionDecisions.Add(new PromotionDecision(id, UserId(context)!.Value, decision, now,
            request.EmployeeVisibleNote, request.InternalNote));
        await SyncStatusAsync(db, submission, ct);
        await StagePromotionEventAsync(db, submission, decision, UserId(context)!.Value, request.EmployeeVisibleNote, ct);
        await audit.RecordAsync($"promotion-submission.{decision}", "PromotionSubmission", id.ToString(), null, $"status={decision}", ct);
        return await SaveResourceAsync(db, submission, context, ct);
    }

    private static async Task<PromotionSubmission?> FindMineAsync(SpmeDbContext db, HttpContext context,
        Guid id, bool tracking, CancellationToken ct)
    {
        if (!TrySelf(context, out var employeeId)) return null;
        var query = tracking ? db.PromotionSubmissions.AsQueryable() : db.PromotionSubmissions.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
    }

    private static async Task<PromotionSubmission?> FindReadableAsync(SpmeDbContext db, HttpContext context, Guid id, CancellationToken ct)
    {
        if (context.User.IsInRole(SpmeRoles.Employee)) return await FindMineAsync(db, context, id, false, ct);
        var query = db.PromotionSubmissions.AsNoTracking().AsQueryable();
        if (!context.User.IsInRole(SpmeRoles.PlatformAdmin))
        {
            var institute = Institute(context);
            if (!institute.HasValue) return null;
            query = query.Where(x => x.InstituteId == institute.Value);
        }
        return await query.SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    private static async Task<List<PromotionRequirementResponse>> BuildRequirementsAsync(SpmeDbContext db, Guid id, CancellationToken ct)
    {
        var snapshots = await db.PromotionSubmissionRequirementSnapshots.AsNoTracking()
            .Where(x => x.PromotionSubmissionId == id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var reportIds = await db.PromotionSubmissionReports.AsNoTracking().Where(x => x.PromotionSubmissionId == id &&
            (x.Status == PromotionConstants.SubmissionReportReady || x.Status == PromotionConstants.SubmissionReportFinalized) &&
            x.ContentJson != "{\"schemaVersion\":1,\"sections\":[]}").Select(x => x.RequirementSnapshotId).ToListAsync(ct);
        var declarationIds = await db.PromotionSubmissionDeclarations.AsNoTracking().Where(x => x.PromotionSubmissionId == id)
            .Select(x => x.RequirementSnapshotId).ToListAsync(ct);
        var documentIds = await db.PromotionSubmissionDocuments.AsNoTracking().Where(x => x.PromotionSubmissionId == id &&
            x.DocumentStatus == PromotionConstants.DocumentAvailable).Select(x => x.RequirementSnapshotId).Distinct().ToListAsync(ct);
        return snapshots.Select(x =>
        {
            var complete = x.RequirementType switch
            {
                PromotionConstants.RequirementReport => reportIds.Contains(x.Id),
                PromotionConstants.RequirementDeclaration => declarationIds.Contains(x.Id),
                PromotionConstants.RequirementDocument => documentIds.Contains(x.Id),
                _ => false
            };
            return new PromotionRequirementResponse(x.Id, x.Code, x.RequirementType, x.Title, x.Description,
                x.IsRequired, x.DisplayOrder, x.ReportTemplateCode, x.DeclarationText,
                ParseContentTypes(x.AcceptedContentTypesJson), x.MaximumFileBytes, x.MaximumDocumentCount,
                complete ? "complete" : "not-started", complete ? null : "Required item is not complete.");
        }).ToList();
    }

    private static async Task StagePromotionEventAsync(SpmeDbContext db, PromotionSubmission submission,
        string status, Guid actorUserId, string? employeeVisibleNote, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        db.CommunicationOutboxMessages.Add(new CommunicationOutboxMessage("event", submission.InstituteId.ToString("N"),
            "promotion-submission.status.v1", JsonSerializer.Serialize(new { eventType = "promotion-submission.status.v1",
                submissionId = submission.Id, submission.InstituteId, submission.EmployeeId, status, actorUserId }), false,
            $"promotion-{status}", $"promotion-{status}:{submission.Id:N}:{now.UtcTicks}"));
        if (status == "submitted")
        {
            var recipients = await (from user in db.Users.AsNoTracking()
                                    join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                                    join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                                    where role.Name == SpmeRoles.HrAdmin && user.InstituteId == submission.InstituteId
                                    select user.Id).Distinct().ToListAsync(ct);
            foreach (var recipient in recipients)
                db.Notifications.Add(new Notification(recipient, "Promotion submission received",
                    "A staff promotion submission is ready for HR review.", $"/promotions/submissions/{submission.Id:D}"));
        }
        else
        {
            var recipient = await db.Users.AsNoTracking().Where(x => x.EmployeeId == submission.EmployeeId)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (recipient.HasValue)
                db.Notifications.Add(new Notification(recipient.Value, $"Promotion submission {status}",
                    employeeVisibleNote ?? $"Your promotion submission is now {status}.", $"/promotions/{submission.Id:D}"));
        }
    }

    private static async Task SyncStatusAsync(SpmeDbContext db, PromotionSubmission submission, CancellationToken ct)
    {
        var snapshot = await db.PromotionStatusSnapshots.SingleOrDefaultAsync(x =>
            x.EmployeeId == submission.EmployeeId && x.PromotionCycleId == submission.PromotionCycleId, ct);
        snapshot?.SyncSubmission(submission);
    }

    private static async Task<IResult> SaveResourceAsync(SpmeDbContext db, PromotionSubmission submission,
        HttpContext context, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Precondition(); }
        return await ResourceAsync(db, submission, context, ct);
    }

    private static async Task<IResult> ResourceAsync(SpmeDbContext db, PromotionSubmission submission,
        HttpContext context, CancellationToken ct)
    {
        var response = await MapAsync(db, submission, ct);
        SetEtag(context, response.Etag);
        return TypedResults.Ok(response);
    }

    private static async Task<List<PromotionSubmissionResponse>> MapManyAsync(SpmeDbContext db,
        IReadOnlyList<PromotionSubmission> submissions, CancellationToken ct)
    {
        var employeeSummaries = await LoadEmployeeSummariesAsync(db, submissions.Select(x => x.EmployeeId).Distinct().ToList(), ct);
        var result = new List<PromotionSubmissionResponse>(submissions.Count);
        foreach (var submission in submissions)
            result.Add(await MapAsync(db, submission, ct, employeeSummaries.GetValueOrDefault(submission.EmployeeId)));
        return result;
    }

    private static async Task<PromotionSubmissionResponse> MapAsync(SpmeDbContext db, PromotionSubmission submission,
        CancellationToken ct, EmployeeSummary? employeeSummary = null)
    {
        employeeSummary ??= (await LoadEmployeeSummariesAsync(db, [submission.EmployeeId], ct)).GetValueOrDefault(submission.EmployeeId);
        var requirements = await BuildRequirementsAsync(db, submission.Id, ct);
        var visibleDecisions = await db.PromotionDecisions.AsNoTracking().Where(x =>
            x.PromotionSubmissionId == submission.Id && x.EmployeeVisibleNote != null).ToListAsync(ct);
        var visibleNote = visibleDecisions.OrderByDescending(x => x.DecidedAt).Select(x => x.EmployeeVisibleNote).FirstOrDefault();
        var grades = await db.Grades.AsNoTracking()
            .Where(x => x.Id == submission.SourceGradeId || x.Id == submission.TargetGradeId)
            .Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct);
        var source = grades.SingleOrDefault(x => x.Id == submission.SourceGradeId);
        var target = grades.SingleOrDefault(x => x.Id == submission.TargetGradeId);
        return new PromotionSubmissionResponse(submission.Id, submission.PromotionAssessmentId, submission.PromotionCycleId,
            new PromotionGradeReference(source?.Code ?? string.Empty, source?.Name ?? string.Empty),
            new PromotionGradeReference(target?.Code ?? string.Empty, target?.Name ?? string.Empty),
            submission.EmployeeNote, submission.Status,
            requirements.Count(x => x.IsRequired && x.CompletionState == "complete"), requirements.Count(x => x.IsRequired),
            visibleNote, AvailableActions(submission.Status), submission.SubmittedAt, submission.ReturnedAt,
            submission.ClosedAt, ConcurrencyToken.Format(submission.RowVersion), submission.CreatedAt, submission.UpdatedAt,
            employeeSummary?.DisplayName, employeeSummary?.StaffId, employeeSummary?.DivisionName);
    }

    private static async Task<Dictionary<Guid, EmployeeSummary>> LoadEmployeeSummariesAsync(
        SpmeDbContext db, IReadOnlyList<Guid> employeeIds, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return [];

        var employees = await db.Employees.AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new { employee.Id, employee.StaffId, employee.PreferredName, employee.OtherNames, employee.Surname })
            .ToListAsync(ct);
        var employments = await db.EmploymentRecords.AsNoTracking()
            .Where(employment => employeeIds.Contains(employment.EmployeeId) && employment.IsCurrent)
            .Select(employment => new { employment.EmployeeId, employment.DivisionId })
            .ToListAsync(ct);
        var divisionIds = employments
            .Where(employment => employment.DivisionId.HasValue)
            .Select(employment => employment.DivisionId!.Value)
            .Distinct()
            .ToList();
        var divisions = divisionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Divisions.AsNoTracking()
                .Where(division => divisionIds.Contains(division.Id))
                .ToDictionaryAsync(division => division.Id, division => division.Name, ct);
        var divisionByEmployee = employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.First().DivisionId);

        var summaries = new Dictionary<Guid, EmployeeSummary>(employees.Count);
        foreach (var employee in employees)
        {
            string? divisionName = null;
            if (divisionByEmployee.TryGetValue(employee.Id, out var divisionId) &&
                divisionId is Guid resolvedDivisionId)
                divisions.TryGetValue(resolvedDivisionId, out divisionName);

            summaries[employee.Id] = new EmployeeSummary(
                employee.Id,
                employee.StaffId,
                employee.PreferredName ??
                string.Join(' ', new[] { employee.OtherNames, employee.Surname }.Where(value => !string.IsNullOrWhiteSpace(value))),
                divisionName);
        }

        return summaries;
    }

    private sealed record EmployeeSummary(Guid EmployeeId, string StaffId, string DisplayName, string? DivisionName);

    private static IReadOnlyList<string> AvailableActions(string status) => status switch
    {
        PromotionConstants.SubmissionDraft => ["edit", "submit", "withdraw"],
        PromotionConstants.SubmissionReturned => ["edit", "submit", "withdraw"],
        PromotionConstants.SubmissionSubmitted or PromotionConstants.SubmissionUnderReview or PromotionConstants.SubmissionAcknowledged => ["withdraw"],
        _ => []
    };

    private static bool SetExpected(SpmeDbContext db, PromotionSubmission entity, HttpContext context)
    {
        var ifMatch = context.Request.Headers[HeaderNames.IfMatch].ToString();
        if (!ConcurrencyToken.TryParse(ifMatch, out var expected)) return false;
        db.SetOriginalRowVersion(entity, expected);
        return true;
    }
    private static IResult Precondition() => EndpointProblems.FromError(Error.PreconditionFailed("A current If-Match ETag is required."));
    private static IResult NotFound() => EndpointProblems.FromError(Error.NotFound("Promotion submission not found."));
    private static void SetEtag(HttpContext context, string etag) => context.Response.Headers.ETag = etag;
    private static Guid? UserId(HttpContext context) => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Guid? Institute(HttpContext context) => Guid.TryParse(context.User.FindFirstValue("institute_id"), out var id) ? id : null;
    private static bool TrySelf(HttpContext context, out Guid employeeId)
    {
        employeeId = Guid.Empty;
        var value = context.User.FindFirstValue("self");
        return value?.StartsWith("Self:", StringComparison.Ordinal) == true && Guid.TryParse(value[5..], out employeeId);
    }
    private static bool TryIdentity(HttpContext context, out Guid userId, out Guid employeeId)
    {
        userId = UserId(context) ?? Guid.Empty;
        employeeId = Guid.Empty;
        return userId != Guid.Empty && TrySelf(context, out employeeId);
    }
    private static IReadOnlyList<string> ParseContentTypes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
    private static bool SignatureMatches(string contentType, ReadOnlySpan<byte> signature) => contentType.ToLowerInvariant() switch
    {
        "application/pdf" => signature.StartsWith("%PDF"u8),
        "image/png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        "image/jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
            signature.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
        _ => false
    };
    private static byte[] BuildPdf(string title, string content)
    {
        static string Safe(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal)
            .Select(ch => ch is >= ' ' and <= '~' ? ch : '?').Aggregate(new StringBuilder(), (b, ch) => b.Append(ch)).ToString();
        var text = Safe($"{title} - {content}"[..Math.Min(title.Length + 3 + content.Length, 1500)]);
        var body = $"BT /F1 10 Tf 50 760 Td ({text}) Tj ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(body)} >>\nstream\n{body}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString())); builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}

public sealed record CreatePromotionSubmissionRequest(Guid PromotionAssessmentId);
public sealed record UpdatePromotionSubmissionRequest(string? EmployeeNote);
public sealed record PromotionDecisionRequest(string? EmployeeVisibleNote, string? InternalNote);
public sealed record CreatePromotionDocumentUploadRequest(string RequirementCode, string FileName,
    string ContentType, long ByteLength, string Sha256Checksum);
public sealed record PromotionUploadSessionResponse(Guid Id, Uri UploadUrl, DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);
public sealed record PromotionDocumentResponse(Guid Id, string RequirementCode, string RequirementTitle,
    string OriginalFileName, string ContentType, long SizeBytes, string DocumentStatus, string ScanStatus,
    string? EmployeeVisibleReviewNote, DateTimeOffset CreatedAt);
public sealed record PromotionRequirementResponse(Guid Id, string Code, string RequirementType, string Title,
    string? Description, bool IsRequired, short DisplayOrder, string? ReportTemplateCode,
    string? DeclarationText, IReadOnlyList<string> AcceptedContentTypes, long? MaximumFileBytes,
    short? MaximumDocumentCount, string CompletionState, string? IncompleteReason);
public sealed record PromotionSubmissionResponse(Guid Id, Guid PromotionAssessmentId, Guid PromotionCycleId,
    PromotionGradeReference SourceGrade, PromotionGradeReference TargetGrade, string? EmployeeNote, string Status,
    int CompletedRequirements, int TotalRequirements, string? EmployeeVisibleReviewNote, IReadOnlyList<string> AllowedActions,
    DateTimeOffset? SubmittedAt, DateTimeOffset? ReturnedAt, DateTimeOffset? ClosedAt, string Etag,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? EmployeeDisplayName = null, string? StaffId = null,
    string? DivisionName = null);
public sealed record PromotionGradeReference(string Code, string Name);
