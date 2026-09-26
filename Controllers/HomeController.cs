using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

public class HomeController : AppController
{
    public HomeController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(Roles.Student))
        {
            var userId = CurrentUserId;
            var entries = await Db.Applicants
                .Include(a => a.Sport)
                .Include(a => a.Schedule)
                .Include(a => a.Evaluations)
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Sport!.Name)
                .ToListAsync();

            var schedulesPerEntry = new Dictionary<int, List<TryoutSchedule>>();
            var now = DateTime.Now;
            foreach (var e in entries)
            {
                schedulesPerEntry[e.Id] = await Db.Schedules
                    .Include(t => t.Sport)
                    .AsNoTracking()
                    .Where(t => t.SportId == e.SportId && !t.IsCancelled && t.EndAt >= now)
                    .OrderBy(t => t.StartAt)
                    .ToListAsync();
            }

            return View("StudentIndex", new StudentDashboardViewModel
            {
                Applicant = entries.OrderBy(a => a.Id).FirstOrDefault(),
                TryoutEntries = entries,
                SchedulesPerEntry = schedulesPerEntry
            });
        }

        var allowed = await AllowedSportIdsAsync();
        var now2 = DateTime.Now;

        var applicants = await Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .AsNoTracking()
            .ToListAsync();

        var sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        var upcoming = await Db.Schedules
            .ScopeTo(allowed)
            .Include(t => t.Sport)
            .AsNoTracking()
            .Where(t => !t.IsCancelled && t.EndAt >= now2)
            .OrderBy(t => t.StartAt)
            .Take(5)
            .ToListAsync();

        var upcomingCount = await Db.Schedules
            .ScopeTo(allowed)
            .CountAsync(t => !t.IsCancelled && t.EndAt >= now2);

        var model = new DashboardViewModel
        {
            TotalApplicants = applicants.Count,
            SportCount = sports.Count,
            UpcomingTryouts = upcomingCount,
            AwaitingEvaluation = applicants.Count(a => a.Evaluations.Count == 0),
            Selected = applicants.Count(a => a.Status == SelectionStatus.Selected),
            Qualified = applicants.Count(a => a.IsQualified),
            UserCount = IsAdmin ? await Db.Users.CountAsync() : 0,
            Upcoming = upcoming,
            Recent = applicants.OrderByDescending(a => a.RegisteredAt).Take(6).ToList(),
            PerSport = sports
                .Select(s => (s.Name, s.Icon, applicants.Count(a => a.SportId == s.Id)))
                .ToList(),
            RegisteredCounts = applicants
                .Where(a => a.TryoutScheduleId.HasValue)
                .GroupBy(a => a.TryoutScheduleId!.Value)
                .ToDictionary(g => g.Key, g => g.Count())
        };
        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Status(int code = 404)
    {
        ViewBag.Code = code;
        Response.StatusCode = code;
        return View();
    }
}
