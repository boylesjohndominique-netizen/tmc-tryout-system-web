using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Services;

/// <summary>
/// Creates and reads the persisted in-app notifications students see in the bell and on the
/// Notifications page. Every new notification is also pushed live through <see cref="IRealtimeNotifier"/>,
/// so the bell updates without a refresh.
/// </summary>
public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IRealtimeNotifier _rt;

    public NotificationService(AppDbContext db, IRealtimeNotifier rt)
    {
        _db = db;
        _rt = rt;
    }

    /// <summary>Stores one notification for a single user and pushes it live.</summary>
    public Task CreateAsync(int userId, string title, string message, string icon,
        string? url, string category) =>
        CreateManyAsync(new[] { userId }, title, message, icon, url, category);

    /// <summary>Stores the same notification for several users (a broadcast) in one save.</summary>
    public async Task CreateManyAsync(IEnumerable<int> userIds, string title, string message,
        string icon, string? url, string category)
    {
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        var now = DateTime.Now;
        var titleText = Truncate(title, 120);
        var messageText = Truncate(message, 500);
        var iconName = string.IsNullOrWhiteSpace(icon) ? "bell-fill" : Truncate(icon, 40);
        var link = string.IsNullOrWhiteSpace(url) ? null : Truncate(url, 260);
        var group = string.IsNullOrWhiteSpace(category) ? NotificationCategories.General : Truncate(category, 30);

        _db.Notifications.AddRange(ids.Select(id => new Notification
        {
            UserId = id,
            Title = titleText,
            Message = messageText,
            Icon = iconName,
            Url = link,
            Category = group,
            CreatedAt = now
        }));
        await _db.SaveChangesAsync();

        foreach (var id in ids)
            await _rt.NotifyUserAsync(id, titleText, messageText, iconName);
    }

    public Task<List<Notification>> RecentAsync(int userId, int take = 12) =>
        _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .ToListAsync();

    public Task<List<Notification>> AllForUserAsync(int userId, bool unreadOnly)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync();
    }

    public Task<int> UnreadCountAsync(int userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public Task MarkReadAsync(int userId, int id) =>
        _db.Notifications.Where(n => n.UserId == userId && n.Id == id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public Task MarkAllReadAsync(int userId) =>
        _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    private static string Truncate(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
