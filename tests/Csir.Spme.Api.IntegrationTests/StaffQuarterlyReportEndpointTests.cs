using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Common;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Projects;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class StaffQuarterlyReportEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    public StaffQuarterlyReportEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Employee_Creates_Submits_And_Assigned_Hod_Reviews_Quarterly_Report()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var options = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyReportOptions>>(
            "/api/v2/staff-quarterly-reports/options");
        options!.Data.ReportingPeriods.Should().ContainSingle(item => item.Id == seed.PeriodId);
        options.Data.Reviewers.Should().ContainSingle(item => item.UserId == seed.ReviewerUserId);
        options.Data.Reviewers[0].Email.Should().Be(seed.ReviewerEmail);
        options.Data.Reviewers[0].Phone.Should().Be("+233200000002");
        options.Data.Projects.Should().ContainSingle(item => item.Id == seed.ProjectId && item.HasInception);

        var request = ReportRequest(seed.PeriodId, seed.ReviewerUserId,
            new string('R', 512), "Research abstract <script>alert(1)</script>", "Completed field sampling.",
            "Collected validated samples.", "Analyse laboratory results.", [seed.ProjectId], []);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports", request);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        created.Headers.ETag.Should().NotBeNull();
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;
        report.Title.Should().HaveLength(512);
        report.AvailableActions.Should().BeEquivalentTo("edit", "submit");
        report.Projects.Should().ContainSingle(item => item.Id == seed.ProjectId);

        using var submit = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        submit.Headers.IfMatch.Add(created.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var submitted = await employee.SendAsync(submit);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());

        using var reviewer = Client(seed.ReviewerUserId, SpmeRoles.HeadOfSection, seed.InstituteId,
            seed.ReviewerEmployeeId, SpmePermissions.ReportsReview);
        var queueResponse = await reviewer.GetAsync("/api/v2/staff-quarterly-reports/review-queue");
        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK, await queueResponse.Content.ReadAsStringAsync());
        var queue = await queueResponse.Content.ReadFromJsonAsync<ListResponse<StaffQuarterlyReportResponse>>();
        queue!.Data.Should().ContainSingle(item => item.Id == report.Id);
        queue.Data[0].AvailableActions.Should().BeEquivalentTo("approve", "return");
        (await reviewer.GetAsync($"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var persisted = await db.Reports.AsNoTracking().SingleAsync(item => item.Id == report.Id);
        persisted.ReportScope.Should().Be(ReportScopes.EmployeeQuarterly);
        persisted.OwnerEmployeeId.Should().Be(seed.EmployeeId);
        (await db.ReportProjects.AsNoTracking().SingleAsync(item => item.ReportId == report.Id))
            .ProjectNameSnapshot.Should().Be("Water quality project");
        (await db.Notifications.AsNoTracking().SingleAsync(item => item.RecipientUserId == seed.ReviewerUserId))
            .ActionLink.Should().Be($"/reports/{report.Id:D}");
        var messages = await db.CommunicationOutboxMessages.AsNoTracking()
            .Where(item => item.Category == "staff-quarterly-report-submitted").ToListAsync();
        messages.Should().Contain(item => item.Channel == "email" &&
            item.Body.Contains("Research abstract &lt;script&gt;alert(1)&lt;/script&gt;") &&
            !item.Body.Contains("<script>alert(1)</script>") &&
            item.AttachmentsJson != null &&
            item.AttachmentsJson.Contains("application/pdf") &&
            item.AttachmentsJson.Contains("JVBERi"));
        messages.Should().Contain(item => item.Channel == "sms" &&
            item.Body.Contains($"/reports/{report.Id:D}") &&
            item.Body.Contains("staff portal"));

        await db.Projects.Where(item => item.Id == seed.ProjectId).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Name, "Changed catalog title")
            .SetProperty(item => item.Method, "Changed catalog method"));
        var snapshotResponse = await reviewer.GetFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>(
            $"/api/v2/staff-quarterly-reports/{report.Id}");
        snapshotResponse!.Data.ProjectProgress.Should().ContainSingle(item =>
            item.Name == "Water quality project" &&
            item.Inception != null &&
            item.Inception.Name == "Water quality project" &&
            item.Inception.Method == "Laboratory analysis");
    }

    [Fact]
    public async Task Project_Draft_Captures_Amount_Currency_Lead_And_Method()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);

        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports/project-drafts",
            new CreateStaffQuarterlyProjectDraftRequest(FormOneRequest(
                "Coastal sediment survey", seed.ReviewerEmployeeId, complete: true)));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var option = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyCatalogOption>>())!.Data;
        option.AlreadyExisted.Should().BeFalse();
        option.HasInception.Should().BeTrue();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var project = await db.Projects.AsNoTracking().SingleAsync(item => item.Id == option.Id);
        project.BudgetAmount.Should().Be(250000.50m);
        project.Currency.Should().Be("USD");
        project.LeadEmployeeId.Should().Be(seed.ReviewerEmployeeId);
        project.Method.Should().Be("Remote sensing with ground-truth samples.");
        project.Justification.Should().Be("Background and justification for coastal sediment mapping.");
    }

    [Fact]
    public async Task Quarterly_Report_Is_Self_And_Assigned_Reviewer_Scoped()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Scoped report", null,
                "Scoped summary", null, null, [seed.ProjectId], []));
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;
        using var other = Client(Guid.NewGuid(), SpmeRoles.Employee, seed.InstituteId,
            Guid.NewGuid(), SpmePermissions.ReportsSelf);
        (await other.GetAsync($"/api/v2/staff-quarterly-reports/{report.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var otherInstitute = Client(Guid.NewGuid(), SpmeRoles.Employee, Guid.NewGuid(),
            Guid.NewGuid(), SpmePermissions.ReportsSelf);
        (await otherInstitute.GetAsync($"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.GetAsync("/api/v2/reports?limit=10"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Quarterly_Report_Patch_Approve_Return_And_Duplicate_Catalog_Are_Enforced()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Draft report",
                "Abstract", "Work summary", "Key results", "Next steps", [seed.ProjectId], []));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;

        using var stale = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/staff-quarterly-reports/{report.Id}")
        {
            Content = JsonContent.Create(ReportRequest(seed.PeriodId, seed.ReviewerUserId,
                "Updated title", "Abstract", "Updated summary", "Key results", "Next steps", [seed.ProjectId], []))
        };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
        (await employee.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        using var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/staff-quarterly-reports/{report.Id}")
        {
            Content = JsonContent.Create(ReportRequest(seed.PeriodId, seed.ReviewerUserId,
                "Updated title", "Abstract", "Updated summary", "Key results", "Next steps", [seed.ProjectId], []))
        };
        patch.Headers.IfMatch.Add(created.Headers.ETag!);
        var patched = await employee.SendAsync(patch);
        patched.StatusCode.Should().Be(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        var patchedReport = (await patched.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;
        patchedReport.Title.Should().Be("Updated title");

        using var duplicatePeriod = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Second report",
                null, "Another summary", null, null, [seed.ProjectId], []));
        duplicatePeriod.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var existingProject = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports/project-drafts",
            new CreateStaffQuarterlyProjectDraftRequest(FormOneRequest(
                "Water quality project", seed.EmployeeId, complete: true)));
        existingProject.StatusCode.Should().Be(HttpStatusCode.OK);
        var existing = (await existingProject.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyCatalogOption>>())!.Data;
        existing.AlreadyExisted.Should().BeTrue();
        existing.Id.Should().Be(seed.ProjectId);
        await using (var duplicateScope = _factory.Services.CreateAsyncScope())
        {
            var duplicateDb = duplicateScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            (await duplicateDb.Projects.AsNoTracking().SingleAsync(item => item.Id == seed.ProjectId))
                .Method.Should().Be("Laboratory analysis");
        }

        using var submit = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        submit.Headers.IfMatch.Add(patched.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var submitted = await employee.SendAsync(submit);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());

        using var otherHod = Client(Guid.NewGuid(), SpmeRoles.HeadOfSection, seed.InstituteId,
            Guid.NewGuid(), SpmePermissions.ReportsReview);
        using var hiddenApprove = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/approve");
        hiddenApprove.Headers.IfMatch.Add(submitted.Headers.ETag!);
        hiddenApprove.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await otherHod.SendAsync(hiddenApprove)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var reviewer = Client(seed.ReviewerUserId, SpmeRoles.HeadOfSection, seed.InstituteId,
            seed.ReviewerEmployeeId, SpmePermissions.ReportsReview);
        using var returned = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/return")
        {
            Content = JsonContent.Create(new ReturnReportRequest("Add laboratory counts."))
        };
        returned.Headers.IfMatch.Add(submitted.Headers.ETag!);
        returned.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var returnResponse = await reviewer.SendAsync(returned);
        returnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await returnResponse.Content.ReadAsStringAsync());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.Notifications.AsNoTracking().AnyAsync(item =>
            item.RecipientUserId == seed.EmployeeUserId &&
            item.ActionLink == $"/reports/{report.Id:D}" &&
            item.Title.Contains("returned"))).Should().BeTrue();
        (await db.CommunicationOutboxMessages.AsNoTracking().AnyAsync(item =>
            item.Category == "staff-quarterly-report-returned" &&
            item.Channel == "email" &&
            item.Body.Contains("Add laboratory counts."))).Should().BeTrue();

        var returnedReport = (await returnResponse.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;
        using var resubmitPatch = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/staff-quarterly-reports/{report.Id}")
        {
            Content = JsonContent.Create(ReportRequest(seed.PeriodId, seed.ReviewerUserId,
                "Updated title", "Abstract", "Updated summary with counts", "Key results", "Next steps",
                [seed.ProjectId], []))
        };
        resubmitPatch.Headers.IfMatch.Add(returnResponse.Headers.ETag!);
        var resubmittedDraft = await employee.SendAsync(resubmitPatch);
        resubmittedDraft.EnsureSuccessStatusCode();
        using var resubmit = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        resubmit.Headers.IfMatch.Add(resubmittedDraft.Headers.ETag!);
        resubmit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var resubmitted = await employee.SendAsync(resubmit);
        resubmitted.EnsureSuccessStatusCode();

        using var approve = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/approve");
        approve.Headers.IfMatch.Add(resubmitted.Headers.ETag!);
        approve.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var approved = await reviewer.SendAsync(approve);
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await approved.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data.Status
            .Should().Be(ReportStatuses.Approved);
    }

    [Fact]
    public async Task Closed_Period_Unverified_Hod_And_Division_Fallback_Are_Enforced()
    {
        var seed = await SeedAsync();
        Guid closedPeriodId;
        Guid unverifiedHodUserId;
        Guid divisionHodUserId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var closed = ReportingPeriod.Create(ScopeTypes.Institute, seed.InstituteId,
                $"2025-Q-{Guid.NewGuid():N}"[..16], "Closed quarter", ReportingPeriodTypes.Quarterly,
                new DateTime(2025, 10, 1), new DateTime(2025, 12, 31), new DateTime(2026, 1, 15)).Value!;
            closed.Open();
            closed.Close();
            var unverified = new User($"unverified.{Guid.NewGuid():N}", SpmeRoles.Employee)
            {
                Email = $"unverified.{Guid.NewGuid():N}@example.test",
                PhoneNumber = "+233200000099",
                EmailConfirmed = false,
                PhoneNumberConfirmed = true
            };
            var unverifiedEmployee = new Employee(seed.InstituteId, $"UV-{Guid.NewGuid():N}"[..16], "Unverified", "unspecified");
            unverified.LinkEmployee(unverifiedEmployee.Id, seed.InstituteId);
            unverified.UpdateDisplayName("Unverified HOD");
            var divisionEmployee = new Employee(seed.InstituteId, $"DH-{Guid.NewGuid():N}"[..16], "Division", "unspecified");
            divisionEmployee.UpdateImportedProfile(null, "Head", null, null, null, null,
                $"division.{Guid.NewGuid():N}@example.test", "+233200000088", true);
            var divisionUser = new User($"division.{Guid.NewGuid():N}", SpmeRoles.Employee)
            {
                Email = divisionEmployee.PrimaryEmail,
                PhoneNumber = divisionEmployee.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            divisionUser.LinkEmployee(divisionEmployee.Id, seed.InstituteId);
            divisionUser.UpdateDisplayName("Division Head");
            var sectionRole = await db.Roles.SingleAsync(item => item.Name == SpmeRoles.HeadOfSection);
            var divisionRole = await db.Roles.SingleOrDefaultAsync(item => item.Name == SpmeRoles.HeadOfDivision) ??
                new Role("head-of-division", SpmeRoles.HeadOfDivision, "Head of division", true);
            if (db.Entry(divisionRole).State == EntityState.Detached) db.Roles.Add(divisionRole);
            db.UserRoles.AddRange(
                new IdentityUserRole<Guid> { UserId = unverified.Id, RoleId = sectionRole.Id },
                new IdentityUserRole<Guid> { UserId = divisionUser.Id, RoleId = divisionRole.Id });
            var employment = await db.EmploymentRecords.SingleAsync(item => item.EmployeeId == seed.EmployeeId && item.IsCurrent);
            db.Employees.AddRange(unverifiedEmployee, divisionEmployee);
            db.Users.AddRange(unverified, divisionUser);
            db.EmploymentRecords.AddRange(
                new EmploymentRecord(unverifiedEmployee.Id, seed.InstituteId, employment.DivisionId, employment.SectionId,
                    null, "Head of Section", "head-of-section", "senior-staff", "active", null, null, null, null, null,
                    new DateTime(2020, 1, 1), true),
                new EmploymentRecord(divisionEmployee.Id, seed.InstituteId, employment.DivisionId, null, null,
                    "Head of Division", "head-of-division", "senior-staff", "active", null, null, null, null, null,
                    new DateTime(2020, 1, 1), true));
            db.ReportingPeriods.Add(closed);
            await db.SaveChangesAsync();
            closedPeriodId = closed.Id;
            unverifiedHodUserId = unverified.Id;
            divisionHodUserId = divisionUser.Id;
        }

        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var options = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyReportOptions>>(
            "/api/v2/staff-quarterly-reports/options");
        options!.Data.ReportingPeriods.Should().NotContain(item => item.Id == closedPeriodId);
        options.Data.Reviewers.Should().Contain(item => item.UserId == unverifiedHodUserId);

        var closedCreate = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(closedPeriodId, seed.ReviewerUserId, "Closed period report",
                null, "Work summary", null, null, [seed.ProjectId], []));
        closedCreate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.UserRoles.RemoveRange(db.UserRoles.Where(item =>
                item.UserId == seed.ReviewerUserId || item.UserId == unverifiedHodUserId));
            var sectionEmployments = await db.EmploymentRecords
                .Where(item => item.IsCurrent &&
                    (item.EmployeeId == seed.ReviewerEmployeeId ||
                     db.Users.Any(user => user.Id == unverifiedHodUserId && user.EmployeeId == item.EmployeeId)))
                .ToListAsync();
            foreach (var sectionEmployment in sectionEmployments)
            {
                sectionEmployment.UpdateCurrent(
                    sectionEmployment.DivisionId, sectionEmployment.SectionId, sectionEmployment.PositionTypeId,
                    sectionEmployment.GradeId, sectionEmployment.JobTitle, null, sectionEmployment.StaffCategory,
                    sectionEmployment.GradeStep, sectionEmployment.AreaOfSpecialization, sectionEmployment.ServiceStatus,
                    sectionEmployment.Organization, sectionEmployment.Location, sectionEmployment.Region,
                    sectionEmployment.District, sectionEmployment.AppointmentDate, sectionEmployment.PromotionDate,
                    sectionEmployment.PensionType, sectionEmployment.PensionId);
            }
            await db.SaveChangesAsync();
        }

        var fallbackOptions = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyReportOptions>>(
            "/api/v2/staff-quarterly-reports/options");
        fallbackOptions!.Data.Reviewers.Should().ContainSingle(item => item.UserId == divisionHodUserId);
        fallbackOptions.Data.Reviewers.Should().NotContain(item => item.UserId == seed.ReviewerUserId);
    }

    [Fact]
    public async Task Quarterly_Report_Create_Is_Idempotent()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var payload = ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Idempotent report",
            null, "Work summary for idempotency", null, null, [seed.ProjectId], []);
        var key = Guid.NewGuid().ToString("N");
        var first = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports", payload, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        var replay = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports", payload, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        (await replay.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data.Id
            .Should().Be((await first.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data.Id);
    }

    [Fact]
    public async Task Form_1_Locks_And_Incomplete_Projects_Block_Form_2()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);

        using var locked = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}/inception")
        {
            Content = JsonContent.Create(FormOneRequest("Water quality project",
                seed.EmployeeId, complete: true))
        };
        (await employee.SendAsync(locked)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var incomplete = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports/project-drafts",
            new CreateStaffQuarterlyProjectDraftRequest(FormOneRequest(
                "Thin project draft", seed.EmployeeId, complete: false)));
        incomplete.StatusCode.Should().Be(HttpStatusCode.Created);
        var thinProject = (await incomplete.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyCatalogOption>>())!.Data;
        thinProject.HasInception.Should().BeFalse();

        var missingProgress = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            new SaveStaffQuarterlyReportRequest(seed.PeriodId, seed.ReviewerUserId, "Missing progress",
                null, "Work summary for validation.", null, null, [thinProject.Id], [], []));
        missingProgress.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var blockedProgress = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Blocked Form 2",
                null, "Work summary for validation.", null, null, [thinProject.Id], []));
        blockedProgress.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var negativeProgress = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            new SaveStaffQuarterlyReportRequest(seed.PeriodId, seed.ReviewerUserId, "Negative counts",
                null, "Work summary for validation.", null, null, [seed.ProjectId], [],
                [new SaveStaffQuarterlyProjectProgressRequest(
                    seed.ProjectId, "Quarterly progress recorded.", null, null, null, null, -1, 0)]));
        negativeProgress.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var duplicateProgress = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            new SaveStaffQuarterlyReportRequest(seed.PeriodId, seed.ReviewerUserId, "Duplicate progress",
                null, "Work summary for validation.", null, null, [seed.ProjectId], [],
                [
                    new SaveStaffQuarterlyProjectProgressRequest(
                        seed.ProjectId, "Quarterly progress recorded.", null, null, null, null, 0, 0),
                    new SaveStaffQuarterlyProjectProgressRequest(
                        seed.ProjectId, "Duplicate progress recorded.", null, null, null, null, 0, 0)
                ]));
        duplicateProgress.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Submit_Rechecks_Form_2_Progress()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Submit validation",
                null, "Work summary for submit validation.", null, null, [seed.ProjectId], []));
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            await db.ReportProjects.Where(item => item.ReportId == report.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ProgressSummary, (string?)null));
        }

        using var submit = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        submit.Headers.IfMatch.Add(created.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await employee.SendAsync(submit)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concept_Note_And_Image_Upload_Sessions_Enforce_Type_And_Size()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var incomplete = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports/project-drafts",
            new CreateStaffQuarterlyProjectDraftRequest(FormOneRequest(
                "Concept note project", seed.EmployeeId, complete: false)));
        var noteProject = (await incomplete.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyCatalogOption>>())!.Data;
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Upload limits",
                null, "Work summary for upload tests.", null, null, [seed.ProjectId], []));
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;

        var oversizeConcept = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/projects/{noteProject.Id}/concept-note-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("note.pdf", "application/pdf", 62_914_561,
                new string('a', 64)));
        oversizeConcept.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var invalidConcept = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/projects/{noteProject.Id}/concept-note-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("note.txt", "text/plain", 1024,
                new string('b', 64)));
        invalidConcept.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var mismatchedConceptExtension = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/projects/{noteProject.Id}/concept-note-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("note.docx", "application/pdf", 1024,
                new string('b', 64)));
        mismatchedConceptExtension.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var oversizeImage = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/{report.Id}/image-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("photo.jpg", "image/jpeg", 20_971_521,
                new string('c', 64)));
        oversizeImage.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var invalidImageType = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/{report.Id}/image-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("photo.gif", "image/gif", 1024,
                new string('c', 64)));
        invalidImageType.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        Guid pendingImageSessionId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var pendingImageSession = new StaffQuarterlyReportUploadSession(
                seed.InstituteId, seed.EmployeeId, seed.EmployeeUserId,
                StaffReportUploadKinds.ReportImage, report.Id, null,
                $"reports/{Guid.NewGuid():N}.jpg", "photo.jpg", "image/jpeg", 1024,
                new string('c', 64), DateTimeOffset.UtcNow.AddMinutes(15));
            pendingImageSessionId = pendingImageSession.Id;
            db.StaffQuarterlyReportUploadSessions.Add(pendingImageSession);
            for (var index = 0; index < 3; index++)
            {
                var file = new FileRecord($"reports/{Guid.NewGuid():N}.jpg", $"image-{index}.jpg", "image/jpeg",
                    1024, new string('d', 64), "staff-quarterly-report", seed.InstituteId, "confidential");
                file.MarkScanStatus("clean");
                db.FileRecords.Add(file);
                db.ReportAttachments.Add(new ReportAttachment(report.Id, file.Id, StaffReportAttachmentTypes.ReportImage));
            }
            await db.SaveChangesAsync();
        }

        var fourthImage = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/{report.Id}/image-upload-sessions",
            new CreateStaffQuarterlyUploadSessionRequest("photo.jpg", "image/jpeg", 1024,
                new string('e', 64)));
        fourthImage.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var completionAtLimit = await PostJsonAsync(employee,
            $"/api/v2/staff-quarterly-reports/upload-sessions/{pendingImageSessionId}/complete",
            new { });
        completionAtLimit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Submit_Rejects_Pending_And_Infected_Report_Images()
    {
        var seed = await SeedAsync();
        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Pending image submit",
                null, "Work summary for pending image.", null, null, [seed.ProjectId], []));
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var file = new FileRecord($"reports/{Guid.NewGuid():N}.jpg", "pending.jpg", "image/jpeg",
                1024, new string('f', 64), "staff-quarterly-report", seed.InstituteId, "confidential");
            file.MarkScanStatus("pending");
            db.FileRecords.Add(file);
            db.ReportAttachments.Add(new ReportAttachment(report.Id, file.Id, StaffReportAttachmentTypes.ReportImage));
            await db.SaveChangesAsync();
        }

        using var submit = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        submit.Headers.IfMatch.Add(created.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await employee.SendAsync(submit)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var file = await db.FileRecords.SingleAsync(item =>
                db.ReportAttachments.Any(link => link.ReportId == report.Id && link.FileId == item.Id));
            file.MarkScanStatus("infected");
            await db.SaveChangesAsync();
        }

        using var infectedSubmit = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        infectedSubmit.Headers.IfMatch.Add(created.Headers.ETag!);
        infectedSubmit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await employee.SendAsync(infectedSubmit)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hod_Options_Use_Section_Leadership_And_Exclude_Other_Sections()
    {
        var seed = await SeedAsync();
        Guid leadershipOnlyUserId;
        Guid otherSectionUserId;
        string leadershipEmail;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var employment = await db.EmploymentRecords.SingleAsync(
                item => item.EmployeeId == seed.EmployeeId && item.IsCurrent);
            var otherSection = new Section(employment.DivisionId!.Value, "Soils");
            var leadershipEmployee = new Employee(seed.InstituteId, $"LH-{Guid.NewGuid():N}"[..16], "Leader", "unspecified");
            leadershipEmail = $"leader.{Guid.NewGuid():N}@example.test";
            leadershipEmployee.UpdateImportedProfile(null, "Kwame", null, null, null, null,
                leadershipEmail, "+233200000077", true);
            var leadershipUser = new User($"leader.{Guid.NewGuid():N}", SpmeRoles.Employee)
            {
                Email = leadershipEmail,
                PhoneNumber = "+233200000077",
                EmailConfirmed = false,
                PhoneNumberConfirmed = false
            };
            leadershipUser.LinkEmployee(leadershipEmployee.Id, seed.InstituteId);
            leadershipUser.UpdateDisplayName("Kwame Leader");
            var otherEmployee = new Employee(seed.InstituteId, $"OS-{Guid.NewGuid():N}"[..16], "Other", "unspecified");
            otherEmployee.UpdateImportedProfile(null, "Ama", null, null, null, null,
                $"other.{Guid.NewGuid():N}@example.test", "+233200000066", true);
            var otherUser = new User($"other.{Guid.NewGuid():N}", SpmeRoles.Employee)
            {
                Email = otherEmployee.PrimaryEmail,
                PhoneNumber = otherEmployee.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            otherUser.LinkEmployee(otherEmployee.Id, seed.InstituteId);
            otherUser.UpdateDisplayName("Ama Other");
            var sectionRole = await db.Roles.SingleAsync(item => item.Name == SpmeRoles.HeadOfSection);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = otherUser.Id, RoleId = sectionRole.Id });
            db.Sections.Add(otherSection);
            db.Employees.AddRange(leadershipEmployee, otherEmployee);
            db.Users.AddRange(leadershipUser, otherUser);
            db.EmploymentRecords.AddRange(
                new EmploymentRecord(leadershipEmployee.Id, seed.InstituteId, employment.DivisionId, employment.SectionId,
                    null, "Head of Section", "head-of-section", "senior-staff", "active", null, null, null, null, null,
                    new DateTime(2020, 1, 1), true),
                new EmploymentRecord(otherEmployee.Id, seed.InstituteId, employment.DivisionId, otherSection.Id,
                    null, "Head of Section", "head-of-section", "senior-staff", "active", null, null, null, null, null,
                    new DateTime(2020, 1, 1), true));
            db.UserRoles.RemoveRange(db.UserRoles.Where(item => item.UserId == seed.ReviewerUserId));
            var seededEmployment = await db.EmploymentRecords.SingleAsync(
                item => item.EmployeeId == seed.ReviewerEmployeeId && item.IsCurrent);
            seededEmployment.UpdateCurrent(
                seededEmployment.DivisionId, seededEmployment.SectionId, seededEmployment.PositionTypeId,
                seededEmployment.GradeId, seededEmployment.JobTitle, null, seededEmployment.StaffCategory,
                seededEmployment.GradeStep, seededEmployment.AreaOfSpecialization, seededEmployment.ServiceStatus,
                seededEmployment.Organization, seededEmployment.Location, seededEmployment.Region,
                seededEmployment.District, seededEmployment.AppointmentDate, seededEmployment.PromotionDate,
                seededEmployment.PensionType, seededEmployment.PensionId);
            await db.SaveChangesAsync();
            leadershipOnlyUserId = leadershipUser.Id;
            otherSectionUserId = otherUser.Id;
        }

        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var options = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyReportOptions>>(
            "/api/v2/staff-quarterly-reports/options");
        options!.Data.Reviewers.Should().ContainSingle(item => item.UserId == leadershipOnlyUserId);
        options.Data.Reviewers[0].Email.Should().Be(leadershipEmail);
        options.Data.Reviewers[0].Phone.Should().Be("+233200000077");
        options.Data.Reviewers.Should().NotContain(item => item.UserId == otherSectionUserId);
        options.Data.Reviewers.Should().NotContain(item => item.UserId == seed.ReviewerUserId);
    }

    [Fact]
    public async Task Scientific_Secretary_Assigns_Pin_And_Draft_Shows_Live_Pin()
    {
        var seed = await SeedAsync();
        var ssUserId = Guid.NewGuid();
        var ssEmployeeId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var ssEmployee = new Employee(seed.InstituteId, $"SS-{Guid.NewGuid():N}"[..16], "Secretary", "unspecified");
            ssEmployee.UpdateImportedProfile(null, "Sam", null, null, null, null,
                $"ss.{Guid.NewGuid():N}@example.test", "+233200000099", true);
            var ssUser = new User($"ss.{Guid.NewGuid():N}", SpmeRoles.ScientificSecretary)
            {
                Email = ssEmployee.PrimaryEmail,
                PhoneNumber = ssEmployee.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            ssUser.LinkEmployee(ssEmployee.Id, seed.InstituteId);
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == SpmeRoles.ScientificSecretary) ??
                new Role("scientific-secretary", SpmeRoles.ScientificSecretary, "Scientific Secretary", true);
            if (db.Entry(role).State == EntityState.Detached) db.Roles.Add(role);
            db.Employees.Add(ssEmployee);
            db.Users.Add(ssUser);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = ssUser.Id, RoleId = role.Id });
            db.EmploymentRecords.Add(new EmploymentRecord(ssEmployee.Id, seed.InstituteId, null, null, null,
                "Scientific Secretary", null, "senior-staff", "active", null, null, null, null, null,
                new DateTime(2020, 1, 1), true));
            await db.SaveChangesAsync();
            ssUserId = ssUser.Id;
            ssEmployeeId = ssEmployee.Id;
        }

        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var beforePin = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyProjectInceptionResponse>>(
            $"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}");
        beforePin!.Data.PinStatus.Should().Be("pending");
        beforePin.Data.Pin.Should().BeNull();

        using var ss = Client(ssUserId, SpmeRoles.ScientificSecretary, seed.InstituteId,
            ssEmployeeId, SpmePermissions.ReportsReview);
        var project = await ss.GetFromJsonAsync<DataResponse<StaffQuarterlyProjectInceptionResponse>>(
            $"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}");
        project!.Data.PinStatus.Should().Be("pending");

        byte[] rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            rowVersion = (await db.Projects.AsNoTracking().SingleAsync(item => item.Id == seed.ProjectId)).RowVersion;
        }

        using var assign = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}/pin")
        {
            Content = JsonContent.Create(new AssignProjectPinRequest("CSIR-PIN-001"))
        };
        assign.Headers.TryAddWithoutValidation("If-Match", ConcurrencyToken.Format(rowVersion));
        var assigned = await ss.SendAsync(assign);
        assigned.StatusCode.Should().Be(HttpStatusCode.OK, await assigned.Content.ReadAsStringAsync());

        var afterPin = await employee.GetFromJsonAsync<DataResponse<StaffQuarterlyProjectInceptionResponse>>(
            $"/api/v2/staff-quarterly-reports/projects/{seed.ProjectId}");
        afterPin!.Data.Pin.Should().Be("CSIR-PIN-001");
        afterPin.Data.PinStatus.Should().Be("assigned");

        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "PIN pending submit",
                null, "Work summary for PIN pending submit.", null, null, [seed.ProjectId], []));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var submit = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v2/staff-quarterly-reports/{(await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data.Id}/submit");
        submit.Headers.IfMatch.Add(created.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await employee.SendAsync(submit)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scientific_Secretary_Collation_Returns_Submitted_Reports_For_Institute_Quarter()
    {
        var seed = await SeedAsync();
        var ssUserId = Guid.NewGuid();
        var ssEmployeeId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var ssEmployee = new Employee(seed.InstituteId, $"SSC-{Guid.NewGuid():N}"[..16], "Secretary", "unspecified");
            ssEmployee.UpdateImportedProfile(null, "Collation", null, null, null, null,
                $"ssc.{Guid.NewGuid():N}@example.test", "+233200000088", true);
            var ssUser = new User($"ssc.{Guid.NewGuid():N}", SpmeRoles.ScientificSecretary)
            {
                Email = ssEmployee.PrimaryEmail,
                PhoneNumber = ssEmployee.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };
            ssUser.LinkEmployee(ssEmployee.Id, seed.InstituteId);
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == SpmeRoles.ScientificSecretary) ??
                new Role("scientific-secretary", SpmeRoles.ScientificSecretary, "Scientific Secretary", true);
            if (db.Entry(role).State == EntityState.Detached) db.Roles.Add(role);
            db.Employees.Add(ssEmployee);
            db.Users.Add(ssUser);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = ssUser.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
            ssUserId = ssUser.Id;
            ssEmployeeId = ssEmployee.Id;
        }

        using var employee = Client(seed.EmployeeUserId, SpmeRoles.Employee, seed.InstituteId,
            seed.EmployeeId, SpmePermissions.ReportsSelf);
        var created = await PostJsonAsync(employee, "/api/v2/staff-quarterly-reports",
            ReportRequest(seed.PeriodId, seed.ReviewerUserId, "Collation coverage report",
                null, "Work summary for collation.", null, null, [seed.ProjectId], []));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = (await created.Content.ReadFromJsonAsync<DataResponse<StaffQuarterlyReportResponse>>())!.Data;
        using var submit = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/staff-quarterly-reports/{report.Id}/submit");
        submit.Headers.IfMatch.Add(created.Headers.ETag!);
        submit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await employee.SendAsync(submit)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var ss = Client(ssUserId, SpmeRoles.ScientificSecretary, seed.InstituteId,
            ssEmployeeId, SpmePermissions.ReportsReview);
        var collation = await ss.GetFromJsonAsync<ListResponse<StaffQuarterlyCollationEntry>>(
            $"/api/v2/staff-quarterly-reports/collation?reportingPeriodId={seed.PeriodId}");
        collation!.Data.Should().ContainSingle(item => item.ReportId == report.Id && item.Status == ReportStatuses.Submitted);
        collation.Data[0].Projects.Should().ContainSingle(item => item.ProjectId == seed.ProjectId);

        using var hod = Client(seed.ReviewerUserId, SpmeRoles.HeadOfSection, seed.InstituteId,
            seed.ReviewerEmployeeId, SpmePermissions.ReportsReview);
        (await hod.GetAsync($"/api/v2/staff-quarterly-reports/collation?reportingPeriodId={seed.PeriodId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var instituteSuffix = Guid.NewGuid().ToString("N")[..8];
        var institute = new Institute($"QR-{instituteSuffix}", $"Quarterly reports institute {instituteSuffix}", "Institute");
        var division = new Division(institute.Id, "Research");
        var section = new Section(division.Id, "Water");
        var employee = new Employee(institute.Id, $"EMP-{Guid.NewGuid():N}"[..16], "Researcher", "unspecified");
        employee.UpdateImportedProfile(null, "Ada", null, null, null, null,
            $"employee.{Guid.NewGuid():N}@example.test", "+233200000001", true);
        var reviewerEmployee = new Employee(institute.Id, $"HOD-{Guid.NewGuid():N}"[..16], "Reviewer", "unspecified");
        reviewerEmployee.UpdateImportedProfile(null, "Hannah", null, null, null, null,
            $"hod.{Guid.NewGuid():N}@example.test", "+233200000002", true);
        var employeeUser = new User($"employee.{Guid.NewGuid():N}", SpmeRoles.Employee);
        employeeUser.LinkEmployee(employee.Id, institute.Id);
        employeeUser.Email = employee.PrimaryEmail;
        employeeUser.PhoneNumber = employee.Phone;
        employeeUser.EmailConfirmed = true;
        employeeUser.PhoneNumberConfirmed = true;
        employeeUser.UpdateDisplayName("Ada Researcher");
        var reviewerUser = new User($"hod.{Guid.NewGuid():N}", SpmeRoles.Employee)
        {
            Email = reviewerEmployee.PrimaryEmail,
            PhoneNumber = reviewerEmployee.Phone,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };
        reviewerUser.LinkEmployee(reviewerEmployee.Id, institute.Id);
        reviewerUser.UpdateDisplayName("Hannah Reviewer");
        var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == SpmeRoles.HeadOfSection) ??
            new Role("head-of-section", SpmeRoles.HeadOfSection, "Head of section", true);
        if (db.Entry(role).State == EntityState.Detached) db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = reviewerUser.Id, RoleId = role.Id });
        db.Institutes.Add(institute);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        db.Employees.AddRange(employee, reviewerEmployee);
        db.Users.AddRange(employeeUser, reviewerUser);
        db.EmploymentRecords.AddRange(
            new EmploymentRecord(employee.Id, institute.Id, division.Id, section.Id, null,
                "Research Scientist", null, "senior-staff", "active", null, null, null, null, null,
                new DateTime(2020, 1, 1), true),
            new EmploymentRecord(reviewerEmployee.Id, institute.Id, division.Id, section.Id, null,
                "Head of Section", "head-of-section", "senior-staff", "active", null, null, null, null, null,
                new DateTime(2020, 1, 1), true));
        var period = ReportingPeriod.Create(ScopeTypes.Institute, institute.Id,
            $"2026-Q-{Guid.NewGuid():N}"[..16], "2026 Quarter 3", ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 7, 1), new DateTime(2026, 9, 30), new DateTime(2026, 10, 15)).Value!;
        period.Open();
        var project = Project.Create(institute.Id, $"PRJ-{Guid.NewGuid():N}"[..16],
            "Water quality project", "Assess water quality", "Water quality need", "Laboratory analysis",
            null, ProjectNatures.Research, new DateTime(2026, 1, 1), null, "GHS", 1000m,
            null, null, employee.Id, null);
        var inception = ProjectInception.Create(project.Id);
        inception.UpdateDraft("1 year", "CSIR Internal Fund", "Accra", null, null,
            "Local communities", "Water quality sensors", "Licensing to utilities", "Methodology advances");
        inception.Complete(DateTimeOffset.UtcNow);
        db.ReportingPeriods.Add(period);
        db.Projects.Add(project);
        db.ProjectInceptions.Add(inception);
        await db.SaveChangesAsync();
        return new(institute.Id, employee.Id, employeeUser.Id, reviewerEmployee.Id, reviewerUser.Id,
            reviewerEmployee.PrimaryEmail!, period.Id, project.Id);
    }

    private HttpClient Client(Guid userId, string role, Guid instituteId, Guid employeeId, params string[] permissions)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwt = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("identity_type", "Employee"),
            new("institute_id", instituteId.ToString()),
            new("employee_id", employeeId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client", claims,
            expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static SaveStaffQuarterlyReportRequest ReportRequest(
        Guid periodId,
        Guid reviewerUserId,
        string title,
        string? @abstract,
        string workSummary,
        string? keyResults,
        string? conclusion,
        IReadOnlyList<Guid> projectIds,
        IReadOnlyList<Guid> technologyIds,
        string progressSummary = "Quarterly progress recorded.")
    {
        var progress = projectIds.Select(id => new SaveStaffQuarterlyProjectProgressRequest(
            id, progressSummary, "Key outputs", "Challenges", "Next quarter", "Way forward", 0, 0)).ToList();
        return new SaveStaffQuarterlyReportRequest(periodId, reviewerUserId, title, @abstract, workSummary,
            keyResults, conclusion, projectIds, technologyIds, progress);
    }

    private static SaveStaffQuarterlyProjectInceptionRequest FormOneRequest(
        string name, Guid leadEmployeeId, bool complete) => new(
        name, "Map coastal sediment transport", "Background and justification for coastal sediment mapping.",
        "Remote sensing with ground-truth samples.", ProjectNatures.Research, new DateTime(2026, 2, 1),
        new DateTime(2026, 11, 30), 250000.50m, "USD", leadEmployeeId, "11 months", "CSIR Internal Fund",
        "Accra", null, "Kenneth Asiamah", "Communities near the coast", "Coastal monitoring platform",
        "Technology licensing pathway", "Policy guidance for sediment management", complete);

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client, string path, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private sealed record Seed(Guid InstituteId, Guid EmployeeId, Guid EmployeeUserId,
        Guid ReviewerEmployeeId, Guid ReviewerUserId, string ReviewerEmail, Guid PeriodId, Guid ProjectId);
}
