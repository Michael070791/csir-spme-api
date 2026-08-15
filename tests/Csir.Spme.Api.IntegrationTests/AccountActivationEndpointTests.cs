using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Csir.Spme.Domain.Hr;
using Csir.Spme.Domain.Iam;
using Csir.Spme.Domain.Org;
using Csir.Spme.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AccountActivationEndpointTests
{
    [Fact]
    public async Task Challenge_Queues_Otp_And_Returns_Full_CodeSent_Response()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact = identity.Email });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("outcome").GetString().Should().Be("code_sent");
        root.GetProperty("deliveryChannel").GetString().Should().Be("email");
        root.GetProperty("maskedDestination").GetString().Should().Contain("***@");
        root.GetProperty("message").GetString().Should().Contain("verification code");
        root.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
        var challengeId = root.GetProperty("challengeId").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        (await db.AccountActivationChallenges.CountAsync(item => item.Id == challengeId)).Should().Be(1);
        (await db.CommunicationOutboxMessages.CountAsync(
            item => item.IdempotencyKey == $"account-activation:{challengeId}:email")).Should().Be(1);
    }

    [Fact]
    public async Task Challenge_Unknown_Identifier_Returns_NotFound_Without_Persisting()
    {
        await using var factory = new SpmeApiFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = $"UNKNOWN/{Guid.NewGuid():N}", contact = "unknown@csir.local" });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
        (await CountChallengesAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Challenge_NonUnique_Employee_Returns_NotFound_Without_Persisting()
    {
        await using var factory = new SpmeApiFactory();
        var staffId = $"DUP/{Guid.NewGuid():N}";
        await CreateEmployeeAsync(factory, staffId: staffId);
        await CreateEmployeeAsync(factory, staffId: staffId);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = staffId, contact = "any@csir.local" });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
        (await CountChallengesAsync(factory)).Should().Be(0);
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("different@csir.local", "does not match")]
    public async Task Challenge_Missing_Or_Mismatched_Contact_Returns_Field_Validation(
        string? contact,
        string detailFragment)
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact });

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        problem.GetProperty("detail").GetString().Should().Contain(detailFragment);
        problem.GetProperty("errors").TryGetProperty("contact", out _).Should().BeTrue();
        (await CountChallengesAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Challenge_AlreadyActive_Account_Returns_Conflict()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory, password: "AlreadyActive!2026");
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact = identity.Email });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "conflict");
        problem.GetProperty("detail").GetString().Should().Contain("already active");
        (await CountChallengesAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Challenge_Placeholder_Destination_Returns_Validation()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory, email: $"placeholder.{Guid.NewGuid():N}@example.invalid");
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact = identity.Email });

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        problem.GetProperty("detail").GetString().Should().Contain("institute HR office");
        (await CountChallengesAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Challenge_Identifier_Or_Destination_Resend_Cap_Returns_VerificationRateLimited()
    {
        await using var factory = new SpmeApiFactory { AccountActivationResendLimit = 2 };
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);

        for (var index = 0; index < 2; index++)
        {
            (await client.PostAsJsonAsync(
                "/api/v2/auth/account-activations/challenges",
                new { identifier = identity.StaffId, contact = identity.Email })).StatusCode
                .Should().Be(HttpStatusCode.Accepted);
        }

        var limited = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact = identity.Email });
        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "verification_rate_limited");
        (await CountChallengesAsync(factory)).Should().Be(2);
    }

    [Fact]
    public async Task Challenge_Framework_Ip_Limit_Returns_RateLimited()
    {
        await using var factory = new SpmeApiFactory();
        using var client = CreateClient(factory);
        HttpResponseMessage? response = null;
        for (var index = 0; index < 6; index++)
        {
            response = await client.PostAsJsonAsync(
                "/api/v2/auth/account-activations/challenges",
                new { identifier = $"UNKNOWN/{index}/{Guid.NewGuid():N}", contact = "unknown@csir.local" });
        }

        await AssertProblemAsync(response!, HttpStatusCode.TooManyRequests, "rate_limited");
    }

    [Fact]
    public async Task Verify_Success_Returns_Token_Expiry_And_Message()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);
        var (challengeId, code) = await CreateChallengeAndReadCodeAsync(factory, client, identity);

        var response = await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("verificationToken").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
        json.RootElement.GetProperty("message").GetString().Should().Contain("Verification succeeded");
    }

    [Fact]
    public async Task Verify_Wrong_Code_Returns_VerificationFailed()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);
        var (challengeId, _) = await CreateChallengeAndReadCodeAsync(factory, client, identity);

        var response = await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code = "999999" });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "verification_failed");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("consumed")]
    public async Task Verify_Missing_Expired_Or_Consumed_Returns_VerificationExpired(string state)
    {
        await using var factory = new SpmeApiFactory();
        var challengeId = Guid.NewGuid();
        if (state != "missing")
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
            var challenge = new AccountActivationChallenge(
                null,
                new string('a', 64),
                "email",
                new string('b', 64),
                new string('c', 64),
                state == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(10),
                5);
            if (state == "consumed")
                challenge.Consume(DateTimeOffset.UtcNow);
            challengeId = challenge.Id;
            db.AccountActivationChallenges.Add(challenge);
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code = "123456" });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "verification_expired");
    }

    [Fact]
    public async Task Verify_Exhausted_Attempts_Returns_Explicit_VerificationFailed()
    {
        await using var factory = new SpmeApiFactory { AccountActivationMaximumAttempts = 3 };
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);
        var (challengeId, _) = await CreateChallengeAndReadCodeAsync(factory, client, identity);
        HttpResponseMessage? response = null;
        for (var index = 0; index < 3; index++)
        {
            response = await client.PostAsJsonAsync(
                $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
                new { code = "999999" });
        }

        var problem = await AssertProblemAsync(
            response!, HttpStatusCode.UnprocessableEntity, "verification_failed");
        problem.GetProperty("detail").GetString().Should().Contain("exhausted");
    }

    [Fact]
    public async Task Complete_Invalid_Verification_Token_Returns_VerificationExpired()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);
        var (challengeId, code) = await CreateChallengeAndReadCodeAsync(factory, client, identity);
        (await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/complete",
            new
            {
                challengeId,
                verificationToken = "invalid-verification-token",
                password = "ValidPassword!2026",
                confirmPassword = "ValidPassword!2026"
            });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "verification_expired");
    }

    [Fact]
    public async Task Complete_PasswordPolicy_Failure_Returns_ValidationFailed()
    {
        await using var factory = new SpmeApiFactory();
        var identity = await CreateEmployeeAsync(factory);
        using var client = CreateClient(factory);
        var (challengeId, code) = await CreateChallengeAndReadCodeAsync(factory, client, identity);
        var verify = await client.PostAsJsonAsync(
            $"/api/v2/auth/account-activations/challenges/{challengeId}/verify",
            new { code });
        using var verificationJson = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var token = verificationJson.RootElement.GetProperty("verificationToken").GetString();

        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/complete",
            new
            {
                challengeId,
                verificationToken = token,
                password = "weak-password",
                confirmPassword = "weak-password"
            });

        var problem = await AssertProblemAsync(
            response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        problem.GetProperty("errors").TryGetProperty("password", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_Declares_Activation_Responses_And_Anonymous_Access()
    {
        await using var factory = new SpmeApiFactory();
        using var client = CreateClient(factory);
        var response = await client.GetAsync("/openapi/v2.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");

        var create = paths.GetProperty("/api/v2/auth/account-activations/challenges").GetProperty("post");
        create.GetProperty("responses").EnumerateObject().Select(item => item.Name)
            .Should().BeEquivalentTo(["202", "404", "409", "422", "429"]);
        create.TryGetProperty("security", out _).Should().BeFalse();

        var verify = paths.GetProperty("/api/v2/auth/account-activations/challenges/{challengeId}/verify")
            .GetProperty("post");
        verify.GetProperty("responses").EnumerateObject().Select(item => item.Name)
            .Should().BeEquivalentTo(["200", "422", "429"]);
        verify.TryGetProperty("security", out _).Should().BeFalse();

        var complete = paths.GetProperty("/api/v2/auth/account-activations/complete").GetProperty("post");
        complete.GetProperty("responses").EnumerateObject().Select(item => item.Name)
            .Should().BeEquivalentTo(["204", "422", "429"]);
        complete.TryGetProperty("security", out _).Should().BeFalse();
    }

    private static async Task<(Guid ChallengeId, string Code)> CreateChallengeAndReadCodeAsync(
        SpmeApiFactory factory,
        HttpClient client,
        TestIdentity identity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v2/auth/account-activations/challenges",
            new { identifier = identity.StaffId, contact = identity.Email });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var challengeId = json.RootElement.GetProperty("challengeId").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var body = await db.CommunicationOutboxMessages.AsNoTracking()
            .Where(item => item.IdempotencyKey == $"account-activation:{challengeId}:email")
            .Select(item => item.Body)
            .SingleAsync();
        return (challengeId, Regex.Match(body, @"\b\d{6}\b").Value);
    }

    private static async Task<TestIdentity> CreateEmployeeAsync(
        SpmeApiFactory factory,
        string? staffId = null,
        string? email = null,
        string? password = null)
    {
        var suffix = Guid.NewGuid().ToString("N");
        staffId ??= $"ACT/{suffix[..12]}";
        email ??= $"activation.{suffix}@csir.local";
        var phone = "024" + Random.Shared.Next(1000000, 9999999);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SpmeDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var institute = new Institute($"A{suffix}"[..12], $"Activation Institute {suffix}", "institute");
        var employee = new Employee(institute.Id, staffId, "Employee", "unspecified");
        employee.UpdateImportedProfile(null, "Activation", null, "Ghanaian", null, null, email, phone, true);
        db.Institutes.Add(institute);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        var user = new User(email, "Employee") { Email = email, PhoneNumber = phone };
        user.LinkEmployee(employee.Id, institute.Id);
        IdentityResult created;
        if (password is null)
        {
            user.MarkPasswordResetRequired();
            created = await userManager.CreateAsync(user);
        }
        else
        {
            created = await userManager.CreateAsync(user, password);
        }
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(error => error.Description)));
        return new(staffId, email);
    }

    private static HttpClient CreateClient(SpmeApiFactory factory) => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    private static async Task<int> CountChallengesAsync(SpmeApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SpmeDbContext>()
            .AccountActivationChallenges.CountAsync();
    }

    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement.Clone();
        root.GetProperty("status").GetInt32().Should().Be((int)status);
        root.GetProperty("code").GetString().Should().Be(code);
        root.GetProperty("errorCode").GetString().Should().Be(code);
        root.GetProperty("type").GetString().Should().Be(
            $"https://api.csir.example/problems/{code.Replace('_', '-')}");
        return root;
    }

    private sealed record TestIdentity(string StaffId, string Email);
}
