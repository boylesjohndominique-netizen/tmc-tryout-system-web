using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

/// <summary>
/// Lets students browse every sport they can try out for, with its coaches,
/// current and recent players, and tryout schedules.
/// </summary>
[AllowAnonymous]
public class ExploreController : AppController
{
    public ExploreController(AppDbContext db) : base(db) { }

    // Signed-in users can open this; the layout gates the nav link to students.
    public async Task<IActionResult> Index()
    {
        var sports = await Db.Sports
            .Include(s => s.Staff).ThenInclude(x => x.User)
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var now = DateTime.Now;
        var tryoutCounts = await Db.Schedules
            .Where(t => !t.IsCancelled && t.EndAt >= now)
            .GroupBy(t => t.SportId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var playerCounts = await Db.Applicants
            .Where(a => a.Status == SelectionStatus.Selected)
            .GroupBy(a => a.SportId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var nextSchedules = await Db.Schedules
            .Where(t => !t.IsCancelled && t.EndAt >= now)
            .GroupBy(t => t.SportId)
            .Select(g => new { SportId = g.Key, NextId = g.OrderBy(t => t.StartAt).Select(t => t.Id).First() })
            .ToDictionaryAsync(x => x.SportId, x => x.NextId);

        var mySportIds = User.Identity?.IsAuthenticated == true
            ? await Db.Applicants
                .Where(a => a.UserId == CurrentUserId)
                .Select(a => a.SportId)
                .ToListAsync()
            : new List<int>();

        return View(new ExploreSportsViewModel
        {
            Sports = sports.Select(s => new ExploreSportCard
            {
                Sport = s,
                TryoutCount = tryoutCounts.TryGetValue(s.Id, out var tc) ? tc : 0,
                PlayerCount = playerCounts.TryGetValue(s.Id, out var pc) ? pc : 0,
                NextScheduleId = nextSchedules.TryGetValue(s.Id, out var nid) ? nid : 0
            }).ToList(),
            MyTryoutSportIds = mySportIds.ToHashSet()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var sport = await Db.Sports
            .Include(s => s.Staff).ThenInclude(x => x.User)
            .Include(s => s.Schedules)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sport == null) return NotFound();

        var applicants = await Db.Applicants
            .Include(a => a.Evaluations)
            .Include(a => a.Schedule)
            .AsNoTracking()
            .Where(a => a.SportId == id)
            .ToListAsync();
        // OverallScore is computed from evaluations, so order in memory after loading.
        applicants = applicants.OrderByDescending(a => a.OverallScore ?? 0).ToList();

        var selected = applicants
            .Where(a => a.Status == SelectionStatus.Selected)
            .Select(a => new PlayerCard { Applicant = a, Score = a.OverallScore, Status = a.Status.DisplayName() })
            .ToList();

        var recent = applicants
            .Where(a => a.Status != SelectionStatus.Selected
                        && a.Status != SelectionStatus.NotSelected
                        && (a.Status == SelectionStatus.ForFurtherEvaluation || a.RegisteredAt >= DateTime.Now.AddDays(-60)))
            .OrderByDescending(a => a.RegisteredAt)
            .Take(12)
            .Select(a => new PlayerCard { Applicant = a, Score = a.OverallScore, Status = a.Status.DisplayName() })
            .ToList();

        var now = DateTime.Now;
        var upcoming = sport.Schedules
            .Where(t => !t.IsCancelled && t.EndAt >= now)
            .OrderBy(t => t.StartAt)
            .ToList();
        var past = sport.Schedules
            .Where(t => !t.IsCancelled && t.EndAt < now)
            .OrderByDescending(t => t.StartAt)
            .Take(5)
            .ToList();

        bool already = User.Identity?.IsAuthenticated == true &&
                       await Db.Applicants.AnyAsync(a => a.UserId == CurrentUserId && a.SportId == id);

        return View(new ExploreSportDetailsViewModel
        {
            Sport = sport,
            Coaches = sport.Staff.Where(x => x.User?.Role == Roles.Coach).Select(x => x.User!).ToList(),
            Evaluators = sport.Staff.Where(x => x.User?.Role == Roles.Evaluator).Select(x => x.User!).ToList(),
            CurrentPlayers = selected,
            RecentPlayers = recent,
            UpcomingSchedules = upcoming,
            PastSchedules = past,
            AlreadyTryingOut = already
        });
    }
}
