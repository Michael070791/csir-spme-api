using System.Security.Cryptography;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Knowledge;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace Csir.Spme.Application.Reporting;

public sealed class StaffQuarterlyReportService(
    IStaffQuarterlyReportRepository repository,
    IApplicationDbContext unitOfWork,
    IAuditService audit,
    IWorkflowNotificationOutbox notifications,
    ICurrentUserService currentUser,
    IDirectFileUploadService uploads,
    IFileStorageService storage,
    IPromotionMalwareScanner scanner,
    IOptions<StaffReportUploadOptions> uploadOptions)
{
    public async Task<Result<StaffQuarterlyReportOptions>> GetOptionsAsync(CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyReportOptions>.Failure(identity.Error!);

        var (employeeId, instituteId, _) = identity.Value!;
        var currentYear = DateTime.UtcNow.Year;
        await repository.EnsureOpenCurrentYearQuartersAsync(instituteId, currentYear, ct);
        var periods = await repository.ListOpenQuarterlyPeriodsAsync(instituteId, ct);
        var projects = await repository.ListProjectOptionsAsync(instituteId, ct);
        var inceptions = await repository.FindProjectInceptionsAsync(projects.Select(item => item.Id).ToList(), ct);
        var technologies = await repository.ListTechnologyOptionsAsync(instituteId, ct);
        var reviewers = await repository.ListReviewerOptionsAsync(employeeId, instituteId, ct);

        return Result<StaffQuarterlyReportOptions>.Success(new(
            periods.Select(period => new StaffQuarterlyPeriodOption(period.Id, period.Code, period.Name,
                period.StartDate, period.EndDate, period.DueDate)).ToList(),
            projects.Select(project => new StaffQuarterlyCatalogOption(project.Id, project.Code, project.Name,
                project.Status, inceptions.TryGetValue(project.Id, out var inception) && inception.IsComplete)).ToList(),
            technologies.Select(technology => new StaffQuarterlyCatalogOption(technology.Id, technology.Code,
                technology.Name, technology.Status)).ToList(),
            reviewers.Select(MapReviewerOption).ToList()));
    }

    public async Task<Result<IReadOnlyList<StaffQuarterlyReviewerOption>>> SearchReviewersAsync(
        string? query, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<IReadOnlyList<StaffQuarterlyReviewerOption>>.Failure(identity.Error!);

        var (employeeId, instituteId, _) = identity.Value!;
        var reviewers = await repository.SearchStaffReviewerCandidatesAsync(
            instituteId, employeeId, query, ct);
        return Result<IReadOnlyList<StaffQuarterlyReviewerOption>>.Success(
            reviewers.Select(MapReviewerOption).ToList());
    }

    public async Task<Result<IReadOnlyList<StaffQuarterlyReportResponse>>> ListMineAsync(CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<IReadOnlyList<StaffQuarterlyReportResponse>>.Failure(identity.Error!);

        var reports = await repository.ListMineAsync(identity.Value!.EmployeeId, ct);
        return Result<IReadOnlyList<StaffQuarterlyReportResponse>>.Success(await MapManyAsync(reports, ct));
    }

    public async Task<Result<IReadOnlyList<StaffQuarterlyReportResponse>>> ListReviewQueueAsync(CancellationToken ct)
    {
        var reviewer = RequireReviewerIdentity();
        if (reviewer.IsFailure)
            return Result<IReadOnlyList<StaffQuarterlyReportResponse>>.Failure(reviewer.Error!);
        var reports = await repository.ListForReviewerAsync(reviewer.Value!.UserId,
            reviewer.Value.InstituteId, ct);
        return Result<IReadOnlyList<StaffQuarterlyReportResponse>>.Success(await MapManyAsync(reports, ct));
    }

    public async Task<Result<StaffQuarterlyReportResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var report = await repository.FindAggregateAsync(id, ct);
        if (!CanRead(report))
            return Result<StaffQuarterlyReportResponse>.Failure(Error.NotFound("Quarterly report not found."));
        return Result<StaffQuarterlyReportResponse>.Success(await MapAsync(report!, ct));
    }

    public async Task<Result<StaffQuarterlyProjectInceptionResponse>> GetProjectInceptionAsync(
        Guid projectId, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(identity.Error!);

        var project = await repository.FindProjectByIdAsync(identity.Value!.InstituteId, projectId, ct);
        if (project is null)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.NotFound("Project not found."));

        if (!await repository.CanReadProjectAsync(projectId, identity.Value.EmployeeId, currentUser.UserId, ct))
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.NotFound("Project not found."));

        return Result<StaffQuarterlyProjectInceptionResponse>.Success(
            await MapInceptionAsync(project, await repository.FindProjectInceptionAsync(projectId, ct), ct));
    }

    public async Task<Result<StaffQuarterlyReportResponse>> CreateAsync(
        SaveStaffQuarterlyReportCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(identity.Error!);
        var (employeeId, instituteId, _) = identity.Value!;
        var validated = await ValidateAsync(command, employeeId, instituteId, null, ct);
        if (validated.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(validated.Error!);
        var value = validated.Value!;

        var report = Report.CreateStaffQuarterly(instituteId, employeeId, value.Reviewer.Employee.Id,
            value.Reviewer.User.Id, value.Period.Id, command.Title.Trim(), command.WorkSummary.Trim(),
            Normalize(command.Abstract), Normalize(command.KeyResults), Normalize(command.ConclusionNextSteps));
        repository.Add(report);
        repository.ReplaceProjects(report.Id, value.ProjectProgress);
        repository.ReplaceTechnologies(report.Id, value.Technologies.Select(technology => technology.Id).ToList());
        await audit.RecordAsync("staff-quarterly-report.created", "Report", report.Id.ToString(), null,
            $"period={report.ReportingPeriodId};owner={employeeId}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(report.Id, ct);
    }

    public async Task<Result<StaffQuarterlyReportResponse>> UpdateAsync(
        Guid id, SaveStaffQuarterlyReportCommand command, byte[]? expectedRowVersion, CancellationToken ct)
    {
        if (expectedRowVersion is null)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(identity.Error!);
        var (employeeId, instituteId, _) = identity.Value!;
        var aggregate = await repository.FindAggregateAsync(id, ct);
        if (aggregate is null || aggregate.Report.OwnerEmployeeId != employeeId || aggregate.Report.InstituteId != instituteId)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.NotFound("Quarterly report not found."));

        var validated = await ValidateAsync(command, employeeId, instituteId, id, ct);
        if (validated.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(validated.Error!);
        var value = validated.Value!;
        var before = Snapshot(aggregate.Report);
        var updated = aggregate.Report.UpdateStaffQuarterly(value.Period.Id, value.Reviewer.Employee.Id,
            value.Reviewer.User.Id, command.Title.Trim(), command.WorkSummary.Trim(),
            Normalize(command.Abstract), Normalize(command.KeyResults), Normalize(command.ConclusionNextSteps));
        if (updated.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(updated.Error!);
        repository.ReplaceProjects(id, value.ProjectProgress);
        repository.ReplaceTechnologies(id, value.Technologies.Select(technology => technology.Id).ToList());
        unitOfWork.SetOriginalRowVersion(aggregate.Report, expectedRowVersion);
        try
        {
            await audit.RecordAsync("staff-quarterly-report.updated", "Report", id.ToString(), before,
                Snapshot(aggregate.Report), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<StaffQuarterlyReportResponse>.Failure(Error.PreconditionFailed(
                "The quarterly report was modified by another request. Reload it and retry."));
        }
        return await GetAsync(id, ct);
    }

    public async Task<Result<StaffQuarterlyReportResponse>> SubmitAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct)
    {
        if (expectedRowVersion is null)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(identity.Error!);
        var aggregate = await repository.FindAggregateAsync(id, ct);
        if (aggregate is null || aggregate.Report.OwnerEmployeeId != identity.Value!.EmployeeId ||
            aggregate.Report.InstituteId != identity.Value.InstituteId)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.NotFound("Quarterly report not found."));

        var openPeriods = await repository.ListOpenQuarterlyPeriodsAsync(aggregate.Report.InstituteId, ct);
        if (!openPeriods.Any(period => period.Id == aggregate.Report.ReportingPeriodId))
            return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict("The quarterly reporting period is not open."));
        var reviewer = await repository.FindEligibleReviewerAsync(aggregate.Report.OwnerEmployeeId!.Value,
            aggregate.Report.InstituteId, aggregate.Report.ReviewerUserId!.Value, ct);
        if (reviewer is null)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict(
                "The selected HOD is no longer eligible or does not have verified delivery contacts."));
        if (aggregate.Projects.Count == 0 && aggregate.Technologies.Count == 0)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict(
                "Select at least one project or technology before submitting."));

        var inceptions = await repository.FindProjectInceptionsAsync(
            aggregate.Projects.Select(project => project.Id).ToList(), ct);
        foreach (var project in aggregate.Projects)
        {
            if (!inceptions.TryGetValue(project.Id, out var inception) || !inception.IsComplete)
                return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict(
                    "Every linked project must have a completed Form 1 before submission."));
            var link = aggregate.ReportProjects.Single(item => item.ProjectId == project.Id);
            if (string.IsNullOrWhiteSpace(link.ProgressSummary))
                return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict(
                    "Every linked project must include Form 2 progress before submission."));
        }

        var conceptNoteIds = inceptions.Values.Select(item => item.ConceptNoteFileId).OfType<Guid>().Distinct().ToList();
        var conceptNoteFiles = await repository.FindFileRecordsAsync(conceptNoteIds, ct);
        if (conceptNoteFiles.Count != conceptNoteIds.Count)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.Conflict(
                "A linked Form 1 concept note is unavailable."));
        var attachmentValidation = ValidateAttachmentsForSubmit(
            aggregate.AttachmentFiles.Concat(conceptNoteFiles).ToList());
        if (attachmentValidation.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(attachmentValidation.Error!);

        var now = DateTimeOffset.UtcNow;
        var transitioned = aggregate.Report.Submit(identity.Value.UserId, now);
        if (transitioned.IsFailure)
            return Result<StaffQuarterlyReportResponse>.Failure(transitioned.Error!);

        var leadNames = await LoadLeadNamesAsync(aggregate.Projects, ct);
        foreach (var link in aggregate.ReportProjects)
        {
            var project = aggregate.Projects.Single(item => item.Id == link.ProjectId);
            link.CaptureSnapshot(project.Code, project.Name);
            if (inceptions.TryGetValue(project.Id, out var inception))
            {
                link.CaptureForm1Snapshot(
                    project.Code,
                    project.Name,
                    leadNames.GetValueOrDefault(project.LeadEmployeeId ?? Guid.Empty, "Unknown"),
                    inception.EstimatedDuration,
                    inception.SponsorName,
                    inception.Location,
                    inception.CollaboratingInstitute,
                    inception.ParticipatingScientists,
                    project.Objective,
                    project.Method,
                    project.Justification,
                    inception.ExpectedBeneficiaries,
                    inception.PotentialTechnology,
                    inception.ContributionToKnowledge);
            }
        }
        foreach (var link in aggregate.ReportTechnologies)
        {
            var technology = aggregate.Technologies.Single(item => item.Id == link.TechnologyId);
            link.CaptureSnapshot(technology.Code, technology.Name);
        }
        var projectReports = aggregate.ReportProjects.Select(link => new StaffQuarterlyProjectReportContent(
            link.ProjectCodeSnapshot ?? string.Empty,
            link.ProjectNameSnapshot ?? string.Empty,
            link.SnapshotLeadName ?? "Unknown",
            link.SnapshotEstimatedDuration ?? string.Empty,
            link.SnapshotSponsorName ?? string.Empty,
            link.SnapshotLocation ?? string.Empty,
            link.SnapshotObjective ?? string.Empty,
            link.SnapshotMethod,
            link.SnapshotJustification,
            link.SnapshotExpectedBeneficiaries,
            link.SnapshotPotentialTechnology,
            link.SnapshotContributionToKnowledge,
            link.ProgressSummary ?? string.Empty,
            link.ProgressKeyResults,
            link.Challenges,
            link.NextQuarterActivities,
            link.WayForward,
            link.ConferencePapersProduced,
            link.IpTechnologiesProtected)).ToList();
        unitOfWork.SetOriginalRowVersion(aggregate.Report, expectedRowVersion);
        var notification = new StaffQuarterlyReportNotification(
            aggregate.Report.Id, aggregate.Report.InstituteId, aggregate.Owner.Id, reviewer.User.Id,
            DisplayName(reviewer.Employee), ReviewerEmail(reviewer)!, ReviewerPhone(reviewer)!,
            DisplayName(aggregate.Owner), aggregate.Period.Name, aggregate.Report.Title,
            aggregate.Report.Abstract, aggregate.Report.Summary, aggregate.Report.KeyResults,
            aggregate.Report.Conclusion, aggregate.Projects.Select(project => project.Name).ToList(),
            aggregate.Technologies.Select(technology => technology.Name).ToList(), projectReports,
            aggregate.AttachmentFiles.Select(file => file.OriginalFileName).ToList(), now);
        await notifications.StageStaffQuarterlyReportSubmittedAsync(notification, ct);
        return await SaveTransitionAsync(aggregate.Report, "staff-quarterly-report.submitted", ct);
    }

    public async Task<Result<StaffQuarterlyReportResponse>> ApproveAsync(
        Guid id, byte[]? expectedRowVersion, CancellationToken ct) =>
        await ReviewerTransitionAsync(id, expectedRowVersion, "staff-quarterly-report.approved",
            report => report.Approve(currentUser.UserId ?? Guid.Empty, DateTimeOffset.UtcNow), "approved", ct);

    public async Task<Result<StaffQuarterlyReportResponse>> ReturnAsync(
        Guid id, string returnReason, byte[]? expectedRowVersion, CancellationToken ct) =>
        await ReviewerTransitionAsync(id, expectedRowVersion, "staff-quarterly-report.returned",
            report => report.Return(returnReason), "returned", ct, returnReason);

    public async Task<Result<StaffQuarterlyCatalogOption>> CreateProjectDraftAsync(
        CreateStaffQuarterlyProjectDraftCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyCatalogOption>.Failure(identity.Error!);

        var fields = StaffQuarterlyReportSupport.ValidateInception(command.Inception);
        if (fields.Count > 0)
            return Result<StaffQuarterlyCatalogOption>.Failure(Error.Validation(fields));

        var existing = await repository.FindProjectByCodeOrNameAsync(identity.Value!.InstituteId,
            command.Inception.Code.Trim(), command.Inception.Name.Trim(), ct);
        if (existing is not null)
        {
            var existingInception = await repository.FindProjectInceptionAsync(existing.Id, ct);
            return Result<StaffQuarterlyCatalogOption>.Success(new(existing.Id, existing.Code, existing.Name,
                existing.Status, existingInception?.IsComplete ?? false, true));
        }

        return await CreateInceptionProjectAsync(identity.Value, command.Inception, ct);
    }

    public async Task<Result<StaffQuarterlyProjectInceptionResponse>> UpsertProjectInceptionAsync(
        Guid projectId, SaveStaffQuarterlyProjectInceptionCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(identity.Error!);

        var fields = StaffQuarterlyReportSupport.ValidateInception(command);
        if (fields.Count > 0)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.Validation(fields));

        var project = await repository.FindProjectForUpdateAsync(identity.Value!.InstituteId, projectId, ct);
        if (project is null)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.NotFound("Project not found."));

        var inception = await repository.FindProjectInceptionForUpdateAsync(projectId, ct);
        if (inception?.IsComplete == true)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.StateTransition(
                "Form 1 is locked after inception is completed."));

        var lead = await repository.FindEmployeeAsync(command.LeadEmployeeId, ct);
        if (lead is null || lead.InstituteId != identity.Value.InstituteId)
        {
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["leadEmployeeId"] = ["The principal investigator must belong to your institute."]
            }));
        }

        var currency = command.Currency.Trim().ToUpperInvariant();
        var updated = project.Update(command.Name.Trim(), command.Objective.Trim(), command.Justification.Trim(),
            command.Method.Trim(), null, null, command.Nature, command.StartDate, command.EndDate, currency,
            command.BudgetAmount, null, null, command.LeadEmployeeId, null);
        if (updated.IsFailure)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(updated.Error!);

        if (inception is null)
        {
            inception = ProjectInception.Create(projectId);
            repository.Add(inception);
        }

        var draftUpdated = inception.UpdateDraft(command.EstimatedDuration.Trim(), command.SponsorName.Trim(),
            command.Location.Trim(), command.CollaboratingInstitute, command.ParticipatingScientists,
            command.ExpectedBeneficiaries, command.PotentialTechnology, command.ContributionToKnowledge);
        if (draftUpdated.IsFailure)
            return Result<StaffQuarterlyProjectInceptionResponse>.Failure(draftUpdated.Error!);

        if (command.CompleteInception)
        {
            var completed = inception.Complete(DateTimeOffset.UtcNow);
            if (completed.IsFailure)
                return Result<StaffQuarterlyProjectInceptionResponse>.Failure(completed.Error!);
        }

        await audit.RecordAsync("staff-quarterly-report.project-inception-saved", "Project", projectId.ToString(),
            null, $"complete={command.CompleteInception}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StaffQuarterlyProjectInceptionResponse>.Success(
            await MapInceptionAsync(project, inception, ct));
    }

    public async Task<Result<StaffQuarterlyCatalogOption>> CreateTechnologyDraftAsync(
        CreateStaffQuarterlyTechnologyDraftCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyCatalogOption>.Failure(identity.Error!);
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 64) fields["code"] = ["A code of at most 64 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 256) fields["name"] = ["A name of at most 256 characters is required."];
        if (string.IsNullOrWhiteSpace(command.Description) || command.Description.Length > 4000) fields["description"] = ["A description of at most 4000 characters is required."];
        if (string.IsNullOrWhiteSpace(command.ApplicationArea) || command.ApplicationArea.Length > 256) fields["applicationArea"] = ["An application area of at most 256 characters is required."];
        if (string.IsNullOrWhiteSpace(command.TechnologyType) || command.TechnologyType.Length > 64) fields["technologyType"] = ["A technology type of at most 64 characters is required."];
        if (fields.Count > 0) return Result<StaffQuarterlyCatalogOption>.Failure(Error.Validation(fields));
        var existing = await repository.FindTechnologyByCodeOrNameAsync(identity.Value!.InstituteId,
            command.Code.Trim(), command.Name.Trim(), ct);
        if (existing is not null) return Result<StaffQuarterlyCatalogOption>.Success(new(existing.Id, existing.Code, existing.Name, existing.Status));
        var technology = Technology.Create(identity.Value.InstituteId, command.Code.Trim(), command.Name.Trim(),
            command.Description.Trim(), command.ApplicationArea.Trim(), identity.Value.EmployeeId,
            command.TechnologyType.Trim(), command.YearIntroduced, command.HasIntellectualProperty);
        repository.Add(technology);
        await audit.RecordAsync("staff-quarterly-report.technology-draft-created", "Technology", technology.Id.ToString(), null,
            $"code={technology.Code};owner={identity.Value.EmployeeId}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StaffQuarterlyCatalogOption>.Success(new(technology.Id, technology.Code, technology.Name, technology.Status));
    }

    public async Task<Result<StaffQuarterlyUploadSessionResponse>> CreateConceptNoteUploadSessionAsync(
        Guid projectId, CreateStaffQuarterlyUploadSessionCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(identity.Error!);

        var project = await repository.FindProjectByIdAsync(identity.Value!.InstituteId, projectId, ct);
        if (project is null)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.NotFound("Project not found."));

        var inception = await repository.FindProjectInceptionAsync(projectId, ct);
        if (inception?.IsComplete == true)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.Conflict(
                "Form 1 is locked after inception is completed."));

        var options = uploadOptions.Value;
        if (!StaffQuarterlyReportSupport.IsConceptNoteContentType(command.ContentType) ||
            !StaffQuarterlyReportSupport.FileNameMatchesContentType(command.FileName, command.ContentType) ||
            command.ByteLength <= 0 || command.ByteLength > options.ConceptNoteMaximumFileBytes ||
            !StaffQuarterlyReportSupport.IsSha256(command.Sha256Checksum))
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.Validation(
                "The concept note metadata, type, size, or SHA-256 checksum is invalid."));

        return await CreateUploadSessionAsync(identity.Value, StaffReportUploadKinds.ConceptNote, null, projectId,
            command, options.ConceptNoteMaximumFileBytes, ct);
    }

    public async Task<Result<StaffQuarterlyUploadSessionResponse>> CreateImageUploadSessionAsync(
        Guid reportId, CreateStaffQuarterlyUploadSessionCommand command, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(identity.Error!);

        var aggregate = await repository.FindAggregateAsync(reportId, ct);
        if (aggregate is null || aggregate.Report.OwnerEmployeeId != identity.Value!.EmployeeId ||
            aggregate.Report.InstituteId != identity.Value.InstituteId || !aggregate.Report.IsEditable)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.NotFound("Quarterly report not found."));

        var options = uploadOptions.Value;
        if (!StaffQuarterlyReportSupport.IsImageContentType(command.ContentType) ||
            !StaffQuarterlyReportSupport.FileNameMatchesContentType(command.FileName, command.ContentType) ||
            command.ByteLength <= 0 || command.ByteLength > options.ImageMaximumFileBytes ||
            !StaffQuarterlyReportSupport.IsSha256(command.Sha256Checksum))
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.Validation(
                "The image metadata, type, size, or SHA-256 checksum is invalid."));

        if (await repository.CountReportImagesAsync(reportId, ct) >= options.MaximumImagesPerReport)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.Conflict(
                "A quarterly report may include at most three images."));

        return await CreateUploadSessionAsync(identity.Value, StaffReportUploadKinds.ReportImage, reportId, null,
            command, options.ImageMaximumFileBytes, ct);
    }

    public async Task<Result<StaffQuarterlyFileMetadata>> CompleteUploadAsync(
        Guid sessionId, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<StaffQuarterlyFileMetadata>.Failure(identity.Error!);

        var session = await repository.FindUploadSessionAsync(sessionId, ct);
        if (session is null || session.EmployeeId != identity.Value!.EmployeeId ||
            session.InitiatedByUserId != identity.Value.UserId)
            return Result<StaffQuarterlyFileMetadata>.Failure(Error.NotFound("Upload session not found."));

        if (session.UploadKind == StaffReportUploadKinds.ReportImage)
        {
            var aggregate = await repository.FindAggregateAsync(session.ReportId!.Value, ct);
            if (aggregate is null || !aggregate.Report.IsEditable)
                return Result<StaffQuarterlyFileMetadata>.Failure(Error.Conflict("The report is not editable."));
            if (await repository.CountReportImagesAsync(session.ReportId.Value, ct) >=
                uploadOptions.Value.MaximumImagesPerReport)
                return Result<StaffQuarterlyFileMetadata>.Failure(Error.Conflict(
                    "A quarterly report may include at most three images."));
        }
        else if (session.UploadKind == StaffReportUploadKinds.ConceptNote)
        {
            var inception = await repository.FindProjectInceptionAsync(session.ProjectId!.Value, ct);
            if (inception?.IsComplete == true)
                return Result<StaffQuarterlyFileMetadata>.Failure(Error.Conflict("Form 1 is locked."));
        }

        var inspected = await uploads.InspectAsync(session.StorageKey, ct);
        if (inspected is null || inspected.SizeBytes != session.DeclaredSizeBytes ||
            inspected.ContentType is not null &&
            !string.Equals(inspected.ContentType, session.ContentType, StringComparison.OrdinalIgnoreCase))
            return Result<StaffQuarterlyFileMetadata>.Failure(Error.Validation(
                "Uploaded content does not match the declared size and SHA-256 checksum."));

        await using var uploadedStream = await storage.DownloadAsync(session.StorageKey, ct);
        if (uploadedStream is null)
            return Result<StaffQuarterlyFileMetadata>.Failure(Error.Validation("The uploaded content could not be verified."));

        var signature = new byte[8];
        var signatureLength = await uploadedStream.ReadAsync(signature, ct);
        if (!uploadedStream.CanSeek)
            return Result<StaffQuarterlyFileMetadata>.Failure(Error.Validation("The uploaded content cannot be securely inspected."));
        uploadedStream.Position = 0;
        var actualSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(uploadedStream, ct));
        if (!string.Equals(actualSha256, session.DeclaredSha256, StringComparison.OrdinalIgnoreCase) ||
            !StaffQuarterlyReportSupport.SignatureMatches(session.ContentType, signature.AsSpan(0, signatureLength)))
            return Result<StaffQuarterlyFileMetadata>.Failure(Error.Validation(
                "Uploaded content does not match the declared type or SHA-256 checksum."));

        var file = new FileRecord(session.StorageKey, session.FileName, session.ContentType,
            inspected.SizeBytes, session.DeclaredSha256, "staff-quarterly-report", session.InstituteId, "confidential");
        repository.Add(file);
        var scan = await scanner.ScanAsync(session.StorageKey, ct);
        file.MarkScanStatus(scan);

        await using var imageTransaction = session.UploadKind == StaffReportUploadKinds.ReportImage
            ? await repository.BeginSerializableTransactionAsync(ct)
            : null;
        if (session.UploadKind == StaffReportUploadKinds.ConceptNote)
        {
            var inception = await repository.FindProjectInceptionForUpdateAsync(session.ProjectId!.Value, ct);
            if (inception is null)
            {
                inception = ProjectInception.Create(session.ProjectId!.Value);
                repository.Add(inception);
            }
            else if (inception.IsComplete)
                return Result<StaffQuarterlyFileMetadata>.Failure(Error.Conflict("Form 1 is locked."));
            if (inception.ConceptNoteFileId is Guid previousFileId)
            {
                var previousFile = await repository.FindFileRecordForUpdateAsync(previousFileId, ct);
                previousFile?.MarkDeleted(DateTimeOffset.UtcNow);
            }
            var attached = inception.AttachConceptNote(file.Id);
            if (attached.IsFailure)
                return Result<StaffQuarterlyFileMetadata>.Failure(attached.Error!);
        }
        else
        {
            if (await repository.CountReportImagesAsync(session.ReportId!.Value, ct) >=
                uploadOptions.Value.MaximumImagesPerReport)
                return Result<StaffQuarterlyFileMetadata>.Failure(Error.Conflict(
                    "A quarterly report may include at most three images."));
            repository.Add(new ReportAttachment(session.ReportId!.Value, file.Id, StaffReportAttachmentTypes.ReportImage));
        }

        var completed = session.Complete(file.Id, DateTimeOffset.UtcNow);
        if (completed.IsFailure)
            return Result<StaffQuarterlyFileMetadata>.Failure(completed.Error!);

        await audit.RecordAsync("staff-quarterly-report.upload-completed", "FileRecord", file.Id.ToString(), null,
            $"kind={session.UploadKind};scan={file.ScanStatus}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (imageTransaction is not null)
            await imageTransaction.CommitAsync(ct);
        return Result<StaffQuarterlyFileMetadata>.Success(StaffQuarterlyReportSupport.MapFile(file));
    }

    public async Task<Result<bool>> RemoveConceptNoteAsync(Guid projectId, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<bool>.Failure(identity.Error!);

        var project = await repository.FindProjectByIdAsync(identity.Value!.InstituteId, projectId, ct);
        if (project is null)
            return Result<bool>.Failure(Error.NotFound("Project not found."));

        var inception = await repository.FindProjectInceptionForUpdateAsync(projectId, ct);
        if (inception is null || inception.ConceptNoteFileId is null)
            return Result<bool>.Failure(Error.NotFound("Concept note not found."));
        if (inception.IsComplete)
            return Result<bool>.Failure(Error.Conflict("Form 1 is locked after inception is completed."));

        var file = await repository.FindFileRecordForUpdateAsync(inception.ConceptNoteFileId.Value, ct);
        file?.MarkDeleted(DateTimeOffset.UtcNow);
        var removed = inception.RemoveConceptNote();
        if (removed.IsFailure)
            return Result<bool>.Failure(removed.Error!);

        await audit.RecordAsync("staff-quarterly-report.concept-note-removed", "Project", projectId.ToString(), null, null, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RemoveImageAsync(Guid reportId, Guid fileId, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<bool>.Failure(identity.Error!);

        var aggregate = await repository.FindAggregateAsync(reportId, ct);
        if (aggregate is null || aggregate.Report.OwnerEmployeeId != identity.Value!.EmployeeId ||
            aggregate.Report.InstituteId != identity.Value.InstituteId || !aggregate.Report.IsEditable)
            return Result<bool>.Failure(Error.NotFound("Quarterly report not found."));

        var attachment = aggregate.Attachments.SingleOrDefault(item => item.FileId == fileId);
        if (attachment is null)
            return Result<bool>.Failure(Error.NotFound("Report image not found."));

        repository.RemoveAttachment(attachment);
        var file = aggregate.AttachmentFiles.Single(item => item.Id == fileId);
        file.MarkDeleted(DateTimeOffset.UtcNow);
        await audit.RecordAsync("staff-quarterly-report.image-removed", "Report", reportId.ToString(), null,
            $"file={fileId}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<(Stream Stream, string ContentType, string FileName)>> DownloadFileAsync(
        Guid fileId, CancellationToken ct)
    {
        var identity = RequireEmployeeIdentity();
        if (identity.IsFailure)
            return Result<(Stream, string, string)>.Failure(identity.Error!);

        var file = await repository.FindFileRecordAsync(fileId, ct);
        if (file is null || file.InstituteId != identity.Value!.InstituteId)
            return Result<(Stream, string, string)>.Failure(Error.NotFound("File not found."));

        var mine = await repository.ListMineAsync(identity.Value.EmployeeId, ct);
        var reviewer = currentUser.UserId.HasValue
            ? await repository.ListForReviewerAsync(currentUser.UserId.Value, identity.Value.InstituteId, ct)
            : [];
        var allowed = mine.Concat(reviewer).Any(aggregate =>
            aggregate.AttachmentFiles.Any(item => item.Id == fileId));
        if (!allowed)
        {
            var projects = await repository.ListProjectOptionsAsync(identity.Value.InstituteId, ct);
            var inceptions = await repository.FindProjectInceptionsAsync(projects.Select(item => item.Id).ToList(), ct);
            foreach (var pair in inceptions.Where(item => item.Value.ConceptNoteFileId == fileId))
            {
                if (await repository.CanReadProjectAsync(pair.Key, identity.Value.EmployeeId, currentUser.UserId, ct))
                {
                    allowed = true;
                    break;
                }
            }
        }

        if (!allowed)
            return Result<(Stream, string, string)>.Failure(Error.NotFound("File not found."));

        if (file.ScanStatus is "infected" or "quarantined")
            return Result<(Stream, string, string)>.Failure(Error.Forbidden("The file is not available for download."));

        var download = await storage.DownloadAsync(file.StorageKey, ct);
        if (download is null)
            return Result<(Stream, string, string)>.Failure(Error.NotFound("File not found."));

        return Result<(Stream, string, string)>.Success((download, file.ContentType, file.OriginalFileName));
    }

    private async Task<Result<StaffQuarterlyCatalogOption>> CreateInceptionProjectAsync(
        (Guid EmployeeId, Guid InstituteId, Guid UserId) identity,
        SaveStaffQuarterlyProjectInceptionCommand command,
        CancellationToken ct)
    {
        var lead = await repository.FindEmployeeAsync(command.LeadEmployeeId, ct);
        if (lead is null || lead.InstituteId != identity.InstituteId)
        {
            return Result<StaffQuarterlyCatalogOption>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                ["leadEmployeeId"] = ["The principal investigator must belong to your institute."]
            }));
        }

        var currency = command.Currency.Trim().ToUpperInvariant();
        var project = Project.Create(identity.InstituteId, command.Code.Trim(), command.Name.Trim(),
            command.Objective.Trim(), command.Justification.Trim(), command.Method.Trim(), null, command.Nature,
            command.StartDate, command.EndDate, currency, command.BudgetAmount, null, null,
            command.LeadEmployeeId, null);
        repository.Add(project);
        var inception = ProjectInception.Create(project.Id);
        repository.Add(inception);
        var draftUpdated = inception.UpdateDraft(command.EstimatedDuration.Trim(), command.SponsorName.Trim(),
            command.Location.Trim(), command.CollaboratingInstitute, command.ParticipatingScientists,
            command.ExpectedBeneficiaries, command.PotentialTechnology, command.ContributionToKnowledge);
        if (draftUpdated.IsFailure)
            return Result<StaffQuarterlyCatalogOption>.Failure(draftUpdated.Error!);

        if (command.CompleteInception)
        {
            var completed = inception.Complete(DateTimeOffset.UtcNow);
            if (completed.IsFailure)
                return Result<StaffQuarterlyCatalogOption>.Failure(completed.Error!);
        }

        await audit.RecordAsync("staff-quarterly-report.project-draft-created", "Project", project.Id.ToString(), null,
            $"code={project.Code};complete={command.CompleteInception}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StaffQuarterlyCatalogOption>.Success(new(project.Id, project.Code, project.Name, project.Status,
            inception.IsComplete));
    }

    private async Task<Result<StaffQuarterlyUploadSessionResponse>> CreateUploadSessionAsync(
        (Guid EmployeeId, Guid InstituteId, Guid UserId) identity,
        string uploadKind,
        Guid? reportId,
        Guid? projectId,
        CreateStaffQuarterlyUploadSessionCommand command,
        long maxBytes,
        CancellationToken ct)
    {
        if (command.ByteLength > maxBytes)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.Validation("The file exceeds the configured maximum size."));

        var options = uploadOptions.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(options.UploadSessionMinutes, 5, 1440));
        var fileName = Path.GetFileName(command.FileName);
        var storageKey = $"staff-quarterly-reports/{identity.InstituteId:N}/{uploadKind}/{Guid.NewGuid():N}/{fileName}";
        var access = await uploads.CreateWriteAccessAsync(storageKey, command.ContentType, command.ByteLength,
            command.Sha256Checksum, expires, ct);
        if (access is null)
            return Result<StaffQuarterlyUploadSessionResponse>.Failure(Error.DependencyUnavailable(
                "Direct upload storage is not configured."));

        var session = new StaffQuarterlyReportUploadSession(identity.InstituteId, identity.EmployeeId,
            identity.UserId, uploadKind, reportId, projectId, storageKey, fileName, command.ContentType,
            command.ByteLength, command.Sha256Checksum.ToLowerInvariant(), expires);
        repository.Add(session);
        await audit.RecordAsync("staff-quarterly-report.upload-session-created", "StaffQuarterlyReportUploadSession",
            session.Id.ToString(), null, $"kind={uploadKind};bytes={command.ByteLength}", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<StaffQuarterlyUploadSessionResponse>.Success(new(session.Id, access.UploadUri, access.ExpiresAt,
            access.RequiredHeaders));
    }

    private async Task<Result<ValidatedSave>> ValidateAsync(
        SaveStaffQuarterlyReportCommand command, Guid employeeId, Guid instituteId, Guid? excludeId, CancellationToken ct)
    {
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 512) fields["title"] = ["A title of at most 512 characters is required."];
        if (string.IsNullOrWhiteSpace(command.WorkSummary) || command.WorkSummary.Length > 4000) fields["workSummary"] = ["A work summary of at most 4000 characters is required."];
        if (command.Abstract?.Length > 4000) fields["abstract"] = ["Abstract cannot exceed 4000 characters."];
        if (command.KeyResults?.Length > 4000) fields["keyResults"] = ["Key results cannot exceed 4000 characters."];
        if (command.ConclusionNextSteps?.Length > 4000) fields["conclusionNextSteps"] = ["Conclusion and next steps cannot exceed 4000 characters."];
        if (command.ProjectIds.Concat(command.TechnologyIds).Any() is false) fields["projectIds"] = ["Select at least one project or technology."];
        if (fields.Count > 0) return Result<ValidatedSave>.Failure(Error.Validation(fields));

        await repository.EnsureOpenCurrentYearQuartersAsync(instituteId, DateTime.UtcNow.Year, ct);
        var period = (await repository.ListOpenQuarterlyPeriodsAsync(instituteId, ct))
            .SingleOrDefault(candidate => candidate.Id == command.ReportingPeriodId);
        if (period is null) fields["reportingPeriodId"] = ["Select an open quarterly reporting period."];
        var reviewer = await repository.FindEligibleReviewerAsync(employeeId, instituteId, command.ReviewerUserId, ct);
        if (reviewer is null)
            fields["reviewerUserId"] = ["Select a reviewer from your HOD list or search for another staff member in your institute."];

        var projectIds = command.ProjectIds.Distinct().ToList();
        var technologyIds = command.TechnologyIds.Distinct().ToList();
        var projects = await repository.FindProjectsAsync(instituteId, projectIds, ct);
        var technologies = await repository.FindTechnologiesAsync(instituteId, technologyIds, ct);
        if (projects.Count != projectIds.Count) fields["projectIds"] = ["One or more selected projects are unavailable."];
        if (technologies.Count != technologyIds.Count) fields["technologyIds"] = ["One or more selected technologies are unavailable."];

        if (command.ProjectProgress.GroupBy(item => item.ProjectId).Any(group => group.Count() > 1))
            fields["projectProgress"] = ["Provide exactly one Form 2 entry for each selected project."];
        if (command.ProjectProgress.Any(item =>
                item.ConferencePapersProduced < 0 || item.IpTechnologiesProtected < 0))
            fields["projectProgress"] = ["Form 2 counts cannot be negative."];
        if (fields.Count > 0) return Result<ValidatedSave>.Failure(Error.Validation(fields));

        var progressByProject = command.ProjectProgress.ToDictionary(item => item.ProjectId);
        if (projectIds.Any(id => !progressByProject.ContainsKey(id)))
            fields["projectProgress"] = ["Provide Form 2 progress for every selected project."];
        if (progressByProject.Keys.Any(id => !projectIds.Contains(id)))
            fields["projectProgress"] = ["Form 2 progress must match the selected projects."];

        var inceptions = await repository.FindProjectInceptionsAsync(projectIds, ct);
        foreach (var projectId in projectIds)
        {
            if (!inceptions.TryGetValue(projectId, out var inception) || !inception.IsComplete)
                fields["projectProgress"] = ["Complete Form 1 for every selected project before recording progress."];
            else if (progressByProject.TryGetValue(projectId, out var progress) &&
                     string.IsNullOrWhiteSpace(progress.ProgressSummary))
                fields["projectProgress"] = ["Summarize progress for every selected project."];
        }

        if (period is not null && await repository.StaffReportExistsAsync(employeeId, period.Id, excludeId, ct))
            fields["reportingPeriodId"] = ["A staff quarterly report already exists for this reporting period."];
        if (fields.Count > 0) return Result<ValidatedSave>.Failure(Error.Validation(fields));

        var normalizedProgress = projectIds.Select(id =>
        {
            var progress = progressByProject[id];
            return new SaveStaffQuarterlyProjectProgressCommand(id, progress.ProgressSummary.Trim(),
                Normalize(progress.ProgressKeyResults), Normalize(progress.Challenges),
                Normalize(progress.NextQuarterActivities), Normalize(progress.WayForward),
                progress.ConferencePapersProduced, progress.IpTechnologiesProtected);
        }).ToList();

        return Result<ValidatedSave>.Success(new(period!, reviewer!, projects, technologies, normalizedProgress));
    }

    private static Result<bool> ValidateAttachmentsForSubmit(IReadOnlyList<FileRecord> files)
    {
        foreach (var file in files)
        {
            if (file.ScanStatus is "infected" or "quarantined")
                return Result<bool>.Failure(Error.Conflict("Remove infected attachments before submitting."));
            if (file.ScanStatus is "pending" or "failed")
                return Result<bool>.Failure(Error.Conflict("Wait for attachment scanning to finish before submitting."));
        }
        return Result<bool>.Success(true);
    }

    private async Task<Result<StaffQuarterlyReportResponse>> ReviewerTransitionAsync(
        Guid id, byte[]? expectedRowVersion, string action, Func<Report, Result<bool>> transition,
        string outcome, CancellationToken ct, string? returnReason = null)
    {
        if (expectedRowVersion is null) return Result<StaffQuarterlyReportResponse>.Failure(Error.PreconditionFailed("An If-Match header is required."));
        var reviewer = RequireReviewerIdentity();
        if (reviewer.IsFailure) return Result<StaffQuarterlyReportResponse>.Failure(reviewer.Error!);
        var aggregate = await repository.FindAggregateAsync(id, ct);
        if (aggregate is null || aggregate.Report.ReviewerUserId != reviewer.Value!.UserId ||
            aggregate.Report.InstituteId != reviewer.Value.InstituteId)
            return Result<StaffQuarterlyReportResponse>.Failure(Error.NotFound("Quarterly report not found."));
        var transitioned = transition(aggregate.Report);
        if (transitioned.IsFailure) return Result<StaffQuarterlyReportResponse>.Failure(transitioned.Error!);
        unitOfWork.SetOriginalRowVersion(aggregate.Report, expectedRowVersion);
        await notifications.StageStaffQuarterlyReportReviewedAsync(
            aggregate.Report.Id, aggregate.Report.InstituteId, aggregate.Owner.Id, aggregate.Period.Name,
            aggregate.Report.Title, outcome, returnReason, ct);
        return await SaveTransitionAsync(aggregate.Report, action, ct);
    }

    private async Task<Result<StaffQuarterlyReportResponse>> SaveTransitionAsync(Report report, string action, CancellationToken ct)
    {
        try
        {
            await audit.RecordAsync(action, "Report", report.Id.ToString(), null, Snapshot(report), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<StaffQuarterlyReportResponse>.Failure(Error.PreconditionFailed(
                "The quarterly report was modified by another request. Reload it and retry."));
        }
        return await GetAsync(report.Id, ct);
    }

    private async Task<IReadOnlyList<StaffQuarterlyReportResponse>> MapManyAsync(
        IReadOnlyList<StaffQuarterlyReportAggregate> values, CancellationToken ct)
    {
        var responses = new List<StaffQuarterlyReportResponse>(values.Count);
        foreach (var value in values)
            responses.Add(await MapAsync(value, ct));
        return responses;
    }

    private async Task<StaffQuarterlyReportResponse> MapAsync(StaffQuarterlyReportAggregate value, CancellationToken ct)
    {
        var inceptions = await repository.FindProjectInceptionsAsync(value.Projects.Select(item => item.Id).ToList(), ct);
        var leadNames = await LoadLeadNamesAsync(value.Projects, ct);
        var conceptNoteIds = inceptions.Values.Select(item => item.ConceptNoteFileId).OfType<Guid>().Distinct().ToList();
        var conceptNoteFiles = (await repository.FindFileRecordsAsync(conceptNoteIds, ct))
            .ToDictionary(file => file.Id);

        var projects = value.Projects.Select(project =>
        {
            var link = value.ReportProjects.Single(candidate => candidate.ProjectId == project.Id);
            return new StaffQuarterlyReportReference(project.Id, link.ProjectCodeSnapshot ?? project.Code,
                link.ProjectNameSnapshot ?? project.Name);
        }).ToList();

        var projectProgress = value.Projects.Select(project =>
        {
            var link = value.ReportProjects.Single(candidate => candidate.ProjectId == project.Id);
            inceptions.TryGetValue(project.Id, out var inception);
            StaffQuarterlyProjectInceptionResponse? inceptionResponse = null;
            if (inception is not null)
            {
                conceptNoteFiles.TryGetValue(inception.ConceptNoteFileId ?? Guid.Empty, out var conceptFile);
                inceptionResponse = link.ProjectCodeSnapshot is not null
                    ? MapSnapshotInception(project, inception, link, conceptFile)
                    : MapInception(project, inception, leadNames, conceptFile);
            }

            return new StaffQuarterlyProjectProgressResponse(
                project.Id,
                link.ProjectCodeSnapshot ?? project.Code,
                link.ProjectNameSnapshot ?? project.Name,
                inception?.IsComplete ?? false,
                inceptionResponse,
                link.ProgressSummary,
                link.ProgressKeyResults,
                link.Challenges,
                link.NextQuarterActivities,
                link.WayForward,
                link.ConferencePapersProduced,
                link.IpTechnologiesProtected);
        }).ToList();

        var technologies = value.Technologies.Select(technology =>
        {
            var link = value.ReportTechnologies.Single(candidate => candidate.TechnologyId == technology.Id);
            return new StaffQuarterlyReportReference(technology.Id, link.TechnologyCodeSnapshot ?? technology.Code,
                link.TechnologyNameSnapshot ?? technology.Name);
        }).ToList();

        return new StaffQuarterlyReportResponse(
            value.Report.Id,
            new(value.Period.Id, value.Period.Code, value.Period.Name, value.Period.StartDate, value.Period.EndDate, value.Period.DueDate),
            new(value.Owner.Id, value.Owner.StaffId, DisplayName(value.Owner)),
            new(value.Reviewer.User.Id, value.Reviewer.Employee.Id, DisplayName(value.Reviewer.Employee),
                value.Reviewer.Role, ReviewerEmail(value.Reviewer), ReviewerPhone(value.Reviewer)),
            value.Report.Title, value.Report.Abstract, value.Report.Summary, value.Report.KeyResults,
            value.Report.Conclusion, value.Report.Status, projects, technologies, projectProgress,
            value.AttachmentFiles.Select(StaffQuarterlyReportSupport.MapFile).ToList(),
            AvailableActions(value.Report), value.Report.ReturnReason, value.Report.SubmittedAt,
            value.Report.ApprovedAt, value.Report.CreatedAt, value.Report.UpdatedAt,
            ConcurrencyToken.Format(value.Report.RowVersion));
    }

    private async Task<StaffQuarterlyProjectInceptionResponse> MapInceptionAsync(
        Project project, ProjectInception? inception, CancellationToken ct)
    {
        var leadNames = await LoadLeadNamesAsync([project], ct);
        FileRecord? conceptFile = null;
        if (inception?.ConceptNoteFileId is Guid fileId)
            conceptFile = (await repository.FindFileRecordsAsync([fileId], ct)).SingleOrDefault();
        return MapInception(project, inception, leadNames, conceptFile);
    }

    private static StaffQuarterlyProjectInceptionResponse MapInception(
        Project project,
        ProjectInception? inception,
        IReadOnlyDictionary<Guid, string> leadNames,
        FileRecord? conceptFile) =>
        new(
            project.Id,
            project.Code,
            project.Name,
            project.Objective,
            project.Justification,
            project.Method,
            project.Nature ?? ProjectNatures.Research,
            project.StartDate,
            project.EndDate,
            project.Currency,
            project.BudgetAmount,
            project.LeadEmployeeId ?? Guid.Empty,
            leadNames.GetValueOrDefault(project.LeadEmployeeId ?? Guid.Empty, "Unknown"),
            inception?.EstimatedDuration ?? string.Empty,
            inception?.SponsorName ?? string.Empty,
            inception?.Location ?? string.Empty,
            inception?.CollaboratingInstitute,
            inception?.ParticipatingScientists,
            inception?.ExpectedBeneficiaries,
            inception?.PotentialTechnology,
            inception?.ContributionToKnowledge,
            inception?.IsComplete ?? false,
            conceptFile is null ? null : StaffQuarterlyReportSupport.MapFile(conceptFile));

    private static StaffQuarterlyProjectInceptionResponse MapSnapshotInception(
        Project project,
        ProjectInception inception,
        ReportProject link,
        FileRecord? conceptFile) =>
        new(
            project.Id,
            link.ProjectCodeSnapshot ?? project.Code,
            link.ProjectNameSnapshot ?? project.Name,
            link.SnapshotObjective ?? project.Objective,
            link.SnapshotJustification,
            link.SnapshotMethod,
            project.Nature ?? ProjectNatures.Research,
            project.StartDate,
            project.EndDate,
            project.Currency,
            project.BudgetAmount,
            project.LeadEmployeeId ?? Guid.Empty,
            link.SnapshotLeadName ?? "Unknown",
            link.SnapshotEstimatedDuration ?? inception.EstimatedDuration,
            link.SnapshotSponsorName ?? inception.SponsorName,
            link.SnapshotLocation ?? inception.Location,
            link.SnapshotCollaboratingInstitute,
            link.SnapshotParticipatingScientists,
            link.SnapshotExpectedBeneficiaries,
            link.SnapshotPotentialTechnology,
            link.SnapshotContributionToKnowledge,
            true,
            conceptFile is null ? null : StaffQuarterlyReportSupport.MapFile(conceptFile));

    private async Task<IReadOnlyDictionary<Guid, string>> LoadLeadNamesAsync(
        IReadOnlyList<Project> projects, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var leadId in projects.Select(item => item.LeadEmployeeId).OfType<Guid>().Distinct())
        {
            var employee = await repository.FindEmployeeAsync(leadId, ct);
            if (employee is not null)
                result[leadId] = DisplayName(employee);
        }
        return result;
    }

    private Result<(Guid EmployeeId, Guid InstituteId, Guid UserId)> RequireEmployeeIdentity()
    {
        if (!currentUser.EmployeeId.HasValue || !currentUser.InstituteId.HasValue || !currentUser.UserId.HasValue)
            return Result<(Guid, Guid, Guid)>.Failure(Error.NotFound("Staff identity link not found."));
        return Result<(Guid, Guid, Guid)>.Success((currentUser.EmployeeId.Value, currentUser.InstituteId.Value, currentUser.UserId.Value));
    }

    private Result<(Guid UserId, Guid InstituteId)> RequireReviewerIdentity()
    {
        if (!currentUser.UserId.HasValue || !currentUser.InstituteId.HasValue)
            return Result<(Guid, Guid)>.Failure(Error.NotFound("Reviewer identity link not found."));
        return Result<(Guid, Guid)>.Success((currentUser.UserId.Value, currentUser.InstituteId.Value));
    }

    private bool CanRead(StaffQuarterlyReportAggregate? aggregate) => aggregate is not null &&
        ((currentUser.EmployeeId.HasValue && aggregate.Report.OwnerEmployeeId == currentUser.EmployeeId &&
          aggregate.Report.InstituteId == currentUser.InstituteId) ||
         (currentUser.UserId.HasValue && aggregate.Report.ReviewerUserId == currentUser.UserId &&
          aggregate.Report.InstituteId == currentUser.InstituteId && aggregate.Report.Status != ReportStatuses.Draft));

    private static StaffQuarterlyReviewerOption MapReviewerOption(StaffQuarterlyReviewer reviewer) =>
        new(reviewer.User.Id, reviewer.Employee.Id, DisplayName(reviewer.Employee), reviewer.Role,
            ReviewerEmail(reviewer) ?? string.Empty, ReviewerPhone(reviewer) ?? string.Empty);

    private IReadOnlyList<string> AvailableActions(Report report)
    {
        if (report.OwnerEmployeeId == currentUser.EmployeeId)
            return report.Status is ReportStatuses.Draft or ReportStatuses.Returned ? ["edit", "submit"] : [];
        if (report.ReviewerUserId == currentUser.UserId)
            return report.Status == ReportStatuses.Submitted ? ["approve", "return"] : [];
        return [];
    }

    private static string DisplayName(Csir.Spme.Domain.Hr.Employee employee) =>
        StaffQuarterlyReportSupport.DisplayName(employee);
    private static string? ReviewerEmail(StaffQuarterlyReviewer reviewer) =>
        Normalize(reviewer.User.Email) ?? Normalize(reviewer.Employee.PrimaryEmail);
    private static string? ReviewerPhone(StaffQuarterlyReviewer reviewer) =>
        Normalize(reviewer.User.PhoneNumber) ?? Normalize(reviewer.Employee.Phone);
    private static string? Normalize(string? value) => StaffQuarterlyReportSupport.Normalize(value);
    private static string Snapshot(Report report) => $"status={report.Status};period={report.ReportingPeriodId};title={report.Title}";

    private sealed record ValidatedSave(
        ReportingPeriod Period,
        StaffQuarterlyReviewer Reviewer,
        IReadOnlyList<Project> Projects,
        IReadOnlyList<Technology> Technologies,
        IReadOnlyList<SaveStaffQuarterlyProjectProgressCommand> ProjectProgress);
}
