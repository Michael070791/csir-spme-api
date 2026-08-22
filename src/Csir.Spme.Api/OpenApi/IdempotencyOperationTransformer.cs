using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Csir.Spme.Api.OpenApi;

internal sealed class IdempotencyOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] ProtectedRoots =
    [
        "/api/v2/reporting-periods",
        "/api/v2/reports",
        "/api/v2/strategic-plans",
        "/api/v2/thrusts",
        "/api/v2/outputs",
        "/api/v2/indicators",
        "/api/v2/indicator-measurements",
        "/api/v2/projects",
        "/api/v2/technologies",
        "/api/v2/leave-requests",
        "/api/v2/appraisal-cycles",
        "/api/v2/performance-appraisals"
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Description.HttpMethod, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var path = "/" + (context.Description.RelativePath ?? string.Empty).TrimStart('/');
        if (path.Equals("/api/v2/leave-requests/calculate-working-days", StringComparison.OrdinalIgnoreCase) ||
            !ProtectedRoots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            return Task.CompletedTask;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Unique retry key retained for 24 hours. Reuse with a changed payload returns idempotency_key_reused.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
        return Task.CompletedTask;
    }
}
