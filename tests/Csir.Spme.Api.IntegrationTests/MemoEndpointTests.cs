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
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class MemoEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public MemoEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Institute_hr_can_preview_create_and_publish_to_the_whole_institute()
    {
        var fixture = await SeedAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, fixture.InstituteA));
        var request = new CreateMemoRequest(
            "Safety briefing",
            "Please review the laboratory safety update before next Monday.",
            [new MemoAudienceInput("all-employees", fixture.InstituteA, null, null, null, null)]);

        var preview = await hr.PostAsJsonAsync("/api/v2/memos/preview", request);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var previewBody = await preview.Content.ReadFromJsonAsync<MemoPreviewResponse>();
        previewBody!.RecipientCount.Should().Be(2);
        previewBody.InAppCount.Should().Be(2);
        previewBody.EmailCount.Should().Be(2);
        previewBody.SmsCount.Should().Be(2);
        previewBody.SmsSynopsis.Should().StartWith("Safety briefing:");

        var createdResponse = await hr.PostAsJsonAsync("/api/v2/memos", request);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var memo = await createdResponse.Content.ReadFromJsonAsync<MemoResponse>();
        memo!.Status.Should().Be("draft");
        memo.Audiences.Should().ContainSingle(audience => audience.AudienceType == "all-employees");

        var publish = await hr.PostAsync($"/api/v2/memos/{memo.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        var employee = Client(CreateToken(
            SpmeRoles.Employee, fixture.InstituteA, fixture.UserA, fixture.EmployeeA));
        var inboxResponse = await employee.GetAsync("/api/v2/notifications");
        inboxResponse.StatusCode.Should().Be(HttpStatusCode.OK, await inboxResponse.Content.ReadAsStringAsync());
        var inbox = await inboxResponse.Content.ReadFromJsonAsync<PageResponse<NotificationResponse>>();
        inbox!.Items.Should().Contain(item => item.Title == "Safety briefing" && item.ActionLink == $"/memos/{memo.Id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var stored = await db.Memos.AsNoTracking().SingleAsync(item => item.Id == memo.Id);
        stored.RowVersion.Should().NotBeNullOrEmpty();
        var outbox = await db.CommunicationOutboxMessages.AsNoTracking()
            .Where(message => message.Category == "memo")
            .ToListAsync();
        outbox.Should().Contain(message => message.Channel == "email" && message.Recipient == fixture.EmailA);
        outbox.Should().Contain(message => message.Channel == "sms" && message.Recipient == "0241110001");
        outbox.Should().Contain(message => message.Channel == "email" && message.Recipient == fixture.EmailB);
    }

    [Fact]
    public async Task Institute_hr_can_share_with_selected_people_only()
    {
        var fixture = await SeedAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, fixture.InstituteA));
        var request = new CreateMemoRequest(
            "Named briefing",
            "This note is only for the selected officer.",
            [new MemoAudienceInput("employee", fixture.InstituteA, null, null, fixture.EmployeeA, null)]);

        var preview = await hr.PostAsJsonAsync("/api/v2/memos/preview", request);
        var previewBody = await preview.Content.ReadFromJsonAsync<MemoPreviewResponse>();
        previewBody!.RecipientCount.Should().Be(1);
        previewBody.Recipients.Should().ContainSingle(item => item.EmployeeId == fixture.EmployeeA);

        var created = await hr.PostAsJsonAsync("/api/v2/memos", request);
        var memo = await created.Content.ReadFromJsonAsync<MemoResponse>();
        (await hr.PostAsync($"/api/v2/memos/{memo!.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var included = Client(CreateToken(SpmeRoles.Employee, fixture.InstituteA, fixture.UserA, fixture.EmployeeA));
        var excluded = Client(CreateToken(SpmeRoles.Employee, fixture.InstituteA, fixture.UserB, fixture.EmployeeB));
        (await included.GetAsync($"/api/v2/memos/{memo.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await excluded.GetAsync($"/api/v2/memos/{memo.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Institute_hr_cannot_address_another_institute()
    {
        var fixture = await SeedAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, fixture.InstituteA));
        var response = await hr.PostAsJsonAsync(
            "/api/v2/memos/preview",
            new CreateMemoRequest(
                "Cross institute",
                "This should be rejected.",
                [new MemoAudienceInput("institute", fixture.InstituteB, null, null, null, null)]));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Institute_admin_can_create_and_publish_an_internal_memo()
    {
        var fixture = await SeedAsync();
        var instituteAdmin = Client(CreateToken(SpmeRoles.InstituteAdmin, fixture.InstituteA));
        var request = new CreateMemoRequest(
            "Internal institute notice",
            "This notice is for all active staff in this institute.",
            null);

        var preview = await instituteAdmin.PostAsJsonAsync("/api/v2/memos/preview", request);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var previewBody = await preview.Content.ReadFromJsonAsync<MemoPreviewResponse>();
        previewBody!.RecipientCount.Should().Be(2);

        var created = await instituteAdmin.PostAsJsonAsync("/api/v2/memos", request);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var memo = await created.Content.ReadFromJsonAsync<MemoResponse>();
        memo!.Audiences.Should().ContainSingle(audience =>
            audience.AudienceType == "all-employees" &&
            audience.InstituteId == fixture.InstituteA);

        var publish = await instituteAdmin.PostAsync($"/api/v2/memos/{memo.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        var otherInstituteEmployee = Client(CreateToken(
            SpmeRoles.Employee, fixture.InstituteB, fixture.UserC, fixture.EmployeeC));
        (await otherInstituteEmployee.GetAsync($"/api/v2/memos/{memo.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Platform_admin_can_share_by_institutes_and_named_people()
    {
        var fixture = await SeedAsync();
        var platform = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var institutePreview = await platform.PostAsJsonAsync(
            "/api/v2/memos/preview",
            new CreateMemoRequest(
                "Council notice",
                "This circular applies to the selected institutes.",
                [
                    new MemoAudienceInput("institute", fixture.InstituteA, null, null, null, null),
                    new MemoAudienceInput("institute", fixture.InstituteB, null, null, null, null)
                ]));
        institutePreview.StatusCode.Should().Be(HttpStatusCode.OK);
        var institutePreviewBody = await institutePreview.Content.ReadFromJsonAsync<MemoPreviewResponse>();
        institutePreviewBody!.RecipientCount.Should().Be(3);

        var created = await platform.PostAsJsonAsync(
            "/api/v2/memos",
            new CreateMemoRequest(
                "Named officers",
                "Please acknowledge this confidential note.",
                [
                    new MemoAudienceInput("institute", fixture.InstituteA, null, null, null, null),
                    new MemoAudienceInput("institute", fixture.InstituteB, null, null, null, null),
                    new MemoAudienceInput("employee", null, null, null, fixture.EmployeeA, null),
                    new MemoAudienceInput("employee", null, null, null, fixture.EmployeeC, null)
                ]));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var memo = await created.Content.ReadFromJsonAsync<MemoResponse>();
        memo!.Audiences.Should().HaveCount(2);
        memo.Audiences.Should().OnlyContain(audience => audience.AudienceType == "employee");
        (await platform.PostAsync($"/api/v2/memos/{memo.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await platform.GetAsync("/api/v2/memos");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, await listResponse.Content.ReadAsStringAsync());
        var list = await listResponse.Content.ReadFromJsonAsync<CollectionResponse<MemoResponse>>();
        list!.Items.Should().Contain(item => item.Id == memo.Id);

        var included = Client(CreateToken(SpmeRoles.Employee, fixture.InstituteB, fixture.UserC, fixture.EmployeeC));
        (await included.GetAsync($"/api/v2/memos/{memo.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var skipped = Client(CreateToken(SpmeRoles.Employee, fixture.InstituteA, fixture.UserB, fixture.EmployeeB));
        (await skipped.GetAsync($"/api/v2/memos/{memo.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<MemoFixture> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var instituteA = new Institute($"MA-{suffix}", $"Memo Institute A {suffix}", "Institute");
        var instituteB = new Institute($"MB-{suffix}", $"Memo Institute B {suffix}", "Institute");
        var emailA = $"staff.a.{suffix}@example.test";
        var emailB = $"staff.b.{suffix}@example.test";
        var emailC = $"staff.c.{suffix}@example.test";
        var employeeA = CreateEmployee(instituteA.Id, $"STA-{suffix}", "Addo", emailA, "0241110001");
        var employeeB = CreateEmployee(instituteA.Id, $"STB-{suffix}", "Boateng", emailB, "0241110002");
        var employeeC = CreateEmployee(instituteB.Id, $"STC-{suffix}", "Mensah", emailC, "0241110003");
        var userA = CreateLinkedUser("staff.a", employeeA, emailA, "0241110001");
        var userB = CreateLinkedUser("staff.b", employeeB, emailB, "0241110002");
        var userC = CreateLinkedUser("staff.c", employeeC, emailC, "0241110003");
        db.Institutes.AddRange(instituteA, instituteB);
        db.Employees.AddRange(employeeA, employeeB, employeeC);
        db.Users.AddRange(userA, userB, userC);
        await db.SaveChangesAsync();
        return new MemoFixture(
            instituteA.Id, instituteB.Id,
            employeeA.Id, employeeB.Id, employeeC.Id,
            userA.Id, userB.Id, userC.Id,
            emailA, emailB);
    }

    private static Employee CreateEmployee(Guid instituteId, string staffId, string surname, string email, string phone)
    {
        var employee = new Employee(instituteId, staffId, surname, "female");
        employee.UpdateProfile(
            staffId, null, surname, "Ama", "female", new DateTime(1990, 1, 1), "Ghanaian",
            null, "single", email, phone, "active", true);
        return employee;
    }

    private static User CreateLinkedUser(string label, Employee employee, string email, string phone)
    {
        var user = new User($"{label}.{Guid.NewGuid():N}@example.test", "Employee");
        user.Email = email;
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PhoneNumber = phone;
        user.LinkEmployee(employee.Id, employee.InstituteId);
        return user;
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(string role, Guid? instituteId, Guid? userId = null, Guid? employeeId = null)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var section = configuration.GetSection("Jwt");
        var id = userId ?? Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id.ToString()),
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, $"memo.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (instituteId.HasValue)
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        if (employeeId.HasValue)
            claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section.GetValue<string>("Key")!)),
            SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            section.GetValue<string>("Issuer") ?? "csir-spme-api",
            section.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials));
    }

    private sealed record MemoFixture(
        Guid InstituteA,
        Guid InstituteB,
        Guid EmployeeA,
        Guid EmployeeB,
        Guid EmployeeC,
        Guid UserA,
        Guid UserB,
        Guid UserC,
        string EmailA,
        string EmailB);
}
