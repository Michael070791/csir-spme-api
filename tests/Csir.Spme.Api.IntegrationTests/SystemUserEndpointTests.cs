using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class SystemUserEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public SystemUserEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PlatformAdmin_Can_List_And_Get_System_Users_Through_Primary_And_Legacy_Routes()
    {
        var seed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var primary = await client.GetFromJsonAsync<PageResponse<SystemUserResponse>>(
            $"/api/v2/system-users?search={seed.SearchToken}&page=1&pageSize=20");
        var legacy = await client.GetFromJsonAsync<PageResponse<SystemUserResponse>>(
            $"/api/v2/users?search={seed.SearchToken}&page=1&pageSize=20");

        primary.Should().NotBeNull();
        legacy.Should().NotBeNull();
        primary!.Items.Select(user => user.Email).Should().BeEquivalentTo(legacy!.Items.Select(user => user.Email));
        primary.Items.Should().NotContain(user => user.Id == seed.EmployeeUserId);

        var staffAccount = primary.Items.Should()
            .ContainSingle(user => user.Id == seed.StaffUserId)
            .Subject;
        staffAccount.IdentityType.Should().Be("StaffUser");
        staffAccount.EmployeeId.Should().BeNull();
        staffAccount.Roles.Should().Contain([SpmeRoles.HrAdmin, "Reader"]);
        staffAccount.Institute.Should().NotBeNull();
        staffAccount.Institute!.Code.Should().Be(seed.InstituteCode);

        var detail = await client.GetFromJsonAsync<SystemUserResponse>($"/api/v2/system-users/{seed.StaffUserId}");
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(seed.StaffUserId);
        detail.Roles.Should().Contain("Reader");

        var hiddenEmployeeDetail = await client.GetAsync($"/api/v2/system-users/{seed.EmployeeUserId}");
        hiddenEmployeeDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IncludeEmployees_Compatibility_Flag_Can_Return_Employee_Accounts()
    {
        var seed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var primary = await client.GetFromJsonAsync<PageResponse<SystemUserResponse>>(
            $"/api/v2/system-users?search={seed.SearchToken}&includeEmployees=true&page=1&pageSize=20");
        primary.Should().NotBeNull();

        var employeeAccount = primary!.Items.Should()
            .ContainSingle(user => user.Id == seed.EmployeeUserId)
            .Subject;
        employeeAccount.IdentityType.Should().Be("Employee");
        employeeAccount.EmployeeId.Should().Be(seed.EmployeeId);
        employeeAccount.InstituteId.Should().Be(seed.InstituteId);
        employeeAccount.Roles.Should().Contain(SpmeRoles.Employee);
        employeeAccount.Employee.Should().NotBeNull();
        employeeAccount.Employee!.StaffId.Should().Be(seed.StaffId);

        var detail = await client.GetFromJsonAsync<SystemUserResponse>($"/api/v2/system-users/{seed.EmployeeUserId}?includeEmployees=true");
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(seed.EmployeeUserId);
        detail.Roles.Should().Contain(SpmeRoles.Employee);
    }

    [Fact]
    public async Task Role_Filter_And_Update_Use_Existing_NonEmployee_Roles()
    {
        var seed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var filtered = await client.GetFromJsonAsync<PageResponse<SystemUserResponse>>(
            $"/api/v2/system-users?search={seed.SearchToken}&role=Reader&page=1&pageSize=20");
        filtered.Should().NotBeNull();
        filtered!.Items.Should().ContainSingle(user => user.Id == seed.StaffUserId);
        filtered.Items.Should().NotContain(user => user.Id == seed.EmployeeUserId);

        var sessionId = Guid.NewGuid();
        string originalSecurityStamp;
        await using (var sessionScope = _factory.Services.CreateAsyncScope())
        {
            var sessionDb = sessionScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var staffUser = await sessionDb.Users.SingleAsync(user => user.Id == seed.StaffUserId);
            originalSecurityStamp = staffUser.SecurityStamp!;
            sessionDb.UserSessions.Add(new UserSession(sessionId, staffUser.Id, "Role test", "integration", DateTimeOffset.UtcNow));
            sessionDb.RefreshTokens.Add(new RefreshToken(staffUser.Id, Guid.NewGuid().ToString("N"),
                Guid.NewGuid(), sessionId, originalSecurityStamp, DateTimeOffset.UtcNow.AddHours(1)));
            await sessionDb.SaveChangesAsync();
        }

        var update = await client.PatchAsJsonAsync($"/api/v2/system-users/{seed.StaffUserId}/roles", new
        {
            roles = new[] { "Reader", "Writer", SpmeRoles.HrAdmin }
        });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<SystemUserResponse>();
        updated.Should().NotBeNull();
        updated!.Roles.Should().BeEquivalentTo(["HrAdmin", "Reader", "Writer"]);

        await using (var verificationScope = _factory.Services.CreateAsyncScope())
        {
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            (await verificationDb.Users.Where(user => user.Id == seed.StaffUserId)
                .Select(user => user.SecurityStamp).SingleAsync()).Should().NotBe(originalSecurityStamp);
            (await verificationDb.UserSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId))
                .RevokedAt.Should().NotBeNull();
            var revokedToken = await verificationDb.RefreshTokens.AsNoTracking()
                .SingleAsync(token => token.SessionId == sessionId);
            revokedToken.RevokedAt.Should().NotBeNull();
            revokedToken.RevocationReason.Should().Be("roles-changed");
        }

        var unknownRole = await client.PatchAsJsonAsync($"/api/v2/system-users/{seed.StaffUserId}/roles", new
        {
            roles = new[] { "UnknownRole" }
        });
        unknownRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var employeeRole = await client.PatchAsJsonAsync($"/api/v2/system-users/{seed.StaffUserId}/roles", new
        {
            roles = new[] { SpmeRoles.Employee }
        });
        employeeRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Delete_Blocks_Self_LastPlatformAdmin_And_Employee_Account_Delete()
    {
        var seed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var employeeDelete = await client.DeleteAsync($"/api/v2/system-users/{seed.EmployeeUserId}");
        employeeDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await KeepOnlyPlatformAdminRoleAsync(seed.PlatformAdminUserId);
        var lastPlatformDelete = await client.DeleteAsync($"/api/v2/system-users/{seed.PlatformAdminUserId}");
        lastPlatformDelete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var selfClient = Client(CreateToken(SpmeRoles.PlatformAdmin, null, seed.StaffUserId));
        var selfDelete = await selfClient.DeleteAsync($"/api/v2/system-users/{seed.StaffUserId}");
        selfDelete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var delete = await client.DeleteAsync($"/api/v2/system-users/{seed.StaffUserId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Email_Recipient_And_Bulk_Email_Endpoints_Exclude_Employee_Accounts()
    {
        var seed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        var recipientsResponse = await client.PostAsJsonAsync("/api/v2/system-users/email-recipients", new
        {
            userIds = new[] { seed.StaffUserId, seed.EmployeeUserId },
            roles = new[] { "Reader" },
            status = "active"
        });
        recipientsResponse.EnsureSuccessStatusCode();
        var recipients = await recipientsResponse.Content.ReadFromJsonAsync<EmailRecipientsResponse>();
        recipients.Should().NotBeNull();
        recipients!.Items.Should().ContainSingle(user => user.UserId == seed.StaffUserId);
        recipients.Items.Should().NotContain(user => user.UserId == seed.EmployeeUserId);

        var bulk = await client.PostAsJsonAsync("/api/v2/system-users/bulk-email", new
        {
            userIds = new[] { seed.StaffUserId, seed.EmployeeUserId },
            roles = new[] { "Reader" },
            status = "active",
            subject = "System notice",
            body = "Message",
            isHtml = false
        });
        bulk.EnsureSuccessStatusCode();
        var sent = await bulk.Content.ReadFromJsonAsync<BulkEmailResponse>();
        sent.Should().NotBeNull();
        sent!.Sent.Should().Be(1);
        sent.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task Login_Lock_Blocks_Normal_Users_And_Allows_PlatformAdmin()
    {
        var seed = await SeedSystemUsersAsync(createPasswords: true);
        var client = Client(CreateToken(SpmeRoles.PlatformAdmin, null));

        try
        {
            var locked = await client.PutAsJsonAsync("/api/v2/system-users/login-lock", new { isLocked = true });
            locked.EnsureSuccessStatusCode();

            var staffLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v2/auth/sessions", new
            {
                username = seed.StaffEmail,
                password = seed.Password
            });
            staffLogin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var problem = System.Text.Json.JsonDocument.Parse(await staffLogin.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("errorCode").GetString().Should().Be("login_locked");

            var platformLogin = await _factory.CreateClient().PostAsJsonAsync("/api/v2/auth/sessions", new
            {
                username = seed.PlatformAdminEmail,
                password = seed.Password
            });
            platformLogin.EnsureSuccessStatusCode();
        }
        finally
        {
            await client.PutAsJsonAsync("/api/v2/system-users/login-lock", new { isLocked = false });
        }
    }

    [Theory]
    [InlineData(SpmeRoles.HrAdmin)]
    [InlineData(SpmeRoles.Employee)]
    public async Task NonPlatform_Roles_Cannot_Read_System_Users(string role)
    {
        var instituteId = Guid.NewGuid();
        var client = Client(CreateToken(role, instituteId));

        var primary = await client.GetAsync("/api/v2/system-users");
        var legacy = await client.GetAsync("/api/v2/users");

        primary.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        legacy.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InstituteAdmin_Can_Manage_Only_Its_Own_System_Users_And_Send_Role_Targeted_Reminders()
    {
        var ownSeed = await SeedSystemUsersAsync();
        var otherSeed = await SeedSystemUsersAsync();
        var client = Client(CreateToken(SpmeRoles.InstituteAdmin, ownSeed.InstituteId));

        var ownDetail = await client.GetAsync($"/api/v2/system-users/{ownSeed.StaffUserId}");
        ownDetail.EnsureSuccessStatusCode();

        var otherDetail = await client.GetAsync($"/api/v2/system-users/{otherSeed.StaffUserId}");
        otherDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownUpdate = await client.PatchAsJsonAsync($"/api/v2/system-users/{ownSeed.StaffUserId}/roles", new
        {
            roles = new[] { "Reader", "Writer", SpmeRoles.HrAdmin }
        });
        ownUpdate.EnsureSuccessStatusCode();

        var platformRole = await client.PatchAsJsonAsync($"/api/v2/system-users/{ownSeed.StaffUserId}/roles", new
        {
            roles = new[] { "Reader", SpmeRoles.PlatformAdmin }
        });
        platformRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var recipientsResponse = await client.PostAsJsonAsync("/api/v2/system-users/email-recipients", new
        {
            roles = new[] { "Reader" },
            status = "active"
        });
        recipientsResponse.EnsureSuccessStatusCode();
        var recipients = await recipientsResponse.Content.ReadFromJsonAsync<EmailRecipientsResponse>();
        recipients.Should().NotBeNull();
        recipients!.Items.Should().ContainSingle(recipient => recipient.UserId == ownSeed.StaffUserId);
        recipients.Items.Should().NotContain(recipient => recipient.UserId == otherSeed.StaffUserId);

        var reminder = await client.PostAsJsonAsync("/api/v2/system-users/bulk-email", new
        {
            roles = new[] { "Writer" },
            status = "active",
            subject = "Quarter 3 (Q3) 2026 report submission reminder",
            body = "Please submit your quarterly report.",
            isHtml = false
        });
        reminder.EnsureSuccessStatusCode();
        var sent = await reminder.Content.ReadFromJsonAsync<BulkEmailResponse>();
        sent.Should().NotBeNull();
        sent!.Sent.Should().Be(1);
    }

    private async Task<SystemUserSeed> SeedSystemUsersAsync(bool createPasswords = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        await EnsureRoleAsync(roleManager, SpmeRoles.PlatformAdmin);
        await EnsureRoleAsync(roleManager, SpmeRoles.HrAdmin);
        await EnsureRoleAsync(roleManager, SpmeRoles.Employee);
        await EnsureRoleAsync(roleManager, "Reader");
        await EnsureRoleAsync(roleManager, "Writer");

        var suffix = Guid.NewGuid().ToString("N");
        var password = "SystemUser!2026";
        var instituteCode = $"SYS-{suffix[..8]}";
        var institute = new Institute(instituteCode, $"System User Test {suffix}", "Institute");
        db.Institutes.Add(institute);
        await db.SaveChangesAsync();

        var employee = new Employee(institute.Id, $"SYS-STF-{suffix}", "Mensah", "female");
        employee.UpdateImportedProfile(
            "Ms.",
            "Akua",
            null,
            "Ghanaian",
            null,
            null,
            $"employee.{suffix}@example.test",
            "0240002222",
            true);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var employeeUser = new User($"employee-user-{suffix}@example.test", "Employee")
        {
            Email = $"employee-user-{suffix}@example.test",
            EmailConfirmed = true
        };
        employeeUser.LinkEmployee(employee.Id, institute.Id);
        employeeUser.RecordLogin(DateTimeOffset.UtcNow);
        var createEmployee = createPasswords
            ? await userManager.CreateAsync(employeeUser, password)
            : await userManager.CreateAsync(employeeUser);
        createEmployee.Succeeded.Should().BeTrue(string.Join("; ", createEmployee.Errors.Select(error => error.Description)));
        var employeeRole = await userManager.AddToRoleAsync(employeeUser, SpmeRoles.Employee);
        employeeRole.Succeeded.Should().BeTrue(string.Join("; ", employeeRole.Errors.Select(error => error.Description)));

        var platformAdmin = new User($"platform-admin-{suffix}@example.test", SpmeRoles.PlatformAdmin)
        {
            Email = $"platform-admin-{suffix}@example.test",
            EmailConfirmed = true
        };
        var createPlatform = createPasswords
            ? await userManager.CreateAsync(platformAdmin, password)
            : await userManager.CreateAsync(platformAdmin);
        createPlatform.Succeeded.Should().BeTrue(string.Join("; ", createPlatform.Errors.Select(error => error.Description)));
        var platformRole = await userManager.AddToRoleAsync(platformAdmin, SpmeRoles.PlatformAdmin);
        platformRole.Succeeded.Should().BeTrue(string.Join("; ", platformRole.Errors.Select(error => error.Description)));

        var staffUser = new User($"staff-user-{suffix}@example.test", "StaffUser")
        {
            Email = $"staff-user-{suffix}@example.test",
            EmailConfirmed = true
        };
        staffUser.AssignInstitute(institute.Id, "StaffUser");
        var createStaff = createPasswords
            ? await userManager.CreateAsync(staffUser, password)
            : await userManager.CreateAsync(staffUser);
        createStaff.Succeeded.Should().BeTrue(string.Join("; ", createStaff.Errors.Select(error => error.Description)));
        var staffRole = await userManager.AddToRoleAsync(staffUser, SpmeRoles.HrAdmin);
        staffRole.Succeeded.Should().BeTrue(string.Join("; ", staffRole.Errors.Select(error => error.Description)));
        var readerRole = await userManager.AddToRoleAsync(staffUser, "Reader");
        readerRole.Succeeded.Should().BeTrue(string.Join("; ", readerRole.Errors.Select(error => error.Description)));

        return new SystemUserSeed(
            suffix,
            institute.Id,
            instituteCode,
            employee.Id,
            employee.StaffId,
            employeeUser.Id,
            staffUser.Id,
            staffUser.Email!,
            platformAdmin.Id,
            platformAdmin.Email!,
            password);
    }

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task KeepOnlyPlatformAdminRoleAsync(Guid platformAdminUserId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var platformRoleId = await db.Roles
            .Where(role => role.NormalizedName == SpmeRoles.PlatformAdmin.ToUpper() || role.Code == SpmeRoles.PlatformAdmin)
            .Select(role => role.Id)
            .SingleAsync();
        var extraAssignments = await db.UserRoles
            .Where(userRole => userRole.RoleId == platformRoleId && userRole.UserId != platformAdminUserId)
            .ToListAsync();
        db.UserRoles.RemoveRange(extraAssignments);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureRoleAsync(RoleManager<Role> roleManager, string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;

        var result = await roleManager.CreateAsync(new Role(role, role, $"{role} test role.", isSystemRole: true));
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private string CreateToken(string role, Guid? instituteId, Guid? userId = null)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "csir-spme-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "csir-spme-client";
        var key = jwtSection.GetValue<string>("Key")
            ?? throw new InvalidOperationException("Jwt:Key is required.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
            new(ClaimTypes.Name, $"integration.{role.ToLowerInvariant()}"),
            new(ClaimTypes.Role, role),
            new("identity_type", role)
        };
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

    private sealed record SystemUserSeed(
        string SearchToken,
        Guid InstituteId,
        string InstituteCode,
        Guid EmployeeId,
        string StaffId,
        Guid EmployeeUserId,
        Guid StaffUserId,
        string StaffEmail,
        Guid PlatformAdminUserId,
        string PlatformAdminEmail,
        string Password);
}
