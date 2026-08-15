using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Csir.Spme.Api.Realtime;

[Authorize]
public sealed class HrRealtimeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HrRealtimeGroups.User(userId));
        }

        var instituteId = Context.User?.FindFirstValue("institute_id");
        if (!string.IsNullOrWhiteSpace(instituteId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HrRealtimeGroups.Institute(instituteId));
        }

        foreach (var role in Context.User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value) ?? [])
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HrRealtimeGroups.Role(role));
        }

        await base.OnConnectedAsync();
    }

    public Task Ping() => Clients.Caller.SendAsync("pong", DateTimeOffset.UtcNow);
}

public static class HrRealtimeGroups
{
    public static string User(string userId) => $"user:{userId}";

    public static string Institute(string instituteId) => $"institute:{instituteId}";

    public static string Role(string role) => $"role:{role}";
}

public sealed record ResourceChangedMessage(
    string Resource,
    string Action,
    string Id,
    Guid? InstituteId,
    DateTimeOffset OccurredAt);
