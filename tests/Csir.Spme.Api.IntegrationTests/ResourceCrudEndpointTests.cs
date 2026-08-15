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
using Csir.Spme.Domain.Reporting;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class ResourceCrudEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    private readonly HttpClient _client;

    public ResourceCrudEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreatePlatformAdminToken(factory));
    }

    [Fact]
    public async Task Projects_CRUD_Uses_Etags_And_Removes_Draft()
    {
        var instituteId = await SeedInstituteAsync("PCRUD", "Project CRUD Institute");
        var code = $"PRJ-{Guid.NewGuid():N}"[..16];

        var create = await PostAsync(_client, "/api/v2/projects", new CreateProjectRequest(
            instituteId, code, "Cassava Productivity Platform", "Improve field research coordination.",
            "Supports institutional delivery tracking.", "Validated productivity workflow.", ProjectNatures.Research,
            DateTime.UtcNow.Date, null, "GHS", 125000m, "Coordinated trial model.", "Improved farmer outcomes.",
            null, null));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var created = (await create.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data;
        created!.Code.Should().Be(code);

        var get = await _client.GetAsync($"/api/v2/projects/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/projects/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateProjectRequest(
                "Cassava Productivity Platform Updated", "Improve field research coordination.",
                "Supports institutional delivery tracking.", "Validated productivity workflow.", null,
                ProjectNatures.Research, DateTime.UtcNow.Date, null, "GHS", 125000m,
                "Coordinated trial model.", "Improved farmer outcomes.", null, null, ProjectStatuses.Draft))
        };
        update.Headers.IfMatch.Add(create.Headers.ETag!);

        var updatedResponse = await _client.SendAsync(update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedResponse.Headers.ETag.Should().NotBeNull();
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<DataResponse<ProjectResponse>>())!.Data;
        updated!.Name.Should().Be("Cassava Productivity Platform Updated");

        var delete = await _client.DeleteAsync($"/api/v2/projects/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var missing = await _client.GetAsync($"/api/v2/projects/{created.Id}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Technologies_CRUD_Uses_Etags_And_Removes_Draft()
    {
        var instituteId = await SeedInstituteAsync("TCRUD", "Technology CRUD Institute");
        var code = $"TECH-{Guid.NewGuid():N}"[..18];

        var create = await PostAsync(_client, "/api/v2/technologies", new CreateTechnologyRequest(
            instituteId, code, "Moisture Sensor", "Low-cost field moisture monitoring device.",
            "Agricultural instrumentation", null, "device", (short)2026, true));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var created = (await create.Content.ReadFromJsonAsync<DataResponse<TechnologyResponse>>())!.Data;
        created!.Code.Should().Be(code);

        var get = await _client.GetAsync($"/api/v2/technologies/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/technologies/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateTechnologyRequest(
                "Moisture Sensor Mk II", "Low-cost field moisture monitoring device.",
                "Agricultural instrumentation", null, "device", (short)2026, true, TechnologyStatuses.Draft))
        };
        update.Headers.IfMatch.Add(create.Headers.ETag!);

        var updatedResponse = await _client.SendAsync(update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedResponse.Headers.ETag.Should().NotBeNull();
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<DataResponse<TechnologyResponse>>())!.Data;
        updated!.Name.Should().Be("Moisture Sensor Mk II");

        var delete = await _client.DeleteAsync($"/api/v2/technologies/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var missing = await _client.GetAsync($"/api/v2/technologies/{created.Id}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reports_CRUD_Uses_Etags_And_Removes_Draft()
    {
        var instituteId = await SeedInstituteAsync("RCRUD", "Report CRUD Institute");
        var periodId = await SeedReportingPeriodAsync(instituteId);

        var create = await PostAsync(_client, "/api/v2/reports", new CreateReportRequest(
            instituteId, periodId, ReportTypes.Strategic, "Quarterly strategic report",
            "Summary of institute strategic execution.", "Executive abstract.", "Key result set.",
            "Continue implementation."));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.ETag.Should().NotBeNull();
        var created = (await create.Content.ReadFromJsonAsync<DataResponse<ReportResponse>>())!.Data;
        created!.ReportingPeriodId.Should().Be(periodId);

        var get = await _client.GetAsync($"/api/v2/reports/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        using var update = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/reports/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateReportRequest(
                "Quarterly strategic report updated", "Summary of institute strategic execution.",
                "Executive abstract.", "Key result set.", "Continue implementation."))
        };
        update.Headers.IfMatch.Add(create.Headers.ETag!);

        var updatedResponse = await _client.SendAsync(update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedResponse.Headers.ETag.Should().NotBeNull();
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<DataResponse<ReportResponse>>())!.Data;
        updated!.Title.Should().Be("Quarterly strategic report updated");

        var delete = await _client.DeleteAsync($"/api/v2/reports/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var missing = await _client.GetAsync($"/api/v2/reports/{created.Id}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string uri, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private async Task<Guid> SeedInstituteAsync(string code, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var existing = db.Institutes.SingleOrDefault(institute => institute.Code == code);
        if (existing is not null)
        {
            return existing.Id;
        }

        var institute = new Institute(code, name, "Institute");
        db.Institutes.Add(institute);
        await db.SaveChangesAsync();
        return institute.Id;
    }

    private async Task<Guid> SeedReportingPeriodAsync(Guid instituteId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var code = $"Q-{Guid.NewGuid():N}"[..16];
        var period = ReportingPeriod.Create(
            ScopeTypes.Institute, instituteId, code, "Quarterly CRUD Period",
            ReportingPeriodTypes.Quarterly, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), null).Value!;
        db.ReportingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }

    private static string CreatePlatformAdminToken(SpmeApiFactory factory)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "integration.platform-admin"),
            new Claim(ClaimTypes.Role, SpmeRoles.PlatformAdmin),
            new Claim("identity_type", "PlatformAdmin")
        };

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
