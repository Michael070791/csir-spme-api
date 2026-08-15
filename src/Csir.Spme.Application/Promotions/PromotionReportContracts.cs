using System.Text.Json;

namespace Csir.Spme.Application.Promotions;

public sealed record PromotionReportSectionDto(
    string Code,
    string? Heading,
    JsonElement Content);

public sealed record PromotionReportContentDto(
    int SchemaVersion,
    IReadOnlyList<PromotionReportSectionDto> Sections);

public sealed record PromotionReportDto(
    Guid Id,
    Guid PromotionSubmissionId,
    Guid RequirementSnapshotId,
    string ReportType,
    string Title,
    PromotionReportContentDto Content,
    string Status,
    Guid? RenderedFileId,
    DateTimeOffset LastSavedAt,
    DateTimeOffset? FinalizedAt,
    string Etag,
    DateTimeOffset UpdatedAt);

public sealed record ReplacePromotionReportCommand(
    string Title,
    PromotionReportContentDto Content);
