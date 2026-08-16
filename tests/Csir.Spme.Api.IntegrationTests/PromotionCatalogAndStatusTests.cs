using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Jobs;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

[CollectionDefinition("Promotion catalog serial", DisableParallelization = true)]
public sealed class PromotionCatalogSerialCollection;

[Collection("Promotion catalog serial")]
public sealed class PromotionCatalogAndStatusTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    public PromotionCatalogAndStatusTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Catalog_Seed_Is_Idempotent_And_Opens_The_2027_Cycle()
    {
        _ = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        await PromotionCatalogSeedHostedService.EnsureAsync(db, CancellationToken.None);
        (await PromotionCatalogSeedHostedService.EnsureAsync(db, CancellationToken.None)).Should().BeFalse();
        var cycle = await db.PromotionCycles.SingleAsync(item => item.CycleYear == PromotionConstants.CurrentCycleYear);
        cycle.Status.Should().Be(PromotionConstants.CycleOpen);
        cycle.EffectivePromotionDate.Should().Be(new DateTime(2027, 1, 1));
        (await db.PromotionPaths.CountAsync()).Should().BeGreaterThanOrEqualTo(6);
        (await db.PromotionPaths.SingleAsync(item => item.Code == "cos-s22-administrative"))
            .Status.Should().Be(PromotionConstants.PathRequiresPolicyConfirmation);

        using var hr = Client(SpmeRoles.HrAdmin, null, Guid.NewGuid());
        var cycles = await hr.GetFromJsonAsync<CollectionResponse<PromotionCycleResponse>>("/api/v2/promotion-cycles");
        cycles!.Items.Should().Contain(item => item.CycleYear == PromotionConstants.CurrentCycleYear && item.Status == PromotionConstants.CycleOpen);
        var paths = await hr.GetFromJsonAsync<CollectionResponse<PromotionPathResponse>>("/api/v2/promotion-paths");
        paths!.Items.Should().Contain(item => item.Code == "cos-s20-technical");
        paths.Items.Should().Contain(item => item.Code == "cos-s22-administrative" && item.Status == PromotionConstants.PathRequiresPolicyConfirmation);
    }

    [Fact]
    public async Task Live_Status_Allows_Verified_Degree_Without_Appraisal_And_Does_Not_Start_Submission()
    {
        var seed = await SeedLinkedEmployeeAsync(PromotionConstants.SeniorStaff, "technical-officer", verifiedDegree: true);
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var mine = await owner.GetFromJsonAsync<PromotionStatusResponse>("/api/v2/promotion-status/me");

        mine!.EligibilityState.Should().Be(PromotionConstants.EligibilityEligibleForReview);
        mine.AvailableActions.Should().BeEmpty();
        mine.NextAction.Should().Be(PromotionStatusMessages.EligibleAwaitingAssessment);
        mine.Criteria.Should().Contain(item => item.Code == "qualification" && item.Status == "satisfied");
        mine.Criteria.Should().Contain(item => item.Code == "satisfactory-appraisal" && item.Status == "pending-hr-review");
        mine.NextPromotion!.PathCode.Should().Be("cos-s20-technical");
    }

    [Fact]
    public async Task Live_Status_Explains_Senior_Member_Coming_Soon_And_Lookup_Accepts_The_Category()
    {
        var seed = await SeedLinkedEmployeeAsync(StaffCategories.SeniorMember, null, verifiedDegree: false);
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var mine = await owner.GetFromJsonAsync<PromotionStatusResponse>("/api/v2/promotion-status/me");
        mine!.EligibilityState.Should().Be(PromotionConstants.EligibilityNotApplicable);
        mine.NextAction.Should().Be(PromotionStatusMessages.SeniorMemberComingSoon);
        mine.AvailableActions.Should().BeEmpty();

        var lookup = await owner.PostAsJsonAsync("/api/v2/promotion-status-lookups",
            new PromotionStatusLookupRequest(seed.StaffId, StaffCategories.SeniorMember, null));
        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        (await lookup.Content.ReadFromJsonAsync<PromotionStatusResponse>())!.NextAction
            .Should().Be(PromotionStatusMessages.SeniorMemberComingSoon);

        var mismatch = await owner.PostAsJsonAsync("/api/v2/promotion-status-lookups",
            new PromotionStatusLookupRequest(seed.StaffId, PromotionConstants.SeniorStaff, null));
        mismatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Lookup_Accepts_Junior_Staff_And_Rejects_Unknown_Categories()
    {
        var seed = await SeedLinkedEmployeeAsync(StaffCategories.JuniorStaff, null, verifiedDegree: false);
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var lookup = await owner.PostAsJsonAsync("/api/v2/promotion-status-lookups",
            new PromotionStatusLookupRequest(seed.StaffId, StaffCategories.JuniorStaff, null));
        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        (await lookup.Content.ReadFromJsonAsync<PromotionStatusResponse>())!.NextAction
            .Should().Be(PromotionStatusMessages.JuniorStaffNotInScheme);

        var invalid = await owner.PostAsJsonAsync("/api/v2/promotion-status-lookups",
            new PromotionStatusLookupRequest(seed.StaffId, "contractor", null));
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<LiveSeed> SeedLinkedEmployeeAsync(string staffCategory, string? gradeCode, bool verifiedDegree)
    {
        _ = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        await PromotionCatalogSeedHostedService.EnsureAsync(db, CancellationToken.None);
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var institute = new Institute($"PC-{suffix}", $"Promotion catalog {suffix}", "Institute");
        var staffId = $"PC{suffix}";
        var employee = new Employee(institute.Id, staffId, "Tester", "female");
        Guid? gradeId = null;
        if (gradeCode is not null)
        {
            var grade = await db.Grades.SingleAsync(item => item.Code == gradeCode);
            gradeId = grade.Id;
        }

        var appointed = new DateTime(2022, 1, 1);
        var employment = new EmploymentRecord(
            employee.Id, institute.Id, null, null, null, gradeId,
            "Officer", null, staffCategory, null, null, "active", null, null, null, null,
            appointed, null, null, null, appointed, true);
        db.AddRange(institute, employee, employment);
        if (verifiedDegree)
        {
            var education = new EducationRecord(
                employee.Id, "University of Ghana", "Laboratory Technology", "BSc",
                QualificationLevels.BachelorOrEquivalent, null, null, null, null, null,
                new DateTime(2017, 9, 1), new DateTime(2021, 6, 1));
            education.SetInstitutionRecognitionStatus("verified");
            education.SetRelevantFieldStatus("verified", null, DateTimeOffset.UtcNow);
            db.EducationRecords.Add(education);
        }

        await db.SaveChangesAsync();
        return new LiveSeed(institute.Id, employee.Id, staffId);
    }

    private HttpClient Client(string role, Guid? employeeId, Guid? instituteId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role, employeeId, instituteId));
        return client;
    }

    private string Token(string role, Guid? employeeId, Guid? instituteId)
    {
        var jwt = _factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (employeeId.HasValue)
        {
            claims.Add(new("employee_id", employeeId.ToString()!));
            claims.Add(new("self", $"Self:{employeeId}"));
        }
        if (instituteId.HasValue)
            claims.Add(new("institute_id", instituteId.ToString()!));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: credentials));
    }

    private sealed record LiveSeed(Guid InstituteId, Guid EmployeeId, string StaffId);
}
