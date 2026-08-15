using System.Text.Json;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Jobs;

public sealed class PromotionRequirementTemplateSeedHostedService : IHostedService
{
    public const long SeniorStaffPdfMaximumFileBytes = 157_286_400;
    private static readonly string PdfContentTypesJson = JsonSerializer.Serialize(new[] { "application/pdf" });

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PromotionRequirementTemplateSeedHostedService> _logger;

    public PromotionRequirementTemplateSeedHostedService(
        IServiceProvider serviceProvider,
        ILogger<PromotionRequirementTemplateSeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var cycles = await db.PromotionCycles.AsNoTracking()
            .Where(cycle => cycle.Status == PromotionConstants.CycleOpen || cycle.Status == PromotionConstants.CyclePlanned)
            .ToListAsync(cancellationToken);
        if (cycles.Count == 0)
        {
            _logger.LogInformation("Promotion requirement template seed skipped because no open or planned cycles exist.");
            return;
        }

        var paths = await db.PromotionPaths.AsNoTracking()
            .Where(path => path.Status == PromotionConstants.PathActive)
            .ToListAsync(cancellationToken);
        if (paths.Count == 0)
        {
            _logger.LogInformation("Promotion requirement template seed skipped because no active promotion paths exist.");
            return;
        }

        var created = 0;
        foreach (var cycle in cycles)
        {
            foreach (var path in paths)
            {
                if (await db.PromotionSubmissionRequirementTemplates.AnyAsync(template =>
                        template.PromotionCycleId == cycle.Id && template.PromotionPathId == path.Id, cancellationToken))
                    continue;

                foreach (var definition in DefaultTemplates())
                {
                    db.PromotionSubmissionRequirementTemplates.Add(new PromotionSubmissionRequirementTemplate(
                        cycle.Id, path.Id, definition.Code, definition.Type, definition.Title, definition.Required,
                        definition.DisplayOrder, definition.Description, definition.DeclarationText,
                        definition.ReportTemplateCode, definition.AcceptedContentTypesJson,
                        definition.MaximumFileBytes, definition.MaximumDocumentCount));
                }

                created++;
            }
        }

        if (created == 0)
        {
            _logger.LogInformation("Promotion requirement templates already present for active cycle/path pairs.");
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded Senior Staff promotion requirement templates for {Count} cycle/path pairs.", created);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<TemplateDefinition> DefaultTemplates() =>
    [
        new("particulars", PromotionConstants.RequirementReport, "Particulars of applicant", true, 1,
            "Confirm your appointment details and proposed promotion grade.", null, "particulars", null, null, null),
        new("qualifications", PromotionConstants.RequirementReport, "Qualifications", true, 2,
            "Summarise your academic and professional qualifications.", null, "qualifications", null, null, null),
        new("service-duties", PromotionConstants.RequirementReport, "Service history and present duties", true, 3,
            "Describe posts held and your current duties.", null, "service-duties", null, null, null),
        new("training", PromotionConstants.RequirementReport, "Training", true, 4,
            "Record relevant training courses attended.", null, "training", null, null, null),
        new("qualification-certificates", PromotionConstants.RequirementDocument, "Qualification certificates", true, 5,
            "Upload PDF copies of qualification certificates.", null, null, PdfContentTypesJson, SeniorStaffPdfMaximumFileBytes, 5),
        new("appraisal-reports", PromotionConstants.RequirementDocument, "Appraisal reports", true, 6,
            "Upload satisfactory appraisal report evidence as PDF.", null, null, PdfContentTypesJson, SeniorStaffPdfMaximumFileBytes, 3),
        new("training-certificates", PromotionConstants.RequirementDocument, "Training certificates", true, 7,
            "Upload training certificates as PDF.", null, null, PdfContentTypesJson, SeniorStaffPdfMaximumFileBytes, 5),
        new("other-supporting-documents", PromotionConstants.RequirementDocument, "Other supporting documents", false, 8,
            "Upload any additional supporting PDF evidence.", null, null, PdfContentTypesJson, SeniorStaffPdfMaximumFileBytes, 10),
        new("applicant", PromotionConstants.RequirementDeclaration, "Applicant declaration", true, 9,
            null,
            "I confirm that the information and documents submitted in this promotion application are true and complete to the best of my knowledge.",
            null, null, null, null)
    ];

    private sealed record TemplateDefinition(
        string Code,
        string Type,
        string Title,
        bool Required,
        short DisplayOrder,
        string? Description,
        string? DeclarationText,
        string? ReportTemplateCode,
        string? AcceptedContentTypesJson,
        long? MaximumFileBytes,
        short? MaximumDocumentCount);
}
