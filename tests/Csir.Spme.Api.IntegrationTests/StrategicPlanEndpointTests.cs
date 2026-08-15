using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Plan;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class StrategicPlanEndpointTests(SpmeApiFactory factory) : IClassFixture<SpmeApiFactory>
{
    [Fact]
    public async Task Strategic_Plans_Support_Scoped_Crud_Activation_And_Idempotent_Create()
    {
        var institute = new Institute(
            $"SP-{Guid.NewGuid():N}"[..16], "Strategic Plan Test Institute", "Institute");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.Institutes.Add(institute);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreatePlatformToken());
        var createRequest = new CreateStrategicPlanRequest(
            institute.Id, $"PLAN-{Guid.NewGuid():N}"[..24], "Plan 2030", "Definition",
            "Objective", 2026, 2030);
        var key = Guid.NewGuid().ToString("N");

        var createdResponse = await SendAsync(client, HttpMethod.Post, "/api/v2/strategic-plans",
            createRequest, key);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createdResponse.Content
            .ReadFromJsonAsync<DataResponse<StrategicPlanResponse>>())!.Data;
        createdResponse.Headers.Location!.ToString()
            .Should().Be($"/api/v2/strategic-plans/{created.Id}");
        createdResponse.Headers.ETag!.Tag.Should().Be(created.Etag);

        var replay = await SendAsync(client, HttpMethod.Post, "/api/v2/strategic-plans",
            createRequest, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        var reused = await SendAsync(client, HttpMethod.Post, "/api/v2/strategic-plans",
            createRequest with { Name = "Different" }, key);
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await reused.Content.ReadAsStringAsync()).Should().Contain("idempotency_key_reused");

        var duplicate = await SendAsync(client, HttpMethod.Post, "/api/v2/strategic-plans",
            createRequest with { Code = createRequest.Code.ToLowerInvariant() },
            Guid.NewGuid().ToString("N"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var missingIfMatch = await client.PatchAsJsonAsync(
            $"/api/v2/strategic-plans/{created.Id}",
            new UpdateStrategicPlanRequest(
                "Updated Plan 2030", "Updated definition", "Updated objective", 2026, 2030));
        missingIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        using var patch = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v2/strategic-plans/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateStrategicPlanRequest(
                "Updated Plan 2030", "Updated definition", "Updated objective", 2026, 2030))
        };
        patch.Headers.IfMatch.Add(createdResponse.Headers.ETag!);
        var updated = await client.SendAsync(patch);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var activated = await SendAsync(
            client, HttpMethod.Post, $"/api/v2/strategic-plans/{created.Id}/activate",
            new { }, Guid.NewGuid().ToString("N"), updated.Headers.ETag);
        activated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activated.Content.ReadFromJsonAsync<DataResponse<StrategicPlanResponse>>())!
            .Data.Status.Should().Be("active");

        using var patchActive = new HttpRequestMessage(
            HttpMethod.Patch, $"/api/v2/strategic-plans/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateStrategicPlanRequest(
                "Too late", "Definition", "Objective", 2026, 2030))
        };
        patchActive.Headers.IfMatch.Add(activated.Headers.ETag!);
        (await client.SendAsync(patchActive)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var listed = await client.GetFromJsonAsync<ListResponse<StrategicPlanResponse>>(
            $"/api/v2/strategic-plans?instituteId={institute.Id}");
        listed!.Data.Should().ContainSingle(plan => plan.Id == created.Id);

        using var auditScope = factory.Services.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        auditDb.AuditRecords.Where(record => record.TargetId == created.Id.ToString())
            .Select(record => record.Action)
            .Should().Contain([
                "strategic-plan.created",
                "strategic-plan.updated",
                "strategic-plan.activated"
            ]);
    }

    [Fact]
    public async Task Strategic_Plans_Enforce_Permissions_And_Institute_Scope()
    {
        var ownInstitute = new Institute(
            $"SPO-{Guid.NewGuid():N}"[..16], "Strategic Plan Own Institute", "Institute");
        var foreignInstitute = new Institute(
            $"SPF-{Guid.NewGuid():N}"[..16], "Strategic Plan Foreign Institute", "Institute");
        var foreignPlan = StrategicPlan.Create(
            foreignInstitute.Id, $"FOREIGN-{Guid.NewGuid():N}"[..32],
            "Foreign plan", "Definition", "Objective", 2026, 2030);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.Institutes.AddRange(ownInstitute, foreignInstitute);
            db.StrategicPlans.Add(foreignPlan);
            await db.SaveChangesAsync();
        }

        using var scopedClient = factory.CreateClient();
        scopedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", CreateToken(SpmeRoles.StrategicPlanAdmin, ownInstitute.Id));
        (await scopedClient.GetAsync($"/api/v2/strategic-plans/{foreignPlan.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", CreateToken(SpmeRoles.Employee, ownInstitute.Id));
        (await employeeClient.GetAsync("/api/v2/strategic-plans"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string uri,
        object body,
        string key,
        EntityTagHeaderValue? etag = null)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        if (etag is not null)
            request.Headers.IfMatch.Add(etag);
        return await client.SendAsync(request);
    }

    private string CreatePlatformToken()
        => CreateToken(SpmeRoles.PlatformAdmin, null);

    private string CreateToken(string role, Guid? instituteId)
    {
        var jwt = factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("identity_type", role)
        };
        if (instituteId.HasValue)
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
