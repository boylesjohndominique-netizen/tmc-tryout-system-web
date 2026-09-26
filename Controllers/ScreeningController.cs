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
public class ScreeningController : AppController
{
    public ScreeningController(AppDbContext db) : base(db) { }

    public async Task<IActionResult> Index(int? sportId, string? filter, string? search)
    {
        var allowed = await AllowedSportIdsAsync();

        var query = Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .AsNoTracking();

        if (sportId.HasValue) query = query.Where(a => a.SportId == sportId.Value);

        var applicants = await query.ToListAsync();
        var rows = ScreeningService.Build(applicants);
        int notEvaluated = applicants.Count(a => a.Evaluations.Count == 0);
        int qualifiedCount = rows.Count(r => r.Qualified);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            rows = rows.Where(r => r.Applicant.StudentId.ToLower().Contains(term)
                                   || r.Applicant.DisplayName.ToLower().Contains(term)
                                   || r.Applicant.FullName.ToLower().Contains(term)).ToList();
        }

        rows = filter switch
        {
            "qualified" => rows.Where(r => r.Qualified).ToList(),
            "notqualified" => rows.Where(r => !r.Qualified).ToList(),
            _ => rows
        };

        return View(new ScreeningViewModel
        {
            Rows = rows,
            Sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            SportId = sportId,
            Filter = filter,
            Search = search,
            NotEvaluated = notEvaluated,
            QualifiedCount = qualifiedCount
        });
    }

    public async Task<IActionResult> Compare(int[]? ids)
    {
        ids ??= Array.Empty<int>();
        ids = ids.Distinct().ToArray();

        if (ids.Length < 2 || ids.Length > 4)
        {
            Warning("Select between 2 and 4 applicants to compare.");
            return RedirectToAction(nameof(Index));
        }

        var allowed = await AllowedSportIdsAsync();

        var chosen = await Db.Applicants.ScopeTo(allowed).AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync();
        if (chosen.Count != ids.Length)
        {
            Warning("Some of the selected applicants could not be found.");
            return RedirectToAction(nameof(Index));
        }

        // Rank against everyone in the same sports so the rank shown is meaningful.
        var sportIds = chosen.Select(a => a.SportId).Distinct().ToList();
        var pool = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Evaluations)
            .AsNoTracking()
            .Where(a => sportIds.Contains(a.SportId))
            .ToListAsync();

        var rows = ScreeningService.Build(pool).Where(r => ids.Contains(r.Applicant.Id)).OrderByDescending(r => r.Overall).ToList();
        if (rows.Count < 2)
        {
            Warning("At least two of the selected applicants need an evaluation before they can be compared.");
            return RedirectToAction(nameof(Index));
        }

        return View(new CompareViewModel { Rows = rows });
    }
}
