using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Csir.Spme.Application.Common.Interfaces;

namespace Csir.Spme.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    IProblemDetailsService problemDetailsService,
    ILogger<ExceptionHandlingMiddleware> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, code, title) = exception switch
        {
            BadHttpRequestException badRequest when
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge =>
                (StatusCodes.Status413PayloadTooLarge, "payload_too_large", "The request is too large."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "malformed_request", "The request is malformed."),
            FileStorageUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "dependency_unavailable",
                    "A required dependency is temporarily unavailable."),
            _ =>
                (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.")
        };

        if (exception is BadHttpRequestException)
        {
            logger.LogDebug("Malformed request processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://api.csir.example/problems/{code.Replace('_', '-')}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["errorCode"] = code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
