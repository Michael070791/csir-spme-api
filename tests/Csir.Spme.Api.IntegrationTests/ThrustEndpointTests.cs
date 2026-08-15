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
using Csir.Spme.Domain.Plan;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public class ThrustEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public ThrustEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Thrust_List_Does_Not_Require_A_Plan_And_Is_Institute_Scoped()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (instituteA, planA, thrustA) = await SeedThrustAsync($"THRUST-A-{suffix}");
        var (_, planB, thrustB) = await SeedThrustAsync($"THRUST-B-{suffix}");
        var client = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var allResponse = await client.GetAsync("/api/v2/thrusts");
        var filteredResponse = await client.GetAsync($"/api/v2/thrusts?strategicPlanId={planA}");
        var foreignPlanResponse = await client.GetAsync($"/api/v2/thrusts?strategicPlanId={planB}");

        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        foreignPlanResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var all = await allResponse.Content.ReadFromJsonAsync<ListResponse<ThrustResponse>>();
        var filtered = await filteredResponse.Content.ReadFromJsonAsync<ListResponse<ThrustResponse>>();
        var foreignPlan = await foreignPlanResponse.Content.ReadFromJsonAsync<ListResponse<ThrustResponse>>();

        all.Should().NotBeNull();
        all!.Data.Should().Contain(item => item.Id == thrustA);
        all.Data.Should().NotContain(item => item.Id == thrustB);
        filtered.Should().NotBeNull();
        filtered!.Data.Should().ContainSingle(item => item.Id == thrustA);
        foreignPlan.Should().NotBeNull();
        foreignPlan!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Thrust_List_Requires_Authentication()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v2/thrusts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Thrust_List_Allows_Platform_Admin_To_Filter_By_Plan()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (_, planA, thrustA) = await SeedThrustAsync($"THRUST-P-A-{suffix}");
        var (_, planB, thrustB) = await SeedThrustAsync($"THRUST-P-B-{suffix}");
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var all = await client.GetFromJsonAsync<ListResponse<ThrustResponse>>("/api/v2/thrusts");
        var filtered = await client.GetFromJsonAsync<ListResponse<ThrustResponse>>(
            $"/api/v2/thrusts?strategicPlanId={planB}");

        all.Should().NotBeNull();
        all!.Data.Should().Contain(item => item.Id == thrustA);
        all.Data.Should().Contain(item => item.Id == thrustB);
        filtered.Should().NotBeNull();
        filtered!.Data.Should().ContainSingle(item => item.Id == thrustB);
    }

    private async Task<(Guid InstituteId, Guid PlanId, Guid ThrustId)> SeedThrustAsync(string code)
    {
        var institute = new Institute(code, $"Institute {code}", "Institute");
        var plan = StrategicPlan.Create(institute.Id, $"PLAN-{code}", $"Plan {code}", "Definition", "Objective", 2026, 2030);
        var thrust = Thrust.Create(plan.Id, institute.Id, $"T-{code}", $"Thrust {code}", "Description", "Objective", 1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        db.Institutes.Add(institute);
        db.StrategicPlans.Add(plan);
        db.Thrusts.Add(thrust);
        await db.SaveChangesAsync();
        return (institute.Id, plan.Id, thrust.Id);
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
        var key = jwt.GetValue<string>("Key") ?? throw new InvalidOperationException("Jwt:Key is required.");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "integration.thrusts"),
            new(ClaimTypes.Role, role),
            new("permission", SpmePermissions.ThrustsRead),
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
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
