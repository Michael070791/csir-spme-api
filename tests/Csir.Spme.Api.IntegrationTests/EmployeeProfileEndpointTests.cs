using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Common;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class EmployeeProfileEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public EmployeeProfileEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Staff_Can_Read_And_Update_Self_Contact_But_Not_Other_Employees()
    {
        var employeeId = await SeedEmployeeAsync();
        var otherEmployeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);
        using var other = StaffClient(otherEmployeeId);

        var read = await staff.GetFromJsonAsync<EmployeeSelfContactResponse>(
            $"/api/v2/employees/{employeeId}/self-contact");
        read.Should().NotBeNull();

        var patch = await staff.PatchAsJsonAsync(
            $"/api/v2/employees/{employeeId}/self-contact",
            new UpdateEmployeeSelfContactRequest("staff.profile@csir.test", "+233241234567", "12 Independence Ave, Accra"));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await patch.Content.ReadFromJsonAsync<EmployeeSelfContactResponse>();
        updated!.Phone.Should().Be("+233241234567");
        updated.ResidentialAddress.Should().Be("12 Independence Ave, Accra");

        var forbidden = await other.PatchAsJsonAsync(
            $"/api/v2/employees/{employeeId}/self-contact",
            new UpdateEmployeeSelfContactRequest(null, "+233200000001", null));
        forbidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Staff_Cannot_Delete_Education_Or_Children_But_Hr_Can()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);
        using var hr = HrClient();

        var education = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("University of Ghana", "Computer Science", "BSc", "bachelor-or-equivalent"));
        education.StatusCode.Should().Be(HttpStatusCode.Created);
        var educationRecord = await education.Content.ReadFromJsonAsync<EducationRecordResponse>();

        var child = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children",
            new UpsertEmployeeChildRequest("Ama Mensah", new DateTime(2015, 1, 3), "female", null, null));
        child.StatusCode.Should().Be(HttpStatusCode.Created);
        var childRecord = await child.Content.ReadFromJsonAsync<EmployeeChildResponse>();

        (await staff.DeleteAsync($"/api/v2/employees/{employeeId}/education/{educationRecord!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.DeleteAsync($"/api/v2/employees/{employeeId}/children/{childRecord!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await hr.DeleteAsync($"/api/v2/employees/{employeeId}/education/{educationRecord.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await hr.DeleteAsync($"/api/v2/employees/{employeeId}/children/{childRecord.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Staff_Edit_Of_Verified_Education_Resets_Hr_Review_To_Pending()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);
        using var hr = HrClient();

        var created = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("KNUST", "Engineering", "BSc", "bachelor-or-equivalent"));
        var record = await created.Content.ReadFromJsonAsync<EducationRecordResponse>();

        await SeedCleanProfileDocumentAsync(employeeId, ProfileDocumentConstants.BscCertificate);

        var review = await hr.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education/{record!.Id}/review",
            new ReviewEducationRecordRequest("verified", "verified"));
        review.StatusCode.Should().Be(HttpStatusCode.OK);

        var patch = await staff.PatchAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education/{record.Id}",
            new UpsertEducationRecordRequest("KNUST", "Mechanical Engineering", "BSc", "bachelor-or-equivalent"));
        patch.EnsureSuccessStatusCode();
        var updated = await patch.Content.ReadFromJsonAsync<EducationRecordResponse>();
        updated!.InstitutionRecognitionStatus.Should().Be("pending");
        updated.RelevantFieldStatus.Should().Be("pending");
    }

    [Fact]
    public async Task Profile_Document_Upload_Rejects_Oversize_And_Invalid_Content_Type()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);

        var oversize = new CreateEmployeeProfileDocumentUploadRequest(
            ProfileDocumentConstants.NationalId,
            "id.png",
            "image/png",
            52_428_801,
            new string('a', 64),
            null);
        var oversizeResponse = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/document-upload-sessions", oversize);
        oversizeResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var invalidType = new CreateEmployeeProfileDocumentUploadRequest(
            ProfileDocumentConstants.NationalId,
            "id.pdf",
            "application/pdf",
            1024,
            new string('b', 64),
            null);
        var invalidResponse = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/document-upload-sessions", invalidType);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Hr_Cannot_Verify_Degree_Education_Without_Clean_Certificate()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);
        using var hr = HrClient();

        var created = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("University of Ghana", "Computer Science", "BSc", "bachelor-or-equivalent"));
        var record = await created.Content.ReadFromJsonAsync<EducationRecordResponse>();

        var blocked = await hr.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education/{record!.Id}/review",
            new ReviewEducationRecordRequest("verified", null));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await SeedCleanProfileDocumentAsync(employeeId, ProfileDocumentConstants.BscCertificate);

        var approved = await hr.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education/{record.Id}/review",
            new ReviewEducationRecordRequest("verified", "verified"));
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Education_Certificate_Types_Are_Canonical_And_Reject_Free_Text()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);

        var catalog = await staff.GetFromJsonAsync<CollectionResponse<EducationCertificateTypeResponse>>(
            "/api/v2/education-certificate-types");
        catalog.Should().NotBeNull();
        catalog!.Items.Select(item => item.Code).Should().Contain(["BSc", "BE", "MSc", "MPhil", "PhD"]);

        var bachelorOnly = await staff.GetFromJsonAsync<CollectionResponse<EducationCertificateTypeResponse>>(
            "/api/v2/education-certificate-types?qualificationLevel=bachelor-or-equivalent");
        bachelorOnly!.Items.Should().Contain(item => item.Code == "BSc");
        bachelorOnly.Items.Should().NotContain(item => item.Code == "MPhil" && !item.IsOpenAward);

        var created = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("University of Ghana", "Computer Science", "B.Sc.", "bachelor-or-equivalent"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var record = await created.Content.ReadFromJsonAsync<EducationRecordResponse>();
        record!.CertificateAwarded.Should().Be("BSc");

        var rejected = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("University of Ghana", "Computer Science", "My Custom Degree", "bachelor-or-equivalent"));
        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var mismatched = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/education",
            new UpsertEducationRecordRequest("University of Ghana", "Computer Science", "MPhil", "bachelor-or-equivalent"));
        mismatched.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Child_Gender_Must_Be_Male_Or_Female()
    {
        var employeeId = await SeedEmployeeAsync();
        using var staff = StaffClient(employeeId);

        var created = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children",
            new UpsertEmployeeChildRequest("Ama Mensah", new DateTime(2015, 1, 3), "Female", null, null));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var child = await created.Content.ReadFromJsonAsync<EmployeeChildResponse>();
        child!.Gender.Should().Be("female");

        var rejected = await staff.PostAsJsonAsync(
            $"/api/v2/employees/{employeeId}/children",
            new UpsertEmployeeChildRequest("Kofi Mensah", new DateTime(2018, 9, 20), "unknown", null, null));
        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task SeedCleanProfileDocumentAsync(Guid employeeId, string documentType)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.Id == employeeId);
        var file = new FileRecord(
            $"employee-profile/{employee.InstituteId:N}/{employeeId:N}/{documentType}/test.pdf",
            "certificate.pdf",
            "application/pdf",
            2048,
            new string('a', 64),
            "employee-profile-document",
            employee.InstituteId,
            "confidential");
        file.MarkScanStatus("clean");
        var document = new EmployeeDocument(employeeId, employee.InstituteId, documentType, file.Id, Guid.NewGuid());
        db.FileRecords.Add(file);
        db.EmployeeDocuments.Add(document);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedEmployeeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = new Employee(Guid.NewGuid(), $"STF-{Guid.NewGuid():N}"[..12], "Mensah", "male");
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private HttpClient StaffClient(Guid employeeId)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwt = configuration.GetSection("Jwt");
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, SpmeRoles.Employee),
            new Claim("employee_id", employeeId.ToString())
        };
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private HttpClient HrClient()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwt = configuration.GetSection("Jwt");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, SpmeRoles.PlatformAdmin),
            new Claim("identity_type", "PlatformAdmin")
        };
        var token = new JwtSecurityToken(
            jwt.GetValue<string>("Issuer") ?? "csir-spme-api",
            jwt.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetValue<string>("Key")!)),
                SecurityAlgorithms.HmacSha256));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
