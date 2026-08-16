using System.Text.Json;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Csir.Spme.Infrastructure.Jobs;

public sealed class PromotionDemoStaffSeedHostedService : IHostedService
{
    public const string SeniorStaffId = "DEMO-SS-001";
    public const string SeniorMemberStaffId = "DEMO-SM-001";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PromotionDemoStaffSeedHostedService> _logger;

    public PromotionDemoStaffSeedHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<PromotionDemoStaffSeedHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await EnsureSeniorStaffAsync(db, userManager, cancellationToken);
        await EnsureSeniorMemberAsync(db, userManager, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureSeniorStaffAsync(SpmeDbContext db, UserManager<User> userManager, CancellationToken ct)
    {
        var account = ReadAccount("Identity:SeedDemoStaff", SeniorStaffId);
        if (account is null)
        {
            _logger.LogInformation("Demo Senior Staff seed skipped because Identity:SeedDemoStaff is incomplete.");
            return;
        }

        var institute = await ResolveInstituteAsync(db, account.InstituteCode, ct);
        if (institute is null)
            return;

        var sourceGrade = await db.Grades.SingleOrDefaultAsync(item => item.Code == "technical-officer", ct);
        var cycle = await db.PromotionCycles.SingleOrDefaultAsync(item => item.CycleYear == PromotionConstants.CurrentCycleYear, ct);
        var path = await db.PromotionPaths.SingleOrDefaultAsync(item => item.Code == "cos-s20-technical", ct);
        if (sourceGrade is null || cycle is null || path is null)
        {
            _logger.LogWarning("Demo Senior Staff seed skipped because the promotion catalog is not ready.");
            return;
        }

        var employee = await db.Employees.FirstOrDefaultAsync(item => item.NormalizedStaffId == SeniorStaffId, ct);
        if (employee is null)
        {
            employee = new Employee(institute.Id, SeniorStaffId, "Mensah", "female");
            employee.UpdateProfile(
                SeniorStaffId, "Ms", "Mensah", "Ama Demo", "female", new DateTime(1990, 4, 12),
                "Ghanaian", null, null, account.Email, null, "active", true);
            db.Employees.Add(employee);
        }

        var employment = await db.EmploymentRecords.FirstOrDefaultAsync(item => item.EmployeeId == employee.Id && item.IsCurrent, ct);
        if (employment is null)
        {
            var appointed = new DateTime(2022, 1, 1);
            employment = new EmploymentRecord(
                employee.Id, institute.Id, null, null, null, sourceGrade.Id,
                "Technical Officer", null, PromotionConstants.SeniorStaff, null, "laboratory-technology",
                "active", institute.Name, null, null, null, appointed, null, null, null, appointed, true);
            db.EmploymentRecords.Add(employment);
        }

        if (!await db.EducationRecords.AnyAsync(item => item.EmployeeId == employee.Id, ct))
        {
            var education = new EducationRecord(
                employee.Id, "Kwame Nkrumah University of Science and Technology", "Laboratory Technology",
                "BSc", QualificationLevels.BachelorOrEquivalent, null, "Laboratory Technology",
                null, null, null, new DateTime(2017, 9, 1), new DateTime(2021, 6, 30));
            education.SetInstitutionRecognitionStatus("verified");
            education.SetRelevantFieldStatus("verified", null, DateTimeOffset.UtcNow);
            db.EducationRecords.Add(education);
        }

        await db.SaveChangesAsync(ct);

        if (!await db.PromotionAssessments.AnyAsync(item => item.EmployeeId == employee.Id && item.PromotionCycleId == cycle.Id, ct))
        {
            var evaluation = PromotionEligibilityEvaluator.Evaluate(new PromotionEligibilityFacts(
                PromotionConstants.SeniorStaff, path.Status, employment.EffectiveFrom, path.MinimumYearsInSourceGrade,
                cycle.EffectivePromotionDate, true, false, false, false));
            var assessment = PromotionAssessment.Create(
                employee.Id, institute.Id, cycle.Id, path.Id, employment.Id, sourceGrade.Id, path.TargetGradeId,
                DateTime.UtcNow.Date, cycle.EffectivePromotionDate, employment.EffectiveFrom, evaluation.ServiceRequirementMetOn,
                evaluation.CompletedSourceGradeYears, evaluation.EligibilityState,
                JsonSerializer.Serialize(evaluation.BlockingReasons),
                JsonSerializer.Serialize(evaluation.PendingHrChecks),
                JsonSerializer.Serialize(evaluation),
                null);
            db.PromotionAssessments.Add(assessment);
            db.PromotionStatusSnapshots.Add(PromotionStatusSnapshot.FromAssessment(assessment, PromotionConstants.SeniorStaff));
            await db.SaveChangesAsync(ct);
        }

        await EnsureUserAsync(userManager, account, employee.Id, institute.Id, "Ama Demo Mensah", ct);
    }

    private async Task EnsureSeniorMemberAsync(SpmeDbContext db, UserManager<User> userManager, CancellationToken ct)
    {
        var account = ReadAccount("Identity:SeedDemoSeniorMember", SeniorMemberStaffId);
        if (account is null)
        {
            _logger.LogInformation("Demo Senior Member seed skipped because Identity:SeedDemoSeniorMember is incomplete.");
            return;
        }

        var institute = await ResolveInstituteAsync(db, account.InstituteCode, ct);
        if (institute is null)
            return;

        var employee = await db.Employees.FirstOrDefaultAsync(item => item.NormalizedStaffId == SeniorMemberStaffId, ct);
        if (employee is null)
        {
            employee = new Employee(institute.Id, SeniorMemberStaffId, "Owusu", "male");
            employee.UpdateProfile(
                SeniorMemberStaffId, "Dr", "Owusu", "Kofi Demo", "male", new DateTime(1984, 8, 3),
                "Ghanaian", null, null, account.Email, null, "active", true);
            db.Employees.Add(employee);
        }

        if (!await db.EmploymentRecords.AnyAsync(item => item.EmployeeId == employee.Id && item.IsCurrent, ct))
        {
            var appointed = new DateTime(2018, 3, 1);
            db.EmploymentRecords.Add(new EmploymentRecord(
                employee.Id, institute.Id, null, null, null, null,
                "Research Scientist", null, StaffCategories.SeniorMember, null, "plant-science",
                "active", institute.Name, null, null, null, appointed, null, null, null, appointed, true));
        }

        await db.SaveChangesAsync(ct);
        await EnsureUserAsync(userManager, account, employee.Id, institute.Id, "Kofi Demo Owusu", ct);
    }

    private async Task<Csir.Spme.Domain.Org.Institute?> ResolveInstituteAsync(
        SpmeDbContext db, string? instituteCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(instituteCode))
        {
            var matched = await db.Institutes.FirstOrDefaultAsync(item => item.Code == instituteCode && item.IsActive, ct);
            if (matched is not null)
                return matched;
            _logger.LogWarning("Demo staff institute {InstituteCode} was not found.", instituteCode);
        }

        var fallback = await db.Institutes.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Code).FirstOrDefaultAsync(ct);
        if (fallback is null)
        {
            _logger.LogWarning("Demo staff seed skipped because no active institute exists.");
            return null;
        }

