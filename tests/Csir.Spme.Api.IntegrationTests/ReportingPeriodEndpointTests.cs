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

public sealed class ReportingPeriodEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public ReportingPeriodEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReportingPeriods_Require_Authentication()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v2/reporting-periods");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReportingPeriods_List_Is_Institute_Scoped_And_Includes_CsirWide_Periods()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"RPA-{suffix}"[..16], "Reporting Period Institute A");
        var instituteB = await SeedInstituteAsync($"RPB-{suffix}"[..16], "Reporting Period Institute B");
        var own = await SeedPeriodAsync(ScopeTypes.Institute, instituteA, $"OWN-{suffix}"[..16]);
        var foreign = await SeedPeriodAsync(ScopeTypes.Institute, instituteB, $"FOR-{suffix}"[..16]);
        var csirWide = await SeedPeriodAsync(ScopeTypes.CsirWide, null, $"ALL-{suffix}"[..16]);
        var client = Client(CreateToken(SpmeRoles.ReportsAdmin, instituteA));

        var response = await client.GetAsync("/api/v2/reporting-periods?limit=100&sort=code");
        var periods = await response.Content.ReadFromJsonAsync<ListResponse<ReportingPeriodResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        periods.Should().NotBeNull();
        periods!.Data.Should().Contain(period => period.Id == own);
        periods.Data.Should().Contain(period => period.Id == csirWide);
        periods.Data.Should().NotContain(period => period.Id == foreign);

        var foreignGet = await client.GetAsync($"/api/v2/reporting-periods/{foreign}");
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignOpen = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{foreign}/open", null);
        foreignOpen.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportingPeriods_Create_Enforces_Institute_Scope_And_Audits_The_Creation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"RPC-A-{suffix}"[..16], "Reporting Period Create Institute A");
        var instituteB = await SeedInstituteAsync($"RPC-B-{suffix}"[..16], "Reporting Period Create Institute B");
        var client = Client(CreateToken(SpmeRoles.ReportsAdmin, instituteA));
        var code = $"Q1-{suffix}"[..16];
        var request = new CreateReportingPeriodRequest(
            ScopeTypes.Institute,
            null,
            code,
            "Quarter one reporting period",
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            new DateTime(2026, 4, 15));

        using var missingRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/reporting-periods")
        {
            Content = JsonContent.Create(request)
        };
        missingRequest.Headers.Add("X-Test-Skip-Idempotency", "true");
        var missingIdempotencyKey = await client.SendAsync(missingRequest);
        missingIdempotencyKey.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missingIdempotencyKey.Content.ReadAsStringAsync()).Should().Contain("idempotency_key_required");

        var creationKey = Guid.NewGuid().ToString("N");
        var create = await PostAsync(client, request, creationKey);
        var created = (await create.Content.ReadFromJsonAsync<DataResponse<ReportingPeriodResponse>>())!.Data;

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        create.Headers.Location!.ToString().Should().Be($"/api/v2/reporting-periods/{created!.Id}");
        create.Headers.ETag!.Tag.Should().Be(created.Etag);
        created.InstituteId.Should().Be(instituteA);
        created.ScopeType.Should().Be(ScopeTypes.Institute);
        created.Status.Should().Be(ReportingPeriodStatuses.Draft);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.AuditRecords.Should().Contain(record =>
                record.Action == "reporting-period.created" && record.TargetId == created.Id.ToString());
        }

        var replay = await PostAsync(client, request, creationKey);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.TryGetValues("Idempotent-Replayed", out var replayValues).Should().BeTrue();
        replayValues.Should().ContainSingle().Which.Should().Be("true");

        var duplicate = await PostAsync(client, request, Guid.NewGuid().ToString("N"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var crossInstitute = await PostAsync(
            client,
            request with { InstituteId = instituteB, Code = $"X-{suffix}"[..16] },
            Guid.NewGuid().ToString("N"));
        crossInstitute.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReportingPeriods_Reject_A_Code_Longer_Than_The_Contract_Maximum()
    {
        var institute = await SeedInstituteAsync($"RPL-{Guid.NewGuid():N}"[..16], "Reporting Period Length Institute");
        var client = Client(CreateToken(SpmeRoles.ReportsAdmin, institute));
        var request = new CreateReportingPeriodRequest(
            ScopeTypes.Institute,
            null,
            new string('R', 65),
            "Invalid code length",
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            null);

        var response = await PostAsync(client, request, Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("validation_failed");
    }

    [Fact]
    public async Task ReportingPeriods_Reject_Unsupported_Scopes_And_Duplicate_CsirWide_Codes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var code = $"CW-{suffix}"[..16];
        var platformClient = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var request = new CreateReportingPeriodRequest(
            ScopeTypes.CsirWide,
            null,
            code,
            "CSIR-wide reporting period",
            ReportingPeriodTypes.Annual,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            null);

        (await PostAsync(platformClient, request, Guid.NewGuid().ToString("N"))).StatusCode
            .Should().Be(HttpStatusCode.Created);
        (await PostAsync(platformClient, request, Guid.NewGuid().ToString("N"))).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        var institute = await SeedInstituteAsync($"RPS-{suffix}"[..16], "Reporting Period Scope Institute");
        var instituteClient = Client(CreateToken(SpmeRoles.ReportsAdmin, institute));
        var unsupported = request with
        {
            ScopeType = ScopeTypes.Self,
            Code = $"SELF-{suffix}"[..16]
        };

        var unsupportedResponse = await PostAsync(instituteClient, unsupported, Guid.NewGuid().ToString("N"));
        unsupportedResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await unsupportedResponse.Content.ReadAsStringAsync()).Should().Contain("validation_failed");
    }

    [Fact]
    public async Task ReportingPeriod_Lifecycle_Requires_Etags_And_Audits_Transitions()
    {
        var institute = await SeedInstituteAsync(
            $"RPT-{Guid.NewGuid():N}"[..16], "Reporting Period Transition Institute");
        var periodId = await SeedPeriodAsync(
            ScopeTypes.Institute, institute, $"LC-{Guid.NewGuid():N}"[..16]);
        var client = Client(CreateToken(SpmeRoles.ReportsAdmin, institute));
        var employeeClient = Client(CreateToken(SpmeRoles.Employee, institute));

        var get = await client.GetAsync($"/api/v2/reporting-periods/{periodId}");
        var forbidden = await SendCommandAsync(
            employeeClient, $"/api/v2/reporting-periods/{periodId}/open", get.Headers.ETag);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var missingEtag = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/open", null);
        missingEtag.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var opened = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/open", get.Headers.ETag);
        opened.StatusCode.Should().Be(HttpStatusCode.OK);
        opened.Headers.ETag.Should().NotBe(get.Headers.ETag);
        (await opened.Content.ReadFromJsonAsync<DataResponse<ReportingPeriodResponse>>())!
            .Data.Status.Should().Be(ReportingPeriodStatuses.Open);
        var staleClose = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/close", get.Headers.ETag);
        staleClose.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        var closed = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/close", opened.Headers.ETag);
        closed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await closed.Content.ReadFromJsonAsync<DataResponse<ReportingPeriodResponse>>())!
            .Data.Status.Should().Be(ReportingPeriodStatuses.Closed);
        var finalized = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/finalize", closed.Headers.ETag);
        finalized.StatusCode.Should().Be(HttpStatusCode.OK);
        (await finalized.Content.ReadFromJsonAsync<DataResponse<ReportingPeriodResponse>>())!
            .Data.Status.Should().Be(ReportingPeriodStatuses.Finalized);
        var invalidReopen = await SendCommandAsync(
            client, $"/api/v2/reporting-periods/{periodId}/open", finalized.Headers.ETag);
        invalidReopen.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await invalidReopen.Content.ReadAsStringAsync()).Should().Contain("invalid_state_transition");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        db.AuditRecords.Where(record => record.TargetId == periodId.ToString())
            .Select(record => record.Action)
            .Should().Contain([
                "reporting-period.opened",
                "reporting-period.closed",
                "reporting-period.finalized"
            ]);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        object request,
        string idempotencyKey)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v2/reporting-periods")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> SendCommandAsync(
        HttpClient client, string uri, EntityTagHeaderValue? etag)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(new { })
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        if (etag is not null)
            message.Headers.IfMatch.Add(etag);
        return await client.SendAsync(message);
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

    private async Task<Guid> SeedPeriodAsync(string scopeType, Guid? instituteId, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var period = ReportingPeriod.Create(
            scopeType,
            instituteId,
            code,
            $"Period {code}",
            ReportingPeriodTypes.Quarterly,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            null).Value!;
        db.ReportingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
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
        var jwt = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (instituteId.HasValue)
        {
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
