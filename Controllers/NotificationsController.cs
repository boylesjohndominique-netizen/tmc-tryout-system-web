using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

/// <summary>
/// Persisted in-app notifications. Every signed-in user can read their own notifications; staff
/// (Admin, Coach, Evaluator) can also send a custom notification to students.
/// </summary>
public class NotificationsController : AppController
{
    private readonly NotificationService _notifications;

    public NotificationsController(AppDbContext db, NotificationService notifications) : base(db)
    {
        _notifications = notifications;
    }

    // ---------- The signed-in user's notifications ----------
    public async Task<IActionResult> Index(string? filter)
    {
        bool unreadOnly = string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase);
        return View(new NotificationListViewModel
        {
            Items = await _notifications.AllForUserAsync(CurrentUserId, unreadOnly),
            UnreadCount = await _notifications.UnreadCountAsync(CurrentUserId),
            UnreadOnly = unreadOnly
        });
    }

    /// <summary>Compact JSON payload the notification bell polls after a live push.</summary>
    [HttpGet]
    public async Task<IActionResult> Feed()
    {
        var userId = CurrentUserId;
        var items = await _notifications.RecentAsync(userId, 12);
        return Json(new
        {
            unread = await _notifications.UnreadCountAsync(userId),
            items = items.Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Icon,
                n.Url,
                n.Category,
                n.IsRead,
                age = n.AgeText
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl)
    {
        await _notifications.MarkReadAsync(CurrentUserId, id);
        return RedirectBack(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead(string? returnUrl)
    {
        await _notifications.MarkAllReadAsync(CurrentUserId);
        Success("All notifications were marked as read.");
        return RedirectBack(returnUrl);
    }

    // ---------- Staff: compose a notification ----------
    [Authorize(Roles = Roles.Staff)]
    [HttpGet]
    public async Task<IActionResult> Compose()
    {
        var model = new ComposeNotificationViewModel();
        await LoadOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost]
    public async Task<IActionResult> Compose(ComposeNotificationViewModel model)
    {
        if (!IsKnownAudience(model.Audience))
            ModelState.AddModelError(nameof(model.Audience), "Choose who should receive this notification.");
        else if (model.Audience == "course" && string.IsNullOrWhiteSpace(model.CourseOrGrade))
            ModelState.AddModelError(nameof(model.CourseOrGrade), "Choose a course.");
        else if (model.Audience == "sport" && model.SportId is null)
            ModelState.AddModelError(nameof(model.SportId), "Choose a sport.");
        else if (model.Audience == "student" && model.TargetUserId is null)
            ModelState.AddModelError(nameof(model.TargetUserId), "Choose a student.");

        // Only in-app links are allowed: block schemes like javascript: from being stored and clicked.
        if (!string.IsNullOrWhiteSpace(model.Url) && !model.Url.TrimStart().StartsWith('/'))
            ModelState.AddModelError(nameof(model.Url), "Enter an in-app link that starts with / (for example /Schedules).");

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(model);
            return View(model);
        }

        var recipients = await ResolveRecipientsAsync(model);
        if (recipients.Count == 0)
        {
            Warning("No students match that audience, so nothing was sent.");
            await LoadOptionsAsync(model);
            return View(model);
        }

        await _notifications.CreateManyAsync(recipients, model.Title, model.Message, model.Icon,
            string.IsNullOrWhiteSpace(model.Url) ? Url.Action(nameof(Index)) : model.Url,
            NotificationCategories.General);

        Success(recipients.Count == 1
            ? "The notification was sent to 1 student."
            : $"The notification was sent to {recipients.Count} students.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------
    private static bool IsKnownAudience(string? audience) =>
        audience is "all" or "course" or "sport" or "student";

    private async Task<List<int>> ResolveRecipientsAsync(ComposeNotificationViewModel model)
    {
        switch (model.Audience)
        {
            case "course":
                return await Db.Users
                    .Where(u => u.Role == Roles.Student && u.IsActive && u.CourseOrGrade == model.CourseOrGrade)
                    .Select(u => u.Id).Distinct().ToListAsync();
            case "sport":
                return await Db.Applicants
                    .Where(a => a.SportId == model.SportId && a.UserId != null)
                    .Select(a => a.UserId!.Value).Distinct().ToListAsync();
            case "student":
                var target = model.TargetUserId!.Value;
                if (await Db.Users.AnyAsync(u => u.Id == target && u.Role == Roles.Student))
                    return new List<int> { target };
                return new List<int>();
            default:
                return await Db.Users
                    .Where(u => u.Role == Roles.Student && u.IsActive)
                    .Select(u => u.Id).ToListAsync();
        }
    }

    private async Task LoadOptionsAsync(ComposeNotificationViewModel model)
    {
        model.Sports = await Db.Sports.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        model.Courses = UiHelpers.Courses.ToList();
        model.Students = await Db.Users.AsNoTracking()
            .Where(u => u.Role == Roles.Student && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new StudentOption { Id = u.Id, DisplayName = u.FullName, StudentId = u.StudentId })
            .ToListAsync();
    }

    private IActionResult RedirectBack(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
