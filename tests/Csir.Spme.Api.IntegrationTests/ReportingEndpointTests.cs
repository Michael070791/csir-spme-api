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

public class ReportingEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    private readonly HttpClient _client;

    public ReportingEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreatePlatformAdminToken(factory));
    }

    [Fact]
    public async Task Reports_List_Accepts_Institute_Code_Filter()
    {
        await SeedInstituteAsync("WRI", "Water Research Institute");

        var response = await _client.GetAsync("/api/v2/reports?instituteId=WRI&reportType=strategic&limit=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(@"""data"":[]");
    }

    [Fact]
    public async Task Reports_List_Requires_Report_Role_And_Enforces_Institute_Scope()
    {
        var instituteA = await SeedInstituteAsync($"RPT-A-{Guid.NewGuid():N}"[..16], "Reports Institute A");
        var instituteB = await SeedInstituteAsync($"RPT-B-{Guid.NewGuid():N}"[..16], "Reports Institute B");
        var reportA = await SeedReportAsync(instituteA, "Institute A report");
        var reportB = await SeedReportAsync(instituteB, "Institute B report");

        var scopedReportsAdmin = Client(CreateToken(SpmeRoles.ReportsAdmin, instituteA));
        var scopedList = await scopedReportsAdmin.GetFromJsonAsync<ListResponse<ReportResponse>>("/api/v2/reports?limit=20");

        scopedList!.Data.Should().Contain(item => item.Id == reportA);
        scopedList.Data.Should().NotContain(item => item.Id == reportB);
        scopedList.Data.Should().OnlyContain(item => item.InstituteId == instituteA);

        var crossInstitute = await scopedReportsAdmin.GetAsync($"/api/v2/reports?instituteId={instituteB}&limit=20");
        crossInstitute.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var crossInstituteItem = await scopedReportsAdmin.GetAsync($"/api/v2/reports/{reportB}");
        crossInstituteItem.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var unscopedReportsAdmin = Client(CreateToken(SpmeRoles.ReportsAdmin, null));
        var unscopedList = await unscopedReportsAdmin.GetAsync("/api/v2/reports?limit=20");
        unscopedList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var unscopedItem = await unscopedReportsAdmin.GetAsync($"/api/v2/reports/{reportA}");
        unscopedItem.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var platformAdmin = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var platformList = await platformAdmin.GetFromJsonAsync<ListResponse<ReportResponse>>("/api/v2/reports?limit=100");
        platformList!.Data.Should().Contain(item => item.Id == reportA);
        platformList.Data.Should().Contain(item => item.Id == reportB);
        var platformItem = await platformAdmin.GetAsync($"/api/v2/reports/{reportB}");
        platformItem.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invalid_Guid_Query_Binding_Returns_BadRequest_Not_InternalServerError()
    {
        var response = await _client.GetAsync("/api/v2/projects?instituteId=WRI&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        content.Should().Contain(@"""type"":""https://api.csir.example/problems/malformed-request""");
        content.Should().Contain(@"""code"":""malformed_request""");
        content.Should().Contain(@"""errorCode"":""malformed_request""");
        content.Should().Contain(@"""traceId"":");
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

    private async Task<Guid> SeedReportAsync(Guid instituteId, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var period = ReportingPeriod.Create(
            ScopeTypes.Institute,
            instituteId,
            $"RP-{Guid.NewGuid():N}"[..16],
            $"Reports period {Guid.NewGuid():N}"[..32],
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            null).Value!;
        var report = Report.Create(
            instituteId,
            period.Id,
            ReportTypes.Strategic,
            title,
            "Scoped report summary.",
            "Scoped report abstract.",
            "Scoped key results.",
            "Scoped conclusion.");
        db.ReportingPeriods.Add(period);
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string CreateToken(string role, Guid? instituteId)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, $"integration.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (instituteId.HasValue)
        {
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        }

        if (role == SpmeRoles.ReportsAdmin)
        {
            claims.Add(new Claim("permission", SpmePermissions.ReportsRead));
        }

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
