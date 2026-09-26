using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

[Authorize(Roles = Roles.Staff)]
public class SportsController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public SportsController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    public async Task<IActionResult> Index()
    {
        var allowed = await AllowedSportIdsAsync();
        var sports = await Db.Sports
            .ScopeTo(allowed)
            .Include(s => s.Staff).ThenInclude(x => x.User)
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        var applicantCounts = await Db.Applicants
            .GroupBy(a => a.SportId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var scheduleCounts = await Db.Schedules
            .GroupBy(t => t.SportId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return View(new SportListViewModel
        {
            Sports = sports,
            ApplicantCounts = applicantCounts,
            ScheduleCounts = scheduleCounts
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, id)) return Forbid();

        var sport = await Db.Sports
            .Include(s => s.Staff).ThenInclude(x => x.User)
            .Include(s => s.Schedules)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sport == null) return NotFound();

        var applicantCount = await Db.Applicants.CountAsync(a => a.SportId == id);
        var selectedCount = await Db.Applicants.CountAsync(a => a.SportId == id && a.Status == SelectionStatus.Selected);

        return View(new SportDetailsViewModel
        {
            Sport = sport,
            Staff = sport.Staff.Where(x => x.User != null).Select(x => x.User!).OrderBy(u => u.FullName).ToList(),
            Schedules = sport.Schedules.OrderByDescending(t => t.StartAt).ToList(),
            ApplicantCount = applicantCount,
            SelectedCount = selectedCount
        });
    }

    // ---------- Create ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new SportFormViewModel();
        await LoadStaffAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create(SportFormViewModel model)
    {
        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadStaffAsync(model);
            return View(model);
        }

        var sport = new Sport();
        Apply(sport, model);
        Db.Sports.Add(sport);
        await Db.SaveChangesAsync();

        await SyncStaffAsync(sport.Id, model.StaffIds);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore, RealtimeScopes.Dashboard, RealtimeScopes.Schedules, RealtimeScopes.Selection, RealtimeScopes.Screening);
        Success($"{sport.Name} was added.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Edit ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sport = await Db.Sports.Include(s => s.Staff).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sport == null) return NotFound();

        var model = new SportFormViewModel
        {
            Id = sport.Id,
            Name = sport.Name,
            Description = sport.Description,
            Icon = sport.Icon,
            QualifyingScore = sport.QualifyingScore,
            Slots = sport.Slots,
            IsActive = sport.IsActive,
            StaffIds = sport.Staff.Select(x => x.UserId).ToList()
        };
        await LoadStaffAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, SportFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var sport = await Db.Sports.FirstOrDefaultAsync(s => s.Id == id);
        if (sport == null) return NotFound();

        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadStaffAsync(model);
            return View(model);
        }

        Apply(sport, model);
        await SyncStaffAsync(sport.Id, model.StaffIds);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore, RealtimeScopes.Dashboard, RealtimeScopes.Schedules, RealtimeScopes.Selection, RealtimeScopes.Screening);
        Success($"Changes to {sport.Name} were saved.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Delete ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var sport = await Db.Sports.FirstOrDefaultAsync(s => s.Id == id);
        if (sport == null) return NotFound();

        if (await Db.Applicants.AnyAsync(a => a.SportId == id))
        {
            Failure($"{sport.Name} has registered applicants and cannot be removed. Set it to inactive to stop new registrations.");
            return RedirectToAction(nameof(Index));
        }

        Db.Sports.Remove(sport);
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore, RealtimeScopes.Dashboard);
        Success($"{sport.Name} was removed.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------
    private async Task ValidateAsync(SportFormViewModel model)
    {
        var name = (model.Name ?? string.Empty).Trim();
        if (name.Length > 0)
        {
            var lowered = name.ToLower();
            bool exists = await Db.Sports.AnyAsync(s => s.Name.ToLower() == lowered && s.Id != model.Id);
            if (exists) ModelState.AddModelError(nameof(model.Name), "A sport with this name already exists.");
        }
        if (!UiHelpers.SportIcons.Contains(model.Icon))
            model.Icon = "trophy";
    }

    private static void Apply(Sport sport, SportFormViewModel model)
    {
        sport.Name = model.Name.Trim();
        sport.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        sport.Icon = model.Icon;
        sport.QualifyingScore = model.QualifyingScore;
        sport.Slots = model.Slots;
        sport.IsActive = model.IsActive;
    }

    private async Task LoadStaffAsync(SportFormViewModel model)
    {
        model.AvailableStaff = await Db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (u.Role == Roles.Coach || u.Role == Roles.Evaluator))
            .OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .ToListAsync();
    }

    private async Task SyncStaffAsync(int sportId, List<int>? staffIds)
    {
        staffIds ??= new List<int>();
        var validIds = await Db.Users
            .Where(u => staffIds.Contains(u.Id) && (u.Role == Roles.Coach || u.Role == Roles.Evaluator))
            .Select(u => u.Id)
            .ToListAsync();

        var current = await Db.SportAssignments.Where(x => x.SportId == sportId).ToListAsync();
        Db.SportAssignments.RemoveRange(current.Where(x => !validIds.Contains(x.UserId)));

        var existingIds = current.Select(x => x.UserId).ToHashSet();
        foreach (var uid in validIds.Where(v => !existingIds.Contains(v)))
            Db.SportAssignments.Add(new SportAssignment { SportId = sportId, UserId = uid });
    }
}
