using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Csir.Spme.Api.Auth;
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

public sealed class PortalProfileEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public PortalProfileEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Portal_Profile_Is_SelfScoped_Minimized_And_Uses_ServerResolvedPermissions()
    {
        var employee = await CreateEmployeeUserAsync(SpmeRoles.Employee, [SpmePermissions.MemosRead]);
        var approver = await CreateEmployeeUserAsync(SpmeRoles.HeadOfSection, [SpmePermissions.LeaveApprove]);

        using var employeeClient = CreateClient(CreateToken(employee.UserId, approver.EmployeeId));
        var employeeResponse = await employeeClient.GetAsync("/api/v2/me/portal-profile");
        employeeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await employeeResponse.Content.ReadAsStringAsync());
        employeeResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var employeeBody = await employeeResponse.Content.ReadAsStringAsync();
        using var employeeJson = JsonDocument.Parse(employeeBody);

        var employeeData = employeeJson.RootElement;
        employeeData.GetProperty("userId").GetGuid().Should().Be(employee.UserId);
        employeeData.GetProperty("employeeId").GetGuid().Should().Be(employee.EmployeeId);
        employeeData.GetProperty("staffId").GetString().Should().Be(employee.StaffId);
        employeeData.GetProperty("displayName").GetString().Should().Be("Dr. Ada Employee");
        employeeData.GetProperty("preferredName").ValueKind.Should().Be(JsonValueKind.Null);
        employeeData.GetProperty("jobTitle").GetString().Should().Be("Research Officer");
        employeeData.GetProperty("staffCategory").GetString().Should().Be("senior-staff");
        employeeData.GetProperty("institute").GetProperty("code").GetString().Should().Be(employee.InstituteCode);
        employeeData.GetProperty("contact").GetProperty("email").GetString().Should().Be(employee.Email);
        employeeData.GetProperty("contact").GetProperty("emailConfirmed").GetBoolean().Should().BeTrue();
        employeeData.GetProperty("contact").GetProperty("employeeContactVerified").GetBoolean().Should().BeTrue();
        employeeData.GetProperty("permissions").EnumerateArray().Select(value => value.GetString())
            .Should().Contain(SpmePermissions.MemosRead)
            .And.NotContain(SpmePermissions.LeaveApprove);
        employeeData.GetProperty("isHod").GetBoolean().Should().BeFalse();
        employeeData.GetProperty("isDirector").GetBoolean().Should().BeFalse();
        employeeData.GetProperty("leadershipRoles").GetArrayLength().Should().Be(0);
        employeeData.GetProperty("profileCompletion").GetInt32().Should().Be(90);

        // The forged employee claim points at an approver. The response remains bound to the subject's persisted link.
        employeeData.GetProperty("staffId").GetString().Should().NotBe(approver.StaffId);
        employeeBody.Should().NotContain(employee.Phone);
        employeeBody.Should().NotContain("1990-01-02");
        employeeData.TryGetProperty("roles", out _).Should().BeFalse();
        employeeData.TryGetProperty("phone", out _).Should().BeFalse();
        employeeData.TryGetProperty("dateOfBirth", out _).Should().BeFalse();

        using var approverClient = CreateClient(CreateToken(approver.UserId, employee.EmployeeId));
        var approverResponse = await approverClient.GetAsync("/api/v2/me/portal-profile");
        approverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var approverJson = JsonDocument.Parse(await approverResponse.Content.ReadAsStringAsync());
        approverJson.RootElement.GetProperty("staffId").GetString().Should().Be(approver.StaffId);
        approverJson.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString())
            .Should().Contain(SpmePermissions.LeaveApprove)
            .And.NotContain(SpmePermissions.MemosRead);
        approverJson.RootElement.GetProperty("isHod").GetBoolean().Should().BeTrue();
        approverJson.RootElement.GetProperty("isDirector").GetBoolean().Should().BeFalse();
        approverJson.RootElement.GetProperty("leadershipRoles").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("Head of Section");
    }

    [Fact]
    public async Task Portal_Profile_Surfaces_Employment_Leadership_And_Director_Flags()
    {
        var director = await CreateEmployeeUserAsync(
            SpmeRoles.InstituteDirector,
            [SpmePermissions.LeaveApprove],
            leadershipRoles: "Institute Director, Head of Division");

        using var client = CreateClient(CreateToken(director.UserId, director.EmployeeId));
        var response = await client.GetAsync("/api/v2/me/portal-profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement;
        data.GetProperty("isHod").GetBoolean().Should().BeTrue();
        data.GetProperty("isDirector").GetBoolean().Should().BeTrue();
        data.GetProperty("leadershipRoles").EnumerateArray().Select(value => value.GetString())
            .Should().BeEquivalentTo("Institute Director", "Head of Division");
    }

    [Fact]
    public async Task Portal_Profile_Includes_Canonical_Present_Grade()
    {
        var employee = await CreateEmployeeUserAsync(
            SpmeRoles.Employee,
            [SpmePermissions.MemosRead],
            gradeCode: $"to-{Guid.NewGuid():N}"[..12],
            gradeName: "Technical Officer");

        using var client = CreateClient(CreateToken(employee.UserId, employee.EmployeeId));
        var response = await client.GetAsync("/api/v2/me/portal-profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("staffCategory").GetString().Should().Be("senior-staff");
        json.RootElement.GetProperty("gradeName").GetString().Should().Be("Technical Officer");
        json.RootElement.GetProperty("gradeCode").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.GetProperty("jobTitle").GetString().Should().Be("Research Officer");
    }

    [Fact]
    public async Task Portal_Profile_Requires_Authentication()
    {
        using var client = _factory.CreateClient();
        (await client.GetAsync("/api/v2/me/portal-profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Portal_Profile_Does_Not_Verify_An_Unconfirmed_Linked_Account()
    {
        var employee = await CreateEmployeeUserAsync(
            SpmeRoles.Employee,
            [SpmePermissions.MemosRead],
            emailConfirmed: false,
            phoneConfirmed: false);

        using var client = CreateClient(CreateToken(employee.UserId, employee.EmployeeId));
        var response = await client.GetAsync("/api/v2/me/portal-profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("contact").GetProperty("employeeContactVerified").GetBoolean()
            .Should().BeFalse();
    }

    private async Task<PortalProfileSeed> CreateEmployeeUserAsync(
        string roleName,
        IReadOnlyList<string> rolePermissions,
        bool emailConfirmed = true,
        bool phoneConfirmed = true,
        string? leadershipRoles = null,
        string? gradeCode = null,
        string? gradeName = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var suffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"P{suffix}"[..12], $"Portal Profile {suffix}", "institute");
        var employee = new Employee(institute.Id, $"SP-{suffix[..8]}", "Employee", "female");
        employee.UpdateImportedProfile("Dr.", "Ada", new DateTime(1990, 1, 2), "Ghanaian", null,
            "single", $"employee.{suffix}@example.test", "0244991234", isHrApproved: true);
        db.Institutes.Add(institute);
        db.Employees.Add(employee);

        Guid? gradeId = null;
        if (!string.IsNullOrWhiteSpace(gradeCode))
        {
            var grade = Grade.Create(gradeCode, gradeName ?? gradeCode, StaffCategories.SeniorStaff, "technical", 1, 10);
            db.Grades.Add(grade);
            gradeId = grade.Id;
        }

        db.EmploymentRecords.Add(new EmploymentRecord(
            employee.Id,
            institute.Id,
            null,
            null,
            null,
            gradeId,
            "Research Officer",
            leadershipRoles,
            "senior-staff",
            null,
            null,
            "active",
            null,
            null,
            null,
            null,
            new DateTime(2020, 1, 1),
            null,
            null,
            null,
            new DateTime(2020, 1, 1),
            isCurrent: true));
        await db.SaveChangesAsync();

        await EnsureRoleWithPermissionsAsync(roleManager, roleName, rolePermissions);
        var email = $"account.{suffix}@example.test";
        var user = new User(email, "Employee")
        {
            Email = email,
            EmailConfirmed = emailConfirmed,
            PhoneNumberConfirmed = phoneConfirmed
        };
        user.LinkEmployee(employee.Id, institute.Id);
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, roleName)).Succeeded.Should().BeTrue();
        return new PortalProfileSeed(user.Id, employee.Id, employee.StaffId, institute.Code, email, "0244991234");
    }

    private static async Task EnsureRoleWithPermissionsAsync(
        RoleManager<Role> roleManager,
        string roleName,
        IReadOnlyList<string> permissions)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            role = new Role(roleName, roleName, $"{roleName} portal profile test role.", isSystemRole: true);
            (await roleManager.CreateAsync(role)).Succeeded.Should().BeTrue();
        }

        var existing = await roleManager.GetClaimsAsync(role);
        foreach (var permission in permissions)
        {
            if (!existing.Any(claim => claim.Type == "permission" && claim.Value == permission))
                (await roleManager.AddClaimAsync(role, new Claim("permission", permission))).Succeeded.Should().BeTrue();
        }
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(Guid userId, Guid forgedEmployeeId)
    {
        var jwt = _factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("employee_id", forgedEmployeeId.ToString()),
            new Claim("institute_id", Guid.NewGuid().ToString()),
            new Claim("permission", "forged.permission")
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

    private sealed record PortalProfileSeed(
        Guid UserId,
        Guid EmployeeId,
        string StaffId,
        string InstituteCode,
        string Email,
        string Phone);
}
