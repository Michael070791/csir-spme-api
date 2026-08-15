using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class PlanWorkflowEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public PlanWorkflowEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Outputs_And_Indicators_Enforce_Crud_Permissions_Scope_Conflicts_Etags_And_Audit()
    {
        var graphA = await SeedPlanGraphAsync("PLAN-A");
        var graphB = await SeedPlanGraphAsync("PLAN-B");
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/v2/outputs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var forbidden = Client(graphA.InstituteId);
        (await forbidden.GetAsync("/api/v2/outputs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var client = Client(graphA.InstituteId,
            SpmePermissions.OutputsRead, SpmePermissions.OutputsWrite,
            SpmePermissions.IndicatorsRead, SpmePermissions.IndicatorsWrite);
        var code = $"OUT-{Guid.NewGuid():N}"[..16];
        var create = await client.PostAsJsonAsync("/api/v2/outputs", new CreateRootOutputRequest(
            graphA.ThrustId, code, "Increase research delivery.", null, new DateTime(2026, 12, 31), 1));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location.Should().NotBeNull();
        create.Headers.ETag.Should().NotBeNull();
        var output = (await create.Content.ReadFromJsonAsync<DataResponse<OutputResponse>>())!.Data;

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v2/outputs", new CreateRootOutputRequest(
            graphA.ThrustId, code, "Duplicate.", null, null, 2)), HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/v2/outputs", new CreateRootOutputRequest(
            graphB.ThrustId, "HIDDEN", "Hidden parent.", null, null, 1)), HttpStatusCode.NotFound, "not_found");

        var missingEtag = await client.PatchAsJsonAsync($"/api/v2/outputs/{output.Id}", new UpdateOutputRequest(
            "Changed", null, null, 1, PlanItemStatuses.Draft));
        await AssertProblemAsync(missingEtag, HttpStatusCode.PreconditionFailed, "concurrency_conflict");

        var update = Patch($"/api/v2/outputs/{output.Id}", create.Headers.ETag!, new UpdateOutputRequest(
            "Updated delivery output.", null, new DateTime(2027, 1, 31), 2, PlanItemStatuses.Active));
        var updatedResponse = await client.SendAsync(update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedResponse.Headers.ETag.Should().NotBeNull();
        var stale = await client.SendAsync(Patch($"/api/v2/outputs/{output.Id}", create.Headers.ETag!,
            new UpdateOutputRequest("Stale", null, null, 3, PlanItemStatuses.Active)));
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "concurrency_conflict");

        var indicatorCode = $"IND-{Guid.NewGuid():N}"[..16];
        var createIndicator = await client.PostAsJsonAsync($"/api/v2/outputs/{output.Id}/indicators",
            new CreateIndicatorRequest(indicatorCode, "Delivery rate", "percent", 10m, 90m,
                "Quarterly verification", new DateTime(2027, 3, 31)));
        createIndicator.StatusCode.Should().Be(HttpStatusCode.Created);
        createIndicator.Headers.ETag.Should().NotBeNull();
        var indicator = (await createIndicator.Content.ReadFromJsonAsync<DataResponse<IndicatorResponse>>())!.Data;

        await AssertProblemAsync(await client.PostAsJsonAsync($"/api/v2/outputs/{output.Id}/indicators",
            new CreateIndicatorRequest(indicatorCode, "Duplicate", "percent", null, null, null, null)),
            HttpStatusCode.Conflict, "conflict");
        var indicatorUpdate = await client.SendAsync(Patch($"/api/v2/indicators/{indicator.Id}",
            createIndicator.Headers.ETag!, new UpdateIndicatorRequest("Verified delivery rate", "percent",
                10m, 95m, "Verified quarterly", new DateTime(2027, 3, 31), PlanItemStatuses.Active)));
        indicatorUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        indicatorUpdate.Headers.ETag.Should().NotBeNull();

        var otherInstitute = Client(graphB.InstituteId, SpmePermissions.OutputsRead, SpmePermissions.IndicatorsRead);
        (await otherInstitute.GetAsync($"/api/v2/outputs/{output.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherInstitute.GetAsync($"/api/v2/indicators/{indicator.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AssertAuditActionsAsync(output.Id, "output.created", "output.updated");
        await AssertAuditActionsAsync(indicator.Id, "indicator.created", "indicator.updated");
    }

    [Fact]
    public async Task Indicator_Measurements_Enforce_Uniqueness_Scope_Period_Immutability_Etags_And_Delete()
    {
        var graphA = await SeedPlanGraphAsync("MEASURE-A");
        var graphB = await SeedPlanGraphAsync("MEASURE-B");
        var seeded = await SeedIndicatorAndPeriodsAsync(graphA, graphB.InstituteId);
        var client = Client(graphA.InstituteId, SpmePermissions.IndicatorsRead, SpmePermissions.IndicatorsWrite);

        var create = await client.PostAsJsonAsync($"/api/v2/indicators/{seeded.IndicatorId}/measurements",
            new CreateIndicatorDataRequest(seeded.DraftPeriodId, 72m, "Initial result", null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var measurement = (await create.Content.ReadFromJsonAsync<DataResponse<IndicatorDataResponse>>())!.Data;
        measurement.Variance.Should().Be(-8m);

        await AssertProblemAsync(await client.PostAsJsonAsync(
            $"/api/v2/indicators/{seeded.IndicatorId}/measurements",
            new CreateIndicatorDataRequest(seeded.DraftPeriodId, 73m, null, null)),
            HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(await client.PostAsJsonAsync(
            $"/api/v2/indicators/{seeded.IndicatorId}/measurements",
            new CreateIndicatorDataRequest(seeded.OtherInstitutePeriodId, 73m, null, null)),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        await AssertProblemAsync(await client.PostAsJsonAsync(
            $"/api/v2/indicators/{seeded.IndicatorId}/measurements",
            new CreateIndicatorDataRequest(seeded.FinalizedPeriodId, 73m, null, null)),
            HttpStatusCode.Conflict, "conflict");

        await AssertProblemAsync(await client.PatchAsJsonAsync($"/api/v2/indicator-measurements/{measurement.Id}",
            new UpdateIndicatorDataRequest(75m, "Missing ETag", null)),
            HttpStatusCode.PreconditionFailed, "concurrency_conflict");
        var update = await client.SendAsync(Patch($"/api/v2/indicator-measurements/{measurement.Id}",
            create.Headers.ETag!, new UpdateIndicatorDataRequest(75m, "Updated result", null)));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        update.Headers.ETag.Should().NotBeNull();
        var stale = await client.SendAsync(Patch($"/api/v2/indicator-measurements/{measurement.Id}",
            create.Headers.ETag!, new UpdateIndicatorDataRequest(76m, "Stale", null)));
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "concurrency_conflict");

        var closedGet = await client.GetAsync($"/api/v2/indicator-measurements/{seeded.ClosedMeasurementId}");
        closedGet.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertProblemAsync(await client.SendAsync(Patch(
            $"/api/v2/indicator-measurements/{seeded.ClosedMeasurementId}", closedGet.Headers.ETag!,
            new UpdateIndicatorDataRequest(99m, null, null))), HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(await client.DeleteAsync(
            $"/api/v2/indicator-measurements/{seeded.ClosedMeasurementId}"), HttpStatusCode.Conflict, "conflict");

        (await client.DeleteAsync($"/api/v2/indicator-measurements/{measurement.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v2/indicator-measurements/{measurement.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertAuditActionsAsync(measurement.Id,
            "indicator-measurement.created", "indicator-measurement.updated", "indicator-measurement.deleted");
    }

    [Fact]
    public async Task Projects_Enforce_Validation_Duplicate_Workflow_Permissions_Immutability_And_Audit()
    {
        var graphA = await SeedPlanGraphAsync("PROJECT-A");
        var graphB = await SeedPlanGraphAsync("PROJECT-B");
        var writer = Client(graphA.InstituteId, SpmePermissions.ProjectsRead, SpmePermissions.ProjectsWrite);
        var approver = Client(graphA.InstituteId, SpmePermissions.ProjectsRead,
            SpmePermissions.ProjectsWrite, SpmePermissions.ProjectsApprove);
        var code = $"PRJ-{Guid.NewGuid():N}"[..16];
        var request = ProjectRequest(graphA.InstituteId, code, graphA.ThrustId);

        var create = await writer.PostAsJsonAsync("/api/v2/projects", request);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var project = (await create.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data;
        await AssertProblemAsync(await writer.PostAsJsonAsync("/api/v2/projects", request),
            HttpStatusCode.Conflict, "conflict");
        await AssertProblemAsync(await writer.PostAsJsonAsync("/api/v2/projects", request with
        {
            Code = $"BAD-{Guid.NewGuid():N}"[..16],
            EndDate = request.StartDate.AddDays(-1)
        }), HttpStatusCode.UnprocessableEntity, "validation_failed");

        (await writer.PostAsync($"/api/v2/projects/{project.Id}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherInstitute = Client(graphB.InstituteId, SpmePermissions.ProjectsRead, SpmePermissions.ProjectsWrite);
        (await otherInstitute.GetAsync($"/api/v2/projects/{project.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AssertProblemAsync(await writer.PatchAsJsonAsync($"/api/v2/projects/{project.Id}",
            ProjectUpdate(ProjectStatuses.Draft)), HttpStatusCode.PreconditionFailed, "concurrency_conflict");
        var update = await writer.SendAsync(Patch($"/api/v2/projects/{project.Id}", create.Headers.ETag!,
            ProjectUpdate(ProjectStatuses.Draft)));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var stale = await writer.SendAsync(Patch($"/api/v2/projects/{project.Id}", create.Headers.ETag!,
            ProjectUpdate(ProjectStatuses.Draft)));
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "concurrency_conflict");

        var submit = await approver.PostAsync($"/api/v2/projects/{project.Id}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await submit.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data.Status.Should().Be(ProjectStatuses.Active);
        await AssertProblemAsync(await approver.PostAsync($"/api/v2/projects/{project.Id}/submit", null),
            HttpStatusCode.Conflict, "invalid_state_transition");
        await AssertProblemAsync(await writer.DeleteAsync($"/api/v2/projects/{project.Id}"),
            HttpStatusCode.Conflict, "conflict");

        var archive = await approver.PostAsync($"/api/v2/projects/{project.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        var archivedEtag = archive.Headers.ETag!;
        await AssertProblemAsync(await writer.SendAsync(Patch($"/api/v2/projects/{project.Id}", archivedEtag,
            ProjectUpdate(ProjectStatuses.Archived))), HttpStatusCode.Conflict, "invalid_state_transition");
        await AssertProblemAsync(await approver.PostAsync($"/api/v2/projects/{project.Id}/archive", null),
            HttpStatusCode.Conflict, "invalid_state_transition");

        await AssertAuditActionsAsync(project.Id,
            "project.created", "project.updated", "project.submitted", "project.archived");
    }

    [Fact]
    public async Task Reports_Enforce_Submit_Return_Approve_Edit_Delete_Scope_And_Audit_Rules()
    {
        var graphA = await SeedPlanGraphAsync("REPORT-A");
        var graphB = await SeedPlanGraphAsync("REPORT-B");
        var periodId = await SeedPeriodAsync(graphA.InstituteId, "REPORT-PERIOD");
        var writer = Client(graphA.InstituteId, SpmePermissions.ReportsRead, SpmePermissions.ReportsWrite);
        var submitter = Client(graphA.InstituteId, SpmePermissions.ReportsRead,
            SpmePermissions.ReportsWrite, SpmePermissions.ReportsSubmit);
        var approver = Client(graphA.InstituteId, SpmePermissions.ReportsRead,
            SpmePermissions.ReportsWrite, SpmePermissions.ReportsApprove);
        var request = new CreateReportRequest(graphA.InstituteId, periodId, ReportTypes.Strategic,
            "Strategic delivery report", "Delivery summary", null, "Results", "Conclusion");

        var create = await writer.PostAsJsonAsync("/api/v2/reports", request);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = (await create.Content.ReadFromJsonAsync<DataResponse<ReportResponse>>())!.Data;
        await AssertProblemAsync(await writer.PostAsJsonAsync("/api/v2/reports", request),
            HttpStatusCode.Conflict, "conflict");
        (await writer.PostAsync($"/api/v2/reports/{report.Id}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherInstitute = Client(graphB.InstituteId, SpmePermissions.ReportsRead, SpmePermissions.ReportsWrite);
        (await otherInstitute.GetAsync($"/api/v2/reports/{report.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var submitted = await submitter.PostAsync($"/api/v2/reports/{report.Id}/submit", null);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertProblemAsync(await writer.SendAsync(Patch($"/api/v2/reports/{report.Id}",
            submitted.Headers.ETag!, new UpdateReportRequest("Blocked", "Blocked", null, null, null))),
            HttpStatusCode.Conflict, "invalid_state_transition");
        await AssertProblemAsync(await writer.DeleteAsync($"/api/v2/reports/{report.Id}"),
            HttpStatusCode.Conflict, "conflict");

        var returned = await approver.PostAsJsonAsync($"/api/v2/reports/{report.Id}/return",
            new ReturnReportRequest("Attach the supporting evidence."));
        returned.StatusCode.Should().Be(HttpStatusCode.OK);
        var corrected = await writer.SendAsync(Patch($"/api/v2/reports/{report.Id}", returned.Headers.ETag!,
            new UpdateReportRequest("Corrected strategic delivery report", "Corrected summary", null,
                "Corrected results", "Corrected conclusion")));
        corrected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await submitter.PostAsync($"/api/v2/reports/{report.Id}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approver.PostAsync($"/api/v2/reports/{report.Id}/approve", null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await approved.Content.ReadFromJsonAsync<DataResponse<ReportResponse>>())!.Data.Status.Should().Be(ReportStatuses.Approved);
        await AssertProblemAsync(await writer.SendAsync(Patch($"/api/v2/reports/{report.Id}", approved.Headers.ETag!,
            new UpdateReportRequest("Too late", "Too late", null, null, null))),
            HttpStatusCode.Conflict, "invalid_state_transition");
        await AssertProblemAsync(await approver.PostAsJsonAsync($"/api/v2/reports/{report.Id}/return",
            new ReturnReportRequest("Too late")), HttpStatusCode.Conflict, "invalid_state_transition");
        await AssertProblemAsync(await writer.DeleteAsync($"/api/v2/reports/{report.Id}"),
            HttpStatusCode.Conflict, "conflict");

        await AssertAuditActionsAsync(report.Id,
            "report.created", "report.submitted", "report.returned", "report.updated", "report.approved");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var submissionEvents = db.CommunicationOutboxMessages
                .Where(message =>
                    message.Channel == "event" &&
                    message.Category == "report-submitted" &&
                    message.IdempotencyKey.StartsWith($"report-submitted:{report.Id:N}:"))
                .ToList();
            submissionEvents.Should().HaveCount(2);
            submissionEvents.Select(message => message.IdempotencyKey).Should().OnlyHaveUniqueItems();
            foreach (var submissionEvent in submissionEvents)
            {
                using var payload = JsonDocument.Parse(submissionEvent.Body);
                payload.RootElement.GetProperty("eventType").GetString().Should().Be("report.submitted.v1");
                payload.RootElement.GetProperty("reportId").GetGuid().Should().Be(report.Id);
                payload.RootElement.GetProperty("instituteId").GetGuid().Should().Be(graphA.InstituteId);
                payload.RootElement.GetProperty("submittedByUserId").GetGuid().Should().NotBe(Guid.Empty);
                payload.RootElement.TryGetProperty("title", out _).Should().BeFalse();
            }
        }
    }

    private async Task<PlanGraph> SeedPlanGraphAsync(string prefix)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var institute = new Institute($"{prefix}-{suffix}"[..Math.Min(16, prefix.Length + 9)],
            $"{prefix} Institute {suffix}", "Institute");
        var plan = StrategicPlan.Create(institute.Id, $"SP-{suffix}", "Strategic Plan", "Definition",
            "Objective", 2026, 2030);
        var thrust = Thrust.Create(plan.Id, institute.Id, $"TH-{suffix}", "Research delivery",
            "Research delivery thrust", "Increase delivery", 1);
        db.Institutes.Add(institute);
        db.StrategicPlans.Add(plan);
        db.Thrusts.Add(thrust);
        await db.SaveChangesAsync();
        return new PlanGraph(institute.Id, plan.Id, thrust.Id);
    }

    private async Task<MeasurementSeed> SeedIndicatorAndPeriodsAsync(PlanGraph graph, Guid otherInstituteId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var output = Output.Create(graph.ThrustId, $"OUT-{suffix}", "Measurement output", null, null, 1);
        var indicator = Indicator.Create(output.Id, $"IND-{suffix}", "Measurement indicator", "percent",
            20m, 80m, null, null);
        var draft = Period(graph.InstituteId, $"D-{suffix}");
        var other = Period(otherInstituteId, $"O-{suffix}");
        var closed = Period(graph.InstituteId, $"C-{suffix}");
        closed.Open();
        closed.Close();
        var finalized = Period(graph.InstituteId, $"F-{suffix}");
        finalized.Open();
        finalized.Close();
        finalized.Finalize();
        var closedMeasurement = IndicatorMeasurement.Create(indicator.Id, closed.Id, 60m, null, null, Guid.NewGuid());
        db.Outputs.Add(output);
        db.Indicators.Add(indicator);
        db.ReportingPeriods.AddRange(draft, other, closed, finalized);
        db.IndicatorMeasurements.Add(closedMeasurement);
        await db.SaveChangesAsync();
        return new MeasurementSeed(indicator.Id, draft.Id, other.Id, finalized.Id, closedMeasurement.Id);
    }

    private async Task<Guid> SeedPeriodAsync(Guid instituteId, string prefix)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var period = Period(instituteId, $"{prefix}-{Guid.NewGuid():N}"[..24]);
        db.ReportingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }

    private static ReportingPeriod Period(Guid instituteId, string code) =>
        ReportingPeriod.Create(ScopeTypes.Institute, instituteId, code, $"Period {code}",
            ReportingPeriodTypes.Quarterly, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null).Value!;

    private HttpClient Client(Guid instituteId, params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken(instituteId, permissions));
        return client;
    }

    private string CreateToken(Guid instituteId, IReadOnlyList<string> permissions)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwt = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "integration.plan-user"),
            new(ClaimTypes.Role, SpmeRoles.Employee),
            new("identity_type", "Employee"),
            new("institute_id", instituteId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpRequestMessage Patch<T>(string uri, EntityTagHeaderValue etag, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, uri) { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(etag);
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("type").GetString()
            .Should().Be($"https://api.csir.example/problems/{code.Replace('_', '-')}");
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be(code);
    }

    private async Task AssertAuditActionsAsync(Guid targetId, params string[] actions)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var recorded = db.AuditRecords.Where(audit => audit.TargetId == targetId.ToString())
            .Select(audit => audit.Action).ToList();
        recorded.Should().Contain(actions);
    }

    private static CreateProjectRequest ProjectRequest(Guid instituteId, string code, Guid thrustId) =>
        new(instituteId, code, "Research delivery project", "Increase research delivery", null,
            "Delivered outputs", ProjectNatures.Research, new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31), "GHS", 1000m, null, null, null, thrustId);

    private static UpdateProjectRequest ProjectUpdate(string status) =>
        new("Updated research delivery project", "Increase research delivery", null,
            "Delivered outputs", null, ProjectNatures.Research, new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31), "GHS", 1200m, null, null, null, null, status);

    private sealed record PlanGraph(Guid InstituteId, Guid PlanId, Guid ThrustId);
    private sealed record MeasurementSeed(Guid IndicatorId, Guid DraftPeriodId,
        Guid OtherInstitutePeriodId, Guid FinalizedPeriodId, Guid ClosedMeasurementId);
}
