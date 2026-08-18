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
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class EmployeeSelfWorkEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public EmployeeSelfWorkEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SelfWork_Returns_Appointment_Current_Grade_And_Ladder()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        using var client = CreateClient(seed.Token);

        var response = await client.GetAsync($"/api/v2/employees/{seed.EmployeeId}/self-work");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<EmployeeSelfWorkResponse>();
        body.Should().NotBeNull();
        body!.AppointmentDate.Should().Be(new DateTime(2015, 3, 1));
        body.CurrentGrade.GradeName.Should().Be("Senior Technologist");
        body.StaffCategory.Should().Be(StaffCategories.SeniorStaff);
        body.GradePromotions.Should().HaveCount(3);
        body.GradePromotions.Select(item => item.GradeName).Should().ContainInOrder(
            "Technical Officer",
            "Senior Technical Officer",
            "Senior Technologist");
        body.GradePromotions.Single(item => item.IsCurrent).GradeName.Should().Be("Senior Technologist");
    }

    [Fact]
    public async Task SelfWork_Allows_Staff_To_Update_Grade_Promotion_Dates()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        using var client = CreateClient(seed.Token);

        var update = new UpdateEmployeeSelfWorkRequest([
            new UpdateEmployeeGradePromotionRequest(seed.StoGradeId, new DateTime(2018, 6, 1)),
            new UpdateEmployeeGradePromotionRequest(seed.SeniorTechnologistGradeId, new DateTime(2022, 1, 15))
        ]);
        var patch = await client.PatchAsJsonAsync($"/api/v2/employees/{seed.EmployeeId}/self-work", update);
        patch.StatusCode.Should().Be(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var saved = await patch.Content.ReadFromJsonAsync<EmployeeSelfWorkResponse>();
        saved!.GradePromotions.Single(item => item.GradeId == seed.StoGradeId).PromotionDate
            .Should().Be(new DateTime(2018, 6, 1));
        saved.GradePromotions.Single(item => item.IsCurrent).PromotionDate
            .Should().Be(new DateTime(2022, 1, 15));
        saved.YearsInCurrentGrade.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SelfWork_Allows_Staff_To_Update_Location_And_Specialization()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        using var client = CreateClient(seed.Token);

        var update = new UpdateEmployeeSelfWorkRequest(
            GradePromotions: null,
            Location: "Accra",
            AreaOfSpecialization: "Forest products");
        var patch = await client.PatchAsJsonAsync($"/api/v2/employees/{seed.EmployeeId}/self-work", update);
        patch.StatusCode.Should().Be(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var saved = await patch.Content.ReadFromJsonAsync<EmployeeSelfWorkResponse>();
        saved!.Location.Should().Be("Accra");
        saved.AreaOfSpecialization.Should().Be("Forest products");
        saved.StaffCategory.Should().Be(StaffCategories.SeniorStaff);
    }

    [Fact]
    public async Task SelfWork_Allows_Senior_Members_To_Update_Research_Interests()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var employment = db.EmploymentRecords.Single(record =>
                record.EmployeeId == seed.EmployeeId && record.IsCurrent);
            employment.UpdateCurrent(
                employment.DivisionId,
                employment.SectionId,
                employment.PositionTypeId,
                employment.GradeId,
                employment.JobTitle,
                employment.LeadershipRoles,
                StaffCategories.SeniorMember,
                employment.GradeStep,
                employment.AreaOfSpecialization,
                employment.ServiceStatus,
                employment.Organization,
                employment.Location,
                employment.Region,
                employment.District,
                employment.AppointmentDate,
                employment.PromotionDate,
                employment.PensionType,
                employment.PensionId);
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(seed.Token);
        var update = new UpdateEmployeeSelfWorkRequest(
            GradePromotions: null,
            ResearchInterests: "Coastal materials and water quality.");
        var patch = await client.PatchAsJsonAsync($"/api/v2/employees/{seed.EmployeeId}/self-work", update);
        patch.StatusCode.Should().Be(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var saved = await patch.Content.ReadFromJsonAsync<EmployeeSelfWorkResponse>();
        saved!.ResearchInterests.Should().Be("Coastal materials and water quality.");
    }

    [Fact]
    public async Task SelfWork_Rejects_Research_Interests_For_Non_Senior_Members()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        using var client = CreateClient(seed.Token);

        var update = new UpdateEmployeeSelfWorkRequest(
            GradePromotions: null,
            ResearchInterests: "Should not be stored.");
        var patch = await client.PatchAsJsonAsync($"/api/v2/employees/{seed.EmployeeId}/self-work", update);
        patch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SelfWork_Counts_Years_From_Appointment_When_Promotion_Date_Is_Missing()
    {
        var seed = await CreateEmployeeUserAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var employment = db.EmploymentRecords.Single(record =>
                record.EmployeeId == seed.EmployeeId && record.IsCurrent);
            employment.UpdateCurrent(
                employment.DivisionId,
                employment.SectionId,
                employment.PositionTypeId,
                employment.GradeId,
                employment.JobTitle,
                employment.LeadershipRoles,
                employment.StaffCategory,
                employment.GradeStep,
                employment.AreaOfSpecialization,
                employment.ServiceStatus,
                employment.Organization,
                employment.Location,
                employment.Region,
                employment.District,
                new DateTime(2021, 1, 1),
                null,
                employment.PensionType,
                employment.PensionId);
            await db.SaveChangesAsync();
        }

        using var client = CreateClient(CreateToken(seed.UserId, seed.EmployeeId));
        var response = await client.GetAsync($"/api/v2/employees/{seed.EmployeeId}/self-work");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<EmployeeSelfWorkResponse>();
        var expected = Math.Max(0m, (decimal)(DateTimeOffset.UtcNow - new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalDays / 365.2425m);

        body.Should().NotBeNull();
        body!.AppointmentDate.Should().Be(new DateTime(2021, 1, 1));
        body.YearsInCurrentGrade.Should().BeApproximately(decimal.Round(expected, 2), 0.02m);
        body.YearsInCurrentGrade.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task SelfWork_Rejects_Forged_Employee_Access()
    {
        var seed = await SeedEmployeeWithGradeLadderAsync();
        var other = await CreateEmployeeUserAsync();
        using var client = CreateClient(seed.Token);

        var response = await client.GetAsync($"/api/v2/employees/{other.EmployeeId}/self-work");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<SelfWorkSeed> SeedEmployeeWithGradeLadderAsync()
    {
        var portalSeed = await CreateEmployeeUserAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        var stream = $"technical-{Guid.NewGuid():N}"[..20];
        var to = Grade.Create($"to-{Guid.NewGuid():N}"[..12], "Technical Officer", StaffCategories.SeniorStaff, stream, 1, 8);
        var sto = Grade.Create($"sto-{Guid.NewGuid():N}"[..12], "Senior Technical Officer", StaffCategories.SeniorStaff, stream, 2, 9);
        var st = Grade.Create($"st-{Guid.NewGuid():N}"[..12], "Senior Technologist", StaffCategories.SeniorStaff, stream, 3, 10);
        db.Grades.AddRange(to, sto, st);

        var employment = db.EmploymentRecords.Single(record =>
            record.EmployeeId == portalSeed.EmployeeId && record.IsCurrent);
        employment.UpdateCurrent(
            employment.DivisionId,
            employment.SectionId,
            employment.PositionTypeId,
            st.Id,
            "Senior Technologist",
            employment.LeadershipRoles,
            StaffCategories.SeniorStaff,
            employment.GradeStep,
            employment.AreaOfSpecialization,
            employment.ServiceStatus,
            employment.Organization,
            employment.Location,
            employment.Region,
            employment.District,
            new DateTime(2015, 3, 1),
            new DateTime(2022, 1, 15),
            employment.PensionType,
            employment.PensionId);
        await db.SaveChangesAsync();

        return new SelfWorkSeed(
            portalSeed.EmployeeId,
            CreateToken(portalSeed.UserId, portalSeed.EmployeeId),
            to.Id,
            sto.Id,
            st.Id);
    }

    private async Task<EmployeeSeed> CreateEmployeeUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var suffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"W{suffix}"[..12], $"Work Profile {suffix}", "institute");
        var employee = new Employee(institute.Id, $"SW-{suffix[..8]}", "Worker", "female");
        employee.UpdateImportedProfile("Mr.", "Work", new DateTime(1990, 1, 2), "Ghanaian", null,
            "single", $"work.{suffix}@example.test", "0244991234", isHrApproved: true);
        db.Institutes.Add(institute);
        db.Employees.Add(employee);
        db.EmploymentRecords.Add(new EmploymentRecord(
            employee.Id,
            institute.Id,
            null,
            null,
            null,
            null,
            "Research Officer",
            null,
            StaffCategories.SeniorStaff,
            null,
            null,
            "active",
            null,
            null,
            null,
            null,
            new DateTime(2015, 3, 1),
            null,
            null,
            null,
            new DateTime(2020, 1, 1),
            isCurrent: true));
        await db.SaveChangesAsync();

        var role = await roleManager.FindByNameAsync(SpmeRoles.Employee);
        if (role is null)
        {
            role = new Role(SpmeRoles.Employee, SpmeRoles.Employee, "Employee", isSystemRole: true);
            (await roleManager.CreateAsync(role)).Succeeded.Should().BeTrue();
        }

        var email = $"work.{suffix}@example.test";
        var user = new User(email, "Worker") { Email = email, EmailConfirmed = true, PhoneNumberConfirmed = true };
        user.LinkEmployee(employee.Id, institute.Id);
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, SpmeRoles.Employee)).Succeeded.Should().BeTrue();
        return new EmployeeSeed(user.Id, employee.Id);
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(Guid userId, Guid employeeId)
    {
        var jwt = _factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("employee_id", employeeId.ToString()),
            new Claim("institute_id", Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer"),
            jwt.GetValue<string>("Audience"),
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record EmployeeSeed(Guid UserId, Guid EmployeeId);

    private sealed record SelfWorkSeed(
        Guid EmployeeId,
        string Token,
        Guid ToGradeId,
        Guid StoGradeId,
        Guid SeniorTechnologistGradeId);
}
