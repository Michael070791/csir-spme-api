using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class HrOrganizationCommunicationTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public HrOrganizationCommunicationTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Organization_requires_authentication_and_keeps_hr_institute_scoped()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/v2/divisions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (instituteA, instituteB) = await SeedInstitutesAsync();
        var divisionB = new Division(instituteB, $"Other Division {Guid.NewGuid():N}");
        await AddAsync(divisionB);
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var create = await hr.PostAsJsonAsync("/api/v2/divisions", new CreateDivisionRequest("People Services", "PS", instituteB));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<DivisionResponse>();
        created!.InstituteId.Should().Be(instituteA);

        var list = await hr.GetFromJsonAsync<CollectionResponse<DivisionResponse>>("/api/v2/divisions");
        list!.Items.Should().OnlyContain(item => item.InstituteId == instituteA);
        list.Items.Should().NotContain(item => item.Id == divisionB.Id);

        var crossInstituteList = await hr.GetAsync($"/api/v2/divisions?instituteId={instituteB}");
        crossInstituteList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var institutes = await hr.GetFromJsonAsync<CollectionResponse<InstituteResponse>>("/api/v2/institutes");
        institutes!.Items.Should().ContainSingle(item => item.Id == instituteA);
    }

    [Fact]
    public async Task Organization_allows_platform_admin_to_select_institute_scope()
    {
        var (instituteA, instituteB) = await SeedInstitutesAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var divisionA = new Division(instituteA, $"Platform Division A {Guid.NewGuid():N}");
        var divisionB = new Division(instituteB, $"Platform Division B {Guid.NewGuid():N}");
        await AddAsync(divisionA);
        await AddAsync(divisionB);

        var platform = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var platformInstitutes = await platform.GetFromJsonAsync<CollectionResponse<InstituteResponse>>("/api/v2/institutes");
        platformInstitutes!.Items.Should().Contain(item => item.Id == instituteA);
        platformInstitutes.Items.Should().Contain(item => item.Id == instituteB);

        var list = await platform.GetFromJsonAsync<CollectionResponse<DivisionResponse>>($"/api/v2/divisions?instituteId={instituteA}");
        list!.Items.Should().Contain(item => item.Id == divisionA.Id);
        list.Items.Should().NotContain(item => item.Id == divisionB.Id);
        list.Items.Should().OnlyContain(item => item.InstituteId == instituteA);

        var createdDivisionResponse = await platform.PostAsJsonAsync(
            "/api/v2/divisions",
            new CreateDivisionRequest($"Selected Institute Division {suffix}", $"SID{suffix[..4]}", instituteB));
        createdDivisionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdDivision = await createdDivisionResponse.Content.ReadFromJsonAsync<DivisionResponse>();
        createdDivision!.InstituteId.Should().Be(instituteB);

        var createdSectionResponse = await platform.PostAsJsonAsync(
            "/api/v2/sections",
            new CreateSectionRequest(createdDivision.Id, $"Selected Institute Section {suffix}", $"SIS{suffix[..4]}"));
        createdSectionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdSection = await createdSectionResponse.Content.ReadFromJsonAsync<SectionResponse>();
        createdSection!.DivisionId.Should().Be(createdDivision.Id);

        var platformSections = await platform.GetFromJsonAsync<CollectionResponse<SectionResponse>>($"/api/v2/sections?divisionId={createdDivision.Id}");
        platformSections!.Items.Should().Contain(item => item.Id == createdSection.Id);

        var createWithoutSelection = await platform.PostAsJsonAsync("/api/v2/divisions", new CreateDivisionRequest("Unscoped Division", "UD", null));
        createWithoutSelection.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var scopedPlatform = Client(CreateToken(SpmeRoles.PlatformAdmin, instituteA));
        var scopedPlatformInstitutes = await scopedPlatform.GetFromJsonAsync<CollectionResponse<InstituteResponse>>("/api/v2/institutes");
        scopedPlatformInstitutes!.Items.Should().Contain(item => item.Id == instituteA);
        scopedPlatformInstitutes.Items.Should().Contain(item => item.Id == instituteB);

        var scopedList = await scopedPlatform.GetFromJsonAsync<CollectionResponse<DivisionResponse>>($"/api/v2/divisions?instituteId={instituteB}");
        scopedList!.Items.Should().NotContain(item => item.Id == divisionA.Id);
        scopedList.Items.Should().Contain(item => item.Id == divisionB.Id);
        scopedList.Items.Should().OnlyContain(item => item.InstituteId == instituteB);

        var scopedGetDivision = await scopedPlatform.GetFromJsonAsync<DivisionResponse>($"/api/v2/divisions/{divisionB.Id}");
        scopedGetDivision!.InstituteId.Should().Be(instituteB);

        var scopedCreatedDivisionResponse = await scopedPlatform.PostAsJsonAsync(
            "/api/v2/divisions",
            new CreateDivisionRequest($"Scoped Platform Division {suffix}", $"SPD{suffix[..4]}", instituteB));
        scopedCreatedDivisionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var scopedCreatedDivision = await scopedCreatedDivisionResponse.Content.ReadFromJsonAsync<DivisionResponse>();
        scopedCreatedDivision!.InstituteId.Should().Be(instituteB);

        var scopedCreatedSectionResponse = await scopedPlatform.PostAsJsonAsync(
            "/api/v2/sections",
            new CreateSectionRequest(divisionB.Id, $"Scoped Platform Section {suffix}", $"SPS{suffix[..4]}"));
        scopedCreatedSectionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var scopedCreatedSection = await scopedCreatedSectionResponse.Content.ReadFromJsonAsync<SectionResponse>();
        scopedCreatedSection!.DivisionId.Should().Be(divisionB.Id);

        var scopedDivisionSections = await scopedPlatform.GetFromJsonAsync<CollectionResponse<SectionResponse>>($"/api/v2/divisions/{divisionB.Id}/sections");
        scopedDivisionSections!.Items.Should().Contain(item => item.Id == scopedCreatedSection.Id);

        var scopedGetSection = await scopedPlatform.GetFromJsonAsync<SectionResponse>($"/api/v2/sections/{scopedCreatedSection.Id}");
        scopedGetSection!.DivisionId.Should().Be(divisionB.Id);

        var scopedUpdateSection = await scopedPlatform.PatchAsJsonAsync(
            $"/api/v2/sections/{scopedCreatedSection.Id}",
            new UpdateSectionRequest($"Scoped Platform Section Updated {suffix}", $"SPU{suffix[..4]}", true));
        scopedUpdateSection.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Section_is_linked_to_division_and_memo_lifecycle_is_scoped()
    {
        var (instituteA, instituteB) = await SeedInstitutesAsync();
        var division = new Division(instituteA, $"Research {Guid.NewGuid():N}");
        await AddAsync(division);
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var sectionResponse = await hr.PostAsJsonAsync("/api/v2/sections", new CreateSectionRequest(division.Id, "Field Operations", "FO"));
        sectionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var section = await sectionResponse.Content.ReadFromJsonAsync<SectionResponse>();
        section!.DivisionId.Should().Be(division.Id);

        var memoResponse = await hr.PostAsJsonAsync("/api/v2/memos", new CreateMemoRequest("Safety update", "Please review the latest safety update.", null));
        memoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var memo = await memoResponse.Content.ReadFromJsonAsync<MemoResponse>();
        memo!.Audiences.Should().ContainSingle().Which.AudienceType.Should().Be("all-employees");

        var publish = await hr.PostAsync($"/api/v2/memos/{memo.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        var otherHr = Client(CreateToken(SpmeRoles.HrAdmin, instituteB));
        (await otherHr.GetAsync($"/api/v2/memos/{memo.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holiday_dates_are_returned_only_for_the_effective_institute_scope()
    {
        var (instituteA, instituteB) = await SeedInstitutesAsync();
        var platform = Client(CreateToken(SpmeRoles.PlatformAdmin, null));
        var wide = await platform.PostAsJsonAsync("/api/v2/holidays", new CreateHolidayRequest("csir-wide", null, $"CSIR Day {Guid.NewGuid():N}", DateTime.UtcNow.Date.AddDays(40)));
        wide.StatusCode.Should().Be(HttpStatusCode.Created);
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));
        var local = await hr.PostAsJsonAsync("/api/v2/holidays", new CreateHolidayRequest("institute", instituteB, $"Other Holiday {Guid.NewGuid():N}", DateTime.UtcNow.Date.AddDays(41)));
        local.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var own = await hr.PostAsJsonAsync("/api/v2/holidays", new CreateHolidayRequest("institute", instituteA, $"Local Holiday {Guid.NewGuid():N}", DateTime.UtcNow.Date.AddDays(42)));
        own.StatusCode.Should().Be(HttpStatusCode.Created);
        var items = await hr.GetFromJsonAsync<CollectionResponse<HolidayResponse>>("/api/v2/holidays");
        items!.Items.Should().Contain(item => item.ScopeType == "csir-wide");
        items.Items.Should().NotContain(item => item.InstituteId == instituteB);
    }

    [Fact]
    public async Task Leave_type_catalog_is_authenticated_structured_and_filterable()
    {
        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/v2/leave-types")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (instituteA, _) = await SeedInstitutesAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));
        var all = await hr.GetFromJsonAsync<ListResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types");
        all!.Data.Should().HaveCount(10);
        all.Data.Should().Contain(item => item.Code == "annual" && item.Entitlement.MaximumDuration == 42m);
        all.Data.Should().Contain(item => item.Code == "study" && item.IsRequestable == false);
        all.Data.Should().Contain(item => item.Code == "resettlement" && item.Unit == "calendar-days");
        all.Data.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.RequestWindow.AdvanceNotice.Status));
        all.Data.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.RequestWindow.AdvanceNotice.Requirement));

        var female = await hr.GetFromJsonAsync<ListResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types?gender=female");
        female!.Data.Should().Contain(item => item.Code == "maternity");
        female.Data.Should().NotContain(item => item.Code == "paternity");

        var malePending = await hr.GetFromJsonAsync<ListResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types?gender=male&policyStatus=policy-source-pending");
        malePending!.Data.Should().ContainSingle(item => item.Code == "paternity");
    }

    [Fact]
    public async Task Leave_type_catalog_returns_maternity_paternity_and_unknown_correctly()
    {
        var (instituteA, _) = await SeedInstitutesAsync();
        var hr = Client(CreateToken(SpmeRoles.HrAdmin, instituteA));

        var maternity = await hr.GetFromJsonAsync<DataResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types/maternity");
        maternity!.Data.Eligibility.AllowedGenders.Should().ContainSingle().Which.Should().Be("female");
        maternity.Data.Deduction.DeductsFromAnnualLeave.Should().BeFalse();
        maternity.Data.Entitlement.MaximumDuration.Should().Be(3m);
        maternity.Data.Unit.Should().Be("months");
        maternity.Data.RequestWindow.EarliestRequestTiming.Should().Contain("six weeks");
        maternity.Data.RequestWindow.AdvanceNotice.Status.Should().Be("not-specified");
        maternity.Data.RequiredDocuments.Should().Contain(document => document.Contains("recognized medical officer", StringComparison.OrdinalIgnoreCase));

        var casual = await hr.GetFromJsonAsync<DataResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types/casual");
        casual!.Data.Code.Should().Be("part");
        casual.Data.RequestWindow.AdvanceNotice.Status.Should().Be("not-specified");
        casual.Data.RequestWindow.AdvanceNotice.Requirement.Should().Contain("Written permission");
        casual.Data.RequestWindow.AdvanceNotice.Exception.Should().Contain("emergency");

        var leaveWithoutPay = await hr.GetFromJsonAsync<DataResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types/leave-of-absence");
        leaveWithoutPay!.Data.RequestWindow.AdvanceNotice.MinimumDuration.Should().Be(3m);
        leaveWithoutPay.Data.RequestWindow.AdvanceNotice.Unit.Should().Be("months");
        leaveWithoutPay.Data.RequestWindow.AdvanceNotice.Status.Should().Be("specified");

        var paternity = await hr.GetFromJsonAsync<DataResponse<LeaveTypeMetadataResponse>>("/api/v2/leave-types/paternity");
        paternity!.Data.PolicyStatus.Should().Be("policy-source-pending");
        paternity.Data.IsRequestable.Should().BeFalse();
        paternity.Data.Eligibility.AllowedGenders.Should().ContainSingle().Which.Should().Be("male");

        var unknown = await hr.GetAsync("/api/v2/leave-types/unknown");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Working_days_exclude_weekends_and_holidays_but_not_other_institute_holidays()
    {
        var holidays = new[] { new DateTime(2026, 8, 4), new DateTime(2026, 8, 5) };
        WorkingDaysCalculator.Calculate(new DateTime(2026, 8, 3), new DateTime(2026, 8, 7), holidays).Should().Be(3m);
        WorkingDaysCalculator.Calculate(new DateTime(2026, 8, 8), new DateTime(2026, 8, 9), holidays).Should().Be(0m);
        // Inclusive leave days Wed 12 Aug – Thu 20 Aug = 7; return to duty is Fri 21 Aug.
        WorkingDaysCalculator.Calculate(new DateTime(2026, 8, 12), new DateTime(2026, 8, 20), []).Should().Be(7m);
        WorkingDaysCalculator.ExpectedReturnDate(new DateTime(2026, 8, 20), []).Should().Be(new DateTime(2026, 8, 21));
        // If Friday after leave is a holiday, return slips to the next working day.
        WorkingDaysCalculator.ExpectedReturnDate(
            new DateTime(2026, 8, 20),
            [new DateTime(2026, 8, 21)]).Should().Be(new DateTime(2026, 8, 24));
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid A, Guid B)> SeedInstitutesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var a = new Institute($"TEST-A-{suffix}", $"Test Institute A {suffix}", "Institute");
        var b = new Institute($"TEST-B-{suffix}", $"Test Institute B {suffix}", "Institute");
        db.Institutes.AddRange(a, b);
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private async Task AddAsync<T>(T entity) where T : class
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
    }

    private string CreateToken(string role, Guid? instituteId)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var section = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, $"integration.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
        if (instituteId.HasValue) claims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section.GetValue<string>("Key")!)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            section.GetValue<string>("Issuer") ?? "csir-spme-api",
            section.GetValue<string>("Audience") ?? "csir-spme-client",
            claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials));
    }
}
