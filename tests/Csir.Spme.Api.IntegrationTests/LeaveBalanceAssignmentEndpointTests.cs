using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class LeaveBalanceAssignmentEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public LeaveBalanceAssignmentEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HrAdmin_Can_Assign_Annual_Leave_Days_And_Directory_Shows_Remaining()
    {
        var leaveYear = (short)DateTime.UtcNow.Year;
        var (instituteId, seniorMember, _) = await SeedCategoryPairAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteId));

        var response = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(seniorMember, 36m, leaveYear));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<DataResponse<LeaveBalanceDto>>();
        body!.Data.EmployeeId.Should().Be(seniorMember);
        body.Data.LeaveType.Should().Be(LeaveTypes.Annual);
        body.Data.LeaveYear.Should().Be(leaveYear);
        body.Data.TotalDays.Should().Be(36m);
        body.Data.RemainingDays.Should().Be(36m);

        var list = await hr.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?instituteId={instituteId}&page=1&pageSize=20");
        list!.Items.Single(item => item.Id == seniorMember).RemainingAnnualLeaveDays.Should().Be(36m);
    }

    [Fact]
    public async Task Assign_Preserves_Used_Days_And_Rejects_Entitlement_Below_Usage()
    {
        var leaveYear = (short)DateTime.UtcNow.Year;
        var (instituteId, employeeId, _) = await SeedCategoryPairAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.LeaveBalances.Add(LeaveBalance.CreateImported(
                employeeId, LeaveTypes.Annual, leaveYear, 20m, 8m, 2m, 0m));
            await db.SaveChangesAsync();
        }

        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteId));
        var updated = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeId, 40m, leaveYear));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await updated.Content.ReadFromJsonAsync<DataResponse<LeaveBalanceDto>>();
        assigned!.Data.TotalDays.Should().Be(40m);
        assigned.Data.UsedDays.Should().Be(8m);
        assigned.Data.PendingDays.Should().Be(2m);
        assigned.Data.RemainingDays.Should().Be(30m);

        var tooLow = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeId, 9m, leaveYear));
        tooLow.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bulk_Assign_Can_Target_One_Staff_Category_And_Skip_Others()
    {
        var leaveYear = (short)DateTime.UtcNow.Year;
        var (instituteId, seniorMember, juniorStaff) = await SeedCategoryPairAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteId));

        var response = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/bulk-assignments",
            new BulkAssignAnnualLeaveRequest(
                [seniorMember, juniorStaff],
                42m,
                leaveYear,
                StaffCategories.SeniorMember));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<DataResponse<BulkAssignAnnualLeaveResult>>();
        body!.Data.Assigned.Should().Be(1);
        body.Data.Skipped.Should().Be(1);
        body.Data.Failed.Should().Be(0);
        body.Data.Results.Should().ContainSingle(item =>
            item.EmployeeId == seniorMember && item.Outcome == "assigned");
        body.Data.Results.Should().ContainSingle(item =>
            item.EmployeeId == juniorStaff && item.Outcome == "skipped-category-mismatch");

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.LeaveBalances.SingleAsync(balance =>
            balance.EmployeeId == seniorMember && balance.LeaveYear == leaveYear)).TotalDays.Should().Be(42m);
        (await db.LeaveBalances.CountAsync(balance =>
            balance.EmployeeId == juniorStaff && balance.LeaveYear == leaveYear)).Should().Be(0);
    }

    [Fact]
    public async Task Scoped_HrAdmin_Cannot_Assign_Outside_Institute_And_Employees_Cannot_Assign()
    {
        var (instituteA, employeeA, _) = await SeedCategoryPairAsync();
        var (_, employeeB, _) = await SeedCategoryPairAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));
        var staff = Client(CreateToken(SpmeRoles.Employee, instituteA));

        var hidden = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeB, 21m, (short)DateTime.UtcNow.Year));
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var forbidden = await staff.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeA, 21m, (short)DateTime.UtcNow.Year));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("HR")]
    [InlineData("Admin")]
    [InlineData("Writer")]
    [InlineData(SpmeRoles.PlatformAdmin)]
    public async Task Hr_Write_Roles_Can_Assign_Annual_Leave_In_Institute(string role)
    {
        var leaveYear = (short)DateTime.UtcNow.Year;
        var (instituteId, employeeId, _) = await SeedCategoryPairAsync();
        var client = Client(CreateToken(role, instituteId));

        var response = await client.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeId, 28m, leaveYear));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<DataResponse<LeaveBalanceDto>>();
        body!.Data.EmployeeId.Should().Be(employeeId);
        body.Data.TotalDays.Should().Be(28m);
        body.Data.LeaveYear.Should().Be(leaveYear);
    }

    [Fact]
    public async Task Legacy_Reader_Cannot_Assign_Annual_Leave()
    {
        var (instituteId, employeeId, _) = await SeedCategoryPairAsync();
        var reader = Client(CreateToken("Reader", instituteId));

        var forbidden = await reader.PostAsJsonAsync(
            "/api/v2/leave-balances/assignments",
            new AssignAnnualLeaveRequest(employeeId, 21m, (short)DateTime.UtcNow.Year));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bulk_Assign_Rejects_Empty_And_Oversized_Selections()
    {
        var hr = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var empty = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/bulk-assignments",
            new BulkAssignAnnualLeaveRequest([], 21m));
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var tooMany = await hr.PostAsJsonAsync(
            "/api/v2/leave-balances/bulk-assignments",
            new BulkAssignAnnualLeaveRequest(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray(), 21m));
        tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<(Guid InstituteId, Guid SeniorMemberId, Guid JuniorStaffId)> SeedCategoryPairAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var institute = new Institute($"LV-{suffix}", $"Leave Institute {suffix}", "Institute");
        var division = new Division(institute.Id, $"Division {suffix}");
        var section = new Section(division.Id, $"Section {suffix}");
        db.Institutes.Add(institute);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        var senior = CreateEmployee(institute.Id, $"SM-{suffix}", StaffCategories.SeniorMember, division.Id, section.Id);
        var junior = CreateEmployee(institute.Id, $"JS-{suffix}", StaffCategories.JuniorStaff, division.Id, section.Id);
        db.Employees.AddRange(senior.Employee, junior.Employee);
        db.EmploymentRecords.AddRange(senior.Employment, junior.Employment);
        await db.SaveChangesAsync();
        return (institute.Id, senior.Employee.Id, junior.Employee.Id);
    }

    private static (Employee Employee, EmploymentRecord Employment) CreateEmployee(
        Guid instituteId,
        string staffId,
        string staffCategory,
        Guid divisionId,
        Guid sectionId)
    {
        var employee = new Employee(instituteId, staffId, $"Surname-{staffId[^6..]}", "female");
        employee.UpdateProfile(
            staffId,
            "Ms.",
            employee.Surname,
            "Leave",
            "female",
            null,
            "Ghanaian",
            null,
            "single",
            $"{staffId.ToLowerInvariant()}@example.test",
            "0241112222",
            "active",
            true);
        var employment = new EmploymentRecord(
            employee.Id,
            instituteId,
            divisionId,
            sectionId,
            null,
            null,
            "Officer",
            null,
            staffCategory,
            null,
            null,
            "active",
            "CSIR",
            "Accra",
            "Greater Accra",
            "Accra Metropolitan",
            new DateTime(2022, 1, 1),
            null,
            null,
            null,
            new DateTime(2024, 1, 1),
            true);
        return (employee, employment);
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(string role, Guid? instituteId)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var user = new User($"leave.{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.test", role);
        if (instituteId.HasValue)
            user.AssignInstitute(instituteId.Value, role);
        db.Users.Add(user);
        db.SaveChanges();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Role, role),
            new("identity_type", role),
            new("security_stamp", user.SecurityStamp!)
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
}
