using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

/// <summary>
/// A persisted, per-user in-app notification. Unlike the live SignalR bell, these are stored so a
/// student can read them later — selection results, new tryouts, account updates and staff notices.
/// A broadcast writes one row per recipient, so every notification has a single owner.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>Recipient of the notification.</summary>
    public int UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Bootstrap Icons name (without the "bi-" prefix) shown beside the notification.</summary>
    [StringLength(40)]
    public string Icon { get; set; } = "bell-fill";

    /// <summary>Optional in-app destination opened when the notification is clicked.</summary>
    [StringLength(260)]
    public string? Url { get; set; }

    [StringLength(30)]
    public string Category { get; set; } = NotificationCategories.General;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Friendly "5m ago" style age for the notification lists.</summary>
    public string AgeText
    {
        get
        {
            var span = DateTime.Now - CreatedAt;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return CreatedAt.ToString("MMM d");
        }
    }
}

/// <summary>Groups notifications so the UI can colour and label them.</summary>
public static class NotificationCategories
{
    public const string Selection = "selection";
    public const string Schedule = "schedule";
    public const string Verification = "verification";
    public const string Account = "account";
    public const string General = "general";

    public static string Label(string category) => category switch
    {
        Selection => "Selection",
        Schedule => "Tryout",
        Verification => "Verification",
        Account => "Account",
        _ => "Notice"
    };

    public static string BadgeClass(string category) => category switch
    {
        Selection => "badge-soft-info",
        Schedule => "badge-soft-success",
        Verification => "badge-soft-warning",
        Account => "badge-soft-danger",
        _ => "badge-soft-muted"
    };
}
