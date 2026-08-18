using System.Text.Json;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Common;

namespace Csir.Spme.Application.Promotions;

public sealed class PromotionReportService
{
    private const int LegacySchemaVersion = 1;
    private const int Form2SchemaVersion = 2;
    private const int MaximumSections = 100;
    private const int MaximumContentBytes = 1_048_576;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IPromotionReportRepository _reports;
    private readonly IApplicationDbContext _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public PromotionReportService(
        IPromotionReportRepository reports,
        IApplicationDbContext unitOfWork,
        IAuditService audit,
        ICurrentUserService currentUser)
    {
        _reports = reports;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Result<PromotionReportDto>> GetAsync(
        Guid promotionSubmissionId,
        string reportType,
        bool allowInstituteReview,
        bool allowCsirWideReview,
        CancellationToken ct)
    {
        var aggregate = await FindAsync(promotionSubmissionId, reportType, ct);
        if (aggregate is null ||
            !CanRead(aggregate, allowInstituteReview, allowCsirWideReview))
        {
            return Result<PromotionReportDto>.Failure(Error.NotFound("Promotion report not found."));
        }

        return Map(aggregate.Report);
    }

    public async Task<Result<PromotionReportDto>> ReplaceAsync(
        Guid promotionSubmissionId,
        string reportType,
        ReplacePromotionReportCommand command,
        byte[]? expectedRowVersion,
        CancellationToken ct)
    {
        var aggregate = await FindAsync(promotionSubmissionId, reportType, ct);
        if (aggregate is null || _currentUser.EmployeeId != aggregate.Submission.EmployeeId ||
            !IsStaffWritableReport(reportType, aggregate.Submission.Status))
        {
            return Result<PromotionReportDto>.Failure(Error.NotFound("Promotion report not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<PromotionReportDto>.Failure(Error.PreconditionFailed(
                "An If-Match header containing the current promotion report ETag is required."));
        }

        var validation = Validate(reportType, command, aggregate.Report.ContentJson);
        if (validation.Count > 0)
        {
            return Result<PromotionReportDto>.Failure(Error.Validation(validation));
        }

        var contentJson = JsonSerializer.Serialize(command.Content, SerializerOptions);
        var before = $"status={aggregate.Report.Status};title={aggregate.Report.Title}";
        var replaced = aggregate.Report.ReplaceWorkflowDraft(
            command.Title.Trim(),
            contentJson,
            aggregate.Submission.Status,
            reportType,
            DateTimeOffset.UtcNow);
        if (replaced.IsFailure)
        {
            return Result<PromotionReportDto>.Failure(replaced.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(aggregate.Report, expectedRowVersion);
        try
        {
            await _audit.RecordAsync(
                "promotion-submission.report.saved",
                "PromotionSubmissionReport",
                aggregate.Report.Id.ToString(),
                before,
                $"status={aggregate.Report.Status};title={aggregate.Report.Title}",
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<PromotionReportDto>.Failure(Error.PreconditionFailed(
                "The promotion report was modified by another request. Reload it and retry."));
        }

        return Map(aggregate.Report);
    }

    private async Task<PromotionReportAggregate?> FindAsync(
        Guid promotionSubmissionId,
        string reportType,
        CancellationToken ct)
    {
        if (promotionSubmissionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reportType) ||
            reportType.Length > 64)
        {
            return null;
        }

        return await _reports.FindAsync(promotionSubmissionId, reportType.Trim(), ct);
    }

    private bool CanRead(
        PromotionReportAggregate aggregate,
        bool allowInstituteReview,
        bool allowCsirWideReview)
    {
        if (_currentUser.EmployeeId == aggregate.Submission.EmployeeId)
        {
            return true;
        }

        if (!allowInstituteReview)
        {
            return false;
        }

        return allowCsirWideReview ||
            (_currentUser.InstituteId.HasValue &&
             _currentUser.InstituteId.Value == aggregate.Submission.InstituteId);
    }

    public async Task<Result<PromotionReportDto>> ReplaceWorkflowAsync(
        Guid promotionSubmissionId,
        string reportType,
        ReplacePromotionReportCommand command,
        byte[]? expectedRowVersion,
        Func<PromotionReportAggregate, bool> authorize,
        CancellationToken ct)
    {
        var aggregate = await FindAsync(promotionSubmissionId, reportType, ct);
        if (aggregate is null || !authorize(aggregate))
        {
            return Result<PromotionReportDto>.Failure(Error.NotFound("Promotion report not found."));
        }

        if (expectedRowVersion is null)
        {
            return Result<PromotionReportDto>.Failure(Error.PreconditionFailed(
                "An If-Match header containing the current promotion report ETag is required."));
        }

        var validation = Validate(reportType, command, aggregate.Report.ContentJson);
        if (validation.Count > 0)
        {
            return Result<PromotionReportDto>.Failure(Error.Validation(validation));
        }

        var contentJson = JsonSerializer.Serialize(command.Content, SerializerOptions);
        var before = $"status={aggregate.Report.Status};title={aggregate.Report.Title}";
        var replaced = aggregate.Report.ReplaceWorkflowDraft(
            command.Title.Trim(),
            contentJson,
            aggregate.Submission.Status,
            reportType,
            DateTimeOffset.UtcNow);
        if (replaced.IsFailure)
        {
            return Result<PromotionReportDto>.Failure(replaced.Error!);
        }

        _unitOfWork.SetOriginalRowVersion(aggregate.Report, expectedRowVersion);
        try
        {
            await _audit.RecordAsync(
                "promotion-submission.report.saved",
                "PromotionSubmissionReport",
                aggregate.Report.Id.ToString(),
                before,
                $"status={aggregate.Report.Status};title={aggregate.Report.Title}",
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<PromotionReportDto>.Failure(Error.PreconditionFailed(
                "The promotion report was modified by another request. Reload it and retry."));
        }

        return Map(aggregate.Report);
    }

    private static bool IsStaffWritableReport(string reportType, string submissionStatus)
    {
        if (reportType is "hod-assessment" or "director-assessment")
            return false;

        if (string.Equals(reportType, "applicant-hod-response", StringComparison.OrdinalIgnoreCase))
        {
            return submissionStatus is
                Csir.Spme.Domain.Promotions.PromotionConstants.SubmissionSubmitted or
                Csir.Spme.Domain.Promotions.PromotionConstants.SubmissionUnderReview or
                Csir.Spme.Domain.Promotions.PromotionConstants.SubmissionAcknowledged;
        }

        return true;
    }

    private static Dictionary<string, string[]> Validate(
        string reportType,
        ReplacePromotionReportCommand command,
        string existingContentJson)
    {
        var fields = ValidateStructure(command);
        if (fields.Count > 0 || command.Content is null)
            return fields;

        if (command.Content.SchemaVersion == LegacySchemaVersion)
            return fields;

        fields = Merge(fields, PromotionForm2ReportValidator.Validate(
            reportType,
            command.Content.SchemaVersion,
            command.Content.Sections));

        if (reportType == "particulars" && command.Content.Sections.Count == 1)
        {
            fields = Merge(fields, PromotionForm2ReportValidator.ValidateStaffParticularsOverrides(
                command.Content.Sections[0].Content,
                TryReadSectionContent(existingContentJson)));
        }

        return fields;
    }

    private static Dictionary<string, string[]> ValidateStructure(ReplacePromotionReportCommand command)
    {
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 512)
        {
            fields["title"] = ["A title of at most 512 characters is required."];
        }

        if (command.Content is null)
        {
            fields["content"] = ["Structured report content is required."];
            return fields;
        }

        if (command.Content.SchemaVersion is not (LegacySchemaVersion or Form2SchemaVersion))
        {
            fields["content.schemaVersion"] =
                [$"Schema version must be {LegacySchemaVersion} or {Form2SchemaVersion}."];
        }

        if (command.Content.Sections is null)
        {
            fields["content.sections"] = ["A sections collection is required."];
            return fields;
        }

        if (command.Content.Sections.Count > MaximumSections)
        {
            fields["content.sections"] =
                [$"A promotion report cannot contain more than {MaximumSections} sections."];
        }
        if (command.Content.Sections.Count == 0 && command.Content.SchemaVersion == LegacySchemaVersion)
        {
            fields["content.sections"] = ["At least one structured report section is required."];
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < command.Content.Sections.Count; index++)
        {
            var section = command.Content.Sections[index];
            var prefix = $"content.sections[{index}]";
            if (section is null || string.IsNullOrWhiteSpace(section.Code) || section.Code.Length > 64)
            {
                fields[$"{prefix}.code"] = ["A section code of at most 64 characters is required."];
                continue;
            }

            if (!codes.Add(section.Code.Trim()))
            {
                fields[$"{prefix}.code"] = ["Section codes must be unique within the report."];
            }

            if (section.Heading?.Length > 256)
            {
                fields[$"{prefix}.heading"] = ["A section heading cannot exceed 256 characters."];
            }

            if (section.Content.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                fields[$"{prefix}.content"] =
                    ["Section content must be a structured JSON object or array, not raw HTML or plain text."];
            }
        }

        try
        {
            if (JsonSerializer.SerializeToUtf8Bytes(command.Content, SerializerOptions).Length > MaximumContentBytes)
            {
                fields["content"] =
                    [$"Structured report content cannot exceed {MaximumContentBytes} bytes."];
            }
        }
        catch (InvalidOperationException)
        {
            fields["content"] = ["Structured report content contains an invalid JSON value."];
        }

        return fields;
    }

    private static Dictionary<string, string[]> Merge(
        Dictionary<string, string[]> left,
        Dictionary<string, string[]> right)
    {
        foreach (var entry in right)
            left[entry.Key] = entry.Value;
        return left;
    }

    private static JsonElement? TryReadSectionContent(string contentJson)
    {
        try
        {
            using var document = JsonDocument.Parse(contentJson);
            if (!document.RootElement.TryGetProperty("sections", out var sections) ||
                sections.ValueKind != JsonValueKind.Array ||
                sections.GetArrayLength() == 0)
            {
                return null;
            }

            return sections[0].TryGetProperty("content", out var content) ? content.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Result<PromotionReportDto> Map(
        Csir.Spme.Domain.Promotions.PromotionSubmissionReport report)
    {
        PromotionReportContentDto? content;
        try
        {
            content = JsonSerializer.Deserialize<PromotionReportContentDto>(
                report.ContentJson,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return Result<PromotionReportDto>.Failure(Error.Conflict(
                "The stored promotion report content is invalid and requires administrative correction."));
        }

        if (content is null ||
            content.Sections is null ||
            content.SchemaVersion < 1)
        {
            if (report.ContentJson.Trim() == "{}")
            {
                content = new PromotionReportContentDto(
                    LegacySchemaVersion,
                    Array.Empty<PromotionReportSectionDto>());
            }
            else
            {
                return Result<PromotionReportDto>.Failure(Error.Conflict(
                    "The stored promotion report content is invalid and requires administrative correction."));
            }
        }

        return Result<PromotionReportDto>.Success(new PromotionReportDto(
            report.Id,
            report.PromotionSubmissionId,
            report.RequirementSnapshotId,
            report.ReportType,
            report.Title,
            content,
            report.Status,
            report.RenderedFileId,
            report.LastSavedAt,
            report.FinalizedAt,
            ConcurrencyToken.Format(report.RowVersion),
            report.UpdatedAt));
    }
}
