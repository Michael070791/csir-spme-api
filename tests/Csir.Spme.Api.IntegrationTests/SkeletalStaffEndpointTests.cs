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
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class SkeletalStaffEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public SkeletalStaffEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Skeletal_staff_request_moves_from_draft_to_credited_with_an_allowance_report()
    {
        var seed = await SeedAsync();
        var employee = Client(CreateToken(SpmeRoles.Employee, seed.InstituteA, seed.EmployeeId));
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, seed.InstituteA, null, seed.HrUserId));
        var sectionHead = Client(CreateToken(SpmeRoles.HeadOfSection, seed.InstituteA, seed.SectionHeadEmployeeId));
        var divisionHead = Client(CreateToken(SpmeRoles.HeadOfDivision, seed.InstituteA, seed.DivisionHeadEmployeeId));
        var instituteDirector = Client(CreateToken(SpmeRoles.InstituteDirector, seed.InstituteA));
        var otherHr = Client(CreateToken(SpmeRoles.HrAdmin, seed.InstituteB));
        var today = DateTime.UtcNow.Date;

        (await _factory.CreateClient().GetAsync("/api/v2/skeletal-staff-requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var activePeriod = await employee.GetFromJsonAsync<HolidayPeriodResponse>("/api/v2/skeletal-staff-requests/active-holiday-period");
        activePeriod!.Id.Should().Be(seed.HolidayPeriodId);

        var create = await employee.PostAsJsonAsync("/api/v2/skeletal-staff-requests", new CreateSkeletalStaffRequest(
            seed.HolidayPeriodId, [today, today.AddDays(1)], "Ama Mensah", "Available for duty.", true));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var draft = await create.Content.ReadFromJsonAsync<SkeletalStaffRequestResponse>();
        draft!.Status.Should().Be(SkeletalStaffRequestStatuses.Draft);

        using var invalidUpdate = Request(HttpMethod.Patch, $"/api/v2/skeletal-staff-requests/{draft.Id}", create.Headers.ETag!.Tag!,
            new UpdateSkeletalStaffRequest([today.AddDays(30)], "Ama Mensah", null, true));
        (await employee.SendAsync(invalidUpdate)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var update = Request(HttpMethod.Patch, $"/api/v2/skeletal-staff-requests/{draft.Id}", create.Headers.ETag!.Tag!,
            new UpdateSkeletalStaffRequest([today, today.AddDays(2)], "Ama Mensah", "Updated availability.", true));
        var updatedResponse = await employee.SendAsync(update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draftEtag = updatedResponse.Headers.ETag!.Tag!;

        using var submit = Request(HttpMethod.Post, $"/api/v2/skeletal-staff-requests/{draft.Id}/submit", draftEtag, null);
        var submittedResponse = await employee.SendAsync(submit);
        submittedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submittedResponse.Content.ReadFromJsonAsync<SkeletalStaffRequestResponse>();
        submitted!.Status.Should().Be(SkeletalStaffRequestStatuses.Submitted);
        var etag = submittedResponse.Headers.ETag!.Tag!;

        var stageApprovers = new[] { sectionHead, divisionHead, instituteDirector };
        using (var hrCannotDecide = Request(HttpMethod.Post, $"/api/v2/skeletal-staff-requests/{draft.Id}/approve", etag,
            new SkeletalStaffDecisionRequest("HR cannot replace the assigned stage approver.")))
        {
            (await hr.SendAsync(hrCannotDecide)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        for (var stage = 0; stage < LeaveApprovalStages.DefaultChain.Length; stage++)
        {
            using var approve = Request(HttpMethod.Post, $"/api/v2/skeletal-staff-requests/{draft.Id}/approve", etag,
                new SkeletalStaffDecisionRequest($"Approved stage {stage + 1}."));
            var approvalResponse = await stageApprovers[stage].SendAsync(approve);
            approvalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var approved = await approvalResponse.Content.ReadFromJsonAsync<SkeletalStaffRequestResponse>();
            approved!.Approvals.Should().HaveCount(stage + 1);
            etag = approvalResponse.Headers.ETag!.Tag!;
        }

        (await otherHr.GetAsync($"/api/v2/skeletal-staff-requests/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var complete = Request(HttpMethod.Post, $"/api/v2/skeletal-staff-requests/{draft.Id}/complete", etag, null);
        var completedResponse = await hr.SendAsync(complete);
        completedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await completedResponse.Content.ReadFromJsonAsync<SkeletalStaffRequestResponse>();
        completed!.Status.Should().Be(SkeletalStaffRequestStatuses.Completed);
        etag = completedResponse.Headers.ETag!.Tag!;

        var pendingReport = await employee.GetFromJsonAsync<SkeletalStaffAllowanceReportResponse>($"/api/v2/skeletal-staff-requests/{draft.Id}/allowance-report");
        pendingReport!.AllowanceEligibility.Status.Should().Be("pending-credit");
        pendingReport.AllowanceEligibility.MonetaryAmount.Should().BeNull();
        pendingReport.MonetaryAllowanceStatus.Should().Be("not-configured");
        pendingReport.Employee.Id.Should().Be(seed.EmployeeId);
        pendingReport.Institute.Id.Should().Be(seed.InstituteA);

        using var credit = Request(HttpMethod.Post, $"/api/v2/skeletal-staff-requests/{draft.Id}/credit-leave", etag,
            new CreditSkeletalStaffLeaveRequest((short)today.Year));
        var creditResponse = await hr.SendAsync(credit);
        creditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var creditedReport = await employee.GetFromJsonAsync<SkeletalStaffAllowanceReportResponse>($"/api/v2/skeletal-staff-requests/{draft.Id}/allowance-report");
        creditedReport!.AllowanceEligibility.Status.Should().Be("credited");
        creditedReport.AllowanceEligibility.LeaveCreditDays.Should().Be(4m);
        creditedReport.AllowanceEligibility.LeaveCreditYear.Should().Be((short)today.Year);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var balance = await db.LeaveBalances.SingleAsync(x => x.EmployeeId == seed.EmployeeId && x.LeaveType == LeaveTypes.Annual && x.LeaveYear == today.Year);
        balance.AdjustedDays.Should().Be(4m);
        var approvals = await db.SkeletalStaffApprovals.Where(x => x.SkeletalStaffRequestId == draft.Id).ToListAsync();
        approvals.Should().HaveCount(LeaveApprovalStages.DefaultChain.Length);
        approvals.Should().OnlyContain(approval => approval.ApproverUserId == null);
    }

    [Fact]
    public async Task Skeletal_staff_draft_can_be_deleted_with_the_current_etag()
    {
        var seed = await SeedAsync();
        var employee = Client(CreateToken(SpmeRoles.Employee, seed.InstituteA, seed.EmployeeId));
        var create = await employee.PostAsJsonAsync("/api/v2/skeletal-staff-requests", new CreateSkeletalStaffRequest(
            seed.HolidayPeriodId, [DateTime.UtcNow.Date], "Ama Mensah", null, true));
        var draft = await create.Content.ReadFromJsonAsync<SkeletalStaffRequestResponse>();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/skeletal-staff-requests/{draft!.Id}");
        delete.Headers.IfMatch.ParseAdd(create.Headers.ETag!.Tag!);
        (await employee.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await employee.GetAsync($"/api/v2/skeletal-staff-requests/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holiday_periods_support_scoped_crud_with_etags()
    {
        var seed = await SeedAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, seed.InstituteA));
        var start = DateTime.UtcNow.Date.AddDays(30);
        var createRequest = new CreateHolidayPeriodRequest(
            ScopeTypes.Institute, null, (short)(DateTime.UtcNow.Year + 1),
            start, start.AddDays(2), start.AddDays(3), start.AddDays(5),
            start, start.AddDays(7), 3, HolidayPeriodStatuses.Draft, "Draft period.");

        var create = await hr.PostAsJsonAsync("/api/v2/holiday-periods", createRequest);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<HolidayPeriodResponse>();
        created!.InstituteId.Should().Be(seed.InstituteA);

        using var update = Request(HttpMethod.Patch, $"/api/v2/holiday-periods/{created.Id}", create.Headers.ETag!.Tag!,
            new UpdateHolidayPeriodRequest(
                start, start.AddDays(2), start.AddDays(3), start.AddDays(5),
                start, start.AddDays(8), 4, HolidayPeriodStatuses.Draft, "Updated draft period."));
        var updateResponse = await hr.SendAsync(update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/holiday-periods/{created.Id}");
        delete.Headers.IfMatch.ParseAdd(updateResponse.Headers.ETag!.Tag!);
        (await hr.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await hr.GetAsync($"/api/v2/holiday-periods/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var instituteA = new Institute($"SKA-{suffix}", $"Skeletal Institute A {suffix}", "Institute");
        var instituteB = new Institute($"SKB-{suffix}", $"Skeletal Institute B {suffix}", "Institute");
        var employee = new Employee(instituteA.Id, $"SK-{suffix}", "Mensah", "female");
        var division = new Division(instituteA.Id, $"Skeletal Division {suffix}");
        var section = new Section(division.Id, $"Skeletal Section {suffix}");
        var sectionHeadEmployee = new Employee(instituteA.Id, $"SK-SH-{suffix}", "Section Head", "unknown");
        var divisionHeadEmployee = new Employee(instituteA.Id, $"SK-DH-{suffix}", "Division Head", "unknown");
        var hrUser = new User($"skeletal.hr.{suffix}", "HrAdmin");
        hrUser.AssignInstitute(instituteA.Id);
        var today = DateTime.UtcNow.Date;
        var period = HolidayPeriod.Create(
            ScopeTypes.Institute, instituteA.Id, (short)today.Year,
            today, today.AddDays(2), today.AddDays(3), today.AddDays(5),
            today.AddDays(-1), today.AddDays(7), 4, HolidayPeriodStatuses.Open, null).Value!;

        db.Institutes.AddRange(instituteA, instituteB);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        db.Employees.AddRange(employee, sectionHeadEmployee, divisionHeadEmployee);
        db.EmploymentRecords.AddRange(
            new EmploymentRecord(employee.Id, instituteA.Id, division.Id, section.Id, null, null, null,
                "senior-staff", "active", null, null, null, null, null, today, true),
            new EmploymentRecord(sectionHeadEmployee.Id, instituteA.Id, division.Id, section.Id, null, null,
                "head-of-section", "senior-staff", "active", null, null, null, null, null, today, true),
            new EmploymentRecord(divisionHeadEmployee.Id, instituteA.Id, division.Id, null, null, null,
                "head-of-division", "senior-staff", "active", null, null, null, null, null, today, true));
        db.Users.Add(hrUser);
        db.HolidayPeriods.Add(period);
        await db.SaveChangesAsync();
        return new Seed(instituteA.Id, instituteB.Id, employee.Id, sectionHeadEmployee.Id,
            divisionHeadEmployee.Id, hrUser.Id, period.Id);
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpRequestMessage Request(HttpMethod method, string uri, string etag, object? payload)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.IfMatch.ParseAdd(etag);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return request;
    }

    private string CreateToken(string role, Guid? instituteId, Guid? employeeId = null, Guid? userId = null)
    {
        var section = _factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var user = userId ?? Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.ToString()),
            new(ClaimTypes.NameIdentifier, user.ToString()),
            new(ClaimTypes.Name, $"skeletal.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (instituteId.HasValue) claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        if (employeeId.HasValue) claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section.GetValue<string>("Key")!)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            section.GetValue<string>("Issuer") ?? "csir-spme-api",
            section.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials));
    }

    private sealed record Seed(
        Guid InstituteA,
        Guid InstituteB,
        Guid EmployeeId,
        Guid SectionHeadEmployeeId,
        Guid DivisionHeadEmployeeId,
        Guid HrUserId,
        Guid HolidayPeriodId);
}
