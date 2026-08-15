using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Csir.Spme.Api.OpenApi;

/// <summary>
/// Mirrors endpoint authorization metadata into the generated OpenAPI operation.
/// Anonymous endpoints deliberately remain undocumented as bearer-protected.
/// </summary>
internal sealed class BearerAuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        // AllowAnonymous wins over group-level RequireAuthorization metadata, matching
        // ASP.NET Core's authorization behavior.
        if (metadata?.OfType<IAllowAnonymous>().Any() == true)
        {
            operation.Security = null;
            return Task.CompletedTask;
        }

        if (metadata?.OfType<IAuthorizeData>().Any() != true)
        {
            return Task.CompletedTask;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("BearerAuth", context.Document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}
