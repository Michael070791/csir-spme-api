namespace Csir.Spme.Api.Middleware;

public class InstituteAccessMiddleware
{
    private readonly RequestDelegate _next;

    public InstituteAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract institute ID from JWT claim or header
        var instituteClaim = context.User?.FindFirst("institute_id")?.Value;
        if (!string.IsNullOrEmpty(instituteClaim) && Guid.TryParse(instituteClaim, out var instituteId))
        {
            context.Items["InstituteId"] = instituteId;
        }
        else
        {
            // Allow requests without institute context to proceed
            // Controller actions with institute scope will enforce the requirement
        }

        await _next(context);
    }
}
