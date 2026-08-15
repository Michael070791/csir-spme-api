using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using Csir.Spme.Infrastructure.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class StaffPortalP0EndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    public StaffPortalP0EndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_Is_SelfScoped_AudienceFiltered_And_NotCacheable()
    {
        var seeded = await SeedEmployeeAsync();
        var userId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.LeaveBalances.Add(LeaveBalance.Create(seeded.EmployeeId, LeaveTypes.Annual, 2026, 32));
            db.Notifications.Add(new Notification(userId, "Unread", "Private"));
            var visible = Memo.Create(seeded.InstituteId, "Visible memo", "Visible body").Value!;
            visible.Publish(userId, DateTimeOffset.UtcNow.AddMinutes(-2));
            var hidden = Memo.Create(seeded.InstituteId, "Hidden memo", "Hidden body").Value!;
            hidden.Publish(userId, DateTimeOffset.UtcNow);
            db.Memos.AddRange(visible, hidden);
            db.MemoAudiences.Add(new MemoAudience(visible.Id, MemoAudienceTypes.Employee, employeeId: seeded.EmployeeId));
            db.MemoAudiences.Add(new MemoAudience(hidden.Id, MemoAudienceTypes.Employee, employeeId: seeded.OtherEmployeeId));
            await db.SaveChangesAsync();
        }

        using var client = Client(CreateToken(userId, SpmeRoles.Employee, seeded.InstituteId, seeded.EmployeeId));
        var response = await client.GetAsync("/api/v2/me/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("latestMemo").GetProperty("title").GetString().Should().Be("Visible memo");
        json.RootElement.GetProperty("unreadNotificationCount").GetInt32().Should().Be(1);

        using var cross = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId, seeded.OtherEmployeeId));
        using var crossJson = JsonDocument.Parse(await (await cross.GetAsync("/api/v2/me/dashboard")).Content.ReadAsStringAsync());
        crossJson.RootElement.GetProperty("latestMemo").GetProperty("title").GetString().Should().Be("Hidden memo");
    }

    [Fact]
    public async Task Education_And_Children_Allow_Owner_But_Hide_Other_Employee()
    {
        var seeded = await SeedEmployeeAsync();
        using var owner = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId, seeded.EmployeeId));
        var education = new UpsertEducationRecordRequest("University", "Computer Science", "BSc",
            "bachelor-or-equivalent", DateCommenced: new DateTime(2018, 9, 1), DateCompleted: new DateTime(2022, 6, 1));
        var created = await owner.PostAsJsonAsync($"/api/v2/employees/{seeded.EmployeeId}/education", education);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await created.Content.ReadFromJsonAsync<EducationRecordResponse>();
        record!.InstitutionRecognitionStatus.Should().Be("pending");

        var updated = await owner.PatchAsJsonAsync($"/api/v2/employees/{seeded.EmployeeId}/education/{record.Id}",
            education with { CourseStudied = "Information Systems" });
        updated.EnsureSuccessStatusCode();
        (await updated.Content.ReadFromJsonAsync<EducationRecordResponse>())!.InstitutionRecognitionStatus.Should().Be("pending");

        (await owner.PostAsJsonAsync($"/api/v2/employees/{seeded.EmployeeId}/children",
            new UpsertEmployeeChildRequest("Child", new DateTime(2018, 1, 1), "female", null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await owner.PostAsJsonAsync($"/api/v2/employees/{seeded.EmployeeId}/children",
            new UpsertEmployeeChildRequest("Unsafe", new DateTime(2019, 1, 1), "female", null, Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.GetAsync($"/api/v2/employees/{seeded.OtherEmployeeId}/education"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"/api/v2/employees/{seeded.OtherEmployeeId}/children"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/v2/employees/{seeded.EmployeeId}/education/{record.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Hr_Can_Review_Education_Recognition_And_Relevant_Field()
    {
        var seeded = await SeedEmployeeAsync();
        using var owner = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId, seeded.EmployeeId));
        var education = new UpsertEducationRecordRequest(
            "University of Ghana",
            "Chemistry",
            "BSc",
            "bachelor-or-equivalent",
            DateCommenced: new DateTime(2017, 9, 1),
            DateCompleted: new DateTime(2021, 6, 1));
        var created = await owner.PostAsJsonAsync($"/api/v2/employees/{seeded.EmployeeId}/education", education);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await created.Content.ReadFromJsonAsync<EducationRecordResponse>();
        record.Should().NotBeNull();

        using var hr = Client(CreateToken(Guid.NewGuid(), SpmeRoles.HrAdmin, seeded.InstituteId, null));
        var review = await hr.PostAsJsonAsync(
            $"/api/v2/employees/{seeded.EmployeeId}/education/{record!.Id}/review",
            new ReviewEducationRecordRequest("verified", "verified"));
        review.StatusCode.Should().Be(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
        var reviewed = await review.Content.ReadFromJsonAsync<EducationRecordResponse>();
        reviewed.Should().NotBeNull();
        reviewed!.InstitutionRecognitionStatus.Should().Be("verified");
        reviewed.RelevantFieldStatus.Should().Be("verified");

        var forbidden = await owner.PostAsJsonAsync(
            $"/api/v2/employees/{seeded.EmployeeId}/education/{record.Id}/review",
            new ReviewEducationRecordRequest("rejected", "rejected"));
        forbidden.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Leave_Workflow_Calculates_Days_Reserves_Releases_And_Rejects_Stale_Etag()
    {
        var seeded = await SeedEmployeeAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.LeaveBalances.Add(LeaveBalance.Create(seeded.EmployeeId, LeaveTypes.Annual, 2026, 3));
            db.Holidays.Add(Holiday.Create("csir-wide", null, "Test holiday", new DateTime(2026, 8, 5), true, false, null).Value!);
            await db.SaveChangesAsync();
        }
        using var employee = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRequest));
        var calculation = await employee.PostAsJsonAsync("/api/v2/leave-requests/calculate-working-days",
            new CalculateWorkingDaysRequest(LeaveTypes.Annual, new DateTime(2026, 8, 3), new DateTime(2026, 8, 9)));
        calculation.StatusCode.Should().Be(HttpStatusCode.OK, await calculation.Content.ReadAsStringAsync());
        var calculated = (await calculation.Content.ReadFromJsonAsync<DataResponse<WorkingDaysResponse>>())!.Data;
        // Mon 3 – Sun 9 with Wed 5 holiday => Mon,Tue,Thu,Fri = 4 inclusive leave days; return Mon 10.
        calculated.WorkingDays.Should().Be(4);
        calculated.ExpectedReturnDate.Date.Should().Be(new DateTime(2026, 8, 10));

        var createRequest = new CreateLeaveRequestRequest(null, LeaveTypes.Annual,
            new DateTime(2026, 8, 3), new DateTime(2026, 8, 4), "Rest");
        var key = Guid.NewGuid().ToString();
        var created = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests", createRequest, key);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var originalBody = await created.Content.ReadAsStringAsync();
        var etag = created.Headers.ETag!.Tag;
        var replay = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests", createRequest, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        replay.Headers.ETag!.Tag.Should().Be(etag);
        replay.Headers.Location.Should().Be(created.Headers.Location);
        replay.Content.Headers.ContentType!.MediaType.Should().Be(created.Content.Headers.ContentType!.MediaType);
        (await replay.Content.ReadAsStringAsync()).Should().Be(originalBody);
        var reused = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests",
            createRequest with { Reason = "Different" }, key);
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var leave = JsonSerializer.Deserialize<DataResponse<LeaveRequestDto>>(
            originalBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Data;
        var submitted = await SendJsonAsync(employee, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/submit", new { }, Guid.NewGuid().ToString(), etag);
        submitted.EnsureSuccessStatusCode();
        var submittedEtag = submitted.Headers.ETag!.Tag;
        var preconditionKey = Guid.NewGuid().ToString();
        var staleCancel = await SendJsonAsync(employee, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/cancel", new { }, preconditionKey, etag);
        staleCancel.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        var changedPrecondition = await SendJsonAsync(employee, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/cancel", new { }, preconditionKey, submittedEtag);
        changedPrecondition.StatusCode.Should().Be(HttpStatusCode.Conflict);

        Guid hrUserId;
        Guid sectionHeadUserId;
        await using (var hrScope = _factory.Services.CreateAsyncScope())
        {
            var hrDb = hrScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var hrUser = new User($"hr.{Guid.NewGuid():N}", SpmeRoles.HrAdmin);
            hrUser.AssignInstitute(seeded.InstituteId);
            var sectionHeadEmployee = new Employee(seeded.InstituteId, $"HEAD-{Guid.NewGuid():N}"[..13], "Section Head", "unspecified");
            var sectionHead = new User($"head.{Guid.NewGuid():N}", SpmeRoles.Employee);
            sectionHead.LinkEmployee(sectionHeadEmployee.Id, seeded.InstituteId);
            hrDb.Users.AddRange(hrUser, sectionHead);
            hrDb.Employees.Add(sectionHeadEmployee);
            hrDb.EmploymentRecords.Add(new EmploymentRecord(sectionHeadEmployee.Id, seeded.InstituteId,
                seeded.DivisionId, seeded.SectionId, null, "Section Head", "head-of-section", "senior-staff",
                "active", null, null, null, null, null, new DateTime(2020, 1, 1), true));
            await hrDb.SaveChangesAsync();
            hrUserId = hrUser.Id;
            sectionHeadUserId = sectionHead.Id;
        }

        using var hr = Client(CreateToken(hrUserId, SpmeRoles.HrAdmin, seeded.InstituteId, null,
            SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        var wrongStage = await SendJsonAsync(hr, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/reject", new RejectLeaveRequest("Not approved"),
            Guid.NewGuid().ToString(), submittedEtag);
        wrongStage.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var sectionHeadClient = Client(CreateToken(sectionHeadUserId, SpmeRoles.HeadOfSection,
            seeded.InstituteId, (await GetEmployeeIdAsync(sectionHeadUserId)),
            SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        var rejected = await SendJsonAsync(sectionHeadClient, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/reject", new RejectLeaveRequest("Not approved"),
            Guid.NewGuid().ToString(), submittedEtag);
        rejected.EnsureSuccessStatusCode();
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await verifyDb.LeaveBalances.AsNoTracking().SingleAsync(x => x.EmployeeId == seeded.EmployeeId))
            .PendingDays.Should().Be(0);
        var rejectionEvents = await verifyDb.CommunicationOutboxMessages.AsNoTracking()
            .Where(message =>
                message.Channel == "event" &&
                message.Category == "leave-rejected" &&
                message.IdempotencyKey == $"leave-rejected:{leave.Id:N}")
            .ToListAsync();
        rejectionEvents.Should().ContainSingle();
        using var rejectionPayload = JsonDocument.Parse(rejectionEvents[0].Body);
        rejectionPayload.RootElement.GetProperty("eventType").GetString().Should().Be("leave.rejected.v1");
        rejectionPayload.RootElement.GetProperty("leaveRequestId").GetGuid().Should().Be(leave.Id);
        rejectionPayload.RootElement.GetProperty("instituteId").GetGuid().Should().Be(seeded.InstituteId);
        rejectionPayload.RootElement.GetProperty("employeeId").GetGuid().Should().Be(seeded.EmployeeId);
        rejectionPayload.RootElement.GetProperty("decidedByUserId").GetGuid().Should().Be(sectionHeadUserId);
        var rejectionEmail = await verifyDb.CommunicationOutboxMessages.AsNoTracking()
            .SingleAsync(message => message.Channel == "email" &&
                message.Category == "leave-rejected" &&
                message.IdempotencyKey.StartsWith($"leave-rejected:{leave.Id:N}:"));
        rejectionEmail.IsHtml.Should().BeTrue();
        rejectionEmail.Body.Should().Contain("Not approved").And.Contain("View leave request")
            .And.Contain($"/leave/{leave.Id:D}");
        rejectionEmail.TextBody.Should().Contain("Not approved");
        rejectionEmail.IdempotencyKey.Should().HaveLength(112).And.NotContain(rejectionEmail.Recipient);
    }

    [Fact]
    public async Task Leave_Idempotency_Requires_Key_Rejects_InProgress_And_Reuses_Expired_Reservation()
    {
        var seeded = await SeedEmployeeAsync();
        var userId = Guid.NewGuid();
        using var employee = Client(CreateToken(userId, SpmeRoles.Employee, seeded.InstituteId, seeded.EmployeeId));
        var createRequest = new CreateLeaveRequestRequest(null, LeaveTypes.Annual,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), "Rest");
        (await employee.PostAsJsonAsync("/api/v2/leave-requests", createRequest)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests",
            createRequest with { HandoverDocumentFileId = Guid.NewGuid() }, Guid.NewGuid().ToString()))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var json = JsonSerializer.Serialize(createRequest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var idempotencyScope = $"{userId}:POST:/api/v2/leave-requests";
        var inProgressKey = Guid.NewGuid().ToString();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.IdempotencyRecords.Add(new IdempotencyRecord(idempotencyScope, inProgressKey,
                requestHash, DateTimeOffset.UtcNow.AddMinutes(5)));
            await db.SaveChangesAsync();
        }

        (await SendRawJsonAsync(employee, "/api/v2/leave-requests", json, inProgressKey)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        var expiredKey = Guid.NewGuid().ToString();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.IdempotencyRecords.Add(new IdempotencyRecord(idempotencyScope, expiredKey,
                requestHash, DateTimeOffset.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }

        (await SendRawJsonAsync(employee, "/api/v2/leave-requests", json, expiredKey)).StatusCode
            .Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Employee_LeaveRead_Remains_SelfScoped_While_Hr_Read_Is_InstituteScoped()
    {
        var seeded = await SeedEmployeeAsync();
        Guid ownRequestId;
        Guid otherRequestId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var own = LeaveRequest.CreateDraft(seeded.EmployeeId, seeded.InstituteId, LeaveTypes.Annual,
                new DateTime(2026, 10, 1), new DateTime(2026, 10, 2), 2, "Own private reason", null,
                null, null, null, null);
            var other = LeaveRequest.CreateDraft(seeded.OtherEmployeeId, seeded.InstituteId, LeaveTypes.Annual,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), 2, "Other private reason", null,
                null, null, null, null);
            db.LeaveRequests.AddRange(own, other);
            db.LeaveBalances.AddRange(
                LeaveBalance.Create(seeded.EmployeeId, LeaveTypes.Annual, 2026, 32),
                LeaveBalance.Create(seeded.OtherEmployeeId, LeaveTypes.Annual, 2026, 32));
            await db.SaveChangesAsync();
            ownRequestId = own.Id;
            otherRequestId = other.Id;
        }

        using var employee = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead));
        (await employee.GetAsync($"/api/v2/leave-requests/{ownRequestId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync($"/api/v2/leave-requests/{otherRequestId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.GetAsync($"/api/v2/leave-requests?employeeId={seeded.OtherEmployeeId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.GetAsync($"/api/v2/leave-balances?employeeId={seeded.OtherEmployeeId}&leaveYear=2026"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var ownList = await employee.GetFromJsonAsync<ListResponse<LeaveRequestDto>>("/api/v2/leave-requests");
        ownList!.Data.Should().ContainSingle(item => item.Id == ownRequestId);
        ownList.Page.NextCursor.Should().BeNull();
        ownList.Meta.RequestId.Should().NotBeNullOrWhiteSpace();
        var leaveTypes = await employee.GetFromJsonAsync<ListResponse<LeaveTypeMetadataResponse>>(
            "/api/v2/leave-types");
        leaveTypes!.Data.Should().Contain(item => item.Code == LeaveTypes.Annual);
        var annualLeave = await employee.GetFromJsonAsync<DataResponse<LeaveTypeMetadataResponse>>(
            $"/api/v2/leave-types/{LeaveTypes.Annual}");
        annualLeave!.Data.Code.Should().Be(LeaveTypes.Annual);
        annualLeave.Data.IsRequestable.Should().BeTrue();
        leaveTypes.Data.Should().Contain(item =>
            item.Code == LeaveTypes.Sick &&
            !item.IsRequestable &&
            item.PolicyStatus == "secure-documents-unavailable");
        var delegates = await employee.GetFromJsonAsync<DataResponse<LeaveDelegateOptionsDto>>(
            "/api/v2/leave-delegates/me");
        delegates!.Data.Delegates.Should().NotContain(item => item.EmployeeId == seeded.EmployeeId);
        delegates.Data.Delegates.Should().Contain(item => item.EmployeeId == seeded.OtherEmployeeId);
        delegates.Data.ScopeMode.Should().Be("section");
        delegates.Data.PreferAlternateDivision.Should().BeFalse();

        using var hr = Client(CreateToken(Guid.NewGuid(), SpmeRoles.HrAdmin, seeded.InstituteId, null,
            SpmePermissions.LeaveRead));
        (await hr.GetAsync($"/api/v2/leave-requests/{otherRequestId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var balancesResponse = await hr.GetAsync(
            $"/api/v2/leave-balances?employeeId={seeded.OtherEmployeeId}&leaveYear=2026");
        balancesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var balances = await balancesResponse.Content.ReadFromJsonAsync<ListResponse<LeaveBalanceDto>>();
        balances!.Data.Should().ContainSingle(item =>
            item.EmployeeId == seeded.OtherEmployeeId && item.LeaveType == LeaveTypes.Annual);
    }

    [Fact]
    public async Task Leave_Delegates_Prefer_Section_And_Fallback_To_Other_Division()
    {
        var seeded = await SeedEmployeeAsync();
        Guid otherDivisionId;
        Guid otherDivisionEmployeeId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var otherEmployment = await db.EmploymentRecords
                .SingleAsync(item => item.EmployeeId == seeded.OtherEmployeeId && item.IsCurrent);
            db.EmploymentRecords.Remove(otherEmployment);
            db.Employees.Remove(await db.Employees.SingleAsync(item => item.Id == seeded.OtherEmployeeId));

            var otherDivision = new Division(seeded.InstituteId, "Fallback Division");
            var otherSection = new Section(otherDivision.Id, "Fallback Section");
            var peer = new Employee(seeded.InstituteId, $"PEER-{Guid.NewGuid():N}"[..13], "Peer", "female");
            peer.UpdateImportedProfile(null, "Delegate", null, null, null, null,
                $"peer.{Guid.NewGuid():N}@test.local", "0241111111", true);
            db.Divisions.Add(otherDivision);
            db.Sections.Add(otherSection);
            db.Employees.Add(peer);
            db.EmploymentRecords.Add(new EmploymentRecord(peer.Id, seeded.InstituteId, otherDivision.Id,
                otherSection.Id, null, "Officer", null, "senior-staff", "active", null, null, null, null, null,
                new DateTime(2020, 1, 1), true));
            await db.SaveChangesAsync();
            otherDivisionId = otherDivision.Id;
            otherDivisionEmployeeId = peer.Id;
        }

        using var employee = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRequest));
        var home = await employee.GetFromJsonAsync<DataResponse<LeaveDelegateOptionsDto>>(
            "/api/v2/leave-delegates/me");
        home!.Data.Delegates.Should().BeEmpty();
        home.Data.PreferAlternateDivision.Should().BeTrue();
        home.Data.AlternateDivisions.Should().Contain(item => item.Id == otherDivisionId);

        var alternate = await employee.GetFromJsonAsync<DataResponse<LeaveDelegateOptionsDto>>(
            $"/api/v2/leave-delegates/me?divisionId={otherDivisionId}");
        alternate!.Data.ScopeMode.Should().Be("alternate-division");
        alternate.Data.Delegates.Should().ContainSingle(item => item.EmployeeId == otherDivisionEmployeeId);
    }

    [Fact]
    public async Task Leave_Draft_Patch_And_Resumption_Expose_Stage_Aware_Actions()
    {
        var seeded = await SeedEmployeeAsync();
        using var employee = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveRequest));
        var created = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests",
            new CreateLeaveRequestRequest(null, LeaveTypes.Annual, new DateTime(2026, 11, 9),
                new DateTime(2026, 11, 10), "Draft rest"), Guid.NewGuid().ToString());
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        draft.AvailableActions.Should().BeEquivalentTo("edit", "submit", "cancel");

        var patched = await SendJsonAsync(employee, HttpMethod.Patch, $"/api/v2/leave-requests/{draft.Id}",
            new UpdateLeaveRequestRequest(LeaveTypes.Annual, new DateTime(2026, 11, 9),
                new DateTime(2026, 11, 10), "Updated rest", "Handover notes"), Guid.NewGuid().ToString(),
            created.Headers.ETag!.Tag);
        patched.StatusCode.Should().Be(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        (await patched.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data.Reason.Should().Be("Updated rest");

        Guid approvedId;
        Guid futureApprovedId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var approved = LeaveRequest.CreateDraft(seeded.EmployeeId, seeded.InstituteId, LeaveTypes.Annual,
                new DateTime(2026, 7, 1), new DateTime(2026, 7, 2), 2, "Approved rest", null,
                null, null, null, null);
            approved.Submit(LeaveApprovalStages.SectionHead, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
            approved.Approve(LeaveApprovalStages.SectionHead, LeaveApprovalStages.HeadOfDivision).IsSuccess.Should().BeTrue();
            approved.Approve(LeaveApprovalStages.HeadOfDivision, LeaveApprovalStages.InstituteDirector).IsSuccess.Should().BeTrue();
            approved.Approve(LeaveApprovalStages.InstituteDirector, null).IsSuccess.Should().BeTrue();
            var futureApproved = LeaveRequest.CreateDraft(seeded.EmployeeId, seeded.InstituteId, LeaveTypes.Annual,
                new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), 2, "Future approved rest", null,
                null, null, null, null);
            futureApproved.Submit(LeaveApprovalStages.SectionHead, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
            futureApproved.Approve(LeaveApprovalStages.SectionHead, LeaveApprovalStages.HeadOfDivision).IsSuccess.Should().BeTrue();
            futureApproved.Approve(LeaveApprovalStages.HeadOfDivision, LeaveApprovalStages.InstituteDirector).IsSuccess.Should().BeTrue();
            futureApproved.Approve(LeaveApprovalStages.InstituteDirector, null).IsSuccess.Should().BeTrue();
            db.LeaveRequests.AddRange(approved, futureApproved);
            await db.SaveChangesAsync();
            approvedId = approved.Id;
            futureApprovedId = futureApproved.Id;
        }

        var futureGet = await employee.GetAsync($"/api/v2/leave-requests/{futureApprovedId}");
        futureGet.EnsureSuccessStatusCode();
        (await futureGet.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data.AvailableActions
            .Should().BeEquivalentTo("cancel");

        var approvedGet = await employee.GetAsync($"/api/v2/leave-requests/{approvedId}");
        approvedGet.EnsureSuccessStatusCode();
        var approvedBody = (await approvedGet.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        approvedBody.AvailableActions.Should().BeEquivalentTo("cancel", "resume");

        var resumed = await SendJsonAsync(employee, HttpMethod.Post, $"/api/v2/leave-requests/{approvedId}/resume",
            new ResumeLeaveRequest(new DateTime(2026, 7, 3), "Ama Mensah"), Guid.NewGuid().ToString(),
            approvedGet.Headers.ETag!.Tag);
        resumed.EnsureSuccessStatusCode();
        var resumedBody = (await resumed.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        resumedBody.AvailableActions.Should().BeEmpty();
        resumedBody.Status.Should().Be(LeaveRequestStatuses.ResumptionPending);
    }

    [Fact]
    public async Task Leave_Submit_Fails_Closed_When_Balance_Is_Missing()
    {
        var seeded = await SeedEmployeeAsync();
        using var employee = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead));
        var created = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests",
            new CreateLeaveRequestRequest(null, LeaveTypes.Annual, new DateTime(2026, 11, 2),
                new DateTime(2026, 11, 3), "Rest"), Guid.NewGuid().ToString());
        created.EnsureSuccessStatusCode();
        var leave = (await created.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        var submitted = await SendJsonAsync(employee, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave!.Id}/submit", new { }, Guid.NewGuid().ToString(), created.Headers.ETag!.Tag);
        submitted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await submitted.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("insufficient_leave_balance");
    }

    [Fact]
    public async Task Canonical_Seed_Roles_Contain_Leave_Permissions()
    {
        var seed = ActivatorUtilities.CreateInstance<IdentitySeedHostedService>(_factory.Services);
        await seed.StartAsync(CancellationToken.None);
        await using var scope = _factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var hr = await roles.FindByNameAsync(SpmeRoles.HrAdmin);
        var employee = await roles.FindByNameAsync(SpmeRoles.Employee);
        (await roles.GetClaimsAsync(hr!)).Should().Contain(claim =>
            claim.Type == "permission" && claim.Value == SpmePermissions.LeaveApprove);
        (await roles.GetClaimsAsync(employee!)).Should().Contain(claim =>
            claim.Type == "permission" && claim.Value == SpmePermissions.LeaveRead);
    }

    [Fact]
    public async Task Oversized_Idempotent_Success_Is_Rolled_Back_And_Returns_Failure()
    {
        using var factory = new SpmeApiFactory { MaximumIdempotencyResponseBytes = 32 };
        var seeded = await SeedEmployeeAsync(factory);
        using var employee = Client(CreateToken(factory, Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead), factory);
        var response = await SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests",
            new CreateLeaveRequestRequest(null, LeaveTypes.Annual, new DateTime(2026, 12, 1),
                new DateTime(2026, 12, 2), "This response must exceed the deliberately tiny replay limit."),
            Guid.NewGuid().ToString());
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("idempotency_response_too_large");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.LeaveRequests.CountAsync(request => request.EmployeeId == seeded.EmployeeId)).Should().Be(0);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Leave_Approval_Requires_Matching_Section_And_Division_Responsibility()
    {
        var seeded = await SeedEmployeeAsync();
        await using (var balanceScope = _factory.Services.CreateAsyncScope())
        {
            var db = balanceScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.LeaveBalances.Add(LeaveBalance.Create(seeded.EmployeeId, LeaveTypes.Annual, 2026, 32));
            await db.SaveChangesAsync();
        }

        var actors = await SeedApprovalActorsAsync(seeded);
        using var owner = Client(CreateToken(Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead));
        var created = await SendJsonAsync(owner, HttpMethod.Post, "/api/v2/leave-requests",
            new CreateLeaveRequestRequest(null, LeaveTypes.Annual, new DateTime(2026, 12, 7),
                new DateTime(2026, 12, 8), "Approval scope"), Guid.NewGuid().ToString());
        created.EnsureSuccessStatusCode();
        var leave = (await created.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        var submitted = await SendJsonAsync(owner, HttpMethod.Post, $"/api/v2/leave-requests/{leave!.Id}/submit",
            new { }, Guid.NewGuid().ToString(), created.Headers.ETag!.Tag);
        submitted.EnsureSuccessStatusCode();
        await AssertPendingApprovalEmailAsync(leave.Id, LeaveApprovalStages.SectionHead, actors.CorrectSectionEmail);
        await AssertNoPendingApprovalEmailAsync(leave.Id, actors.WrongSectionEmail);

        using var wrongSection = Client(CreateToken(actors.WrongSectionUserId, SpmeRoles.HeadOfSection,
            seeded.InstituteId, actors.WrongSectionEmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        var wrongView = (await (await wrongSection.GetAsync($"/api/v2/leave-requests/{leave.Id}"))
            .Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data;
        wrongView.AvailableActions.Should().BeEmpty();
        (await SendJsonAsync(wrongSection, HttpMethod.Post, $"/api/v2/leave-requests/{leave.Id}/approve",
            new LeaveDecisionRequest("Wrong section", "Wrong"), Guid.NewGuid().ToString(), submitted.Headers.ETag!.Tag))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var correctSection = Client(CreateToken(actors.CorrectSectionUserId, SpmeRoles.HeadOfSection,
            seeded.InstituteId, actors.CorrectSectionEmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        (await (await correctSection.GetAsync($"/api/v2/leave-requests/{leave.Id}"))
            .Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data.AvailableActions
            .Should().BeEquivalentTo("approve", "reject");
        var sectionApproved = await SendJsonAsync(correctSection, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/approve", new LeaveDecisionRequest("Approved", "Section Head"),
            Guid.NewGuid().ToString(), submitted.Headers.ETag!.Tag);
        sectionApproved.EnsureSuccessStatusCode();
        await AssertPendingApprovalEmailAsync(leave.Id, LeaveApprovalStages.HeadOfDivision, actors.CorrectDivisionEmail);
        await AssertNoPendingApprovalEmailAsync(leave.Id, actors.WrongDivisionEmail);

        using var wrongDivision = Client(CreateToken(actors.WrongDivisionUserId, SpmeRoles.HeadOfDivision,
            seeded.InstituteId, actors.WrongDivisionEmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        (await SendJsonAsync(wrongDivision, HttpMethod.Post, $"/api/v2/leave-requests/{leave.Id}/approve",
            new LeaveDecisionRequest("Wrong division", "Wrong"), Guid.NewGuid().ToString(), sectionApproved.Headers.ETag!.Tag))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var correctDivision = Client(CreateToken(actors.CorrectDivisionUserId, SpmeRoles.HeadOfDivision,
            seeded.InstituteId, actors.CorrectDivisionEmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        var divisionApproved = await SendJsonAsync(correctDivision, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/approve", new LeaveDecisionRequest("Approved", "Division Head"),
            Guid.NewGuid().ToString(), sectionApproved.Headers.ETag!.Tag);
        divisionApproved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await divisionApproved.Content.ReadFromJsonAsync<DataResponse<LeaveRequestDto>>())!.Data.CurrentApprovalStage
            .Should().Be(LeaveApprovalStages.InstituteDirector);
        await AssertPendingApprovalEmailAsync(leave.Id, LeaveApprovalStages.InstituteDirector, actors.DirectorEmail);

        using var director = Client(CreateToken(actors.DirectorUserId, SpmeRoles.InstituteDirector,
            seeded.InstituteId, actors.DirectorEmployeeId, SpmePermissions.LeaveRead, SpmePermissions.LeaveApprove));
        var fullyApproved = await SendJsonAsync(director, HttpMethod.Post,
            $"/api/v2/leave-requests/{leave.Id}/approve",
            new LeaveDecisionRequest("Approved", "Institute Director"),
            Guid.NewGuid().ToString(),
            divisionApproved.Headers.ETag!.Tag);
        fullyApproved.EnsureSuccessStatusCode();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var approvalEvents = await verifyDb.CommunicationOutboxMessages.AsNoTracking()
            .Where(message =>
                message.Channel == "event" &&
                message.Category == "leave-approved" &&
                message.IdempotencyKey == $"leave-approved:{leave.Id:N}")
            .ToListAsync();
        approvalEvents.Should().ContainSingle();
        using var approvalPayload = JsonDocument.Parse(approvalEvents[0].Body);
        approvalPayload.RootElement.GetProperty("eventType").GetString().Should().Be("leave.approved.v1");
        approvalPayload.RootElement.GetProperty("leaveRequestId").GetGuid().Should().Be(leave.Id);
        approvalPayload.RootElement.GetProperty("instituteId").GetGuid().Should().Be(seeded.InstituteId);
        approvalPayload.RootElement.GetProperty("employeeId").GetGuid().Should().Be(seeded.EmployeeId);
        approvalPayload.RootElement.GetProperty("decidedByUserId").GetGuid().Should().Be(actors.DirectorUserId);
        var approvalEmail = await verifyDb.CommunicationOutboxMessages.AsNoTracking()
            .SingleAsync(message => message.Channel == "email" &&
                message.Category == "leave-approved" &&
                message.IdempotencyKey.StartsWith($"leave-approved:{leave.Id:N}:"));
        approvalEmail.IsHtml.Should().BeTrue();
        approvalEmail.Body.Should().Contain("annual").And.Contain("working days")
            .And.Contain($"/leave/{leave.Id:D}");
        approvalEmail.TextBody.Should().Contain("View leave request");
        approvalEmail.IdempotencyKey.Should().HaveLength(112).And.NotContain(approvalEmail.Recipient);
    }

    [Fact]
    public async Task Concurrent_Identical_Leave_Create_Commits_One_Mutation_And_Replays_One_Result()
    {
        using var factory = new SpmeApiFactory();
        var seeded = await SeedEmployeeAsync(factory);
        using var employee = Client(CreateToken(factory, Guid.NewGuid(), SpmeRoles.Employee, seeded.InstituteId,
            seeded.EmployeeId, SpmePermissions.LeaveRead), factory);
        var key = Guid.NewGuid().ToString();
        var request = new CreateLeaveRequestRequest(null, LeaveTypes.Annual,
            new DateTime(2026, 12, 14), new DateTime(2026, 12, 15), "Concurrent create");
        var responses = await Task.WhenAll(
            SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests", request, key),
            SendJsonAsync(employee, HttpMethod.Post, "/api/v2/leave-requests", request, key));
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        responses.Count(response => response.Headers.TryGetValues("Idempotent-Replayed", out _)).Should().Be(1);
        (await responses[0].Content.ReadAsStringAsync()).Should().Be(await responses[1].Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.LeaveRequests.CountAsync(item => item.EmployeeId == seeded.EmployeeId)).Should().Be(1);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(1);
    }

    private async Task<(Guid InstituteId, Guid EmployeeId, Guid OtherEmployeeId, Guid DivisionId, Guid SectionId)> SeedEmployeeAsync(
        SpmeApiFactory? factory = null)
    {
        await using var scope = (factory ?? _factory).Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"P{suffix}"[..12], $"Portal {suffix}", "institute");
        var employee = new Employee(institute.Id, $"STAFF-{suffix[..8]}", "Owner", "female");
        employee.UpdateImportedProfile(null, "Employee", new DateTime(1990, 1, 1), "Ghanaian", null,
            "single", $"owner.{suffix}@test.local", "0240000000", true);
        var other = new Employee(institute.Id, $"OTHER-{suffix[..8]}", "Other", "male");
        var division = new Division(institute.Id, "Portal Division");
        var section = new Section(division.Id, "Portal Section");
        db.Institutes.Add(institute);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        db.Employees.AddRange(employee, other);
        db.EmploymentRecords.AddRange(
            new EmploymentRecord(employee.Id, institute.Id, division.Id, section.Id, null, "Officer", null,
                "senior-staff", "active", null, null, null, null, null, new DateTime(2020, 1, 1), true),
            new EmploymentRecord(other.Id, institute.Id, division.Id, section.Id, null, "Officer", null,
                "senior-staff", "active", null, null, null, null, null, new DateTime(2020, 1, 1), true));
        await db.SaveChangesAsync();
        return (institute.Id, employee.Id, other.Id, division.Id, section.Id);
    }

    private async Task<Guid> GetEmployeeIdAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId)).EmployeeId!.Value;
    }

    private async Task<ApprovalActors> SeedApprovalActorsAsync(
        (Guid InstituteId, Guid EmployeeId, Guid OtherEmployeeId, Guid DivisionId, Guid SectionId) target)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var otherSection = new Section(target.DivisionId, "Other Section");
        var otherDivision = new Division(target.InstituteId, "Other Division");
        var otherDivisionSection = new Section(otherDivision.Id, "Other Division Section");
        db.Sections.Add(otherSection);
        db.Divisions.Add(otherDivision);
        db.Sections.Add(otherDivisionSection);

        (User User, Employee Employee, string Email) AddActor(string label, Guid divisionId, Guid sectionId, string roleName)
        {
            var employee = new Employee(target.InstituteId, $"{label}-{Guid.NewGuid():N}"[..13], label, "unspecified");
            var email = $"{label}.{Guid.NewGuid():N}@csir.test";
            var user = new User($"{label}.{Guid.NewGuid():N}", "StaffUser")
            {
                Email = email,
                EmailConfirmed = true
            };
            user.LinkEmployee(employee.Id, target.InstituteId, "StaffUser");
            user.UpdateDisplayName(label);
            db.Employees.Add(employee);
            db.Users.Add(user);
            db.EmploymentRecords.Add(new EmploymentRecord(employee.Id, target.InstituteId, divisionId, sectionId,
                null, label, label, "senior-staff", "active", null, null, null, null, null,
                new DateTime(2020, 1, 1), true));
            var role = db.Roles.Local.FirstOrDefault(candidate => candidate.Name == roleName) ??
                db.Roles.FirstOrDefault(candidate => candidate.Name == roleName);
            if (role is null)
            {
                role = new Role(roleName.ToLowerInvariant(), roleName, roleName, true);
                db.Roles.Add(role);
            }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
            return (user, employee, email);
        }

        var wrongSection = AddActor("wrongsection", target.DivisionId, otherSection.Id, SpmeRoles.HeadOfSection);
        var correctSection = AddActor("rightsection", target.DivisionId, target.SectionId, SpmeRoles.HeadOfSection);
        var wrongDivision = AddActor("wrongdivision", otherDivision.Id, otherDivisionSection.Id, SpmeRoles.HeadOfDivision);
        var correctDivision = AddActor("rightdivision", target.DivisionId, target.SectionId, SpmeRoles.HeadOfDivision);
        var director = AddActor("director", target.DivisionId, target.SectionId, SpmeRoles.InstituteDirector);
        await db.SaveChangesAsync();
        return new ApprovalActors(wrongSection.User.Id, wrongSection.Employee.Id, wrongSection.Email,
            correctSection.User.Id, correctSection.Employee.Id, correctSection.Email,
            wrongDivision.User.Id, wrongDivision.Employee.Id, wrongDivision.Email,
            correctDivision.User.Id, correctDivision.Employee.Id, correctDivision.Email,
            director.User.Id, director.Employee.Id, director.Email);
    }

    private async Task AssertPendingApprovalEmailAsync(Guid leaveRequestId, string stage, string recipient)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var email = await db.CommunicationOutboxMessages.AsNoTracking()
            .SingleAsync(message =>
                message.Channel == "email" &&
                message.Category == "leave-pending-approval" &&
                message.Recipient == recipient &&
                message.IdempotencyKey == $"leave-pending-approval:{leaveRequestId:N}:{stage}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(recipient.ToUpperInvariant())))}");
        email.IsHtml.Should().BeTrue();
        email.Body.Should().Contain("awaiting approval").And.Contain($"/leave/{leaveRequestId:D}");
        email.TextBody.Should().Contain("Review leave request");
    }

    private async Task AssertNoPendingApprovalEmailAsync(Guid leaveRequestId, string recipient)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.CommunicationOutboxMessages.AsNoTracking().AnyAsync(message =>
            message.Channel == "email" &&
            message.Category == "leave-pending-approval" &&
            message.Recipient == recipient &&
            message.IdempotencyKey.StartsWith($"leave-pending-approval:{leaveRequestId:N}:"))).Should().BeFalse();
    }

    private sealed record ApprovalActors(
        Guid WrongSectionUserId, Guid WrongSectionEmployeeId, string WrongSectionEmail,
        Guid CorrectSectionUserId, Guid CorrectSectionEmployeeId, string CorrectSectionEmail,
        Guid WrongDivisionUserId, Guid WrongDivisionEmployeeId, string WrongDivisionEmail,
        Guid CorrectDivisionUserId, Guid CorrectDivisionEmployeeId, string CorrectDivisionEmail,
        Guid DirectorUserId, Guid DirectorEmployeeId, string DirectorEmail);

    private HttpClient Client(string token, SpmeApiFactory? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(Guid userId, string role, Guid instituteId, Guid? employeeId, params string[] permissions)
        => CreateToken(_factory, userId, role, instituteId, employeeId, permissions);

    private static string CreateToken(
        SpmeApiFactory factory, Guid userId, string role, Guid instituteId, Guid? employeeId,
        params string[] permissions)
    {
        var jwt = factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()), new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"test-{userId:N}"), new(ClaimTypes.Role, role),
            new("institute_id", instituteId.ToString())
        };
        if (employeeId.HasValue) claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(jwt.GetValue<string>("Issuer"), jwt.GetValue<string>("Audience"), claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client, HttpMethod method, string path, object body, string idempotencyKey, string? etag = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendRawJsonAsync(
        HttpClient client, string path, string json, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }
}
