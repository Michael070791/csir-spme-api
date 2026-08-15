using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Domain.Constants;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Leave;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public class EmployeeEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreatePlatformAdminToken(factory));
    }

    [Fact]
    public async Task Employee_List_And_Detail_Return_Institute_And_Current_Employment_Context()
    {
        var employeeId = await SeedEmployeeAsync();

        var list = await _client.GetFromJsonAsync<EmployeePageResponse>(
            "/api/v2/employees?search=STF-CONTEXT&page=1&pageSize=20");

        list.Should().NotBeNull();
        var item = list!.Items.Should().ContainSingle().Subject;
        item.Religion.Should().Be("Islam");
        item.Institute.Should().NotBeNull();
        item.Institute!.Code.Should().Be("FORI");
        item.Institute.Name.Should().Be("Forestry Research Institute");
        item.CurrentEmployment.Should().NotBeNull();
        item.CurrentEmployment!.JobTitle.Should().Be("Principal Research Assistant");
        item.CurrentEmployment.LeadershipRoles.Should().Equal("Head of Division", "Head of Section");
        item.CurrentEmployment.StaffCategory.Should().Be("senior-staff");
        item.CurrentEmployment.GradeStep.Should().Be("SS1");
        item.CurrentEmployment.AreaOfSpecialization.Should().Be("Forest Products");
        item.CurrentEmployment.ServiceStatus.Should().Be("active");
        item.CurrentEmployment.Organization.Should().Be("CSIR");
        item.CurrentEmployment.Location.Should().Be("Kumasi");
        item.CurrentEmployment.Region.Should().Be("Ashanti");
        item.CurrentEmployment.District.Should().Be("Oforikrom");
        item.CurrentEmployment.AppointmentDate.Should().Be(new DateTime(2020, 1, 15));
        item.CurrentEmployment.PromotionDate.Should().Be(new DateTime(2024, 1, 1));

        var detail = await _client.GetFromJsonAsync<EmployeeDetailResponse>($"/api/v2/employees/{employeeId}");

        detail.Should().NotBeNull();
        detail!.Religion.Should().Be("Islam");
        detail!.Institute.Should().NotBeNull();
        detail.Institute!.Code.Should().Be("FORI");
        detail.CurrentEmployment.Should().NotBeNull();
        detail.CurrentEmployment!.JobTitle.Should().Be("Principal Research Assistant");
        detail.CurrentEmployment.LeadershipRoles.Should().Equal("Head of Division", "Head of Section");
        detail.CurrentEmployment.GradeStep.Should().Be("SS1");
        detail.CurrentEmployment.AreaOfSpecialization.Should().Be("Forest Products");
        detail.CurrentEmployment.Organization.Should().Be("CSIR");
        detail.CurrentEmployment.Location.Should().Be("Kumasi");
        detail.CurrentEmployment.Region.Should().Be("Ashanti");
        detail.CurrentEmployment.District.Should().Be("Oforikrom");
        detail.CurrentEmployment.AppointmentDate.Should().Be(new DateTime(2020, 1, 15));
        detail.CurrentEmployment.PromotionDate.Should().Be(new DateTime(2024, 1, 1));
    }

    [Fact]
    public async Task Employee_List_And_Detail_Are_Institute_Scoped_For_Hr()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"SCOPE-A-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"SCOPE-B-{suffix[..8]}");
        var employeeA = await SeedEmployeeInInstituteAsync(instituteA, $"SCOPE-STF-A-{suffix}");
        var employeeB = await SeedEmployeeInInstituteAsync(instituteB, $"SCOPE-STF-B-{suffix}");
        var hr = Client(CreateToken(_factory, SpmeRoles.HrAdmin, instituteA));

        var list = await hr.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=SCOPE-STF&page=1&pageSize=20");
        var inaccessibleDetail = await hr.GetAsync($"/api/v2/employees/{employeeB}");
        var accessibleDetail = await hr.GetAsync($"/api/v2/employees/{employeeA}");

        list.Should().NotBeNull();
        list!.Items.Should().ContainSingle(item => item.Id == employeeA);
        list.Items.Should().NotContain(item => item.Id == employeeB);
        inaccessibleDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        accessibleDetail.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Legacy_Hr_Role_Can_Update_Employees_In_Own_Institute_Only()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"LEGHR-A-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"LEGHR-B-{suffix[..8]}");
        var employeeA = await SeedEmployeeInInstituteAsync(instituteA, $"LEGHR-STF-A-{suffix}");
        var employeeB = await SeedEmployeeInInstituteAsync(instituteB, $"LEGHR-STF-B-{suffix}");
        var legacyHr = Client(CreateToken(_factory, LegacyStaffManagementRoles.HR, instituteA));

        static UpsertEmployeeRequest Request(Guid instituteId, string staffId, string surname, string? otherNames = null, string? jobTitle = null) =>
            new(
                InstituteId: instituteId,
                StaffId: staffId,
                Prefix: null,
                Surname: surname,
                OtherNames: otherNames,
                Gender: "female",
                DateOfBirth: null,
                Nationality: null,
                Religion: null,
                MaritalStatus: null,
                PrimaryEmail: null,
                Phone: null,
                ProfileStatus: "active",
                IsHrApproved: null,
                DivisionId: null,
                SectionId: null,
                GradeId: null,
                JobTitle: jobTitle,
                LeadershipRoles: null,
                StaffCategory: null,
                GradeStep: null,
                AreaOfSpecialization: null,
                ServiceStatus: "active",
                Organization: null,
                Location: null,
                Region: null,
                District: null,
                PensionType: null,
                PensionId: null,
                AppointmentDate: null,
                PromotionDate: null);

        var forbiddenCreate = await Client(CreateToken(_factory, LegacyStaffManagementRoles.Reader, instituteA))
            .PatchAsJsonAsync($"/api/v2/employees/{employeeA}", Request(instituteA, $"LEGHR-STF-A-{suffix}", "Blocked"));
        forbiddenCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var updateOwnInstitute = await legacyHr.PatchAsJsonAsync(
            $"/api/v2/employees/{employeeA}",
            Request(instituteA, $"LEGHR-STF-A-{suffix}", "Mensah", "Ama", "Senior HR Officer"));
        updateOwnInstitute.StatusCode.Should().Be(HttpStatusCode.OK, await updateOwnInstitute.Content.ReadAsStringAsync());
        var updated = await updateOwnInstitute.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        updated.Should().NotBeNull();
        updated!.Surname.Should().Be("Mensah");
        updated.OtherNames.Should().Be("Ama");
        updated.CurrentEmployment!.JobTitle.Should().Be("Senior HR Officer");

        var updateOtherInstitute = await legacyHr.PatchAsJsonAsync(
            $"/api/v2/employees/{employeeB}",
            Request(instituteB, $"LEGHR-STF-B-{suffix}", "Outside"));
        updateOtherInstitute.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlatformAdmin_Can_See_All_Employees_And_Filter_By_Institute()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"PFILTER-A-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"PFILTER-B-{suffix[..8]}");
        var employeeA = await SeedEmployeeInInstituteAsync(instituteA, $"PFILTER-STF-A-{suffix}");
        var employeeB = await SeedEmployeeInInstituteAsync(instituteB, $"PFILTER-STF-B-{suffix}");

        var all = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=PFILTER-STF&page=1&pageSize=20");
        var filtered = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=PFILTER-STF&instituteId={instituteB}&page=1&pageSize=20");

        all.Should().NotBeNull();
        all!.Items.Select(item => item.Id).Should().Contain([employeeA, employeeB]);
        filtered.Should().NotBeNull();
        filtered!.Items.Should().ContainSingle(item => item.Id == employeeB);
        filtered.Items.Should().NotContain(item => item.Id == employeeA);
    }

    [Fact]
    public async Task Employee_List_Filters_By_Division_Section_Status_And_Approval()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"EFILTER-{suffix[..8]}");
        var (divisionA, sectionA) = await SeedDivisionAndSectionAsync(instituteId, $"Admin {suffix[..6]}", $"Records {suffix[..6]}");
        var (divisionB, sectionB) = await SeedDivisionAndSectionAsync(instituteId, $"Research {suffix[..6]}", $"Field {suffix[..6]}");
        var activeApproved = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"EFILTER-A-{suffix}",
            divisionA,
            sectionA,
            profileStatus: "active",
            serviceStatus: "active",
            isHrApproved: true);
        var inactive = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"EFILTER-I-{suffix}",
            divisionA,
            sectionA,
            profileStatus: "inactive",
            serviceStatus: "inactive",
            isHrApproved: true);
        var onLeave = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"EFILTER-L-{suffix}",
            divisionB,
            sectionB,
            profileStatus: "active",
            serviceStatus: "on-leave",
            isHrApproved: false);

        var activeResult = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=EFILTER-&instituteId={instituteId}&divisionId={divisionA}&sectionId={sectionA}&statuses=active&isHrApproved=true&page=1&pageSize=20");
        var onLeaveResult = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=EFILTER-&instituteId={instituteId}&divisionId={divisionB}&statuses=on-leave&page=1&pageSize=20");
        var approvalTabs = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=EFILTER-&instituteId={instituteId}&isHrApproved=false&page=1&pageSize=20");

        activeResult.Should().NotBeNull();
        activeResult!.Items.Should().ContainSingle(item => item.Id == activeApproved);
        activeResult.Items.Should().NotContain(item => item.Id == inactive);
        activeResult.Items.Should().NotContain(item => item.Id == onLeave);
        onLeaveResult.Should().NotBeNull();
        onLeaveResult!.Items.Should().ContainSingle(item => item.Id == onLeave);
        approvalTabs.Should().NotBeNull();
        approvalTabs!.Items.Should().ContainSingle(item => item.Id == onLeave);
        approvalTabs.Total.Should().Be(1);
        approvalTabs.ApprovedTotal.Should().Be(2);
        approvalTabs.UnapprovedTotal.Should().Be(1);
    }

    [Fact]
    public async Task Employee_List_Filters_By_IsHod_And_Reports_HodTotal()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"EHOD-{suffix[..8]}");
        var (division, section) = await SeedDivisionAndSectionAsync(instituteId, $"Ops {suffix[..6]}", $"Team {suffix[..6]}");
        var hod = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"EHOD-H-{suffix}",
            division,
            section,
            profileStatus: "active",
            serviceStatus: "active",
            isHrApproved: true,
            leadershipRoles: "Head of Division");
        var member = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"EHOD-M-{suffix}",
            division,
            section,
            profileStatus: "active",
            serviceStatus: "active",
            isHrApproved: true,
            leadershipRoles: null);

        var hodResult = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=EHOD-&instituteId={instituteId}&isHod=true&page=1&pageSize=20");
        var allResult = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=EHOD-&instituteId={instituteId}&page=1&pageSize=20");

        hodResult.Should().NotBeNull();
        hodResult!.Items.Should().ContainSingle(item => item.Id == hod);
        hodResult.Items.Should().NotContain(item => item.Id == member);
        hodResult.Total.Should().Be(1);
        hodResult.HodTotal.Should().Be(1);
        allResult.Should().NotBeNull();
        allResult!.Items.Select(item => item.Id).Should().Contain([hod, member]);
        allResult.HodTotal.Should().Be(1);
        allResult.Items.Single(item => item.Id == hod).CurrentEmployment!.LeadershipRoles
            .Should().Contain("Head of Division");
    }

    [Fact]
    public async Task Employee_List_Includes_Current_Year_Annual_Leave_Remaining_Days()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"ELEAVE-{suffix[..8]}");
        var (division, section) = await SeedDivisionAndSectionAsync(instituteId, $"Leave {suffix[..6]}", $"Team {suffix[..6]}");
        var withBalance = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"ELEAVE-B-{suffix}",
            division,
            section,
            profileStatus: "active",
            serviceStatus: "active",
            isHrApproved: true);
        var withoutBalance = await SeedEmployeeWithEmploymentAsync(
            instituteId,
            $"ELEAVE-Z-{suffix}",
            division,
            section,
            profileStatus: "active",
            serviceStatus: "active",
            isHrApproved: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            db.LeaveBalances.Add(LeaveBalance.CreateImported(
                withBalance,
                LeaveTypes.Annual,
                (short)DateTime.UtcNow.Year,
                32m,
                8m,
                2m,
                0m));
            await db.SaveChangesAsync();
        }

        var list = await _client.GetFromJsonAsync<EmployeePageResponse>(
            $"/api/v2/employees?search=ELEAVE-&instituteId={instituteId}&page=1&pageSize=20");

        list.Should().NotBeNull();
        list!.Items.Single(item => item.Id == withBalance).RemainingAnnualLeaveDays.Should().Be(22m);
        list.Items.Single(item => item.Id == withoutBalance).RemainingAnnualLeaveDays.Should().Be(0m);
    }

    [Fact]
    public async Task Employee_Profile_Image_Can_Be_Uploaded_And_Read()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"EIMAGE-{suffix[..8]}");
        var employeeId = await SeedEmployeeInInstituteAsync(instituteId, $"EIMAGE-STF-{suffix}");
        using var content = new MultipartFormDataContent();
        var imageBytes = ValidPngBytes();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "file", "profile.png");

        var uploadResponse = await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var employee = await uploadResponse.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        employee.Should().NotBeNull();
        employee!.ProfileImageFileId.Should().NotBeNull();
        employee.ProfileImage.Should().NotBeNull();
        employee.ProfileImage!.ContentType.Should().Be("image/webp");
        employee.ProfileImage.Url.Should().Be($"/api/v2/employees/{employeeId}/profile-image");

        var access = await _client.GetFromJsonAsync<ProfileImageAccessResponse>($"/api/v2/employees/{employeeId}/profile-image/access");
        access.Should().NotBeNull();
        access!.ContentType.Should().Be("image/webp");
        access.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(4));
        access.Url.Should().Contain("sp=r");

        var redirectClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        redirectClient.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        var readResponse = await redirectClient.GetAsync($"/api/v2/employees/{employeeId}/profile-image");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        var bytes = await readResponse.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Employee_Profile_Image_Access_Is_Self_And_Institute_Scoped()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteA = await SeedInstituteAsync($"IMAGE-A-{suffix[..8]}");
        var instituteB = await SeedInstituteAsync($"IMAGE-B-{suffix[..8]}");
        var employeeA = await SeedEmployeeInInstituteAsync(instituteA, $"IMAGE-STF-A-{suffix}");
        var employeeA2 = await SeedEmployeeInInstituteAsync(instituteA, $"IMAGE-STF-A2-{suffix}");
        var employeeB = await SeedEmployeeInInstituteAsync(instituteB, $"IMAGE-STF-B-{suffix}");
        var ownClient = Client(CreateToken(_factory, SpmeRoles.Employee, instituteA, employeeA));

        using (var ownImage = ProfileImageContent())
        {
            (await ownClient.PostAsync($"/api/v2/employees/{employeeA}/profile-image", ownImage))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var anonymous = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await anonymous.GetAsync($"/api/v2/employees/{employeeA}/profile-image")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using (var otherImage = ProfileImageContent())
        {
            (await ownClient.PostAsync($"/api/v2/employees/{employeeA2}/profile-image", otherImage))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var sameInstituteReader = Client(CreateToken(_factory, SpmeRoles.Employee, instituteA, employeeA2), allowAutoRedirect: false);
        (await sameInstituteReader.GetAsync($"/api/v2/employees/{employeeA}/profile-image"))
            .StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);

        var otherInstituteReader = Client(CreateToken(_factory, SpmeRoles.Employee, instituteB, employeeB), allowAutoRedirect: false);
        (await otherInstituteReader.GetAsync($"/api/v2/employees/{employeeA}/profile-image"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var otherHr = Client(CreateToken(_factory, SpmeRoles.HrAdmin, instituteB), allowAutoRedirect: false);
        (await otherHr.GetAsync($"/api/v2/employees/{employeeA}/profile-image/access"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Employee_Profile_Image_Rejects_Unsupported_Malformed_And_Oversized_Files()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"IMAGE-VALIDATE-{suffix[..8]}");
        var employeeId = await SeedEmployeeInInstituteAsync(instituteId, $"IMAGE-VALIDATE-STF-{suffix}");

        using var unsupported = ProfileImageContent([1, 2, 3], "text/plain", "profile.txt");
        (await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", unsupported))
            .StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        using var malformed = ProfileImageContent([1, 2, 3, 4], "image/png", "profile.png");
        (await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", malformed))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var mismatched = ProfileImageContent(ValidPngBytes(), "image/jpeg", "profile.jpg");
        (await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", mismatched))
            .StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        using var oversized = ProfileImageContent(new byte[(5 * 1024 * 1024) + 1], "image/png", "profile.png");
        (await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", oversized))
            .StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Employee_Profile_Image_Replacement_Normalizes_And_Erases_Previous_Blob()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"IMAGE-REPLACE-{suffix[..8]}");
        var staffId = $"IMAGE-REPLACE-STF-{suffix}";
        var employeeId = await SeedEmployeeInInstituteAsync(instituteId, staffId);

        using var firstContent = ProfileImageContent(ValidPngBytes(), "image/png", "../../staff-name.png");
        var firstResponse = await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", firstContent);
        firstResponse.EnsureSuccessStatusCode();
        var firstEmployee = await firstResponse.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        var firstFileId = firstEmployee!.ProfileImageFileId!.Value;

        string firstStorageKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var firstFile = await db.FileRecords.FindAsync(firstFileId);
            firstFile.Should().NotBeNull();
            firstFile!.OriginalFileName.Should().Be("profile.webp");
            firstFile.ContentType.Should().Be("image/webp");
            firstFile.SizeBytes.Should().BeLessThanOrEqualTo(256 * 1024);
            firstFile.StorageKey.Should().MatchRegex(
                $"^employee-profile-images/{instituteId:N}/[0-9]{{4}}/[0-9]{{2}}/[0-9a-f]{{32}}\\.webp$");
            firstFile.StorageKey.Should().NotContain(staffId).And.NotContain("..");
            firstStorageKey = firstFile.StorageKey;

            await using var normalized = await storage.DownloadAsync(firstStorageKey);
            normalized.Should().NotBeNull();
            using var buffer = new MemoryStream();
            await normalized!.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
            Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP");
        }

        using var replacementContent = ProfileImageContent();
        var replacementResponse = await _client.PostAsync($"/api/v2/employees/{employeeId}/profile-image", replacementContent);
        replacementResponse.EnsureSuccessStatusCode();
        var replacementEmployee = await replacementResponse.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        replacementEmployee!.ProfileImageFileId.Should().NotBe(firstFileId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var firstFile = await db.FileRecords.FindAsync(firstFileId);
            firstFile!.IsDeleted.Should().BeTrue();
            firstFile.DeletedAt.Should().NotBeNull();
            firstFile.StorageDeletedAt.Should().NotBeNull();
            (await storage.ExistsAsync(firstStorageKey)).Should().BeFalse();
            (await storage.ExistsAsync((await db.FileRecords.FindAsync(replacementEmployee.ProfileImageFileId))!.StorageKey))
                .Should().BeTrue();
        }
    }

    [Fact]
    public async Task Employee_Create_And_Update_Persist_Profile_And_Current_Employment()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var instituteId = await SeedInstituteAsync($"WRITE-{suffix[..8]}");
        var (divisionId, sectionId) = await SeedDivisionAndSectionAsync(instituteId, $"People Ops {suffix[..6]}", $"Records {suffix[..6]}");
        var staffId = $"WRITE-STF-{suffix}";

        var createRequest = new UpsertEmployeeRequest(
            InstituteId: instituteId,
            StaffId: staffId,
            Prefix: "Dr.",
            Surname: "Boateng",
            OtherNames: "Akua",
            Gender: "female",
            DateOfBirth: new DateTime(1990, 3, 12),
            Nationality: "Ghanaian",
            Religion: "Christianity",
            MaritalStatus: "married",
            PrimaryEmail: $"{staffId.ToLowerInvariant()}@example.test",
            Phone: "0240009999",
            ProfileStatus: "active",
            IsHrApproved: true,
            DivisionId: divisionId,
            SectionId: sectionId,
            GradeId: null,
            JobTitle: "Research Officer",
            LeadershipRoles: ["Institute Director", "Head of Division"],
            StaffCategory: "senior-staff",
            GradeStep: "S1",
            AreaOfSpecialization: "Plant genetics",
            ServiceStatus: "active",
            Organization: "CSIR",
            Location: "Kumasi",
            Region: "Ashanti",
            District: "Oforikrom",
            PensionType: "SSNIT",
            PensionId: "PEN-001",
            AppointmentDate: new DateTime(2021, 5, 4),
            PromotionDate: new DateTime(2025, 1, 1));

        var createResponse = await _client.PostAsJsonAsync("/api/v2/employees", createRequest);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        created.Should().NotBeNull();
        created!.StaffId.Should().Be(staffId);
        created.Surname.Should().Be("Boateng");
        created.CurrentEmployment.Should().NotBeNull();
        created.CurrentEmployment!.DivisionId.Should().Be(divisionId);
        created.CurrentEmployment.SectionId.Should().Be(sectionId);
        created.CurrentEmployment.JobTitle.Should().Be("Research Officer");
        created.CurrentEmployment.LeadershipRoles.Should().Equal("Institute Director", "Head of Division");
        created.CurrentEmployment.AreaOfSpecialization.Should().Be("Plant genetics");
        created.CurrentEmployment.PensionType.Should().Be("SSNIT");

        var updateRequest = createRequest with
        {
            Surname = "Owusu-Boateng",
            JobTitle = "Senior Research Officer",
            LeadershipRoles = ["Deputy Director"],
            StaffCategory = "senior-member",
            GradeStep = "SM2",
            AreaOfSpecialization = "Applied biotechnology",
            Location = "Accra",
            PromotionDate = new DateTime(2026, 1, 1)
        };
        using var updateMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/v2/employees/{created.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };

        var updateResponse = await _client.SendAsync(updateMessage);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<EmployeeDetailResponse>();
        updated.Should().NotBeNull();
        updated!.Surname.Should().Be("Owusu-Boateng");
        updated.CurrentEmployment.Should().NotBeNull();
        updated.CurrentEmployment!.JobTitle.Should().Be("Senior Research Officer");
        updated.CurrentEmployment.LeadershipRoles.Should().Equal("Deputy Director");
        updated.CurrentEmployment.StaffCategory.Should().Be("senior-member");
        updated.CurrentEmployment.GradeStep.Should().Be("SM2");
        updated.CurrentEmployment.AreaOfSpecialization.Should().Be("Applied biotechnology");
        updated.CurrentEmployment.Location.Should().Be("Accra");
        updated.CurrentEmployment.PromotionDate.Should().Be(new DateTime(2026, 1, 1));
    }

    private async Task<Guid> SeedEmployeeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        var institute = db.Institutes.SingleOrDefault(existing => existing.Code == "FORI");
        if (institute is null)
        {
            institute = new Institute("FORI", "Forestry Research Institute", "Institute");
            db.Institutes.Add(institute);
            await db.SaveChangesAsync();
        }

        var employee = new Employee(institute.Id, $"STF-CONTEXT-{Guid.NewGuid():N}", "Adu", "male");
        employee.UpdateImportedProfile(
            "Mr.",
            "Kwame",
            new DateTime(1988, 2, 4),
            "Ghanaian",
            "Islam",
            "married",
            "kwame.adu@example.test",
            "0240001111",
            true);

        db.Employees.Add(employee);
        db.EmploymentRecords.Add(new EmploymentRecord(
            employee.Id,
            institute.Id,
            null,
            null,
            null,
            null,
            "Principal Research Assistant",
            "Head of Division, Head of Section",
            "senior-staff",
            "SS1",
            "Forest Products",
            "active",
            "CSIR",
            "Kumasi",
            "Ashanti",
            "Oforikrom",
            new DateTime(2020, 1, 15),
            new DateTime(2024, 1, 1),
            null,
            null,
            new DateTime(2024, 1, 1),
            true));
        await db.SaveChangesAsync();

        return employee.Id;
    }

    private async Task<Guid> SeedInstituteAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var institute = new Institute(code, $"Test Institute {code}", "Institute");
        db.Institutes.Add(institute);
        await db.SaveChangesAsync();
        return institute.Id;
    }

    private async Task<Guid> SeedEmployeeInInstituteAsync(Guid instituteId, string staffId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = new Employee(instituteId, staffId, $"Surname-{staffId[^8..]}", "male");
        employee.UpdateImportedProfile(
            "Mr.",
            "Scoped",
            null,
            "Ghanaian",
            null,
            null,
            $"{staffId.ToLowerInvariant()}@example.test",
            "0240003333",
            true);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<Guid> SeedEmployeeWithEmploymentAsync(
        Guid instituteId,
        string staffId,
        Guid divisionId,
        Guid sectionId,
        string profileStatus,
        string serviceStatus,
        bool isHrApproved,
        string? leadershipRoles = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var employee = new Employee(instituteId, staffId, $"Surname-{staffId[^8..]}", "male");
        employee.UpdateProfile(
            staffId,
            "Mr.",
            employee.Surname,
            "Filtered",
            "male",
            null,
            "Ghanaian",
            null,
            null,
            $"{staffId.ToLowerInvariant()}@example.test",
            "0240004444",
            profileStatus,
            isHrApproved);
        db.Employees.Add(employee);
        db.EmploymentRecords.Add(new EmploymentRecord(
            employee.Id,
            instituteId,
            divisionId,
            sectionId,
            null,
            null,
            "Administrative Officer",
            leadershipRoles,
            "senior-staff",
            null,
            null,
            serviceStatus,
            "CSIR",
            "Accra",
            "Greater Accra",
            "Accra Metropolitan",
            new DateTime(2022, 1, 1),
            null,
            null,
            null,
            new DateTime(2024, 1, 1),
            true));
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<(Guid DivisionId, Guid SectionId)> SeedDivisionAndSectionAsync(Guid instituteId, string divisionName, string sectionName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();

        var division = new Division(instituteId, divisionName);
        var section = new Section(division.Id, sectionName);
        db.Divisions.Add(division);
        db.Sections.Add(section);
        await db.SaveChangesAsync();

        return (division.Id, section.Id);
    }

    private HttpClient Client(string token, bool allowAutoRedirect = true)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CreatePlatformAdminToken(SpmeApiFactory factory)
    {
        return CreateToken(factory, SpmeRoles.PlatformAdmin, null);
    }

    private static string CreateToken(SpmeApiFactory factory, string role, Guid? instituteId, Guid? employeeId = null)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var user = new User($"integration.{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.test", role);
        if (employeeId.HasValue && instituteId.HasValue)
            user.LinkEmployee(employeeId.Value, instituteId.Value);
        else if (instituteId.HasValue)
            user.AssignInstitute(instituteId.Value, role);
        db.Users.Add(user);
        db.SaveChanges();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Role, role),
            new Claim("identity_type", role),
            new Claim("security_stamp", user.SecurityStamp!)
        };
        var scopedClaims = claims.ToList();
        if (instituteId.HasValue)
        {
            scopedClaims.Add(new Claim("institute_id", instituteId.Value.ToString()));
        }
        if (employeeId.HasValue)
        {
            scopedClaims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            scopedClaims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static byte[] ValidPngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static MultipartFormDataContent ProfileImageContent() =>
        ProfileImageContent(ValidPngBytes(), "image/png", "profile.png");

    private static MultipartFormDataContent ProfileImageContent(byte[] bytes, string contentType, string fileName)
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(image, "file", fileName);
        return content;
    }
}
