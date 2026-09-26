using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

/// <summary>Final selection decisions. Only Admin and Coach roles can record them.</summary>
[Authorize(Roles = Roles.Deciders)]
public class SelectionController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public SelectionController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    public async Task<IActionResult> Index(string? search, int? sportId, string? status)
    {
        var allowed = await AllowedSportIdsAsync();

        var query = Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .AsNoTracking();
        if (sportId.HasValue) query = query.Where(a => a.SportId == sportId.Value);

        var applicants = await query.ToListAsync();
        var rankLookup = ScreeningService.Build(applicants).ToDictionary(r => r.Applicant.Id, r => r);

        IEnumerable<Applicant> filtered = applicants;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            filtered = filtered.Where(a => a.StudentId.ToLower().Contains(term)
                                           || a.DisplayName.ToLower().Contains(term)
                                           || a.FullName.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SelectionStatus>(status, out var parsed))
            filtered = filtered.Where(a => a.Status == parsed);

        var items = filtered
            .Select(a =>
            {
                rankLookup.TryGetValue(a.Id, out var row);
                return new ScreeningRowLite
                {
                    Applicant = a,
                    Score = row?.Overall,
                    Rank = row?.Rank,
                    Qualified = row?.Qualified ?? false
                };
            })
            .OrderBy(i => i.Applicant.Sport?.Name)
            .ThenBy(i => i.Rank ?? int.MaxValue)
            .ThenBy(i => i.Applicant.LastName)
            .ToList();

        return View(new SelectionListViewModel
        {
            Items = items,
            Sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            Search = search,
            SportId = sportId,
            Status = status,
            SelectedPerSport = applicants
                .Where(a => a.Status == SelectionStatus.Selected)
                .GroupBy(a => a.SportId)
                .ToDictionary(g => g.Key, g => g.Count())
        });
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, SelectionStatus status, string? remarks, string? returnUrl)
    {
        var applicant = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        if (!Enum.IsDefined(typeof(SelectionStatus), status))
        {
            Failure("Choose a valid status.");
            return RedirectBack(returnUrl);
        }

        if ((status == SelectionStatus.Selected || status == SelectionStatus.NotSelected) && applicant.Evaluations.Count == 0)
        {
            Failure($"{applicant.DisplayName} has no evaluation yet. Record an evaluation first, or mark the applicant For Further Evaluation.");
            return RedirectBack(returnUrl);
        }

        if (status == SelectionStatus.Selected && applicant.Status != SelectionStatus.Selected && applicant.Sport?.Slots != null)
        {
            int selected = await Db.Applicants.CountAsync(a => a.SportId == applicant.SportId && a.Status == SelectionStatus.Selected);
            if (selected >= applicant.Sport.Slots.Value)
                Warning($"{applicant.Sport.Name} already has {selected} selected athletes, which meets its {applicant.Sport.Slots} roster slots.");
        }

        applicant.Status = status;
        applicant.StatusRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        applicant.DecidedById = status == SelectionStatus.Pending ? null : CurrentUserId;
        applicant.DecidedAt = status == SelectionStatus.Pending ? null : DateTime.Now;
        await Db.SaveChangesAsync();

        if (applicant.UserId.HasValue)
        {
            await _rt.NotifyUserAsync(applicant.UserId.Value, "Tryout result updated",
                $"{applicant.DisplayName}: you are now marked {status.DisplayName()} for {applicant.Sport?.Name}.",
                status == SelectionStatus.Selected ? "patch-check-fill" : "info-circle-fill",
                new[] { RealtimeScopes.Registrations, RealtimeScopes.Selection });
        }
        await _rt.ChangedAsync(RealtimeScopes.Selection, RealtimeScopes.Screening, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Reports);

        Success($"{applicant.DisplayName} is now marked {EnumExtensions.DisplayName(status)}.");
        return RedirectBack(returnUrl);
    }

    /// <summary>Marks the highest-ranked qualified pending applicants of a sport as Selected, within roster slots.</summary>
    [HttpPost]
    public async Task<IActionResult> SelectQualified(int sportId)
    {
        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, sportId)) return Forbid();

        var sport = await Db.Sports.FirstOrDefaultAsync(s => s.Id == sportId);
        if (sport == null) return NotFound();

        var applicants = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .Where(a => a.SportId == sportId)
            .ToListAsync();

        var candidates = ScreeningService.Build(applicants)
            .Where(r => r.Qualified && r.Applicant.Status == SelectionStatus.Pending)
            .ToList();

        int alreadySelected = applicants.Count(a => a.Status == SelectionStatus.Selected);
        if (sport.Slots.HasValue)
            candidates = candidates.Take(Math.Max(0, sport.Slots.Value - alreadySelected)).ToList();

        foreach (var r in candidates)
        {
            r.Applicant.Status = SelectionStatus.Selected;
            r.Applicant.DecidedById = CurrentUserId;
            r.Applicant.DecidedAt = DateTime.Now;
        }
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Selection, RealtimeScopes.Screening, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Reports);

        if (candidates.Count == 0)
            Warning($"No pending qualified applicants were available for {sport.Name}, or its roster slots are full.");
        else
            Success($"{candidates.Count} qualified {(candidates.Count == 1 ? "applicant was" : "applicants were")} selected for {sport.Name}.");

        return RedirectToAction(nameof(Index), new { sportId });
    }

    private IActionResult RedirectBack(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
