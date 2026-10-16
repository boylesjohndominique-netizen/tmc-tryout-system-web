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
public class EvaluationsController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public EvaluationsController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    [Authorize(Policy = Permissions.EvaluationsView)]
    public async Task<IActionResult> Index(string? search, int? sportId, string? show)
    {
        var allowed = await AllowedSportIdsAsync();
        var myId = CurrentUserId;

        var query = Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations).ThenInclude(e => e.Scores)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.StudentId.ToLower().Contains(term)
                                     || a.FirstName.ToLower().Contains(term)
                                     || a.LastName.ToLower().Contains(term));
        }
        if (sportId.HasValue) query = query.Where(a => a.SportId == sportId.Value);

        var list = await query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync();

        var mine = list.Where(a => a.Evaluations.Any(e => e.EvaluatorId == myId)).Select(a => a.Id).ToHashSet();

        list = show switch
        {
            "todo" => list.Where(a => !mine.Contains(a.Id)).ToList(),
            "done" => list.Where(a => mine.Contains(a.Id)).ToList(),
            _ => list
        };

        return View(new EvaluationListViewModel
        {
            Applicants = list,
            Sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            EvaluatedByMe = mine,
            Search = search,
            SportId = sportId,
            Show = show
        });
    }

    [Authorize(Policy = Permissions.EvaluationsManage)]
    [HttpGet]
    public async Task<IActionResult> Evaluate(int id)
    {
        var applicant = await Db.Applicants
            .Include(a => a.Sport).ThenInclude(s => s!.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        var criteria = await LoadCriteriaAsync(applicant.Sport?.SportCategoryId);
        var model = BuildForm(applicant, criteria);

        var myId = CurrentUserId;
        var existing = await Db.Evaluations
            .Include(e => e.Scores)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ApplicantId == id && e.EvaluatorId == myId);
        if (existing != null)
        {
            model.IsEdit = true;
            model.Comments = existing.Comments;
            var byCriterion = existing.Scores.ToDictionary(s => s.EvaluationCriterionId, s => s.Value);
            foreach (var c in model.Criteria)
                if (byCriterion.TryGetValue(c.CriterionId, out var value)) c.Value = value;
        }
        return View(model);
    }

    [Authorize(Policy = Permissions.EvaluationsManage)]
    [HttpPost]
    public async Task<IActionResult> Evaluate(int id, EvaluationFormViewModel model)
    {
        var applicant = await Db.Applicants.Include(a => a.Sport).ThenInclude(s => s!.Category).FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        var criteria = await LoadCriteriaAsync(applicant.Sport?.SportCategoryId);
        if (criteria.Count == 0)
        {
            FillHeader(model, applicant);
            ModelState.AddModelError(string.Empty,
                "This sport's category has no evaluation criteria yet. Ask an administrator to add them.");
            return View(model);
        }

        var validIds = criteria.Select(c => c.Id).ToHashSet();

        // Keep only scores for criteria that belong to this sport's category, and make sure
        // every criterion of the category is present (so removed ones cannot linger).
        model.Criteria = (model.Criteria ?? new()).Where(c => validIds.Contains(c.CriterionId)).ToList();
        foreach (var c in criteria)
        {
            var input = model.Criteria.FirstOrDefault(x => x.CriterionId == c.Id);
            if (input == null)
                model.Criteria.Add(new CriterionScoreInput { CriterionId = c.Id, Name = c.Name, Icon = c.Icon });
            else
            {
                input.Name = c.Name;
                input.Icon = c.Icon;
            }
        }

        if (!ModelState.IsValid)
        {
            FillHeader(model, applicant);
            return View(model);
        }

        var myId = CurrentUserId;
        var evaluation = await Db.Evaluations
            .Include(e => e.Scores)
            .FirstOrDefaultAsync(e => e.ApplicantId == id && e.EvaluatorId == myId);
        bool isNew = evaluation == null;
        if (evaluation == null)
        {
            evaluation = new Evaluation { ApplicantId = id, EvaluatorId = myId };
            Db.Evaluations.Add(evaluation);
        }

        foreach (var c in model.Criteria)
        {
            var score = evaluation.Scores.FirstOrDefault(s => s.EvaluationCriterionId == c.CriterionId);
            if (score == null)
                evaluation.Scores.Add(new EvaluationScore { EvaluationCriterionId = c.CriterionId, Value = c.Value });
            else
                score.Value = c.Value;
        }
        foreach (var stale in evaluation.Scores.Where(s => !validIds.Contains(s.EvaluationCriterionId)).ToList())
            evaluation.Scores.Remove(stale);

        evaluation.Comments = string.IsNullOrWhiteSpace(model.Comments) ? null : model.Comments.Trim();
        evaluation.EvaluatedAt = DateTime.Now;

        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Evaluations, RealtimeScopes.Screening, RealtimeScopes.Selection, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Reports);
        if (applicant.UserId.HasValue)
            await _rt.NotifyUserAsync(applicant.UserId.Value, "New evaluation recorded",
                $"An evaluator scored your {applicant.Sport?.Name} tryout performance.", "clipboard2-pulse-fill");

        Success(isNew
            ? $"Evaluation for {applicant.DisplayName} was saved."
            : $"Your evaluation for {applicant.DisplayName} was updated.");
        return RedirectToAction("Details", "Applicants", new { id });
    }

    [Authorize(Policy = Permissions.EvaluationsManage)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var evaluation = await Db.Evaluations.Include(e => e.Applicant).FirstOrDefaultAsync(e => e.Id == id);
        if (evaluation == null) return NotFound();

        if (!IsAdmin && evaluation.EvaluatorId != CurrentUserId)
            return Forbid();

        int applicantId = evaluation.ApplicantId;
        Db.Evaluations.Remove(evaluation);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Evaluations, RealtimeScopes.Screening, RealtimeScopes.Selection, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Reports);
        Success("The evaluation was removed.");
        return RedirectToAction("Details", "Applicants", new { id = applicantId });
    }

    private async Task<List<EvaluationCriterion>> LoadCriteriaAsync(int? categoryId)
    {
        if (!categoryId.HasValue) return new List<EvaluationCriterion>();
        return await Db.EvaluationCriteria.AsNoTracking()
            .Where(c => c.SportCategoryId == categoryId.Value)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    private static EvaluationFormViewModel BuildForm(Applicant applicant, List<EvaluationCriterion> criteria)
    {
        var model = new EvaluationFormViewModel
        {
            Criteria = criteria
                .Select(c => new CriterionScoreInput { CriterionId = c.Id, Name = c.Name, Icon = c.Icon })
                .ToList()
        };
        FillHeader(model, applicant);
        return model;
    }

    private static void FillHeader(EvaluationFormViewModel model, Applicant applicant)
    {
        model.ApplicantId = applicant.Id;
        model.ApplicantName = applicant.DisplayName;
        model.StudentId = applicant.StudentId;
        model.SportName = applicant.Sport?.Name ?? string.Empty;
        model.SportIcon = applicant.Sport?.Icon ?? "trophy";
        model.CategoryName = applicant.Sport?.Category?.Name ?? string.Empty;
        model.QualifyingScore = applicant.Sport?.QualifyingScore ?? 0;
    }
}
