using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Csir.Spme.Domain.Comms;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Communications;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class StaffIdentityEndpointTests : IClassFixture<SpmeApiFactory>
{
    private readonly SpmeApiFactory _factory;

    public StaffIdentityEndpointTests(SpmeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Activation_Verifies_Only_StaffId_And_Proven_Email_Then_Allows_Login()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var staffId = $"CSIR/{suffix[..8]}";
        var email = $"employee.{suffix}@csir.local";
        var phone = "024" + Random.Shared.Next(1000000, 9999999).ToString();
        var employeeId = await CreateProvisionedEmployeeAsync(staffId, email, phone);

        using var client = CreateHttpsClient();
        var challengeResponse = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = staffId, contact = email });
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var challengeJson = JsonDocument.Parse(await challengeResponse.Content.ReadAsStringAsync());
        var challengeId = challengeJson.RootElement.GetProperty("challengeId").GetGuid();

        string code;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var message = await db.CommunicationOutboxMessages.AsNoTracking()
                .SingleAsync(item => item.IdempotencyKey == $"account-activation:{challengeId}:email");
            code = Regex.Match(message.Body, @"\b\d{6}\b").Value;
            code.Should().HaveLength(6);
        }

        var verifyResponse = await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code });
        verifyResponse.EnsureSuccessStatusCode();
        using var verifyJson = JsonDocument.Parse(await verifyResponse.Content.ReadAsStringAsync());
        var verificationToken = verifyJson.RootElement.GetProperty("verificationToken").GetString();

        var completeResponse = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/complete",
            new
            {
                challengeId,
                verificationToken,
                password = "ActivatedEmployee!2026",
                confirmPassword = "ActivatedEmployee!2026"
            });
        completeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var identifiers = await db.UserLoginIdentifiers.AsNoTracking()
                .Where(identifier => identifier.EmployeeId == employeeId)
                .Select(identifier => identifier.IdentifierType)
                .ToListAsync();
            identifiers.Should().Contain(["staff-id", "email"]);
            identifiers.Should().NotContain("phone");
        }

        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = staffId, password = "ActivatedEmployee!2026" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "ActivatedEmployee!2026" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = phone, password = "ActivatedEmployee!2026" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Activation_Request_Discloses_Unknown_Identifier_As_NotFound()
    {
        using var client = CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = $"UNKNOWN/{Guid.NewGuid():N}", contact = "unknown@example.invalid" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("not_found");
        json.RootElement.GetProperty("type").GetString().Should()
            .Be("https://api.csir.example/problems/not-found");
    }

    [Fact]
    public async Task Refresh_Rotates_Hashed_Token_And_Reused_Token_Revokes_Family()
    {
        var email = $"refresh.{Guid.NewGuid():N}@csir.local";
        await CreateStandaloneUserAsync(email, "RefreshUser!2026");
        using var client = CreateHttpsClient();

        var login = await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "RefreshUser!2026" });
        login.EnsureSuccessStatusCode();
        var firstToken = ReadRefreshCookie(login);

        var firstRefresh = await SendWithRefreshCookieAsync(client, firstToken);
        firstRefresh.EnsureSuccessStatusCode();
        var secondToken = ReadRefreshCookie(firstRefresh);
        secondToken.Should().NotBe(firstToken);

        (await SendWithRefreshCookieAsync(client, firstToken)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await SendWithRefreshCookieAsync(client, secondToken)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userId = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        var family = await db.RefreshTokens.AsNoTracking()
            .Where(token => token.UserId == userId)
            .ToListAsync();
        family.Should().HaveCount(2);
        family.Should().OnlyContain(token => token.RevokedAt != null);
        family.Should().Contain(token => token.RevocationReason == "reuse-detected");
    }

    [Fact]
    public async Task Session_List_Revoke_And_CookieOnly_Logout_Invalidate_Refresh_Tokens()
    {
        var email = $"sessions.{Guid.NewGuid():N}@csir.local";
        await CreateStandaloneUserAsync(email, "SessionUser!2026");
        using var client = CreateHttpsClient();

        var login = await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "SessionUser!2026", deviceName = "Work Laptop", platform = "Linux" });
        login.EnsureSuccessStatusCode();
        var refreshToken = ReadRefreshCookie(login);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString()!;
        var sessionId = loginJson.RootElement.GetProperty("sessionId").GetGuid();

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v2/users/me/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        listRequest.Headers.Add("Cookie", $"spme_refresh={refreshToken}");
        var listResponse = await client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var sessions = await listResponse.Content.ReadFromJsonAsync<List<Csir.Spme.Api.Endpoints.V2.UserSessionResponse>>();
        sessions.Should().ContainSingle(item => item.Id == sessionId && item.IsCurrent &&
            item.DeviceName == "Work Laptop" && item.Platform == "Linux");

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/users/me/sessions/{sessionId}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        revokeRequest.Headers.Add("Cookie", $"spme_refresh={refreshToken}");
        (await client.SendAsync(revokeRequest)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendWithRefreshCookieAsync(client, refreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var secondLogin = await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "SessionUser!2026", deviceName = "Phone", platform = "Android" });
        secondLogin.EnsureSuccessStatusCode();
        var secondRefreshToken = ReadRefreshCookie(secondLogin);
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v2/auth/sessions/current");
        logoutRequest.Headers.Add("Cookie", $"spme_refresh={secondRefreshToken}");
        var logout = await client.SendAsync(logoutRequest);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Should().Contain(value =>
            value.StartsWith("spme_refresh=", StringComparison.Ordinal) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        (await SendWithRefreshCookieAsync(client, secondRefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userId = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        (await db.UserSessions.AsNoTracking().Where(session => session.UserId == userId).ToListAsync())
            .Should().OnlyContain(session => session.RevokedAt != null);
        (await db.RefreshTokens.AsNoTracking().Where(token => token.UserId == userId).ToListAsync())
            .Should().OnlyContain(token => token.RevokedAt != null);
    }

    [Fact]
    public async Task Revoked_Session_Rejects_Its_Bearer_While_Sibling_Session_Remains_Valid()
    {
        using var factory = new SpmeApiFactory { PreserveJwtValidation = true };
        var email = $"validated-sessions.{Guid.NewGuid():N}@csir.local";
        await CreateStandaloneUserAsync(email, "ValidatedSession!2026", factory);
        using var client = CreateHttpsClient(factory);

        var firstLogin = await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "ValidatedSession!2026", deviceName = "First" });
        var secondLogin = await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "ValidatedSession!2026", deviceName = "Second" });
        firstLogin.EnsureSuccessStatusCode();
        secondLogin.EnsureSuccessStatusCode();
        var first = await firstLogin.Content.ReadFromJsonAsync<Csir.Spme.Api.Endpoints.V2.LoginResponse>();
        var second = await secondLogin.Content.ReadFromJsonAsync<Csir.Spme.Api.Endpoints.V2.LoginResponse>();
        var firstRefresh = ReadRefreshCookie(firstLogin);
        var secondRefresh = ReadRefreshCookie(secondLogin);

        using var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/users/me/sessions/{first!.SessionId}");
        revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        revoke.Headers.Add("Cookie", $"spme_refresh={firstRefresh}");
        (await client.SendAsync(revoke)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var rejectedBearer = new HttpRequestMessage(HttpMethod.Get, "/api/v2/me");
        rejectedBearer.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        (await client.SendAsync(rejectedBearer)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var siblingBearer = new HttpRequestMessage(HttpMethod.Get, "/api/v2/me");
        siblingBearer.Headers.Authorization = new AuthenticationHeaderValue("Bearer", second!.AccessToken);
        (await client.SendAsync(siblingBearer)).StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = await SendWithRefreshCookieAsync(client, secondRefresh);
        rotated.EnsureSuccessStatusCode();
        var rotatedLogin = await rotated.Content.ReadFromJsonAsync<Csir.Spme.Api.Endpoints.V2.LoginResponse>();
        rotatedLogin!.SessionId.Should().Be(second.SessionId);
        new JwtSecurityTokenHandler().ReadJwtToken(rotatedLogin.AccessToken).Claims
            .Single(claim => claim.Type == "sid").Value.Should().Be(second.SessionId.ToString());

        using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/v2/auth/sessions/current");
        logout.Headers.Add("Cookie", $"spme_refresh={ReadRefreshCookie(rotated)}");
        (await client.SendAsync(logout)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var loggedOutBearer = new HttpRequestMessage(HttpMethod.Get, "/api/v2/me");
        loggedOutBearer.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotatedLogin.AccessToken);
        (await client.SendAsync(loggedOutBearer)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Existing_Active_Employee_Can_Login_With_Unique_StaffId_And_Confirmed_Contacts()
    {
        using var factory = new SpmeApiFactory();
        var suffix = Guid.NewGuid().ToString("N");
        var staffId = $"LEGACY/{suffix[..8]}";
        var email = $"legacy.{suffix}@csir.local";
        var phone = "024" + Random.Shared.Next(1000000, 9999999).ToString();
        var employeeId = await CreateLegacyEmployeeUserAsync(
            factory, staffId, email, phone, "LegacyEmployee!2026", emailConfirmed: true, phoneConfirmed: true);
        using var client = CreateHttpsClient(factory);

        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = staffId, password = "LegacyEmployee!2026" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "LegacyEmployee!2026" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = phone, password = "LegacyEmployee!2026" })).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var identifiers = await db.UserLoginIdentifiers.AsNoTracking()
            .Where(identifier => identifier.EmployeeId == employeeId && identifier.IsActive)
            .ToListAsync();
        identifiers.Select(identifier => identifier.IdentifierType)
            .Should().BeEquivalentTo(["staff-id", "email", "phone"]);
        identifiers.Should().OnlyContain(identifier =>
            identifier.VerificationSource == "verified-legacy-password-login");
    }

    [Fact]
    public async Task Existing_Employee_Email_And_Phone_Require_Identity_Confirmation()
    {
        using var factory = new SpmeApiFactory();
        var suffix = Guid.NewGuid().ToString("N");
        var staffId = $"UNCONF/{suffix[..8]}";
        var email = $"unconfirmed.{suffix}@csir.local";
        var phone = "025" + Random.Shared.Next(1000000, 9999999).ToString();
        await CreateLegacyEmployeeUserAsync(
            factory, staffId, email, phone, "UnconfirmedEmployee!2026", emailConfirmed: false, phoneConfirmed: false);
        using var client = CreateHttpsClient(factory);

        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = email, password = "UnconfirmedEmployee!2026" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = phone, password = "UnconfirmedEmployee!2026" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = staffId, password = "UnconfirmedEmployee!2026" })).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var identifierTypes = await db.UserLoginIdentifiers.AsNoTracking()
            .Where(identifier => identifier.NormalizedValue == staffId.ToUpperInvariant() ||
                identifier.NormalizedValue == email.ToUpperInvariant() ||
                identifier.NormalizedValue == LoginIdentifierNormalizer.NormalizeGhanaPhone(phone))
            .Select(identifier => identifier.IdentifierType)
            .ToListAsync();
        identifierTypes.Should().Equal("staff-id");
    }

    [Fact]
    public async Task Duplicate_CrossInstitute_StaffId_Is_Blocked_Without_Provisioning()
    {
        using var factory = new SpmeApiFactory();
        var suffix = Guid.NewGuid().ToString("N");
        var staffId = $"DUP/{suffix[..8]}";
        await CreateLegacyEmployeeUserAsync(
            factory, staffId, $"first.{suffix}@csir.local", "0201111111", "FirstEmployee!2026", true, true);
        await CreateLegacyEmployeeUserAsync(
            factory, staffId, $"second.{suffix}@csir.local", "0202222222", "SecondEmployee!2026", true, true);
        using var client = CreateHttpsClient(factory);

        (await client.PostAsJsonAsync("/api/v2/auth/sessions",
            new { username = staffId, password = "FirstEmployee!2026" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.UserLoginIdentifiers.AsNoTracking()
            .CountAsync(identifier => identifier.IdentifierType == "staff-id" &&
                identifier.NormalizedValue == staffId.ToUpperInvariant()))
            .Should().Be(0);
    }

    private HttpClient CreateHttpsClient(SpmeApiFactory? factory = null) => (factory ?? _factory).CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static async Task<HttpResponseMessage> SendWithRefreshCookieAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/auth/sessions/refresh");
        request.Headers.Add("Cookie", $"spme_refresh={token}");
        return await client.SendAsync(request);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("spme_refresh=", StringComparison.Ordinal));
        return setCookie.Split(';', 2)[0]["spme_refresh=".Length..];
    }

    private async Task<Guid> CreateProvisionedEmployeeAsync(string staffId, string email, string phone)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var institute = new Institute($"T{Guid.NewGuid():N}"[..12], "Identity Test Institute", "institute");
        var employee = new Employee(institute.Id, staffId, "Employee", "unspecified");
        employee.UpdateImportedProfile(null, "Test", null, "Ghanaian", null, null, email, phone, true);
        db.Institutes.Add(institute);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var user = new User(email, "Employee") { Email = email, PhoneNumber = phone };
        user.LinkEmployee(employee.Id, institute.Id);
        user.MarkPasswordResetRequired();
        var created = await userManager.CreateAsync(user);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
        return employee.Id;
    }

    private async Task CreateStandaloneUserAsync(string email, string password, SpmeApiFactory? factory = null)
    {
        await using var scope = (factory ?? _factory).Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User(email, "StaffUser") { Email = email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, password);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
    }

    private async Task<Guid> CreateLegacyEmployeeUserAsync(
        SpmeApiFactory factory,
        string staffId,
        string email,
        string phone,
        string password,
        bool emailConfirmed,
        bool phoneConfirmed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var instituteSuffix = Guid.NewGuid().ToString("N");
        var institute = new Institute($"L{instituteSuffix}"[..12], $"Legacy Identity {instituteSuffix}", "institute");
        var employee = new Employee(institute.Id, staffId, "Legacy", "unspecified");
        employee.UpdateImportedProfile(null, "Employee", null, "Ghanaian", null, null, email, phone, true);
        db.Institutes.Add(institute);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var user = new User(email, "Employee")
        {
            Email = email,
            PhoneNumber = phone,
            EmailConfirmed = emailConfirmed,
            PhoneNumberConfirmed = phoneConfirmed
        };
        user.LinkEmployee(employee.Id, institute.Id);
        var created = await userManager.CreateAsync(user, password);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
        return employee.Id;
    }
}
