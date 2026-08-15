using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

[CollectionDefinition("Promotion submission serial", DisableParallelization = true)]
public sealed class PromotionSubmissionSerialCollection;

[Collection("Promotion submission serial")]
public sealed class PromotionSubmissionEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    public PromotionSubmissionEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Eligible_Employee_Creates_One_Idempotent_Draft_With_Immutable_Requirements()
    {
        var seed = await SeedEligibleAssessmentAsync();
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var key = Guid.NewGuid().ToString("N");
        using var firstRequest = Post("/api/v2/promotion-submissions",
            new CreatePromotionSubmissionRequest(seed.AssessmentId), key);
        var first = await owner.SendAsync(firstRequest);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await first.Content.ReadFromJsonAsync<PromotionSubmissionResponse>();
        created!.Status.Should().Be(PromotionConstants.SubmissionDraft);
        created.TotalRequirements.Should().Be(2);
        created.AllowedActions.Should().Contain(["edit", "submit", "withdraw"]);

        using var replayRequest = Post("/api/v2/promotion-submissions",
            new CreatePromotionSubmissionRequest(seed.AssessmentId), key);
        var replay = await owner.SendAsync(replayRequest);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!.Id.Should().Be(created.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        db.PromotionSubmissions.Count(x => x.PromotionAssessmentId == seed.AssessmentId).Should().Be(1);
        db.PromotionSubmissionRequirementSnapshots.Count(x => x.PromotionSubmissionId == created.Id).Should().Be(2);
    }

    [Fact]
    public async Task Self_Isolation_Declaration_Etag_And_Upload_Metadata_Limits_Are_Enforced()
    {
        var seed = await SeedEligibleAssessmentAsync();
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        using var createdRequest = Post("/api/v2/promotion-submissions",
            new CreatePromotionSubmissionRequest(seed.AssessmentId), Guid.NewGuid().ToString("N"));
        var createdResponse = await owner.SendAsync(createdRequest);
        createdResponse.EnsureSuccessStatusCode();
        var submission = (await createdResponse.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;

        using var other = Client(SpmeRoles.Employee, Guid.NewGuid(), seed.InstituteId);
        (await other.GetAsync($"/api/v2/promotion-submissions/{submission.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var missingEtag = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v2/promotion-submissions/{submission.Id}/declarations/applicant")
        { Content = JsonContent.Create(new { accepted = true }) };
        (await owner.SendAsync(missingEtag)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        using var accepted = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v2/promotion-submissions/{submission.Id}/declarations/applicant")
        { Content = JsonContent.Create(new { accepted = true }) };
        accepted.Headers.TryAddWithoutValidation("If-Match", submission.Etag);
        (await owner.SendAsync(accepted)).StatusCode.Should().Be(HttpStatusCode.OK);

        var oversize = new CreatePromotionDocumentUploadRequest("evidence", "evidence.pdf", "application/pdf",
            209_715_201, new string('a', 64));
        using var oversizeRequest = Post(
            $"/api/v2/promotion-submissions/{submission.Id}/document-upload-sessions", oversize,
            Guid.NewGuid().ToString("N"));
        var response = await owner.SendAsync(oversizeRequest);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Document_Requirement_Completes_Only_After_Clean_Available_Scan()
    {
        var seed = await SeedEligibleAssessmentAsync();
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        using var createdRequest = Post("/api/v2/promotion-submissions",
            new CreatePromotionSubmissionRequest(seed.AssessmentId), Guid.NewGuid().ToString("N"));
        var createdResponse = await owner.SendAsync(createdRequest);
        createdResponse.EnsureSuccessStatusCode();
        var submission = (await createdResponse.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var snapshot = await db.PromotionSubmissionRequirementSnapshots.SingleAsync(item =>
                item.PromotionSubmissionId == submission.Id &&
                item.RequirementType == PromotionConstants.RequirementDocument);
            var file = new FileRecord($"promotions/{Guid.NewGuid():N}.pdf", "evidence.pdf", "application/pdf",
                2048, new string('a', 64), "promotion-document", seed.InstituteId, "confidential");
            file.MarkScanStatus("pending");
            var document = new PromotionSubmissionDocument(submission.Id, snapshot.Id, file.Id, Guid.NewGuid());
            db.FileRecords.Add(file);
            db.PromotionSubmissionDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        var pending = await owner.GetFromJsonAsync<CollectionResponse<PromotionRequirementResponse>>(
            $"/api/v2/promotion-submissions/{submission.Id}/requirements");
        pending!.Items.Should().ContainSingle(item =>
            item.Code == "evidence" && item.CompletionState == "not-started");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var document = await db.PromotionSubmissionDocuments.SingleAsync(item =>
                item.PromotionSubmissionId == submission.Id);
            var file = await db.FileRecords.SingleAsync(item => item.Id == document.FileId);
            document.MarkAvailable(DateTimeOffset.UtcNow);
            file.MarkScanStatus("clean");
            await db.SaveChangesAsync();
        }

        var complete = await owner.GetFromJsonAsync<CollectionResponse<PromotionRequirementResponse>>(
            $"/api/v2/promotion-submissions/{submission.Id}/requirements");
        complete!.Items.Should().ContainSingle(item =>
            item.Code == "evidence" && item.CompletionState == "complete");
    }

    [Fact]
    public async Task Hr_Review_Transitions_Require_Current_Etags_And_Institute_Scope()
    {
        var seed = await SeedSubmittedAsync();
        using var otherInstitute = Client(SpmeRoles.HrAdmin, null, Guid.NewGuid());
        using var hidden = Post($"/api/v2/promotion-submissions/{seed.SubmissionId}/begin-review",
            new PromotionDecisionRequest(null, null));
        hidden.Headers.TryAddWithoutValidation("If-Match", seed.Etag);
        (await otherInstitute.SendAsync(hidden)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var hr = Client(SpmeRoles.HrAdmin, null, seed.InstituteId);
        using var begin = Post($"/api/v2/promotion-submissions/{seed.SubmissionId}/begin-review",
            new PromotionDecisionRequest(null, "assigned"));
        begin.Headers.TryAddWithoutValidation("If-Match", seed.Etag);
        var reviewing = await hr.SendAsync(begin);
        reviewing.StatusCode.Should().Be(HttpStatusCode.OK, await reviewing.Content.ReadAsStringAsync());
        var underReview = (await reviewing.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;
        underReview.Status.Should().Be(PromotionConstants.SubmissionUnderReview);

        using var staleApprove = Post($"/api/v2/promotion-submissions/{seed.SubmissionId}/approve",
            new PromotionDecisionRequest("Approved", null));
        staleApprove.Headers.TryAddWithoutValidation("If-Match", seed.Etag);
        (await hr.SendAsync(staleApprove)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        using var acknowledge = Post($"/api/v2/promotion-submissions/{seed.SubmissionId}/acknowledge",
            new PromotionDecisionRequest(null, "checked"));
        acknowledge.Headers.TryAddWithoutValidation("If-Match", underReview.Etag);
        var acknowledged = await hr.SendAsync(acknowledge);
        var acknowledgedBody = (await acknowledged.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;
        acknowledgedBody.Status.Should().Be(PromotionConstants.SubmissionAcknowledged);

        using var approve = Post($"/api/v2/promotion-submissions/{seed.SubmissionId}/approve",
            new PromotionDecisionRequest("Your promotion submission was approved.", "council minute"));
        approve.Headers.TryAddWithoutValidation("If-Match", acknowledgedBody.Etag);
        var approved = await hr.SendAsync(approve);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approved.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!.Status
            .Should().Be(PromotionConstants.SubmissionApproved);
    }

    [Fact]
    public async Task Promotion_Status_Me_Is_Self_Scoped_And_Adds_Optional_Fields()
    {
        var seed = await SeedEligibleAssessmentAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var assessment = await db.PromotionAssessments.SingleAsync(item => item.Id == seed.AssessmentId);
            db.PromotionStatusSnapshots.Add(PromotionStatusSnapshot.FromAssessment(assessment, PromotionConstants.SeniorStaff));
            await db.SaveChangesAsync();
        }

        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var mine = await owner.GetFromJsonAsync<PromotionStatusResponse>("/api/v2/promotion-status/me");
        mine!.StaffId.Should().NotBeNullOrWhiteSpace();
        mine.EligibilityState.Should().Be(PromotionConstants.EligibilityEligibleForReview);
        mine.AvailableActions.Should().Contain("start-promotion-submission");
        mine.Criteria.Should().NotBeNull();
        mine.NextAction.Should().Contain("promotion submission");
        mine.CurrentGrade!.Code.Should().StartWith("SRC-");

        using var other = Client(SpmeRoles.Employee, Guid.NewGuid(), seed.InstituteId);
        (await other.GetAsync("/api/v2/promotion-status/me")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Promotion_Status_Me_Returns_Live_Status_Without_Snapshot()
    {
        var seed = await SeedEligibleAssessmentAsync();
        using var owner = Client(SpmeRoles.Employee, seed.EmployeeId, seed.InstituteId);
        var response = await owner.GetAsync("/api/v2/promotion-status/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var mine = await response.Content.ReadFromJsonAsync<PromotionStatusResponse>();
        mine!.StaffId.Should().NotBeNullOrWhiteSpace();
        mine.AssessmentState.Should().Be(PromotionConstants.AssessmentNotAssessed);
        mine.LatestAssessmentId.Should().BeNull();
        mine.AvailableActions.Should().NotContain("start-promotion-submission");
        mine.NextAction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Staff_Can_Withdraw_And_Hr_Can_Return_Then_Reject()
    {
        var submitted = await SeedSubmittedAsync();
        using var owner = Client(SpmeRoles.Employee, submitted.EmployeeId, submitted.InstituteId);
        using var withdraw = Post($"/api/v2/promotion-submissions/{submitted.SubmissionId}/withdraw", new { });
        withdraw.Headers.TryAddWithoutValidation("If-Match", submitted.Etag);
        var withdrawn = await owner.SendAsync(withdraw);
        withdrawn.StatusCode.Should().Be(HttpStatusCode.OK, await withdrawn.Content.ReadAsStringAsync());
        (await withdrawn.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!.Status
            .Should().Be(PromotionConstants.SubmissionWithdrawn);

        var returnedSeed = await SeedSubmittedAsync();
        using var hr = Client(SpmeRoles.HrAdmin, null, returnedSeed.InstituteId);
        using var begin = Post($"/api/v2/promotion-submissions/{returnedSeed.SubmissionId}/begin-review",
            new PromotionDecisionRequest(null, "assigned"));
        begin.Headers.TryAddWithoutValidation("If-Match", returnedSeed.Etag);
        var reviewing = await hr.SendAsync(begin);
        reviewing.EnsureSuccessStatusCode();
        var underReview = (await reviewing.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;
        using var returned = Post($"/api/v2/promotion-submissions/{returnedSeed.SubmissionId}/return",
            new PromotionDecisionRequest("Please attach the certificate.", "internal"));
        returned.Headers.TryAddWithoutValidation("If-Match", underReview.Etag);
        var returnResponse = await hr.SendAsync(returned);
        returnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await returnResponse.Content.ReadAsStringAsync());
        var returnedBody = (await returnResponse.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;
        returnedBody.Status.Should().Be(PromotionConstants.SubmissionReturned);
        returnedBody.EmployeeVisibleReviewNote.Should().Be("Please attach the certificate.");

        using var ownerReturned = Client(SpmeRoles.Employee, returnedSeed.EmployeeId, returnedSeed.InstituteId);
        using var resubmit = Post($"/api/v2/promotion-submissions/{returnedSeed.SubmissionId}/submit", new { });
        resubmit.Headers.TryAddWithoutValidation("If-Match", returnedBody.Etag);
        var resubmitted = await ownerReturned.SendAsync(resubmit);
        resubmitted.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var rejectSeed = await SeedSubmittedAsync();
        using var rejectHr = Client(SpmeRoles.HrAdmin, null, rejectSeed.InstituteId);
        using var rejectBegin = Post($"/api/v2/promotion-submissions/{rejectSeed.SubmissionId}/begin-review",
            new PromotionDecisionRequest(null, "assigned"));
        rejectBegin.Headers.TryAddWithoutValidation("If-Match", rejectSeed.Etag);
        var rejectReview = await rejectHr.SendAsync(rejectBegin);
        var rejectReviewBody = (await rejectReview.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!;
        using var reject = Post($"/api/v2/promotion-submissions/{rejectSeed.SubmissionId}/reject",
            new PromotionDecisionRequest("Not approved this cycle.", "panel"));
        reject.Headers.TryAddWithoutValidation("If-Match", rejectReviewBody.Etag);
        var rejected = await rejectHr.SendAsync(reject);
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync());
        (await rejected.Content.ReadFromJsonAsync<PromotionSubmissionResponse>())!.Status
            .Should().Be(PromotionConstants.SubmissionRejected);
    }

    private async Task<Seed> SeedEligibleAssessmentAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"PS-{suffix[..8]}", $"Promotion {suffix[..8]}", "Institute");
        var employee = new Employee(institute.Id, $"PS-{suffix[..12]}", "Employee", "female");
        var source = Private<Grade>(); Set(source, nameof(Grade.Code), $"SRC-{suffix[..6]}"); Set(source, nameof(Grade.Name), "Source grade");
        var target = Private<Grade>(); Set(target, nameof(Grade.Code), $"TGT-{suffix[..6]}"); Set(target, nameof(Grade.Name), "Target grade");
        var nextCycleYear = (short)((await db.PromotionCycles.Select(x => (short?)x.CycleYear).MaxAsync() ?? 2026) + 1);
        var cycle = new PromotionCycle(nextCycleYear); cycle.Open(DateTimeOffset.UtcNow);
        var pathId = Guid.NewGuid();
        var assessment = PromotionAssessment.Create(employee.Id, institute.Id, cycle.Id, pathId,
            Guid.NewGuid(), source.Id, target.Id, DateTime.UtcNow.Date, cycle.EffectivePromotionDate,
            new DateTime(2020, 1, 1), new DateTime(2024, 1, 1), 7,
            PromotionConstants.EligibilityEligibleForReview, "[]", "[]", "{}", null);
        var declaration = new PromotionSubmissionRequirementTemplate(cycle.Id, pathId, "applicant",
            PromotionConstants.RequirementDeclaration, "Applicant declaration", true, 1,
            declarationText: "I confirm that the submitted information is accurate.");
        var document = new PromotionSubmissionRequirementTemplate(cycle.Id, pathId, "evidence",
            PromotionConstants.RequirementDocument, "Qualification evidence", true, 2,
            acceptedContentTypesJson: "[\"application/pdf\"]", maximumFileBytes: 209_715_200, maximumDocumentCount: 1);
        db.AddRange(institute, employee, source, target, cycle, assessment, declaration, document);
        await db.SaveChangesAsync();
        return new Seed(institute.Id, employee.Id, assessment.Id);
    }

    private async Task<SubmittedSeed> SeedSubmittedAsync()
    {
        var seed = await SeedEligibleAssessmentAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var assessment = await db.PromotionAssessments.SingleAsync(x => x.Id == seed.AssessmentId);
        var submission = PromotionSubmission.Create(seed.EmployeeId, Guid.NewGuid(), seed.InstituteId, assessment,
            DateTimeOffset.UtcNow);
        submission.Submit(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        db.PromotionSubmissions.Add(submission);
        var templates = await db.PromotionSubmissionRequirementTemplates
            .Where(item => item.PromotionCycleId == assessment.PromotionCycleId &&
                item.PromotionPathId == assessment.PromotionPathId)
            .ToListAsync();
        foreach (var template in templates)
            db.PromotionSubmissionRequirementSnapshots.Add(new PromotionSubmissionRequirementSnapshot(submission.Id, template));
        await db.SaveChangesAsync();
        return new SubmittedSeed(seed.InstituteId, seed.EmployeeId, submission.Id,
            Csir.Spme.Application.Common.ConcurrencyToken.Format(submission.RowVersion));
    }

    private HttpClient Client(string role, Guid? employeeId, Guid? instituteId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role, employeeId, instituteId));
        return client;
    }

    private string Token(string role, Guid? employeeId, Guid? instituteId)
    {
        var jwt = _factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var userId = Guid.NewGuid();
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role), new("identity_type", role) };
        if (employeeId.HasValue) { claims.Add(new("employee_id", employeeId.ToString()!)); claims.Add(new("self", $"Self:{employeeId}")); }
        if (instituteId.HasValue) claims.Add(new("institute_id", instituteId.ToString()!));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims,
            notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: credentials));
    }

    private static HttpRequestMessage Post<T>(string route, T body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }
    private static T Private<T>() where T : class => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    private static void Set<T>(T target, string name, object value) where T : class =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed record Seed(Guid InstituteId, Guid EmployeeId, Guid AssessmentId);
    private sealed record SubmittedSeed(Guid InstituteId, Guid EmployeeId, Guid SubmissionId, string Etag);
}
