using System.Diagnostics;

namespace Csir.Spme.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = GetCorrelationId(context.Request.Headers[CorrelationHeader]);
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    private static string GetCorrelationId(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 128 && candidate.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_')
            ? candidate
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
