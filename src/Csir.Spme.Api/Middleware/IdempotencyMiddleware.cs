using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Csir.Spme.Domain.Common;
using Csir.Spme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Csir.Spme.Api.Middleware;

internal sealed class IdempotencyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly int _maximumStoredResponseBytes = Math.Clamp(
        configuration.GetValue("Idempotency:MaximumStoredResponseBytes", 256 * 1024), 1, 1024 * 1024);

    public async Task InvokeAsync(HttpContext context, SpmeDbContext db)
    {
        if (!RequiresIdempotency(context.Request))
        {
            await next(context);
            return;
        }

        var key = context.Request.Headers["Idempotency-Key"].ToString().Trim();
        if (key.Length is 0 or > 256)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "idempotency_key_required",
                "A valid Idempotency-Key header is required.");
            return;
        }

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var scope = $"{subject}:{context.Request.Method}:{context.Request.Path}";
        if (scope.Length > 128) scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        var requestHash = await HashRequestAsync(context.Request, context.RequestAborted);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, context.RequestAborted);
        var existing = await db.IdempotencyRecords
            .SingleOrDefaultAsync(x => x.Scope == scope && x.IdempotencyKey == key, context.RequestAborted);
        if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow)
        {
            await transaction.RollbackAsync(context.RequestAborted);
            await ReplayOrRejectAsync(context, existing, requestHash);
            return;
        }

        if (existing is not null)
        {
            db.IdempotencyRecords.Remove(existing);
            await db.SaveChangesAsync(context.RequestAborted);
        }

        var reservation = new IdempotencyRecord(scope, key, requestHash, DateTimeOffset.UtcNow.AddHours(24));
        db.IdempotencyRecords.Add(reservation);
        try
        {
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            existing = await db.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Scope == scope && x.IdempotencyKey == key, context.RequestAborted);
            if (existing is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status409Conflict, "idempotency_request_in_progress",
                    "A concurrent request is reserving this Idempotency-Key. Retry with the same key.");
                return;
            }
            await ReplayOrRejectAsync(context, existing, requestHash);
            return;
        }

        var originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;
        try
        {
            await next(context);
            bufferedBody.Position = 0;
            var canStore = bufferedBody.Length <= _maximumStoredResponseBytes;
            var responseBytes = canStore ? bufferedBody.ToArray() : [];

            if (context.Response.StatusCode < 400 && canStore)
            {
                Complete(reservation, context, responseBytes);
                await db.SaveChangesAsync(context.RequestAborted);
                await transaction.CommitAsync(context.RequestAborted);
            }
            else if (context.Response.StatusCode < 400)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await transaction.DisposeAsync();
                db.ChangeTracker.Clear();
                bufferedBody.SetLength(0);
                context.Response.Clear();
                context.Response.Body = bufferedBody;
                await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                    "idempotency_response_too_large",
                    "The operation was rolled back because its response exceeded the replay safety limit.");
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await transaction.DisposeAsync();
                db.ChangeTracker.Clear();
                if (context.Response.StatusCode < 500 && canStore)
                {
                    var completed = new IdempotencyRecord(scope, key, requestHash, DateTimeOffset.UtcNow.AddHours(24));
                    Complete(completed, context, responseBytes);
                    db.IdempotencyRecords.Add(completed);
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }

            context.Response.Body = originalBody;
            bufferedBody.Position = 0;
            await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch
        {
            context.Response.Body = originalBody;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // The transaction was already committed; the mutation and replay record are durable together.
            }
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static void Complete(IdempotencyRecord reservation, HttpContext context, byte[] responseBytes) =>
        reservation.Complete(context.Response.StatusCode,
            responseBytes.Length == 0 ? null : Encoding.UTF8.GetString(responseBytes),
            context.Response.ContentType,
            context.Response.Headers.ETag.ToString(),
            context.Response.Headers.Location.ToString());

    private static bool RequiresIdempotency(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
            return false;

        var retryProtectedRoots = new[]
        {
            "/api/v2/reporting-periods",
            "/api/v2/reports",
            "/api/v2/staff-quarterly-reports",
            "/api/v2/promotion-submissions",
            "/api/v2/strategic-plans",
            "/api/v2/thrusts",
            "/api/v2/outputs",
            "/api/v2/indicators",
            "/api/v2/indicator-measurements",
            "/api/v2/projects",
            "/api/v2/technologies"
        };
        if (retryProtectedRoots.Any(root =>
                request.Path.StartsWithSegments(root, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!request.Path.StartsWithSegments("/api/v2/leave-requests", out var remaining))
            return false;

        if (!remaining.HasValue || remaining.Value == string.Empty || remaining.Value == "/") return true;
        var suffix = remaining.Value!;
        if (suffix.Equals("/calculate-working-days", StringComparison.OrdinalIgnoreCase)) return false;
        return suffix.EndsWith("/submit", StringComparison.OrdinalIgnoreCase) ||
               suffix.EndsWith("/approve", StringComparison.OrdinalIgnoreCase) ||
               suffix.EndsWith("/reject", StringComparison.OrdinalIgnoreCase) ||
               suffix.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase) ||
               suffix.EndsWith("/resume", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct);
        request.Body.Position = 0;
        var precondition = request.Headers.IfMatch.ToString().Trim();
        var fingerprint = new byte[buffer.Length + Encoding.UTF8.GetByteCount(precondition) + 1];
        buffer.ToArray().CopyTo(fingerprint, 0);
        fingerprint[buffer.Length] = 0;
        Encoding.UTF8.GetBytes(precondition).CopyTo(fingerprint, (int)buffer.Length + 1);
        return Convert.ToHexStringLower(SHA256.HashData(fingerprint));
    }

    private static Task ReplayOrRejectAsync(HttpContext context, IdempotencyRecord existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            return WriteProblemAsync(context, StatusCodes.Status409Conflict, "idempotency_key_reused",
                "The Idempotency-Key was already used for a different request or precondition.");
        if (!existing.IsComplete)
            return WriteProblemAsync(context, StatusCodes.Status409Conflict, "idempotency_request_in_progress",
                "A request with this Idempotency-Key is still in progress.");

        context.Response.StatusCode = existing.ResponseStatus;
        context.Response.ContentType = existing.ResponseContentType;
        if (!string.IsNullOrWhiteSpace(existing.ResponseEtag)) context.Response.Headers.ETag = existing.ResponseEtag;
        if (!string.IsNullOrWhiteSpace(existing.ResponseLocation)) context.Response.Headers.Location = existing.ResponseLocation;
        context.Response.Headers["Idempotent-Replayed"] = "true";
        return existing.ResponseBody is null ? Task.CompletedTask : context.Response.WriteAsync(existing.ResponseBody);
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://api.csir.example/problems/{code.Replace('_', '-')}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["errorCode"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
    }
}
