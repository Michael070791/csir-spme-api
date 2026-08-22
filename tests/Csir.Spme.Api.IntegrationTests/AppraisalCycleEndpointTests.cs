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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AppraisalCycleEndpointTests(SpmeApiFactory factory) : IClassFixture<SpmeApiFactory>
{
    [Fact]
    public async Task Activation_Creates_Active_Roster_With_Verified_Snapshots_And_Exact_Routing()
    {
        var seed = await SeedAsync();
        using var hr = Client(seed.HrUserId, SpmeRoles.HrAdmin, seed.InstituteId, null,
            SpmePermissions.AppraisalsAdmin);
        var getCycle = await hr.GetAsync($"/api/v2/appraisal-cycles/{seed.CycleId}");
        getCycle.StatusCode.Should().Be(HttpStatusCode.OK);

        var activated = await Post(hr, $"/api/v2/appraisal-cycles/{seed.CycleId}/activate",
            getCycle.Headers.ETag!.Tag);
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        var activationEtag = activated.Headers.ETag!.Tag;

        var roster = await hr.GetFromJsonAsync<CollectionResponse<AppraisalSummaryResponse>>(
            $"/api/v2/appraisal-cycles/{seed.CycleId}/roster");
        roster.Should().NotBeNull();
        roster!.Total.Should().Be(7);
        roster.Items.Should().NotContain(item => item.EmployeeId == seed.InactiveEmployeeId);

        var sectionStaff = roster.Items.Single(item => item.EmployeeId == seed.SectionStaffEmployeeId);
        sectionStaff.HodUserId.Should().Be(seed.SectionHeadUserId);
        sectionStaff.DirectorUserId.Should().Be(seed.DirectorUserId);
        sectionStaff.IsRoutingException.Should().BeFalse();

        var noSectionStaff = roster.Items.Single(item => item.EmployeeId == seed.NoSectionStaffEmployeeId);
        noSectionStaff.HodUserId.Should().Be(seed.DivisionHeadUserId);
        noSectionStaff.DirectorUserId.Should().Be(seed.DirectorUserId);
        noSectionStaff.IsRoutingException.Should().BeFalse();

        var sectionHead = roster.Items.Single(item => item.EmployeeId == seed.SectionHeadEmployeeId);
        sectionHead.HodUserId.Should().Be(seed.DivisionHeadUserId);
        sectionHead.DirectorUserId.Should().Be(seed.DirectorUserId);
        sectionHead.IsRoutingException.Should().BeFalse();

        var divisionHead = roster.Items.Single(item => item.EmployeeId == seed.DivisionHeadEmployeeId);
        divisionHead.HodUserId.Should().Be(seed.DirectorUserId);
        divisionHead.DirectorUserId.Should().Be(seed.DeputyDirectorGeneralUserId);
        divisionHead.IsRoutingException.Should().BeFalse();

        var director = roster.Items.Single(item => item.EmployeeId == seed.DirectorEmployeeId);
        director.HodUserId.Should().Be(seed.DeputyDirectorGeneralUserId);
        director.DirectorUserId.Should().Be(seed.DirectorGeneralUserId);
        director.IsRoutingException.Should().BeFalse();

        var candidates = await hr.GetFromJsonAsync<CollectionResponse<AppraisalAssignmentCandidateResponse>>(
            $"/api/v2/appraisal-cycles/{seed.CycleId}/assignment-candidates");
        candidates!.Items.Single(item => item.UserId == seed.SectionHeadUserId).EligibleRoles
            .Should().ContainSingle(SpmeRoles.HeadOfSection, "because acting leadership from current employment is a verified route");
        candidates.Items.Single(item => item.UserId == seed.DirectorUserId).EligibleRoles
            .Should().Contain(SpmeRoles.InstituteDirector);
        candidates.Items.Single(item => item.UserId == seed.DeputyDirectorGeneralUserId).EligibleRoles
            .Should().Contain(SpmeRoles.DeputyDirectorGeneral);

        using var sectionStaffClient = Client(seed.SectionStaffUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.SectionStaffEmployeeId, SpmePermissions.AppraisalsSelf);
        var detail = await sectionStaffClient.GetFromJsonAsync<PerformanceAppraisalResponse>(
            $"/api/v2/performance-appraisals/{sectionStaff.Id}");
        detail.Should().NotBeNull();
        detail!.Employee.PresentGrade.Should().Be("Research Scientist");
        detail.Employee.SalaryGradeStep.Should().Be("SG 12/2");
        detail.Employee.DivisionUnit.Should().Be("Water Research Section");
        detail.Appraiser.Surname.Should().Be("Section Head");
        detail.Appraiser.PositionOfAppraiser.Should().Be("Acting Head of Section");
        detail.Approver.Surname.Should().Be("Director");

        var firstReminders = await Post(hr, $"/api/v2/appraisal-cycles/{seed.CycleId}/reminders", activationEtag);
        firstReminders.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstReminderRun = await firstReminders.Content.ReadFromJsonAsync<AppraisalReminderRunResponse>();
        firstReminderRun!.Processed.Should().Be(7);
        firstReminderRun.Staged.Should().Be(6, "because one active employee has no linked portal user");

        var repeatedReminders = await Post(hr, $"/api/v2/appraisal-cycles/{seed.CycleId}/reminders", activationEtag);
        repeatedReminders.StatusCode.Should().Be(HttpStatusCode.OK);
        (await repeatedReminders.Content.ReadFromJsonAsync<AppraisalReminderRunResponse>())!.Staged.Should().Be(0);

        await using (var reminderScope = factory.Services.CreateAsyncScope())
        {
            var reminderDb = reminderScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var records = await reminderDb.AppraisalReminderRecords.AsNoTracking()
                .Where(item => roster.Items.Select(appraisal => appraisal.Id).Contains(item.PerformanceAppraisalId))
                .ToListAsync();
            records.Should().HaveCount(6);
            records.Should().OnlyContain(item => item.OffsetCode.StartsWith("overdue-", StringComparison.Ordinal));
            var messages = await reminderDb.CommunicationOutboxMessages.AsNoTracking()
                .Where(item => item.Category == "appraisal-deadline-reminder" &&
                    (item.Channel == "event" || item.Channel == "sms"))
                .ToListAsync();
            messages.Should().NotBeEmpty();
            messages.Select(item => item.Body).Should().OnlyContain(body =>
                !body.Contains("score", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("reason", StringComparison.OrdinalIgnoreCase) &&
                !body.Contains("recommendation", StringComparison.OrdinalIgnoreCase));
        }

        var closed = await Post(hr, $"/api/v2/appraisal-cycles/{seed.CycleId}/close", activationEtag);
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        var reminders = await Post(hr, $"/api/v2/appraisal-cycles/{seed.CycleId}/reminders",
            closed.Headers.ETag!.Tag);
        reminders.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var institute = new Institute($"AR-{suffix}", $"Appraisal routing institute {suffix}", "Institute");
        var division = new Division(institute.Id, "Research Division");
        var section = new Section(division.Id, "Water Research Section");
        var grade = Grade.Create($"RS-{suffix}", "Research Scientist", "senior-staff", "research", 1, 1);

        var sectionStaff = Employee(institute.Id, $"SS-{suffix}", "Section Staff", "Ama");
        var noSectionStaff = Employee(institute.Id, $"NS-{suffix}", "Division Staff", "Yaw");
        var sectionHead = Employee(institute.Id, $"SH-{suffix}", "Section Head", "Akosua");
        var divisionHead = Employee(institute.Id, $"DH-{suffix}", "Division Head", "Kojo");
        var director = Employee(institute.Id, $"ID-{suffix}", "Director", "Esi");
        var deputyDirectorGeneral = Employee(institute.Id, $"DD-{suffix}", "Deputy Director General", "Kwame");
        var directorGeneral = Employee(institute.Id, $"DG-{suffix}", "Director General", "Afia");
        var inactive = Employee(institute.Id, $"IN-{suffix}", "Inactive", "Nana");
        inactive.UpdateProfile(inactive.StaffId, inactive.Prefix, inactive.Surname, inactive.OtherNames,
            inactive.Gender, null, null, null, null, inactive.PrimaryEmail, inactive.Phone, "inactive", true);

        var sectionStaffUser = User(sectionStaff, "section-staff");
        var sectionHeadUser = User(sectionHead, "section-head");
        var divisionHeadUser = User(divisionHead, "division-head");
        var directorUser = User(director, "director");
        var deputyDirectorGeneralUser = User(deputyDirectorGeneral, "deputy-director-general");
        var directorGeneralUser = User(directorGeneral, "director-general");
        var hrUser = new User($"routing-hr.{suffix}@example.test", "HrAdmin") { Email = $"routing-hr.{suffix}@example.test" };
        hrUser.AssignInstitute(institute.Id, "HrAdmin");

        var divisionHeadRole = await Role(db, SpmeRoles.HeadOfDivision, "head-of-division");
        var directorRole = await Role(db, SpmeRoles.InstituteDirector, "institute-director");
        var deputyDirectorGeneralRole = await Role(db, SpmeRoles.DeputyDirectorGeneral, "deputy-director-general");
        var directorGeneralRole = await Role(db, SpmeRoles.DirectorGeneral, "director-general");

        db.Institutes.Add(institute);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        db.Grades.Add(grade);
        db.Employees.AddRange(sectionStaff, noSectionStaff, sectionHead, divisionHead, director,
            deputyDirectorGeneral, directorGeneral, inactive);
        db.Users.AddRange(sectionStaffUser, sectionHeadUser, divisionHeadUser, directorUser,
            deputyDirectorGeneralUser, directorGeneralUser, hrUser);
        db.UserRoles.AddRange(
            new IdentityUserRole<Guid> { UserId = divisionHeadUser.Id, RoleId = divisionHeadRole.Id },
            new IdentityUserRole<Guid> { UserId = directorUser.Id, RoleId = directorRole.Id },
            new IdentityUserRole<Guid> { UserId = deputyDirectorGeneralUser.Id, RoleId = deputyDirectorGeneralRole.Id },
            new IdentityUserRole<Guid> { UserId = directorGeneralUser.Id, RoleId = directorGeneralRole.Id });
        db.EmploymentRecords.AddRange(
            Employment(sectionStaff, institute.Id, division.Id, section.Id, grade.Id, "Research Scientist", null),
            Employment(noSectionStaff, institute.Id, division.Id, null, grade.Id, "Research Scientist", null),
            Employment(sectionHead, institute.Id, division.Id, section.Id, grade.Id, "Acting Head of Section", "Acting Head of Section"),
            Employment(divisionHead, institute.Id, division.Id, null, grade.Id, "Head of Division", "head-of-division"),
            Employment(director, institute.Id, division.Id, null, grade.Id, "Institute Director", "institute-director"),
            Employment(deputyDirectorGeneral, institute.Id, division.Id, null, grade.Id, "Deputy Director General", "deputy-director-general"),
            Employment(directorGeneral, institute.Id, division.Id, null, grade.Id, "Director General", "director-general"),
            Employment(inactive, institute.Id, division.Id, section.Id, grade.Id, "Research Scientist", null));
        var cycle = AppraisalCycle.Create(
            institute.Id,
            "2026 routing cycle",
            2026,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            new DateTime(2026, 4, 1),
            new DateTime(2026, 8, 31),
            new DateTime(2026, 9, 1),
            new DateTime(2026, 12, 31)).Value!;
        db.AppraisalCycles.Add(cycle);
        await db.SaveChangesAsync();
        return new Seed(institute.Id, cycle.Id, sectionStaff.Id, sectionStaffUser.Id, noSectionStaff.Id,
            sectionHead.Id, sectionHeadUser.Id, divisionHead.Id, divisionHeadUser.Id, director.Id,
            directorUser.Id, deputyDirectorGeneralUser.Id, directorGeneralUser.Id, inactive.Id, hrUser.Id);
    }

    private static Employee Employee(Guid instituteId, string staffId, string surname, string firstName)
    {
        var employee = new Employee(instituteId, staffId, surname, "unspecified");
        employee.UpdateImportedProfile("Dr", firstName, null, "Ghanaian", null, null,
            $"{staffId.ToLowerInvariant()}@example.test", "+233245678901", true);
        return employee;
    }

    private static User User(Employee employee, string label)
    {
        var user = new User($"{label}.{Guid.NewGuid():N}@example.test", "Employee")
        {
            Email = employee.PrimaryEmail,
            PhoneNumber = employee.Phone,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };
        user.LinkEmployee(employee.Id, employee.InstituteId);
        user.UpdateDisplayName($"{employee.OtherNames} {employee.Surname}");
        return user;
    }

    private static EmploymentRecord Employment(
        Employee employee, Guid instituteId, Guid divisionId, Guid? sectionId, Guid gradeId,
        string jobTitle, string? leadershipRole) => new(
        employee.Id,
        instituteId,
        divisionId,
        sectionId,
        null,
        gradeId,
        jobTitle,
        leadershipRole,
        "senior-staff",
        "SG 12/2",
        null,
        "active",
        null,
        null,
        null,
        null,
        new DateTime(2020, 1, 1),
        new DateTime(2024, 1, 1),
        null,
        null,
        new DateTime(2024, 1, 1),
        true);

    private static async Task<Role> Role(SpmeDbContext db, string name, string code)
    {
        var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == name);
        if (role is not null) return role;
        role = new Role(code, name, $"{name} appraisal routing role.", true);
        db.Roles.Add(role);
        return role;
    }

    private HttpClient Client(Guid userId, string role, Guid instituteId, Guid? employeeId, params string[] permissions)
    {
        var jwt = factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("identity_type", role),
            new("institute_id", instituteId.ToString())
        };
        if (employeeId.HasValue) claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.IfMatch.ParseAdd(etag);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private sealed record Seed(
        Guid InstituteId,
        Guid CycleId,
        Guid SectionStaffEmployeeId,
        Guid SectionStaffUserId,
        Guid NoSectionStaffEmployeeId,
        Guid SectionHeadEmployeeId,
        Guid SectionHeadUserId,
        Guid DivisionHeadEmployeeId,
        Guid DivisionHeadUserId,
        Guid DirectorEmployeeId,
        Guid DirectorUserId,
        Guid DeputyDirectorGeneralUserId,
        Guid DirectorGeneralUserId,
        Guid InactiveEmployeeId,
        Guid HrUserId);
}
