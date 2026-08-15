using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Csir.Spme.Api.Endpoints.V2;

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", GetHealth)
            .WithName("Health_Liveness")
            .WithSummary("Check API liveness.")
            .ExcludeFromDescription()
            .AllowAnonymous();
        endpoints.MapGet("/healthz", GetHealthz)
            .WithName("Healthz_Liveness")
            .WithSummary("Check API liveness for orchestration.")
            .ExcludeFromDescription()
            .AllowAnonymous();
        endpoints.MapGet("/readyz", GetReadyzAsync)
            .WithName("Readyz_DependencyCheck")
            .WithSummary("Check database readiness for orchestration.")
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    /// <summary>Returns a successful API liveness response.</summary>
    private static Ok<HealthResponse> GetHealth() =>
        TypedResults.Ok(new HealthResponse("healthy", Version: "2.0.0"));

    /// <summary>Returns a successful orchestration liveness response.</summary>
    private static Ok<HealthResponse> GetHealthz() =>
        TypedResults.Ok(new HealthResponse("ok"));

    /// <summary>Returns readiness only when the configured database is reachable.</summary>
    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> GetReadyzAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
        if (report.Status == HealthStatus.Healthy)
        {
            return TypedResults.Ok(new HealthResponse("ready", "reachable"));
        }

        return TypedResults.Json(
            new HealthResponse("not-ready", "unreachable"),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