        _logger.LogWarning("Demo staff using first active institute {InstituteCode}.", fallback.Code);
        return await db.Institutes.FirstAsync(item => item.Id == fallback.Id, ct);
    }

    private async Task EnsureUserAsync(
        UserManager<User> userManager,
        DemoAccount account,
        Guid employeeId,
        Guid instituteId,
        string displayName,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(account.UserName)
            ?? await userManager.FindByEmailAsync(account.Email);
        if (user is null)
        {
            user = new User(account.UserName, "Employee")
            {
                Email = account.Email,
                EmailConfirmed = true
            };
            user.UpdateDisplayName(displayName);
            user.LinkEmployee(employeeId, instituteId);
            var create = await userManager.CreateAsync(user, account.Password);
            if (!create.Succeeded)
            {
                _logger.LogWarning("Could not create demo user {UserName}: {Errors}", account.UserName, FormatErrors(create));
                return;
            }
        }
        else
        {
            user.LinkEmployee(employeeId, instituteId);
            user.UpdateDisplayName(displayName);
            if (!string.Equals(user.Email, account.Email, StringComparison.OrdinalIgnoreCase))
                user.Email = account.Email;
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, "Employee"))
            await userManager.AddToRoleAsync(user, "Employee");
    }

    private DemoAccount? ReadAccount(string sectionName, string defaultStaffId)
    {
        var section = _configuration.GetSection(sectionName);
        var userName = section.GetValue<string>("UserName");
        var email = section.GetValue<string>("Email");
        var password = section.GetValue<string>("Password");
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        return new DemoAccount(
            userName.Trim(),
            email.Trim(),
            password,
            section.GetValue<string>("InstituteCode"),
            section.GetValue<string>("StaffId") ?? defaultStaffId);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));

    private sealed record DemoAccount(string UserName, string Email, string Password, string? InstituteCode, string StaffId);
}
