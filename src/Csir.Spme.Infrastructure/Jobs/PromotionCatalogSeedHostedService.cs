using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Jobs;

public sealed class PromotionCatalogSeedHostedService : IHostedService
{
    public const string PolicyChecksum = "cos-snr-staff-sections-20-22";
    public const string PolicySectionRange = "20-22";
    public const string ResearchNonPhdChecksum = "v1-research-ars-rs-non-phd";
    public const string ResearchNonPhdSection = "ars-rs-non-phd";

    private static readonly string[] RetiredTechnologistGradeCodes =
    [
        "technologist",
        "senior-technologist",
        "principal-technologist",
        "chief-technologist"
    ];

    private static readonly string[] RetiredTechnologistPathCodes =
    [
        "cos-s20-technologist",
        "cos-s21-technologist",
        "cos-s22-technologist"
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PromotionCatalogSeedHostedService> _logger;

    public PromotionCatalogSeedHostedService(
        IServiceProvider serviceProvider,
        ILogger<PromotionCatalogSeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var created = await EnsureAsync(db, cancellationToken);
        if (created)
            _logger.LogInformation("Seeded promotion catalog for the {CycleYear} cycle.", PromotionConstants.CurrentCycleYear);
        else
            _logger.LogInformation("Promotion catalog already present.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static async Task<bool> EnsureAsync(SpmeDbContext db, CancellationToken cancellationToken)
    {
        var changed = false;
        var grades = await EnsureGradesAsync(db, cancellationToken);
        changed |= grades.Created;
        changed |= await RetireTechnologistCatalogAsync(db, cancellationToken);

        var policy = await db.PromotionPolicySources
            .FirstOrDefaultAsync(item => item.SourceChecksum == PolicyChecksum && item.SectionReference == PolicySectionRange, cancellationToken);
        if (policy is null)
        {
            policy = PromotionPolicySource.Create(
                "Revised Conditions of Service, Senior Staff, Sections 20-22",
                "final-draft",
                PolicySectionRange,
                "printed-page-5",
                PolicyChecksum,
                new DateTime(PromotionConstants.CurrentCycleYear, 1, 1));
            db.PromotionPolicySources.Add(policy);
            changed = true;
        }

        var researchPolicy = await db.PromotionPolicySources
            .FirstOrDefaultAsync(item => item.SourceChecksum == ResearchNonPhdChecksum && item.SectionReference == ResearchNonPhdSection, cancellationToken);
        if (researchPolicy is null)
        {
            researchPolicy = PromotionPolicySource.Create(
                "Assistant Research Scientist to Research Scientist (non-PhD upgrade)",
                "v1-helper",
                ResearchNonPhdSection,
                "research-grade-non-phd",
                ResearchNonPhdChecksum,
                new DateTime(PromotionConstants.CurrentCycleYear, 1, 1));
            db.PromotionPolicySources.Add(researchPolicy);
            changed = true;
        }

        changed |= await EnsureSeniorStaffPathsAsync(db, policy.Id, grades.ByCode, cancellationToken);
        changed |= await EnsureResearchNonPhdPathAsync(db, researchPolicy.Id, grades.ByCode, cancellationToken);
        changed |= await EnsureCycleAsync(db, cancellationToken);

        if (changed)
            await db.SaveChangesAsync(cancellationToken);

        return changed;
    }

    private static async Task<(bool Created, Dictionary<string, Grade> ByCode)> EnsureGradesAsync(
        SpmeDbContext db,
        CancellationToken cancellationToken)
    {
        var seniorStaffDefinitions = new (string Code, string Name, string Stream, short Level, short Rank)[]
        {
            ("technical-officer", "Technical Officer", PromotionConstants.TechnicalStream, 1, 10),
            ("senior-technical-officer", "Senior Technical Officer", PromotionConstants.TechnicalStream, 2, 20),
            ("principal-technical-officer", "Principal Technical Officer", PromotionConstants.TechnicalStream, 3, 30),
            ("chief-technical-officer", "Chief Technical Officer", PromotionConstants.TechnicalStream, 4, 40),
            ("administrative-assistant", "Administrative Assistant", PromotionConstants.AdministrativeStream, 1, 11),
            ("senior-administrative-assistant", "Senior Administrative Assistant", PromotionConstants.AdministrativeStream, 2, 21),
            ("principal-administrative-assistant", "Principal Administrative Assistant", PromotionConstants.AdministrativeStream, 3, 31),
            ("chief-administrative-assistant", "Chief Administrative Assistant", PromotionConstants.AdministrativeStream, 4, 41)
        };

        var seniorMemberDefinitions = new (string Code, string Name, short Level, short Rank)[]
        {
            ("assistant-research-scientist", "Assistant Research Scientist", 1, 50),
            ("research-scientist", "Research Scientist", 2, 60),
            ("principal-research-scientist", "Principal Research Scientist", 3, 70),
            ("chief-research-scientist", "Chief Research Scientist", 4, 80)
        };

        var codes = seniorStaffDefinitions.Select(item => item.Code)
            .Concat(seniorMemberDefinitions.Select(item => item.Code))
            .ToArray();
        var existing = await db.Grades
            .Where(grade => codes.Contains(grade.Code))
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(grade => grade.Code, StringComparer.OrdinalIgnoreCase);
        var created = false;

        foreach (var definition in seniorStaffDefinitions)
        {
            if (byCode.ContainsKey(definition.Code))
                continue;

            var grade = Grade.Create(
                definition.Code,
                definition.Name,
                PromotionConstants.SeniorStaff,
                definition.Stream,
                definition.Level,
                definition.Rank);
            db.Grades.Add(grade);
            byCode[definition.Code] = grade;
            created = true;
        }

        foreach (var definition in seniorMemberDefinitions)
        {
            if (byCode.ContainsKey(definition.Code))
                continue;

            var grade = Grade.Create(
                definition.Code,
                definition.Name,
                StaffCategories.SeniorMember,
                PromotionConstants.ResearchStream,
                definition.Level,
                definition.Rank);
            db.Grades.Add(grade);
            byCode[definition.Code] = grade;
            created = true;
        }

        return (created, byCode);
    }

    private static async Task<bool> RetireTechnologistCatalogAsync(SpmeDbContext db, CancellationToken cancellationToken)
    {
        var changed = false;
        var grades = await db.Grades
            .Where(grade => RetiredTechnologistGradeCodes.Contains(grade.Code) && grade.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var grade in grades)
        {
            grade.Deactivate();
            changed = true;
        }

        var retireOn = new DateTime(PromotionConstants.CurrentCycleYear, 1, 1);
        var paths = await db.PromotionPaths
            .Where(path => RetiredTechnologistPathCodes.Contains(path.Code) && path.Status != PromotionConstants.PathInactive)
            .ToListAsync(cancellationToken);
        foreach (var path in paths)
        {
            path.Retire(retireOn);
            changed = true;
        }

        return changed;
    }

    private static async Task<bool> EnsureSeniorStaffPathsAsync(
        SpmeDbContext db,
        Guid policySourceId,
        IReadOnlyDictionary<string, Grade> grades,
        CancellationToken cancellationToken)
    {
        var definitions = new (string Code, string Section, string Stream, string Source, string Target, short Years, string Qualification, string Status)[]
        {
            ("cos-s20-technical", "20", PromotionConstants.TechnicalStream, "technical-officer", "senior-technical-officer", 4, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathActive),
            ("cos-s20-administrative", "20", PromotionConstants.AdministrativeStream, "administrative-assistant", "senior-administrative-assistant", 4, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathActive),
            ("cos-s21-technical", "21", PromotionConstants.TechnicalStream, "senior-technical-officer", "principal-technical-officer", 4, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathActive),
            ("cos-s21-administrative", "21", PromotionConstants.AdministrativeStream, "senior-administrative-assistant", "principal-administrative-assistant", 4, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathActive),
            ("cos-s22-technical", "22", PromotionConstants.TechnicalStream, "principal-technical-officer", "chief-technical-officer", 5, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathActive),
            ("cos-s22-administrative", "22", PromotionConstants.AdministrativeStream, "principal-administrative-assistant", "chief-administrative-assistant", 5, QualificationLevels.BachelorOrEquivalent, PromotionConstants.PathRequiresPolicyConfirmation)
        };

        return await AddMissingPathsAsync(db, policySourceId, grades, PromotionConstants.SeniorStaff, definitions, cancellationToken);
    }

    private static async Task<bool> EnsureResearchNonPhdPathAsync(
        SpmeDbContext db,
        Guid policySourceId,
        IReadOnlyDictionary<string, Grade> grades,
        CancellationToken cancellationToken)
    {
        var definitions = new (string Code, string Section, string Stream, string Source, string Target, short Years, string Qualification, string Status)[]
        {
            (PromotionConstants.ResearchArsRsNonPhdPath, ResearchNonPhdSection, PromotionConstants.ResearchStream,
                "assistant-research-scientist", "research-scientist", 5, QualificationLevels.MastersOrEquivalent, PromotionConstants.PathActive)
        };

        return await AddMissingPathsAsync(db, policySourceId, grades, PromotionConstants.SeniorMember, definitions, cancellationToken);
    }

    private static async Task<bool> AddMissingPathsAsync(
        SpmeDbContext db,
        Guid policySourceId,
        IReadOnlyDictionary<string, Grade> grades,
        string staffCategory,
        (string Code, string Section, string Stream, string Source, string Target, short Years, string Qualification, string Status)[] definitions,
        CancellationToken cancellationToken)
    {
        var created = false;
        var existingCodes = await db.PromotionPaths.Select(path => path.Code).ToListAsync(cancellationToken);
        var effectiveFrom = new DateTime(PromotionConstants.CurrentCycleYear, 1, 1);

        foreach (var definition in definitions)
        {
            if (existingCodes.Contains(definition.Code, StringComparer.OrdinalIgnoreCase))
                continue;
            if (!grades.TryGetValue(definition.Source, out var source) || !grades.TryGetValue(definition.Target, out var target))
                continue;

            db.PromotionPaths.Add(PromotionPath.Create(
                definition.Code,
                policySourceId,
                definition.Section,
                staffCategory,
                definition.Stream,
                source.Id,
                target.Id,
                definition.Years,
                definition.Qualification,
                effectiveFrom,
                definition.Status));
            created = true;
        }

        return created;
    }

    private static async Task<bool> EnsureCycleAsync(SpmeDbContext db, CancellationToken cancellationToken)
    {
        var cycle = await db.PromotionCycles
            .FirstOrDefaultAsync(item => item.CycleYear == PromotionConstants.CurrentCycleYear, cancellationToken);
        if (cycle is not null)
            return false;

        cycle = new PromotionCycle(PromotionConstants.CurrentCycleYear);
        cycle.Open(DateTimeOffset.UtcNow);
        db.PromotionCycles.Add(cycle);
        return true;
    }
}
