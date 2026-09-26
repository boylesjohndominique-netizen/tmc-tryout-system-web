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

    protected void Success(string message) => TempData["Success"] = message;
    protected void Warning(string message) => TempData["Warning"] = message;
    protected void Failure(string message) => TempData["Error"] = message;
}
