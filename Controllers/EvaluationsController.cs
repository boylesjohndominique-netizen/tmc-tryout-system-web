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

    public async Task<IActionResult> Index(string? search, int? sportId, string? show)
    {
        var allowed = await AllowedSportIdsAsync();
        var myId = CurrentUserId;

        var query = Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations)
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

    [HttpGet]
    public async Task<IActionResult> Evaluate(int id)
    {
        var applicant = await Db.Applicants.Include(a => a.Sport).AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        var myId = CurrentUserId;
        var existing = await Db.Evaluations.AsNoTracking().FirstOrDefaultAsync(e => e.ApplicantId == id && e.EvaluatorId == myId);

        var model = BuildForm(applicant);
        if (existing != null)
        {
            model.IsEdit = true;
            model.Skill = existing.Skill;
            model.Speed = existing.Speed;
            model.Agility = existing.Agility;
            model.Endurance = existing.Endurance;
            model.Teamwork = existing.Teamwork;
            model.Discipline = existing.Discipline;
            model.OverallPerformance = existing.OverallPerformance;
            model.Comments = existing.Comments;
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Evaluate(int id, EvaluationFormViewModel model)
    {
        var applicant = await Db.Applicants.Include(a => a.Sport).FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        if (!ModelState.IsValid)
        {
            FillHeader(model, applicant);
            return View(model);
        }

        var myId = CurrentUserId;
        var evaluation = await Db.Evaluations.FirstOrDefaultAsync(e => e.ApplicantId == id && e.EvaluatorId == myId);
        bool isNew = evaluation == null;
        if (evaluation == null)
        {
            evaluation = new Evaluation { ApplicantId = id, EvaluatorId = myId };
            Db.Evaluations.Add(evaluation);
        }

        evaluation.Skill = model.Skill;
        evaluation.Speed = model.Speed;
        evaluation.Agility = model.Agility;
        evaluation.Endurance = model.Endurance;
        evaluation.Teamwork = model.Teamwork;
        evaluation.Discipline = model.Discipline;
        evaluation.OverallPerformance = model.OverallPerformance;
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

    private static EvaluationFormViewModel BuildForm(Applicant applicant)
    {
        var model = new EvaluationFormViewModel();
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
        model.QualifyingScore = applicant.Sport?.QualifyingScore ?? 0;
    }
}
