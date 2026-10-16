using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Controllers;

public abstract class AppController : Controller
{
    protected readonly AppDbContext Db;

    protected AppController(AppDbContext db)
    {
        Db = db;
    }

    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected bool IsAdmin => User.IsInRole(Roles.Admin);

    /// <summary>
    /// True when the signed-in user holds a granted capability. Administrators are superadmins and
    /// always pass, so permission checks never need to special-case the Admin role.
    /// </summary>
    protected bool HasPermission(string permission) =>
        IsAdmin || User.HasClaim(Permissions.ClaimType, permission);

    /// <summary>Sport ids the signed-in user may work with. Null means all sports (Admin).</summary>
    protected async Task<List<int>?> AllowedSportIdsAsync()
    {
        if (IsAdmin) return null;
        var userId = CurrentUserId;
        return await Db.SportAssignments
            .Where(x => x.UserId == userId)
            .Select(x => x.SportId)
            .ToListAsync();
    }

    protected static bool CanAccessSport(List<int>? allowed, int sportId) =>
        allowed == null || allowed.Contains(sportId);

    /// <summary>
    /// The course the signed-in user is tied to. Admins have no course limit (null), and so do
    /// accounts that were never given a course. Students and course-scoped staff return their label.
    /// </summary>
    protected async Task<string?> CurrentUserCourseAsync()
    {
        if (IsAdmin) return null;
        var userId = CurrentUserId;
        return await Db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CourseOrGrade)
            .FirstOrDefaultAsync();
    }

    /// <summary>True when a schedule's course is open to the given course (null = open to every course).</summary>
    protected static bool MatchesCourse(string? scheduleCourse, string? userCourse) =>
        string.IsNullOrWhiteSpace(scheduleCourse)
        || string.IsNullOrWhiteSpace(userCourse)
        || string.Equals(scheduleCourse, userCourse, StringComparison.OrdinalIgnoreCase);

    protected void Success(string message) => TempData["Success"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;
    protected void Failure(string message) => TempData["Error"] = message;
}
