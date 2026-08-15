using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public class EmployeeDependantsEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeDependantsEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreatePlatformAdminToken(factory));
    }

    [Fact]
    public async Task Spouse_Endpoints_Create_Update_Read_And_Delete_Record()
    {
        var employeeId = await SeedEmployeeAsync();

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/spouse",
            new UpsertEmployeeSpouseRequest(
                "  Akua Mensah  ",
                new DateTime(1990, 5, 12),
                "0240000000",
                "akua@example.test",
                "Accountant",
                "CSIR"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeeSpouseResponse>();
        created.Should().NotBeNull();
        created!.EmployeeId.Should().Be(employeeId);
        created.Name.Should().Be("Akua Mensah");

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/spouse",
            new UpsertEmployeeSpouseRequest("Duplicate", null, null, null, null, null));
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v2/employees/{employeeId}/spouse/{created.Id}",
            new UpsertEmployeeSpouseRequest(
                "Akua Owusu",
                new DateTime(1990, 5, 12),
                "0241111111",
                null,
                "Auditor",
                null));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<EmployeeSpouseResponse>();
        updated!.Name.Should().Be("Akua Owusu");
        updated.Email.Should().BeNull();

        var getResponse = await _client.GetFromJsonAsync<EmployeeSpouseResponse>(
            $"/api/v2/employees/{employeeId}/spouse");
        getResponse!.Id.Should().Be(created.Id);

        var deleteResponse = await _client.DeleteAsync($"/api/v2/employees/{employeeId}/spouse/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missingResponse = await _client.GetAsync($"/api/v2/employees/{employeeId}/spouse");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Children_Endpoints_Enforce_Maximum_Of_Two_Children_Per_Employee()
    {
        var employeeId = await SeedEmployeeAsync();

        var first = await CreateChildAsync(employeeId, "Ama Mensah", new DateTime(2015, 1, 3), "female");
        var second = await CreateChildAsync(employeeId, "Kofi Mensah", new DateTime(2018, 9, 20), "male");

        var thirdResponse = await _client.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children",
            new UpsertEmployeeChildRequest("Third Child", new DateTime(2020, 1, 1), "female", null, null));
        thirdResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children/{first.Id}",
            new UpsertEmployeeChildRequest("Ama Owusu", first.DateOfBirth, first.Gender, "BC-001", null));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<EmployeeChildResponse>();
        updated!.Name.Should().Be("Ama Owusu");
        updated.BirthCertificateNumber.Should().Be("BC-001");

        var listResponse = await _client.GetFromJsonAsync<CollectionResponse<EmployeeChildResponse>>(
            $"/api/v2/employees/{employeeId}/children");
        listResponse!.Items.Should().HaveCount(2);

        var deleteResponse = await _client.DeleteAsync($"/api/v2/employees/{employeeId}/children/{second.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var replacement = await CreateChildAsync(employeeId, "Yaw Mensah", new DateTime(2021, 4, 2), "male");
        replacement.EmployeeId.Should().Be(employeeId);
    }

    private async Task<EmployeeChildResponse> CreateChildAsync(
        Guid employeeId,
        string name,
        DateTime dateOfBirth,
        string gender)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children",
            new UpsertEmployeeChildRequest(name, dateOfBirth, gender, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var child = await response.Content.ReadFromJsonAsync<EmployeeChildResponse>();
        return child!;
    }

    private async Task<Guid> SeedEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = new Employee(Guid.NewGuid(), $"STF-{Guid.NewGuid():N}", "Mensah", "male");
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
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
