using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Org;
using Csir.Spme.Domain.Promotions;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class PromotionReportEndpointTests : IClassFixture<SpmeApiFactory>
{
    private const string ReportType = "prescribed-promotion-report";

    private readonly SpmeApiFactory _factory;

    public PromotionReportEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_Can_Read_And_Replace_Report_With_A_Fresh_Etag_And_Audit()
    {
        var seeded = await SeedReportAsync();
        using var client = CreateClient(
            SpmeRoles.Employee,
            seeded.EmployeeId,
            seeded.InstituteId);

        var get = await client.GetAsync(Route(seeded.SubmissionId));
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Headers.ETag.Should().NotBeNull();

        using var replace = CreateReplaceRequest(
            seeded.SubmissionId,
            get.Headers.ETag!,
            "Promotion achievements and impact",
            JsonSerializer.SerializeToElement(new
            {
                type = "doc",
                content = new[]
                {
                    new { type = "paragraph", text = "Delivered measurable institutional impact." }
                }
            }));

        var replaced = await client.SendAsync(replace);

        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        replaced.Headers.ETag.Should().NotBeNull();
        replaced.Headers.ETag.Should().NotBe(get.Headers.ETag);
        var response = await replaced.Content.ReadFromJsonAsync<PromotionReportResponse>();
        response!.Title.Should().Be("Promotion achievements and impact");
        response.Content.SchemaVersion.Should().Be(1);
        response.Content.Sections.Should().ContainSingle();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        db.AuditRecords.Should().Contain(record =>
            record.Action == "promotion-submission.report.saved" &&
            record.TargetId == seeded.ReportId.ToString());
    }

    [Fact]
    public async Task Replace_Requires_The_Current_Etag_And_Rejects_Unstructured_Content()
    {
        var seeded = await SeedReportAsync();
        using var client = CreateClient(
            SpmeRoles.Employee,
            seeded.EmployeeId,
            seeded.InstituteId);

        var get = await client.GetAsync(Route(seeded.SubmissionId));
        var originalEtag = get.Headers.ETag!;

        using var missingEtag = CreateReplaceRequest(
            seeded.SubmissionId,
            null,
            "Missing concurrency header",
            JsonSerializer.SerializeToElement(new { type = "doc" }));
        (await client.SendAsync(missingEtag)).StatusCode
            .Should().Be(HttpStatusCode.PreconditionFailed);

        using var invalidContent = CreateReplaceRequest(
            seeded.SubmissionId,
            originalEtag,
            "Invalid content",
            JsonSerializer.SerializeToElement("<p>Raw HTML is not structured content.</p>"));
        (await client.SendAsync(invalidContent)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        using var firstUpdate = CreateReplaceRequest(
            seeded.SubmissionId,
            originalEtag,
            "First valid update",
            JsonSerializer.SerializeToElement(new { type = "doc", content = Array.Empty<object>() }));
        (await client.SendAsync(firstUpdate)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var staleUpdate = CreateReplaceRequest(
            seeded.SubmissionId,
            originalEtag,
            "Stale update",
            JsonSerializer.SerializeToElement(new { type = "doc", content = Array.Empty<object>() }));
        (await client.SendAsync(staleUpdate)).StatusCode
            .Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Employee_Access_Is_Owner_Only_And_Unknown_Report_Types_Are_Non_Disclosing()
    {
        var seeded = await SeedReportAsync();
        using var otherEmployee = CreateClient(
            SpmeRoles.Employee,
            Guid.NewGuid(),
            seeded.InstituteId);

        (await otherEmployee.GetAsync(Route(seeded.SubmissionId))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await otherEmployee.GetAsync(
            $"/api/v2/promotion-submissions/{seeded.SubmissionId}/reports/unknown-report"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hr_Can_Read_Within_Institute_But_Cannot_Edit_Employee_Content()
    {
        var seeded = await SeedReportAsync();
        using var sameInstituteHr = CreateClient(
            SpmeRoles.HrAdmin,
            employeeId: null,
            seeded.InstituteId);

        (await sameInstituteHr.GetAsync(Route(seeded.SubmissionId))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        using var forbiddenReplace = CreateReplaceRequest(
            seeded.SubmissionId,
            new EntityTagHeaderValue("\"unused\""),
            "HR must not edit this report",
            JsonSerializer.SerializeToElement(new { type = "doc" }));
        (await sameInstituteHr.SendAsync(forbiddenReplace)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        using var otherInstituteHr = CreateClient(
            SpmeRoles.HrAdmin,
            employeeId: null,
            Guid.NewGuid());
        (await otherInstituteHr.GetAsync(Route(seeded.SubmissionId))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        using var unscopedHr = CreateClient(
            SpmeRoles.HrAdmin,
            employeeId: null,
            instituteId: null);
        (await unscopedHr.GetAsync(Route(seeded.SubmissionId))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submitted_Promotion_Report_Is_Immutable_And_Anonymous_Access_Is_Rejected()
    {
        var seeded = await SeedReportAsync(
            PromotionConstants.SubmissionSubmitted,
            PromotionConstants.SubmissionReportFinalized);
        using var owner = CreateClient(
            SpmeRoles.Employee,
            seeded.EmployeeId,
            seeded.InstituteId);

        var get = await owner.GetAsync(Route(seeded.SubmissionId));
        using var replace = CreateReplaceRequest(
            seeded.SubmissionId,
            get.Headers.ETag!,
            "Attempted submitted update",
            JsonSerializer.SerializeToElement(new { type = "doc" }));

        (await owner.SendAsync(replace)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync(Route(seeded.SubmissionId))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returned_Finalized_Report_Can_Be_Reopened_For_Employee_Correction()
    {
        var seeded = await SeedReportAsync(
            PromotionConstants.SubmissionReturned,
            PromotionConstants.SubmissionReportFinalized);
        using var owner = CreateClient(
            SpmeRoles.Employee,
            seeded.EmployeeId,
            seeded.InstituteId);

        var get = await owner.GetAsync(Route(seeded.SubmissionId));
        using var replace = CreateReplaceRequest(
            seeded.SubmissionId,
            get.Headers.ETag!,
            "Corrected returned report",
            JsonSerializer.SerializeToElement(new { type = "doc" }));

        var response = await owner.SendAsync(replace);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<PromotionReportResponse>();
        report!.Status.Should().Be(PromotionConstants.SubmissionReportReady);
        report.FinalizedAt.Should().BeNull();
    }

    private async Task<SeededPromotionReport> SeedReportAsync(
        string submissionStatus = PromotionConstants.SubmissionDraft,
        string reportStatus = PromotionConstants.SubmissionReportDraft)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"PRI-{suffix[..8]}", $"Promotion Reports {suffix[..8]}", "Institute");
        var employee = new Employee(
            institute.Id,
            $"PR-{suffix[..12]}",
            "Employee",
            "female");

        var submission = CreatePrivate<PromotionSubmission>();
        Set(submission, nameof(PromotionSubmission.EmployeeId), employee.Id);
        Set(submission, nameof(PromotionSubmission.ApplicantUserId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.InstituteId), institute.Id);
        Set(submission, nameof(PromotionSubmission.PromotionAssessmentId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.PromotionCycleId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.PromotionPathId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.SourceGradeId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.TargetGradeId), Guid.NewGuid());
        Set(submission, nameof(PromotionSubmission.RequirementsLockedAt), DateTimeOffset.UtcNow);
        Set(submission, nameof(PromotionSubmission.Status), submissionStatus);

        var requirement = CreatePrivate<PromotionSubmissionRequirementSnapshot>();
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.PromotionSubmissionId), submission.Id);
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.RequirementTemplateId), Guid.NewGuid());
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.Code), "promotion-report");
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.RequirementType), PromotionConstants.RequirementReport);
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.Title), "Prescribed promotion report");
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.IsRequired), true);
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.DisplayOrder), (short)1);
        Set(requirement, nameof(PromotionSubmissionRequirementSnapshot.ReportTemplateCode), ReportType);

        var report = PromotionSubmissionReport.CreateDraft(
            submission.Id,
            requirement.Id,
            ReportType,
            "Prescribed promotion report",
            DateTimeOffset.UtcNow).Value!;
        Set(report, nameof(PromotionSubmissionReport.Status), reportStatus);
        if (reportStatus == PromotionConstants.SubmissionReportFinalized)
        {
            Set(report, nameof(PromotionSubmissionReport.FinalizedAt), DateTimeOffset.UtcNow);
        }

        db.AddRange(institute, employee, submission, requirement, report);
        await db.SaveChangesAsync();

        return new SeededPromotionReport(
            institute.Id,
            employee.Id,
            submission.Id,
            report.Id);
    }

    private HttpClient CreateClient(string role, Guid? employeeId, Guid? instituteId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(role, employeeId, instituteId));
        return client;
    }

    private string CreateToken(string role, Guid? employeeId, Guid? instituteId)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");
        var userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"integration.{role}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (employeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        }

        if (instituteId.HasValue)
        {
            claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
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

    private static HttpRequestMessage CreateReplaceRequest(
        Guid submissionId,
        EntityTagHeaderValue? etag,
        string title,
        JsonElement sectionContent)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Route(submissionId))
        {
            Content = JsonContent.Create(new ReplacePromotionReportRequest(
                title,
                new PromotionReportContentRequest(
                    1,
                    [
                        new PromotionReportSectionRequest(
                            "achievements",
                            "Achievements",
                            sectionContent)
                    ])))
        };

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        return request;
    }

    private static string Route(Guid submissionId) =>
        $"/api/v2/promotion-submissions/{submissionId}/reports/{ReportType}";

    private static T CreatePrivate<T>() where T : class =>
        (T)(Activator.CreateInstance(typeof(T), nonPublic: true)
            ?? throw new InvalidOperationException($"Could not create {typeof(T).Name}."));

    private static void Set<T>(T target, string propertyName, object value) where T : class
    {
        var property = typeof(T).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Property {typeof(T).Name}.{propertyName} was not found.");
        property.SetValue(target, value);
    }

    private sealed record SeededPromotionReport(
        Guid InstituteId,
        Guid EmployeeId,
        Guid SubmissionId,
        Guid ReportId);
}
