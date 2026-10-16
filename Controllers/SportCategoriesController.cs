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
/// Sport categories decide how a sport is evaluated: each category owns its own set of
/// performance criteria. Only administrators manage them.
/// </summary>
[Authorize(Roles = Roles.Admin)]
public class SportCategoriesController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public SportCategoriesController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await Db.SportCategories
            .Include(c => c.Criteria)
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

        var sportCounts = await Db.Sports
            .Where(s => s.SportCategoryId != null)
            .GroupBy(s => s.SportCategoryId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return View(new CategoryListViewModel
        {
            Categories = categories.Select(c => new CategoryRow
            {
                Category = c,
                SportCount = sportCounts.TryGetValue(c.Id, out var n) ? n : 0
            }).ToList()
        });
    }

    // ---------- Create ----------
    [HttpGet]
    public IActionResult Create()
    {
        var model = new CategoryFormViewModel
        {
            Criteria = new List<CriterionFormItem> { new(), new() }
        };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid) return View(model);

        var category = new SportCategory
        {
            Name = model.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Icon = UiHelpers.CategoryIcons.Contains(model.Icon) ? model.Icon : "collection",
            DisplayOrder = model.DisplayOrder
        };
        int order = 0;
        foreach (var c in model.Criteria.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            category.Criteria.Add(new EvaluationCriterion
            {
                Name = c.Name.Trim(),
                Icon = UiHelpers.CriterionIcons.Contains(c.Icon) ? c.Icon : "star",
                DisplayOrder = order++
            });
        }

        Db.SportCategories.Add(category);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore, RealtimeScopes.Evaluations,
            RealtimeScopes.Screening, RealtimeScopes.Reports, RealtimeScopes.Dashboard);
        Success($"{category.Name} was added.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Edit ----------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await Db.SportCategories
            .Include(c => c.Criteria)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        var model = new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            DisplayOrder = category.DisplayOrder,
            SportCount = await Db.Sports.CountAsync(s => s.SportCategoryId == id),
            Criteria = category.Criteria
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new CriterionFormItem { Id = c.Id, Name = c.Name, Icon = c.Icon, DisplayOrder = c.DisplayOrder })
                .ToList()
        };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var category = await Db.SportCategories.Include(c => c.Criteria).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        Normalize(model);
        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            model.SportCount = await Db.Sports.CountAsync(s => s.SportCategoryId == id);
            return View(model);
        }

        category.Name = model.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        category.Icon = UiHelpers.CategoryIcons.Contains(model.Icon) ? model.Icon : "collection";
        category.DisplayOrder = model.DisplayOrder;

        var kept = model.Criteria.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
        if (kept.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Criteria), "Add at least one criterion.");
            model.SportCount = await Db.Sports.CountAsync(s => s.SportCategoryId == id);
            return View(model);
        }

        // Remove scores for criteria that no longer exist, then delete those criteria.
        var keptIds = kept.Where(k => k.Id != 0).Select(k => k.Id).ToHashSet();
        var removed = category.Criteria.Where(c => !keptIds.Contains(c.Id)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(c => c.Id).ToList();
            Db.EvaluationScores.RemoveRange(Db.EvaluationScores.Where(s => removedIds.Contains(s.EvaluationCriterionId)));
            Db.EvaluationCriteria.RemoveRange(removed);
        }

        int order = 0;
        foreach (var k in kept)
        {
            if (k.Id != 0)
            {
                var existing = category.Criteria.FirstOrDefault(c => c.Id == k.Id);
                if (existing != null)
                {
                    existing.Name = k.Name.Trim();
                    existing.Icon = UiHelpers.CriterionIcons.Contains(k.Icon) ? k.Icon : "star";
                    existing.DisplayOrder = order;
                }
            }
            else
            {
                category.Criteria.Add(new EvaluationCriterion
                {
                    Name = k.Name.Trim(),
                    Icon = UiHelpers.CriterionIcons.Contains(k.Icon) ? k.Icon : "star",
                    DisplayOrder = order
                });
            }
            order++;
        }

        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore, RealtimeScopes.Evaluations,
            RealtimeScopes.Screening, RealtimeScopes.Reports, RealtimeScopes.Dashboard);
        Success($"Changes to {category.Name} were saved.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Delete ----------
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await Db.SportCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        int used = await Db.Sports.CountAsync(s => s.SportCategoryId == id);
        if (used > 0)
        {
            Failure($"{category.Name} is used by {used} sport{(used == 1 ? "" : "s")}. Move them to another category first.");
            return RedirectToAction(nameof(Index));
        }

        Db.SportCategories.Remove(category);
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Sports, RealtimeScopes.Explore);
        Success($"{category.Name} was removed.");
        return RedirectToAction(nameof(Index));
    }

    private void Normalize(CategoryFormViewModel model)
    {
        model.Criteria ??= new();
        if (!UiHelpers.CategoryIcons.Contains(model.Icon)) model.Icon = "collection";
    }

    private async Task ValidateAsync(CategoryFormViewModel model)
    {
        var name = (model.Name ?? string.Empty).Trim();
        if (name.Length > 0)
        {
            var lowered = name.ToLower();
            bool exists = await Db.SportCategories.AnyAsync(c => c.Name.ToLower() == lowered && c.Id != model.Id);
            if (exists) ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
        }

        if (!model.Criteria.Any(c => !string.IsNullOrWhiteSpace(c.Name)))
            ModelState.AddModelError(nameof(model.Criteria), "Add at least one criterion.");
    }
}
