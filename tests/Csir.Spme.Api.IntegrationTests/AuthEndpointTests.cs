using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Jobs;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public class AuthEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(SpmeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Development_Seed_Platform_Admin_Can_Login_By_UserName()
    {
        var seed = ActivatorUtilities.CreateInstance<IdentitySeedHostedService>(_factory.Services);
        await seed.StartAsync(CancellationToken.None);

        var login = await _client.PostAsJsonAsync("/api/v2/auth/sessions", new
        {
            username = "platform.admin",
            password = "TestOnly_Admin_2026!"
        });

        login.EnsureSuccessStatusCode();
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        loginJson.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        loginJson.RootElement.GetProperty("user").GetProperty("userName").GetString().Should().Be("platform.admin");
        loginJson.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Contain("PlatformAdmin");
    }

    [Fact]
    public async Task Employee_Provisioning_Creates_Employee_Identity_When_StaffUser_Is_Already_Linked()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var primaryEmail = $"employee.{suffix}@csir.local";
        Guid employeeId;
        Guid existingUserId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var institute = new Institute($"I{suffix}"[..12], $"Imported Identity {suffix}", "institute");
            var employee = new Employee(institute.Id, $"S{suffix}"[..12], "Imported", "unspecified");
            employee.UpdateImportedProfile(null, null, null, null, null, null, primaryEmail, null, true);
            db.Institutes.Add(institute);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var importedUser = new User($"legacy.{suffix}@csir.local", "StaffUser")
            {
                Email = $"legacy.contact.{suffix}@csir.local",
                EmailConfirmed = true
            };
            importedUser.LinkEmployee(employee.Id, institute.Id, "StaffUser");
            var created = await userManager.CreateAsync(importedUser, "ImportedUser!2026");
            created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
            employeeId = employee.Id;
            existingUserId = importedUser.Id;
        }

        var seed = ActivatorUtilities.CreateInstance<IdentitySeedHostedService>(_factory.Services);
        await seed.StartAsync(CancellationToken.None);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var linkedUsers = await verificationDb.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employeeId)
            .ToListAsync();
        linkedUsers.Should().Contain(user => user.Id == existingUserId && user.IdentityType == "StaffUser");
        linkedUsers.Should().Contain(user =>
            user.IdentityType == "Employee" &&
            string.Equals(user.Email, primaryEmail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Employee_Provisioning_Keeps_StaffUser_And_Employee_Links_Together()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var primaryEmail = $"staff.portal.{suffix}@csir.local";
        Guid employeeId;
        Guid staffUserId;
        Guid employeeUserId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var institute = new Institute($"I{suffix}"[..12], $"Dual Identity {suffix}", "institute");
            var employee = new Employee(institute.Id, $"S{suffix}"[..12], "Dual", "unspecified");
            employee.UpdateImportedProfile(null, null, null, null, null, null, primaryEmail, null, true);
            db.Institutes.Add(institute);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var staffUser = new User($"hod.{suffix}@csir.local", "StaffUser")
            {
                Email = $"hod.{suffix}@csir.local",
                EmailConfirmed = true
            };
            staffUser.LinkEmployee(employee.Id, institute.Id, "StaffUser");
            (await userManager.CreateAsync(staffUser, "StaffUser!2026")).Succeeded.Should().BeTrue();

            var employeeUser = new User(primaryEmail, "Employee")
            {
                Email = primaryEmail,
                EmailConfirmed = true
            };
            employeeUser.LinkEmployee(employee.Id, institute.Id);
            (await userManager.CreateAsync(employeeUser, "EmployeeUser!2026")).Succeeded.Should().BeTrue();

            employeeId = employee.Id;
            staffUserId = staffUser.Id;
            employeeUserId = employeeUser.Id;
        }

        var seed = ActivatorUtilities.CreateInstance<IdentitySeedHostedService>(_factory.Services);
        await seed.StartAsync(CancellationToken.None);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var manager = verificationScope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var linkedUsers = await verificationDb.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employeeId)
            .Select(user => new { user.Id, user.IdentityType })
            .ToListAsync();
        linkedUsers.Should().HaveCount(2);
        linkedUsers.Should().Contain(user => user.Id == staffUserId && user.IdentityType == "StaffUser");
        linkedUsers.Should().Contain(user => user.Id == employeeUserId && user.IdentityType == "Employee");

        var provisionedEmployee = await manager.FindByIdAsync(employeeUserId.ToString());
        provisionedEmployee.Should().NotBeNull();
        (await manager.IsInRoleAsync(provisionedEmployee!, "Employee")).Should().BeTrue();
    }

    [Fact]
    public async Task Employee_Provisioning_Unlinks_Duplicate_Employee_Identities()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var primaryEmail = $"canonical.{suffix}@csir.local";
        Guid employeeId;
        Guid canonicalUserId;
        Guid duplicateUserId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var institute = new Institute($"I{suffix}"[..12], $"Duplicate Identity {suffix}", "institute");
            var employee = new Employee(institute.Id, $"S{suffix}"[..12], "Duplicate", "unspecified");
            employee.UpdateImportedProfile(null, null, null, null, null, null, primaryEmail, null, true);
            db.Institutes.Add(institute);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var canonical = new User(primaryEmail, "Employee")
            {
                Email = primaryEmail,
                EmailConfirmed = true
            };
            canonical.LinkEmployee(employee.Id, institute.Id);
            (await userManager.CreateAsync(canonical, "CanonicalUser!2026")).Succeeded.Should().BeTrue();

            var duplicate = new User($"duplicate.{suffix}@csir.local", "Employee")
            {
                Email = $"duplicate.{suffix}@csir.local",
                EmailConfirmed = true
            };
            duplicate.LinkEmployee(employee.Id, institute.Id);
            (await userManager.CreateAsync(duplicate, "DuplicateUser!2026")).Succeeded.Should().BeTrue();

            employeeId = employee.Id;
            canonicalUserId = canonical.Id;
            duplicateUserId = duplicate.Id;
        }

        var seed = ActivatorUtilities.CreateInstance<IdentitySeedHostedService>(_factory.Services);
        await seed.StartAsync(CancellationToken.None);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var remaining = await verificationDb.Users.AsNoTracking()
            .Where(user => user.EmployeeId == employeeId && user.IdentityType == "Employee")
            .Select(user => user.Id)
            .ToListAsync();
        remaining.Should().ContainSingle().Which.Should().Be(canonicalUserId);

        var unlinked = await verificationDb.Users.AsNoTracking()
            .SingleAsync(user => user.Id == duplicateUserId);
        unlinked.EmployeeId.Should().BeNull();
        unlinked.IdentityType.Should().Be("Employee");
    }

    [Fact]
    public async Task Login_Accepts_Email_And_Current_User_Returns_Identity_Context()
    {
        var email = $"real.user.{Guid.NewGuid():N}@csir.local";
        await CreateUserAsync(email, "RealUser!2026", "HrAdmin");

        var login = await _client.PostAsJsonAsync("/api/v2/auth/sessions", new
        {
            username = email,
            password = "RealUser!2026"
        });

        login.EnsureSuccessStatusCode();
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("accessToken").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        loginJson.RootElement.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
        loginJson.RootElement.GetProperty("user").GetProperty("accountStatus").GetString().Should().Be("active");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.GetAsync("/api/v2/auth/me");

        me.EnsureSuccessStatusCode();
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        meJson.RootElement.GetProperty("email").GetString().Should().Be(email);
        meJson.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .Should().Contain("HrAdmin");
    }

    [Fact]
    public async Task My_Settings_Profile_And_Notification_Preferences_Are_Persisted_For_Authenticated_User()
    {
        var email = $"settings.user.{Guid.NewGuid():N}@csir.local";
        await CreateUserAsync(email, "SettingsUser!2026", "HrAdmin");
        var login = await _client.PostAsJsonAsync("/api/v2/auth/sessions", new { username = email, password = "SettingsUser!2026" });
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginJson.RootElement.GetProperty("accessToken").GetString());

        var profile = await _client.PatchAsJsonAsync("/api/v2/me", new { displayName = "Settings User" });
        profile.EnsureSuccessStatusCode();
        using var profileJson = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        profileJson.RootElement.GetProperty("displayName").GetString().Should().Be("Settings User");

        var defaults = await _client.GetAsync("/api/v2/notification-preferences/me");
        defaults.EnsureSuccessStatusCode();
        var updated = await _client.PatchAsJsonAsync("/api/v2/notification-preferences/me", new
        {
            emailAlerts = false, leaveReminders = true, promotionUpdates = false, systemAnnouncements = true
        });
        updated.EnsureSuccessStatusCode();
        using var preferenceJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        preferenceJson.RootElement.GetProperty("emailAlerts").GetBoolean().Should().BeFalse();
        preferenceJson.RootElement.GetProperty("promotionUpdates").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Login_With_Reset_Required_User_Returns_Specific_Problem()
    {
        var email = $"reset.required.{Guid.NewGuid():N}@csir.local";
        await CreateUserAsync(email, "ResetRequired!2026", "Employee", resetRequired: true);

        var response = await _client.PostAsJsonAsync("/api/v2/auth/sessions", new
        {
            username = email,
            password = "ResetRequired!2026"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("password_reset_required");
        problem.RootElement.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Password_Reset_Confirm_Activates_Provisioned_User_For_Login()
    {
        var email = $"provisioned.{Guid.NewGuid():N}@csir.local";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            await EnsureRoleAsync(roleManager, "Employee");
            var user = new User(email, "Employee")
            {
                Email = email,
                EmailConfirmed = true
            };
            user.MarkPasswordResetRequired();
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
        }

        (await _client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await GetLatestResetLinkAsync(_factory, email);
        var staffResetUri = new Uri(link.Url);
        staffResetUri.Host.Should().Be("localhost");
        staffResetUri.Port.Should().Be(5177);
        var reset = await _client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
        {
            requestId = link.RequestId,
            token = link.Token,
            newPassword = "Provisioned!2026",
            confirmNewPassword = "Provisioned!2026"
        });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await _client.PostAsJsonAsync("/api/v2/auth/sessions", new
        {
            username = email,
            password = "Provisioned!2026"
        });

        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Password_Reset_For_StaffUser_Uses_Staff_Portal_Url()
    {
        var email = $"staffuser.reset.{Guid.NewGuid():N}@csir.local";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(email, "StaffUser")
            {
                Email = email,
                EmailConfirmed = true
            };
            (await userManager.CreateAsync(user, "StaffUser!2026")).Succeeded.Should().BeTrue();
        }

        (await _client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var link = await GetLatestResetLinkAsync(_factory, email);
        var resetUri = new Uri(link.Url);
        resetUri.Host.Should().Be("localhost");
        resetUri.Port.Should().Be(5177);
        resetUri.AbsolutePath.Should().Be("/reset-password");
    }

    [Fact]
    public async Task Password_Reset_Is_NonEnumerating_Persisted_SingleUse_And_Revokes_Sessions()
    {
        var email = $"secure.reset.{Guid.NewGuid():N}@csir.local";
        await CreateUserAsync(email, "SecureReset!2026", "HrAdmin");
        var login = await _client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "SecureReset!2026" });
        login.EnsureSuccessStatusCode();

        var unknown = await _client.PostAsJsonAsync("/api/v2/auth/password-resets",
            new { email = $"unknown.{Guid.NewGuid():N}@csir.local" });
        var known = await _client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email });
        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await known.Content.ReadAsStringAsync());

        var first = await GetLatestResetLinkAsync(_factory, email);
        first.Url.Should().Contain("requestId=").And.Contain("token=");
        first.Url.ToLowerInvariant().Should().NotContain("email=").And.NotContain(email.ToLowerInvariant());
        first.Message.Category.Should().Be("authentication");
        first.Message.IsHtml.Should().BeTrue();
        first.Message.Body.Should().Contain("#13294B").And.Contain("#D0006F").And.Contain("24 hours");
        first.Message.TextBody.Should().Contain("24 hours");
        var hrResetUri = new Uri(first.Url);
        hrResetUri.Host.Should().Be("localhost");
        hrResetUri.Port.Should().Be(5176);
        await using (var persistenceScope = _factory.Services.CreateAsyncScope())
        {
            var persistenceDb = persistenceScope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var request = await persistenceDb.PasswordResetRequests.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == first.RequestId);
            var challenge = await persistenceDb.VerificationChallenges.AsNoTracking()
                .SingleAsync(candidate => candidate.Id == request.VerificationChallengeId);
            (challenge.ExpiresAt - request.RequestedAt).Should().Be(TimeSpan.FromHours(24));
            challenge.DestinationHash.Should().HaveLength(64).And.NotContain(email);
            challenge.CodeHash.Should().HaveLength(64).And.NotContain(first.Token);
        }

        (await _client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var second = await GetLatestResetLinkAsync(_factory, email);
        second.RequestId.Should().NotBe(first.RequestId);

        var superseded = await ConfirmAsync(first.RequestId, first.Token, "Replacement!2026");
        await AssertProblemCodeAsync(superseded, "verification_expired");
        var invalidId = await ConfirmAsync(Guid.NewGuid(), second.Token, "Replacement!2026");
        await AssertProblemCodeAsync(invalidId, "verification_expired");
        var tampered = await ConfirmAsync(second.RequestId, second.Token + "x", "Replacement!2026");
        await AssertProblemCodeAsync(tampered, "verification_failed");
        var weakPassword = await ConfirmAsync(second.RequestId, second.Token, "weak-password");
        await AssertProblemCodeAsync(weakPassword, "validation_failed");

        (await ConfirmAsync(second.RequestId, second.Token, "Replacement!2026"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var replay = await ConfirmAsync(second.RequestId, second.Token, "AnotherPass!2026");
        await AssertProblemCodeAsync(replay, "verification_expired");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        (await db.UserSessions.Where(candidate => candidate.UserId == user.Id).ToListAsync())
            .Should().OnlyContain(session => session.RevokedAt != null);
        (await db.RefreshTokens.Where(candidate => candidate.UserId == user.Id).ToListAsync())
            .Should().OnlyContain(token => token.RevokedAt != null);
        (await db.AuditRecords.AnyAsync(record =>
            record.Action == "auth.password-reset-completed" && record.TargetId == user.Id.ToString()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Password_Reset_Expires_After_Exactly_TwentyFour_Hours()
    {
        using var factory = new SpmeApiFactory();
        using var client = factory.CreateClient();
        var email = $"expired.reset.{Guid.NewGuid():N}@csir.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(email, "Employee") { Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "ExpiryStart!2026")).Succeeded.Should().BeTrue();
        }

        (await client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await GetLatestResetLinkAsync(factory, email);
        factory.Clock.Advance(TimeSpan.FromHours(24));
        var response = await client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
        {
            requestId = link.RequestId,
            token = link.Token,
            newPassword = "ExpiryAfter!2026",
            confirmNewPassword = "ExpiryAfter!2026"
        });
        await AssertProblemCodeAsync(response, "verification_expired");
    }

    [Fact]
    public async Task Password_Reset_Remains_Valid_Just_Before_TwentyFour_Hours()
    {
        using var factory = new SpmeApiFactory();
        using var client = factory.CreateClient();
        var email = $"almost.expired.{Guid.NewGuid():N}@csir.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(email, "Employee") { Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "AlmostExpire!2026")).Succeeded.Should().BeTrue();
        }

        (await client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await GetLatestResetLinkAsync(factory, email);
        factory.Clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1));
        (await client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
        {
            requestId = link.RequestId,
            token = link.Token,
            newPassword = "StillValid!2026",
            confirmNewPassword = "StillValid!2026"
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Password_Reset_Locks_After_Five_Invalid_Token_Attempts()
    {
        using var factory = new SpmeApiFactory();
        using var client = factory.CreateClient();
        var email = $"locked.reset.{Guid.NewGuid():N}@csir.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User(email, "Employee") { Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "LockStart!2026")).Succeeded.Should().BeTrue();
        }

        (await client.PostAsJsonAsync("/api/v2/auth/password-resets", new { email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var link = await GetLatestResetLinkAsync(factory, email);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
            {
                requestId = link.RequestId,
                token = link.Token + attempt,
                newPassword = "LockAfter!2026",
                confirmNewPassword = "LockAfter!2026"
            });
            await AssertProblemCodeAsync(failed, "verification_failed");
        }

        var locked = await client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
        {
            requestId = link.RequestId,
            token = link.Token,
            newPassword = "LockAfter!2026",
            confirmNewPassword = "LockAfter!2026"
        });
        await AssertProblemCodeAsync(locked, "verification_expired");
    }

    private Task<HttpResponseMessage> ConfirmAsync(Guid requestId, string token, string password) =>
        _client.PostAsJsonAsync("/api/v2/auth/password-resets/confirm", new
        {
            requestId,
            token,
            newPassword = password,
            confirmNewPassword = password
        });

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be(expectedCode);
    }

    private static async Task<ResetLink> GetLatestResetLinkAsync(SpmeApiFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userId = await db.Users.Where(candidate => candidate.Email == email)
            .Select(candidate => candidate.Id).SingleAsync();
        var requests = await db.PasswordResetRequests.AsNoTracking()
            .Where(candidate => candidate.UserId == userId)
            .ToListAsync();
        var request = requests.OrderByDescending(candidate => candidate.RequestedAt).First();
        var message = await db.CommunicationOutboxMessages.AsNoTracking()
            .SingleAsync(candidate => candidate.IdempotencyKey == $"password-reset:{request.Id:N}");
        var urlLine = message.TextBody!.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Reset password: ", StringComparison.Ordinal));
        var url = urlLine["Reset password: ".Length..].Trim();
        var uri = new Uri(url);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        return new ResetLink(
            Guid.Parse(query["requestId"].Single()!),
            query["token"].Single()!,
            url,
            message);
    }

    private sealed record ResetLink(
        Guid RequestId,
        string Token,
        string Url,
        Csir.Spme.Domain.Comms.CommunicationOutboxMessage Message);

    private async Task CreateUserAsync(
        string email,
        string password,
        string role,
        bool resetRequired = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        await EnsureRoleAsync(roleManager, role);
        var user = new User(email, role)
        {
            Email = email,
            EmailConfirmed = true
        };
        if (resetRequired)
        {
            user.MarkPasswordResetRequired();
        }

        var create = await userManager.CreateAsync(user, password);
        create.Succeeded.Should().BeTrue(string.Join("; ", create.Errors.Select(error => error.Description)));
        var addRole = await userManager.AddToRoleAsync(user, role);
        addRole.Succeeded.Should().BeTrue(string.Join("; ", addRole.Errors.Select(error => error.Description)));
    }

    private static async Task EnsureRoleAsync(RoleManager<Role> roleManager, string role)
    {
        if (await roleManager.RoleExistsAsync(role))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new Role(role, role, $"{role} test role.", isSystemRole: true));
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
