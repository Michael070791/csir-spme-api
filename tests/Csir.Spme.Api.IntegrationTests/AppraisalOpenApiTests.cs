using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Csir.Spme.Api.IntegrationTests;

public sealed class AppraisalOpenApiTests(SpmeApiFactory factory) : IClassFixture<SpmeApiFactory>
{
    [Fact]
    public async Task Appraisal_Contract_Documents_Canonical_Routes_Security_Concurrency_And_Retry_Headers()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v2.json");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v2/appraisal-cycles", out var cycles).Should().BeTrue();
        cycles.TryGetProperty("get", out _).Should().BeTrue();
        cycles.TryGetProperty("post", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/appraisal-cycles/{id}/activate", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/appraisal-cycles/{id}/roster", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/appraisal-cycles/{id}/metrics", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/appraisal-cycles/{id}/reminders", out var reminders).Should().BeTrue();

        paths.TryGetProperty("/api/v2/performance-appraisals/me", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/review-queue", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/planning", out var planning).Should().BeTrue();
        planning.TryGetProperty("patch", out var planningPatch).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/submit-planning", out var submitPlanning).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/midyear-signature", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/staff-signature", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/director-approve", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v2/performance-appraisals/{id}/document", out var documentPath).Should().BeTrue();
        documentPath.TryGetProperty("get", out _).Should().BeTrue();

        AssertProtectedMutation(planningPatch, expectIdempotency: false, expectValidation: true);
        AssertProtectedMutation(submitPlanning.GetProperty("post"), expectIdempotency: true, expectValidation: true);
        AssertProtectedMutation(reminders.GetProperty("post"), expectIdempotency: true, expectValidation: false);
    }

    private static void AssertProtectedMutation(JsonElement operation, bool expectIdempotency, bool expectValidation)
    {
        operation.GetProperty("security").GetArrayLength().Should().BeGreaterThan(0);
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToList();
        parameters.Should().Contain(parameter =>
            parameter.GetProperty("in").GetString() == "header" &&
            parameter.GetProperty("name").GetString() == "If-Match" &&
            parameter.GetProperty("required").GetBoolean());
        parameters.Any(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key")
            .Should().Be(expectIdempotency);

        var responses = operation.GetProperty("responses");
        var expectedResponses = expectValidation
            ? new[] { "400", "401", "403", "404", "409", "412", "422" }
            : new[] { "401", "403", "404", "409", "412" };
        foreach (var status in expectedResponses)
            responses.TryGetProperty(status, out _).Should().BeTrue($"because appraisal mutations document {status}");
    }
}
