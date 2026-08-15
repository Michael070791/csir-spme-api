using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class IdempotencyEndpointTests(SpmeApiFactory factory) : IClassFixture<SpmeApiFactory>
{
    [Fact]
    public async Task Retry_Protected_Create_Requires_Idempotency_Key()
    {
        var instituteId = await SeedInstituteAsync($"IDM-{Guid.NewGuid():N}"[..16]);
        using var client = CreatePlatformClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/projects")
        {
            Content = JsonContent.Create(new CreateProjectRequest(
                instituteId, $"PRJ-{Guid.NewGuid():N}"[..16], "Idempotency Project",
                "Objective", null, null, ProjectNatures.Research, DateTime.UtcNow.Date, null,
                "GHS", 1000m, null, null, null, null))
        };
        request.Headers.Add("X-Test-Skip-Idempotency", "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("idempotency_key_required");
    }

    [Fact]
    public async Task Project_Create_Replays_Original_Response_And_Rejects_Changed_Payload()
    {
        var instituteId = await SeedInstituteAsync($"IDP-{Guid.NewGuid():N}"[..16]);
        using var client = CreatePlatformClient();
        var body = new CreateProjectRequest(
            instituteId, $"PRJ-{Guid.NewGuid():N}"[..16], "Idempotent Project",
            "Objective", null, null, ProjectNatures.Research, DateTime.UtcNow.Date, null,
            "GHS", 1000m, null, null, null, null);
        var key = Guid.NewGuid().ToString("N");

        var created = await SendAsync(client, "/api/v2/projects", body, key);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = (await created.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data;

        var replay = await SendAsync(client, "/api/v2/projects", body, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        (await replay.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data.Id
            .Should().Be(project.Id);

        var reused = await SendAsync(
            client, "/api/v2/projects", body with { Name = "Changed name" }, key);
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await reused.Content.ReadAsStringAsync()).Should().Contain("idempotency_key_reused");
    }

    [Fact]
    public async Task Technology_Create_Replays_Original_Response()
    {
        var instituteId = await SeedInstituteAsync($"IDT-{Guid.NewGuid():N}"[..16]);
        using var client = CreatePlatformClient();
        var body = new CreateTechnologyRequest(
            instituteId, $"TECH-{Guid.NewGuid():N}"[..18], "Sensor", "Description",
            "Instrumentation", null, "device", (short)2026, false);
        var key = Guid.NewGuid().ToString("N");

        var created = await SendAsync(client, "/api/v2/technologies", body, key);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var technology = (await created.Content.ReadFromJsonAsync<DataResponse<TechnologyResponse>>())!.Data;

        var replay = await SendAsync(client, "/api/v2/technologies", body, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        (await replay.Content.ReadFromJsonAsync<DataResponse<TechnologyResponse>>())!.Data.Id
            .Should().Be(technology.Id);
    }

    private HttpClient CreatePlatformClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreatePlatformToken());
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string uri, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private async Task<Guid> SeedInstituteAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var existing = db.Institutes.SingleOrDefault(institute => institute.Code == code);
        if (existing is not null)
            return existing.Id;

        var institute = new Institute(code, $"{code} Institute", "Institute");
        db.Institutes.Add(institute);
        await db.SaveChangesAsync();
        return institute.Id;
    }

    private string CreatePlatformToken()
    {
        var jwt = factory.Services.GetRequiredService<IConfiguration>().GetSection("Jwt");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, SpmeRoles.PlatformAdmin),
            new Claim("identity_type", SpmeRoles.PlatformAdmin)
        };
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    jwt.GetValue<string>("Key") ?? throw new InvalidOperationException("Jwt:Key is required."))),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
