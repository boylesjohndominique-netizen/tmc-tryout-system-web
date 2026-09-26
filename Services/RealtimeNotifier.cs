using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Services;

/// <summary>
/// Real-time push channel. On connect a user is added to:
/// user-{id} (personal notifications), staff (coaches/admin/evaluators),
/// sport-{id} for every sport they coach or evaluate, and sport-{id} for every sport
/// the student has registered for. Controllers broadcast after SaveChangesAsync.
/// </summary>
[Authorize]
public sealed class TryoutHub : Hub
{
    // Group names are derived from claims set at sign-in; nothing to inject.
    private static string UserGroup(int id) => $"user-{id}";
    private static string StaffGroup() => "staff";
    private static string SportGroup(int id) => $"sport-{id}";

    public override async Task OnConnectedAsync()
    {
        var ctx = Context.GetHttpContext();
        if (ctx != null)
        {
            if (int.TryParse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));

            var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var isStaff = role is Roles.Admin or Roles.Coach or Roles.Evaluator;
            if (isStaff) await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup());

            var db = ctx.RequestServices.GetRequiredService<Data.AppDbContext>();
            var userId = uid;
            var sportIds = await db.SportAssignments.AsNoTracking()
                .Where(x => x.UserId == userId).Select(x => x.SportId).ToListAsync();
            if (!isStaff && userId != 0)
            {
                sportIds = await db.Applicants.AsNoTracking()
                    .Where(a => a.UserId == userId).Select(a => a.SportId).ToListAsync();
            }
            foreach (var sid in sportIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, SportGroup(sid));
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Heartbeat target the client pings to keep the connection alive through proxies.</summary>
    public Task Ping() => Task.CompletedTask;
}

/// <summary>Broadcast payload tags so clients can decide how to react.</summary>
public static class RealtimeEvents
{
    public const string DataChanged = "dataChanged";
    public const string NotifyUser = "notifyUser";
}

/// <summary>Areas of the app a change can affect; views map these to a refresh scope.</summary>
public static class RealtimeScopes
{
    public const string Dashboard = "dashboard";
    public const string Applicants = "applicants";
    public const string Evaluations = "evaluations";
    public const string Screening = "screening";
    public const string Selection = "selection";
    public const string Schedules = "schedules";
    public const string Sports = "sports";
    public const string Explore = "explore";
    public const string Registrations = "registrations";
    public const string Users = "users";
    public const string Reports = "reports";
}

/// <summary>Typed wrapper controllers use to push updates after a successful save.</summary>
public interface IRealtimeNotifier
{
    /// <summary>Refresh every open page that shows <paramref name="scopes"/>.</summary>
    Task ChangedAsync(params string[] scopes);

    /// <summary>Refresh + a bell notification for one user (selection results, verification...).</summary>
    Task NotifyUserAsync(int userId, string title, string message, string icon = "bell-fill", string[]? scopes = null);
}

public sealed class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<TryoutHub> _hub;
    private readonly ILogger<RealtimeNotifier> _logger;

    public RealtimeNotifier(IHubContext<TryoutHub> hub, ILogger<RealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task ChangedAsync(params string[] scopes) =>
        SafeSendAsync(new { scopes, at = DateTime.UtcNow }, RealtimeEvents.DataChanged, null);

    public Task NotifyUserAsync(int userId, string title, string message, string icon = "bell-fill", string[]? scopes = null) =>
        SafeSendAsync(new { title, message, icon, scopes = scopes ?? Array.Empty<string>(), at = DateTime.UtcNow },
            RealtimeEvents.NotifyUser, $"user-{userId}");

    private async Task SafeSendAsync(object payload, string evt, string? group)
    {
        try
        {
            if (group == null)
                await _hub.Clients.All.SendAsync(evt, payload);
            else
                await _hub.Clients.Group(group).SendAsync(evt, payload);
        }
        catch (Exception ex)
        {
            // A broadcast failure must never fail the user's own request.
            _logger.LogWarning(ex, "Realtime broadcast failed for event {Event}", evt);
        }
    }
}


