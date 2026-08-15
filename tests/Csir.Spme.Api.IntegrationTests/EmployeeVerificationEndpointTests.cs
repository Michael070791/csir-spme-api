using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class EmployeeVerificationEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public EmployeeVerificationEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PlatformAdmin_Can_Approve_Employee_Idempotently()
    {
        var employeeId = await SeedUnapprovedEmployeeAsync($"VAPP-{Guid.NewGuid():N}");
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var first = await client.PostAsync($"/api/v2/employee-verifications/{employeeId}/approve", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await first.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        approved.Should().NotBeNull();
        approved!.IsHrApproved.Should().BeTrue();
        approved.ProfileStatus.Should().Be("active");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var portalUrl = _factory.Services.GetRequiredService<IOptions<PortalUrlOptions>>().Value.StaffPortalUrl.TrimEnd('/');
            var key = employeeId.ToString("N");
            var messages = await db.CommunicationOutboxMessages.AsNoTracking()
                .Where(message => message.IdempotencyKey.Contains(key))
                .ToListAsync();
            messages.Should().ContainSingle(message => message.Channel == "email");
            messages.Should().ContainSingle(message => message.Channel == "sms");
            messages.Should().ContainSingle(message => message.Channel == "event");
            var email = messages.Single(message => message.Channel == "email");
            email.IsHtml.Should().BeTrue();
            email.Recipient.Should().EndWith("@example.test");
            email.Subject.Should().Be("Your CSIR staff portal access is ready");
            email.Body.Should().Contain(portalUrl)
                .And.Contain("staff record has been approved")
                .And.Contain("Apply for leave")
                .And.Contain("Submit quarterly reports");
            email.TextBody.Should().Contain("Open staff portal").And.Contain(portalUrl);
            var sms = messages.Single(message => message.Channel == "sms");
            sms.Recipient.Should().Be("0241112222");
            sms.Body.Should().Contain("approved").And.Contain(portalUrl);
            sms.Body.Length.Should().BeLessThan(160);
        }

        var second = await client.PostAsync($"/api/v2/employee-verifications/{employeeId}/approve", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await second.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        again!.IsHrApproved.Should().BeTrue();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var stored = await verifyDb.Employees.FindAsync(employeeId);
        stored!.IsHrApproved.Should().BeTrue();
        (await verifyDb.CommunicationOutboxMessages.CountAsync(message =>
            message.IdempotencyKey.Contains(employeeId.ToString("N")) &&
            (message.Channel == "email" || message.Channel == "sms"))).Should().Be(2);
    }

    [Fact]
    public async Task Scoped_HrAdmin_Can_Approve_In_Institute_And_Gets_NotFound_Outside()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"VA-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"VB-{suffix[..8]}");
        var inScope = await SeedUnapprovedEmployeeAsync($"VA-STF-{suffix}", instituteA);
        var outOfScope = await SeedUnapprovedEmployeeAsync($"VB-STF-{suffix}", instituteB);
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var ok = await hr.PostAsync($"/api/v2/employee-verifications/{inScope}/approve", null);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var denied = await hr.PostAsync($"/api/v2/employee-verifications/{outOfScope}/approve", null);
        denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unscoped_HrAdmin_Can_Approve_Across_Institutes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"UA-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"UB-{suffix[..8]}");
        var employeeA = await SeedUnapprovedEmployeeAsync($"UA-STF-{suffix}", instituteA);
        var employeeB = await SeedUnapprovedEmployeeAsync($"UB-STF-{suffix}", instituteB);
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, null));

        var first = await hr.PostAsync($"/api/v2/employee-verifications/{employeeA}/approve", null);
        var second = await hr.PostAsync($"/api/v2/employee-verifications/{employeeB}/approve", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StaffUser_Cannot_Approve_Or_Bulk_Approve()
    {
        var instituteId = await SeedInstituteAsync($"STF-{Guid.NewGuid():N}"[..12]);
        var employeeId = await SeedUnapprovedEmployeeAsync($"STF-EMP-{Guid.NewGuid():N}", instituteId);
        var reader = Client(CreateToken("Reader", instituteId, identityType: "StaffUser"));

        var approve = await reader.PostAsync($"/api/v2/employee-verifications/{employeeId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var bulk = await reader.PostAsJsonAsync(
            "/api/v2/employee-verifications/bulk-approve",
            new BulkApproveEmployeesRequest([employeeId]));
        bulk.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Approve_Returns_Unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/v2/employee-verifications/{Guid.NewGuid()}/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bulk_Approve_Returns_Per_Item_Outcomes_And_Validates_Size()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"BA-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"BB-{suffix[..8]}");
        var unapproved = await SeedUnapprovedEmployeeAsync($"BA-U-{suffix}", instituteA);
        var alreadyApproved = await SeedApprovedEmployeeAsync($"BA-A-{suffix}", instituteA);
        var outOfScope = await SeedUnapprovedEmployeeAsync($"BB-U-{suffix}", instituteB);
        var unknown = Guid.NewGuid();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var empty = await hr.PostAsJsonAsync(
            "/api/v2/employee-verifications/bulk-approve",
            new BulkApproveEmployeesRequest([]));
        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var tooMany = await hr.PostAsJsonAsync(
            "/api/v2/employee-verifications/bulk-approve",
            new BulkApproveEmployeesRequest(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray()));
        tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var response = await hr.PostAsJsonAsync(
            "/api/v2/employee-verifications/bulk-approve",
            new BulkApproveEmployeesRequest([unapproved, alreadyApproved, outOfScope, unknown, unapproved]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BulkApproveEmployeesResponse>();
        body.Should().NotBeNull();
        body!.Approved.Should().Be(1);
        body.Skipped.Should().Be(1);
        body.Failed.Should().Be(2);
        body.Results.Should().HaveCount(4);
        body.Results.Should().ContainSingle(item => item.EmployeeId == unapproved && item.Outcome == "approved");
        body.Results.Should().ContainSingle(item => item.EmployeeId == alreadyApproved && item.Outcome == "skipped-already-approved");
        body.Results.Should().Contain(item => item.EmployeeId == outOfScope && item.Outcome == "not-found");
        body.Results.Should().Contain(item => item.EmployeeId == unknown && item.Outcome == "not-found");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.Employees.FindAsync(unapproved))!.IsHrApproved.Should().BeTrue();
        (await db.Employees.FindAsync(outOfScope))!.IsHrApproved.Should().BeFalse();
        (await db.CommunicationOutboxMessages.CountAsync(message =>
            message.IdempotencyKey.Contains(unapproved.ToString("N")) &&
            (message.Channel == "email" || message.Channel == "sms"))).Should().Be(2);
        (await db.CommunicationOutboxMessages.CountAsync(message =>
            message.IdempotencyKey.Contains(alreadyApproved.ToString("N")))).Should().Be(0);
    }

    [Fact]
    public async Task Reject_Clears_Hr_Approval_Without_Changing_Profile_Status()
    {
        var employeeId = await SeedApprovedEmployeeAsync($"REJ-{Guid.NewGuid():N}");
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var response = await client.PostAsync($"/api/v2/employee-verifications/{employeeId}/reject", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        detail!.IsHrApproved.Should().BeFalse();
        detail.ProfileStatus.Should().Be("active");
    }

    [Fact]
    public async Task Approve_Skips_Placeholder_Email_And_Still_Sends_Sms()
    {
        var staffId = $"VPH-{Guid.NewGuid():N}";
        var instituteId = await SeedInstituteAsync($"VPH-{Guid.NewGuid():N}"[..12]);
        Guid employeeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var employee = new Employee(instituteId, staffId, "Placeholder", "male");
            employee.UpdateProfile(
                staffId, "Mr.", "Placeholder", "Contact", "male", null, "Ghanaian", null, "single",
                $"{staffId.ToLowerInvariant()}@pending.csir.local", "0245556666", "active", false);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            employeeId = employee.Id;
        }

        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var response = await client.PostAsync($"/api/v2/employee-verifications/{employeeId}/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var key = employeeId.ToString("N");
        (await verifyDb.CommunicationOutboxMessages.CountAsync(message =>
            message.IdempotencyKey.Contains(key) && message.Channel == "email")).Should().Be(0);
        var sms = await verifyDb.CommunicationOutboxMessages.SingleAsync(message =>
            message.IdempotencyKey.Contains(key) && message.Channel == "sms");
        sms.Recipient.Should().Be("0245556666");
        sms.Body.Should().Contain("approved");
    }

    private async Task<Guid> SeedInstituteAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var institute = new Institute(code, $"Verify Institute {code}", "Institute");
        db.Institutes.Add(institute);
        await db.SaveChangesAsync();
        return institute.Id;
    }

    private async Task<Guid> SeedUnapprovedEmployeeAsync(string staffId, Guid? instituteId = null)
    {
        instituteId ??= await SeedInstituteAsync($"VI-{Guid.NewGuid():N}"[..12]);
        return await SeedEmployeeAsync(instituteId.Value, staffId, isHrApproved: false);
    }

    private async Task<Guid> SeedApprovedEmployeeAsync(string staffId, Guid? instituteId = null)
    {
        instituteId ??= await SeedInstituteAsync($"VI-{Guid.NewGuid():N}"[..12]);
        return await SeedEmployeeAsync(instituteId.Value, staffId, isHrApproved: true);
    }

    private async Task<Guid> SeedEmployeeAsync(Guid instituteId, string staffId, bool isHrApproved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = new Employee(instituteId, staffId, $"Surname-{staffId[^8..]}", "female");
        employee.UpdateProfile(
            staffId,
            "Ms.",
            employee.Surname,
            "Verify",
            "female",
            new DateTime(1991, 4, 2),
            "Ghanaian",
            null,
            "single",
            $"{staffId.ToLowerInvariant()}@example.test",
            "0241112222",
            "active",
            isHrApproved);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
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

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var user = new User($"verify.{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.test", identityType ?? role);
        if (instituteId.HasValue)
            user.AssignInstitute(instituteId.Value, identityType ?? role);
        db.Users.Add(user);
        db.SaveChanges();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Role, role),
            new("identity_type", identityType ?? role),
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
