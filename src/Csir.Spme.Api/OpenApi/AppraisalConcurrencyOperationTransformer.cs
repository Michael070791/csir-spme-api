using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Csir.Spme.Api.OpenApi;

internal sealed class AppraisalConcurrencyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!RequiresIfMatch(context.Description.HttpMethod, context.Description.RelativePath))
            return Task.CompletedTask;

        operation.Parameters ??= [];
        if (operation.Parameters.Any(parameter =>
                parameter.In == ParameterLocation.Header &&
                string.Equals(parameter.Name, "If-Match", StringComparison.OrdinalIgnoreCase)))
            return Task.CompletedTask;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "If-Match",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Current opaque ETag returned by the appraisal resource. Missing or stale values return precondition_failed.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
        return Task.CompletedTask;
    }

    private static bool RequiresIfMatch(string? method, string? relativePath)
    {
        var isPatch = string.Equals(method, HttpMethods.Patch, StringComparison.OrdinalIgnoreCase);
        var isPut = string.Equals(method, HttpMethods.Put, StringComparison.OrdinalIgnoreCase);
        var isPost = string.Equals(method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase);
        if (!isPatch && !isPut && !isPost)
            return false;

        var path = "/" + (relativePath ?? string.Empty).TrimStart('/');
        if (path.StartsWith("/api/v2/performance-appraisals/", StringComparison.OrdinalIgnoreCase))
            return true;

        return !path.Equals("/api/v2/appraisal-cycles", StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith("/api/v2/appraisal-cycles/", StringComparison.OrdinalIgnoreCase);
    }
}
