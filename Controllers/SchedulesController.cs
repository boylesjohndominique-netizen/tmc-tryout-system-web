using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

public class SchedulesController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public SchedulesController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    // Everyone signed in can see schedules; students only see what is coming up.
    public async Task<IActionResult> Index(string? view, int? sportId)
    {
        bool isStudent = User.IsInRole(Roles.Student);
        var allowed = isStudent ? null : await AllowedSportIdsAsync();

        var all = await Db.Schedules
            .ScopeTo(allowed)
            .Include(t => t.Sport)
            .AsNoTracking()
            .ToListAsync();

        if (isStudent)
        {
            all = all.Where(t => t.Status == ScheduleStatus.Upcoming || t.Status == ScheduleStatus.Ongoing).ToList();
            view = "upcoming";
        }

        view = string.IsNullOrWhiteSpace(view) ? "upcoming" : view.ToLowerInvariant();

        var scoped = sportId.HasValue ? all.Where(t => t.SportId == sportId.Value).ToList() : all;

        List<TryoutSchedule> shown = view switch
        {
            "completed" => scoped.Where(t => t.Status == ScheduleStatus.Completed).OrderByDescending(t => t.StartAt).ToList(),
            "cancelled" => scoped.Where(t => t.Status == ScheduleStatus.Cancelled).OrderByDescending(t => t.StartAt).ToList(),
            "all" => scoped.OrderByDescending(t => t.StartAt).ToList(),
            _ => scoped.Where(t => t.Status == ScheduleStatus.Upcoming || t.Status == ScheduleStatus.Ongoing).OrderBy(t => t.StartAt).ToList()
        };

        var ids = shown.Select(t => t.Id).ToList();
        var counts = await Db.Applicants
            .Where(a => a.TryoutScheduleId != null && ids.Contains(a.TryoutScheduleId.Value))
            .GroupBy(a => a.TryoutScheduleId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        return View(new ScheduleListViewModel
        {
            Schedules = shown,
            Sports = sports,
            RegisteredCounts = counts,
            View = view,
            SportId = sportId,
            UpcomingCount = scoped.Count(t => t.Status == ScheduleStatus.Upcoming || t.Status == ScheduleStatus.Ongoing),
            CompletedCount = scoped.Count(t => t.Status == ScheduleStatus.Completed),
            CancelledCount = scoped.Count(t => t.Status == ScheduleStatus.Cancelled),
            AllCount = scoped.Count
        });
    }

    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Details(int id)
    {
        var schedule = await Db.Schedules
            .Include(t => t.Sport)
            .Include(t => t.Applicants).ThenInclude(a => a.Evaluations)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (schedule == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, schedule.SportId)) return Forbid();

        return View(schedule);
    }

    // ---------- Create ----------
    [Authorize(Roles = Roles.Deciders)]
    [HttpGet]
    public async Task<IActionResult> Create(int? sportId)
    {
        var model = new ScheduleFormViewModel
        {
            SportId = sportId,
            Date = DateTime.Today.AddDays(7),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(12, 0, 0)
        };
        await LoadSportsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Deciders)]
    [HttpPost]
    public async Task<IActionResult> Create(ScheduleFormViewModel model)
    {
        var allowed = await AllowedSportIdsAsync();
        if (model.SportId.HasValue && !CanAccessSport(allowed, model.SportId.Value))
            ModelState.AddModelError(nameof(model.SportId), "You can only schedule tryouts for your assigned sports.");

        if (!ModelState.IsValid)
        {
            await LoadSportsAsync(model);
            return View(model);
        }

        var schedule = new TryoutSchedule();
        Apply(schedule, model);
        Db.Schedules.Add(schedule);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Schedules, RealtimeScopes.Dashboard, RealtimeScopes.Registrations, RealtimeScopes.Explore, RealtimeScopes.Sports);
        Success("The tryout schedule was created.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Edit ----------
    [Authorize(Roles = Roles.Deciders)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await Db.Schedules.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (schedule == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, schedule.SportId)) return Forbid();

        var model = new ScheduleFormViewModel
        {
            Id = schedule.Id,
            SportId = schedule.SportId,
            Title = schedule.Title,
            Date = schedule.StartAt.Date,
            StartTime = schedule.StartAt.TimeOfDay,
            EndTime = schedule.EndAt.TimeOfDay,
            Venue = schedule.Venue,
            Notes = schedule.Notes,
            IsCancelled = schedule.IsCancelled
        };
        await LoadSportsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Deciders)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ScheduleFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var schedule = await Db.Schedules.FirstOrDefaultAsync(t => t.Id == id);
        if (schedule == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, schedule.SportId)) return Forbid();
        if (model.SportId.HasValue && !CanAccessSport(allowed, model.SportId.Value))
            ModelState.AddModelError(nameof(model.SportId), "You can only schedule tryouts for your assigned sports.");

        // Moving a schedule to another sport would strand applicants who registered for the old sport.
        if (model.SportId.HasValue && model.SportId.Value != schedule.SportId
            && await Db.Applicants.AnyAsync(a => a.TryoutScheduleId == schedule.Id))
        {
            ModelState.AddModelError(nameof(model.SportId), "Applicants are already assigned to this schedule, so its sport cannot change.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSportsAsync(model);
            return View(model);
        }

        Apply(schedule, model);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Schedules, RealtimeScopes.Dashboard, RealtimeScopes.Registrations, RealtimeScopes.Explore, RealtimeScopes.Sports);
        Success("The tryout schedule was updated.");
        return RedirectToAction(nameof(Index), new { view = "all" });
    }

    // ---------- Delete ----------
    [Authorize(Roles = Roles.Deciders)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var schedule = await Db.Schedules.FirstOrDefaultAsync(t => t.Id == id);
        if (schedule == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, schedule.SportId)) return Forbid();

        // Registered applicants keep their registration; they just lose the schedule link.
        var linked = await Db.Applicants.Where(a => a.TryoutScheduleId == id).ToListAsync();
        foreach (var a in linked) a.TryoutScheduleId = null;

        Db.Schedules.Remove(schedule);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Schedules, RealtimeScopes.Dashboard, RealtimeScopes.Registrations, RealtimeScopes.Explore, RealtimeScopes.Sports);
        Success("The tryout schedule was deleted.");
        return RedirectToAction(nameof(Index), new { view = "all" });
    }

    // ---------- Helpers ----------
    private async Task LoadSportsAsync(ScheduleFormViewModel model)
    {
        var allowed = await AllowedSportIdsAsync();
        model.Sports = await Db.Sports
            .ScopeTo(allowed)
            .AsNoTracking()
            .Where(s => s.IsActive || s.Id == model.SportId)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    private static void Apply(TryoutSchedule schedule, ScheduleFormViewModel model)
    {
        var date = model.Date!.Value.Date;
        schedule.SportId = model.SportId!.Value;
        schedule.Title = model.Title.Trim();
        schedule.StartAt = date.Add(model.StartTime!.Value);
        schedule.EndAt = date.Add(model.EndTime!.Value);
        schedule.Venue = model.Venue.Trim();
        schedule.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        schedule.IsCancelled = model.IsCancelled;
    }
}
