using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class HrInstituteScopeEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public HrInstituteScopeEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PlatformAdmin_Can_Assign_Institute_Without_Changing_StaffUser_IdentityType()
    {
        var seed = await SeedAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var response = await client.PatchAsJsonAsync(
            $"/api/v2/system-users/{seed.UnscopedStaffUserId}/institute",
            new { instituteId = seed.InstituteA });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SystemUserResponse>();
        body.Should().NotBeNull();
        body!.InstituteId.Should().Be(seed.InstituteA);
        body.IdentityType.Should().Be("StaffUser");
        body.Roles.Should().Contain("Reader");
        body.Roles.Should().NotContain(SpmeRoles.HrAdmin);
    }

    [Fact]
    public async Task Assigning_HrAdmin_Without_Institute_Is_Rejected()
    {
        var seed = await SeedAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var response = await client.PatchAsJsonAsync(
            $"/api/v2/system-users/{seed.UnscopedStaffUserId}/roles",
            new { roles = new[] { SpmeRoles.HrAdmin } });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Unscoped_HrAdmin_Can_List_Employees_But_Not_Dashboard_Or_Divisions()
    {
        var seed = await SeedAsync();
        var client = Client(CreateToken(SpmeRoles.HrAdmin, null));

        var employees = await client.GetAsync("/api/v2/employees");
        employees.EnsureSuccessStatusCode();
        var page = await employees.Content.ReadFromJsonAsync<EmployeePageResponse>();
        page.Should().NotBeNull();
        page!.Items.Should().Contain(item => item.Id == seed.EmployeeA);
        page.Items.Should().Contain(item => item.Id == seed.EmployeeB);

        var dashboard = await client.GetAsync("/api/v2/hr/dashboard");
        dashboard.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var divisions = await client.GetAsync("/api/v2/divisions");
        divisions.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Legacy_Reader_With_Institute_Can_Read_But_Cannot_Write()
    {
        var seed = await SeedAsync();
        var reader = Client(CreateToken("Reader", seed.InstituteA, identityType: "StaffUser"));

        var employees = await reader.GetAsync("/api/v2/employees");
        employees.EnsureSuccessStatusCode();
        var page = await employees.Content.ReadFromJsonAsync<PageResponse<EmployeeListItem>>();
        page.Should().NotBeNull();
        page!.Items.Should().Contain(item => item.Id == seed.EmployeeA);
        page.Items.Should().NotContain(item => item.Id == seed.EmployeeB);

        var dashboard = await reader.GetAsync("/api/v2/hr/dashboard");
        dashboard.EnsureSuccessStatusCode();
        var dashEnvelope = await dashboard.Content.ReadFromJsonAsync<DataResponse<HrDashboardResponse>>();
        dashEnvelope.Should().NotBeNull();
        dashEnvelope!.Data.TotalEmployees.Should().BeGreaterThanOrEqualTo(1);

        var divisions = await reader.GetAsync("/api/v2/divisions");
        divisions.EnsureSuccessStatusCode();

        var createEmployee = await reader.PostAsJsonAsync("/api/v2/employees", new
        {
            staffId = $"LEG-{Guid.NewGuid():N}"[..12],
            surname = "Blocked",
            gender = "female"
        });
        createEmployee.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createDivision = await reader.PostAsJsonAsync("/api/v2/divisions", new
        {
            name = $"Blocked Division {Guid.NewGuid():N}",
            code = "BD"
        });
        createDivision.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var memos = await reader.GetAsync("/api/v2/memos");
        memos.EnsureSuccessStatusCode();
        var memoPage = await memos.Content.ReadFromJsonAsync<CollectionResponse<MemoResponse>>();
        memoPage.Should().NotBeNull();

        var notifications = await reader.GetAsync("/api/v2/notifications?unreadOnly=true");
        notifications.EnsureSuccessStatusCode();

        var createMemo = await reader.PostAsJsonAsync("/api/v2/memos", new CreateMemoRequest(
            "Blocked memo",
            "Legacy readers must not create memos.",
            null));
        createMemo.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HrAdmin_With_Institute_Can_Read_Dashboard_And_Cross_Institute_Is_Not_Disclosed()
    {
        var seed = await SeedAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, seed.InstituteA));

        var dashboard = await hr.GetAsync("/api/v2/hr/dashboard");
        dashboard.EnsureSuccessStatusCode();
        var envelope = await dashboard.Content.ReadFromJsonAsync<DataResponse<HrDashboardResponse>>();
        envelope.Should().NotBeNull();
        envelope!.Data.TotalEmployees.Should().BeGreaterThanOrEqualTo(1);

        var cross = await hr.GetAsync($"/api/v2/hr/dashboard?instituteId={seed.InstituteB}");
        cross.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Legacy_HR_Role_Can_Read_Employees_In_Scope()
    {
        var seed = await SeedAsync();
        var hrLegacy = Client(CreateToken("HR", seed.InstituteA, identityType: "StaffUser"));

        var employees = await hrLegacy.GetAsync("/api/v2/employees");
        employees.EnsureSuccessStatusCode();
        var page = await employees.Content.ReadFromJsonAsync<PageResponse<EmployeeListItem>>();
        page!.Items.Should().OnlyContain(item => item.Institute!.Id == seed.InstituteA);
    }

    [Fact]
    public async Task Legacy_StaffUser_Can_List_Institute_Leave_And_PlatformAdmin_Sees_All()
    {
        var seed = await SeedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        var leaveA = LeaveRequest.CreateDraft(
            seed.EmployeeA,
            seed.InstituteA,
            "annual",
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(2),
            2m,
            null,
            null,
            null,
            null,
            null,
            null);
        var leaveB = LeaveRequest.CreateDraft(
            seed.EmployeeB,
            seed.InstituteB,
            "annual",
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(1),
            1m,
            null,
            null,
            null,
            null,
            null,
            null);
        db.LeaveRequests.AddRange(leaveA, leaveB);
        await db.SaveChangesAsync();

        var reader = Client(CreateToken("Reader", seed.InstituteA, identityType: "StaffUser"));
        var scopedLeave = await reader.GetAsync("/api/v2/leave-requests?limit=50");
        scopedLeave.EnsureSuccessStatusCode();
        var scoped = await scopedLeave.Content.ReadFromJsonAsync<ListResponse<LeaveRequestDto>>();
        scoped.Should().NotBeNull();
        scoped!.Data.Should().Contain(item => item.Id == leaveA.Id);
        scoped.Data.Should().NotContain(item => item.Id == leaveB.Id);

        var platform = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var allLeave = await platform.GetAsync("/api/v2/leave-requests?limit=50");
        allLeave.EnsureSuccessStatusCode();
        var all = await allLeave.Content.ReadFromJsonAsync<ListResponse<LeaveRequestDto>>();
        all!.Data.Should().Contain(item => item.Id == leaveA.Id);
        all.Data.Should().Contain(item => item.Id == leaveB.Id);

        var unscopedHr = Client(CreateToken(SpmeRoles.HrAdmin, null));
        var hrAll = await unscopedHr.GetAsync("/api/v2/leave-requests?limit=50");
        hrAll.EnsureSuccessStatusCode();
        var hrBody = await hrAll.Content.ReadFromJsonAsync<ListResponse<LeaveRequestDto>>();
        hrBody!.Data.Should().Contain(item => item.Id == leaveA.Id);
        hrBody.Data.Should().Contain(item => item.Id == leaveB.Id);
    }

    private async Task<ScopeSeed> SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        foreach (var role in new[] { SpmeRoles.PlatformAdmin, SpmeRoles.HrAdmin, SpmeRoles.Employee, "Reader", "HR", "Writer" })
            await EnsureRoleAsync(roleManager, role);

        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = new Institute($"HRA-{suffix[..8]}", $"HR Scope A {suffix}", "Institute");
        var instituteB = new Institute($"HRB-{suffix[..8]}", $"HR Scope B {suffix}", "Institute");
        db.Institutes.AddRange(instituteA, instituteB);
        await db.SaveChangesAsync();

        var employeeA = new Employee(instituteA.Id, $"HRA-{suffix}", "Owusu", "male");
        var employeeB = new Employee(instituteB.Id, $"HRB-{suffix}", "Mensah", "female");
        db.Employees.AddRange(employeeA, employeeB);
        await db.SaveChangesAsync();

        var unscoped = new User($"unscoped-staff-{suffix}@example.test", "StaffUser")
        {
            Email = $"unscoped-staff-{suffix}@example.test",
            EmailConfirmed = true
        };
        (await userManager.CreateAsync(unscoped)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(unscoped, "Reader")).Succeeded.Should().BeTrue();

        return new ScopeSeed(instituteA.Id, instituteB.Id, employeeA.Id, employeeB.Id, unscoped.Id);
    }

    private static async Task EnsureRoleAsync(RoleManager<Role> roleManager, string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;
        var result = await roleManager.CreateAsync(new Role(role, role, $"{role} test role.", isSystemRole: true));
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(string role, Guid? instituteId, string? identityType = null)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, $"integration.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", identityType ?? role)
        };
        if (instituteId.HasValue)
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ScopeSeed(
        Guid InstituteA,
        Guid InstituteB,
        Guid EmployeeA,
        Guid EmployeeB,
        Guid UnscopedStaffUserId);
}
