using Scalar.AspNetCore;
using Csir.Spme.Application.Common.Interfaces;
using Csir.Spme.Application;
using Csir.Spme.Application.Common.Pagination;
using Csir.Spme.Application.Knowledge;
using Csir.Spme.Application.Plan;
using Csir.Spme.Application.Projects;
using Csir.Spme.Application.Promotions;
using Csir.Spme.Application.Reporting;
using Csir.Spme.Api.Endpoints.V2;
using Csir.Spme.Api.OpenApi;
using Csir.Spme.Infrastructure;
using Csir.Spme.Infrastructure.Jobs;
using Csir.Spme.Api.Middleware;
using Csir.Spme.Api.Auth;
using Csir.Spme.Api.Realtime;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Security.Claims;
using Prometheus;
using Csir.Spme.ServiceDefaults;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var isOpenApiDocumentGeneration = args.Any(argument =>
    argument.StartsWith("--applicationName=", StringComparison.OrdinalIgnoreCase));

if (isOpenApiDocumentGeneration)
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["AccountActivation:HashKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["PasswordReset:HashKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["DatabaseProvider:UseSqlite"] = "true",
        ["DatabaseProvider:SqlitePath"] = ":memory:",
        ["Storage:Provider"] = "local",
        ["Messaging:DispatcherEnabled"] = "false"
    });
}

builder.AddSpmeServiceDefaults();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var problem = context.ProblemDetails;
        if (problem is Microsoft.AspNetCore.Http.HttpValidationProblemDetails)
        {
            problem.Status = StatusCodes.Status422UnprocessableEntity;
            context.HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            problem.Title = "One or more fields are invalid.";
            problem.Extensions["code"] = "validation_failed";
            problem.Extensions["errorCode"] = "validation_failed";
        }

        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        var code = problem.Extensions.TryGetValue("code", out var codeValue) && codeValue is string explicitCode
            ? explicitCode
            : problem.Extensions.TryGetValue("errorCode", out var legacyCodeValue) && legacyCodeValue is string legacyCode
                ? legacyCode
                : status switch
                {
                    StatusCodes.Status400BadRequest => "malformed_request",
                    StatusCodes.Status401Unauthorized => "unauthenticated",
                    StatusCodes.Status403Forbidden => "forbidden",
                    StatusCodes.Status404NotFound => "not_found",
                    StatusCodes.Status409Conflict => "conflict",
                    StatusCodes.Status412PreconditionFailed => "concurrency_conflict",
                    StatusCodes.Status413PayloadTooLarge => "payload_too_large",
                    StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
                    StatusCodes.Status422UnprocessableEntity => "validation_failed",
                    StatusCodes.Status429TooManyRequests => "rate_limited",
                    StatusCodes.Status503ServiceUnavailable => "dependency_unavailable",
                    _ => "internal_error"
                };

        problem.Status = status;
        problem.Extensions["code"] = code;
        problem.Extensions.TryAdd("errorCode", code);
        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        problem.Extensions["requestId"] = context.HttpContext.TraceIdentifier;
        problem.Instance ??= context.HttpContext.Request.Path;
        if (string.IsNullOrWhiteSpace(problem.Type) ||
            string.Equals(problem.Type, "about:blank", StringComparison.OrdinalIgnoreCase) ||
            problem.Type.Contains("rfc9110", StringComparison.OrdinalIgnoreCase) ||
            problem.Type.Contains("rfc9457", StringComparison.OrdinalIgnoreCase))
        {
            problem.Type = $"https://api.csir.example/problems/{code.Replace('_', '-')}";
        }
    };
});
builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();
builder.Services.AddValidation();
builder.Services.AddOpenApi("v2", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerAuthorizationOperationTransformer>();
    options.AddOperationTransformer<IdempotencyOperationTransformer>();
});
builder.Services.AddSignalR();

builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests.",
            detail: "The request rate limit for this client IP address has been exceeded. Try again later.",
            type: "https://api.csir.example/problems/rate-limited",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "rate_limited",
                ["errorCode"] = "rate_limited"
            }).ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("promotion-status-lookup", context =>
    {
        var partitionKey = context.User.FindFirst("employee_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("password-reset", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Clamp(builder.Configuration.GetValue("PasswordReset:PermitLimit", 5), 1, 100),
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("login", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("token-refresh", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("account-activation", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("password-change", context =>
    {
        var partitionKey = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true
        });
    });
    options.AddPolicy("profile-image-upload", context =>
    {
        var partitionKey = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SpmeDbContext>("database", tags: ["ready"]);
if (!isOpenApiDocumentGeneration)
{
    builder.Services.AddHostedService<IdentitySeedHostedService>();
    builder.Services.AddHostedService<PromotionRequirementTemplateSeedHostedService>();
}

builder.Services.AddOutputCache(options =>
{
    options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(60);
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<PaginationOptions>(builder.Configuration.GetSection(PaginationOptions.SectionName));
builder.Services.AddSingleton<ICursorCodec>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PaginationOptions>>().Value;
    var key = options.CursorSigningKey
        ?? builder.Configuration.GetSection("Jwt").GetValue<string>("Key")
        ?? throw new InvalidOperationException("Pagination:CursorSigningKey or Jwt:Key is required.");
    return new HmacCursorCodec(key);
});
builder.Services.AddScoped<PromotionReportService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();
var isProduction = app.Environment.IsProduction();

if (isProduction)
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<CorrelationIdMiddleware>();
if (!isProduction)
{
    app.UseHttpMetrics();
}
app.UseResponseCompression();
app.UseOutputCache();
app.UseCors();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<InstituteAccessMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapV2Endpoints();
app.MapHub<HrRealtimeHub>("/hubs/hr", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
if (!isProduction)
{
    app.MapMetrics("/metrics").AllowAnonymous().ExcludeFromDescription();
}

var openApi = app.MapOpenApi("/openapi/{documentName}.json");
var scalar = app.MapScalarApiReference("/scalar/v2", options =>
{
    options.WithTitle("CSIR SPME API V2")
           .AddDocument("v2", "CSIR SPME API V2", "../../openapi/v2.json", isDefault: true)
           .WithTheme(ScalarTheme.Purple)
           .WithSearchHotKey("s")
           .DisableDefaultFonts();
});

if (isProduction)
{
    openApi.RequireAuthorization(AuthorizationPolicies.PlatformAdmin);
    scalar.RequireAuthorization(AuthorizationPolicies.PlatformAdmin);
}
else
{
    openApi.AllowAnonymous();
    scalar.AllowAnonymous();
}

app.Run();
