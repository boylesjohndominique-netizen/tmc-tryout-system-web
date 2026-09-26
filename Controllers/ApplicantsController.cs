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
public class ApplicantsController : AppController
{
    private readonly ApplicantFormService _forms;
    private readonly IRealtimeNotifier _rt;

    public ApplicantsController(AppDbContext db, ApplicantFormService forms, IRealtimeNotifier rt) : base(db)
    {
        _forms = forms;
        _rt = rt;
    }

    // ---------- Applicant records: search and filter ----------
    public async Task<IActionResult> Index(string? search, int? sportId, int? scheduleId, string? result, string? status, string? verification)
    {
        var allowed = await AllowedSportIdsAsync();

        var baseQuery = Db.Applicants.ScopeTo(allowed);
        int total = await baseQuery.CountAsync();

        var query = baseQuery
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.StudentId.ToLower().Contains(term)
                                     || a.FirstName.ToLower().Contains(term)
                                     || a.LastName.ToLower().Contains(term)
                                     || (a.MiddleName != null && a.MiddleName.ToLower().Contains(term))
                                     || (a.FirstName + " " + a.LastName).ToLower().Contains(term)
                                     || (a.LastName + ", " + a.FirstName).ToLower().Contains(term));
        }
        if (sportId.HasValue) query = query.Where(a => a.SportId == sportId.Value);
        if (scheduleId.HasValue) query = query.Where(a => a.TryoutScheduleId == scheduleId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SelectionStatus>(status, out var parsed))
            query = query.Where(a => a.Status == parsed);
        if (verification == "pending") query = query.Where(a => !a.IdVerified);
        if (verification == "verified") query = query.Where(a => a.IdVerified);

        var list = await query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync();

        list = result switch
        {
            "evaluated" => list.Where(a => a.EvaluationCount > 0).ToList(),
            "notevaluated" => list.Where(a => a.EvaluationCount == 0).ToList(),
            "qualified" => list.Where(a => a.IsQualified).ToList(),
            "notqualified" => list.Where(a => a.OverallScore.HasValue && !a.IsQualified).ToList(),
            _ => list
        };

        return View(new ApplicantListViewModel
        {
            Applicants = list,
            Sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            Schedules = await Db.Schedules.ScopeTo(allowed).AsNoTracking().OrderByDescending(t => t.StartAt).ToListAsync(),
            Search = search,
            SportId = sportId,
            ScheduleId = scheduleId,
            Result = result,
            Status = status,
            Verification = verification,
            TotalBeforeFilter = total
        });
    }

    // ---------- Details ----------
    public async Task<IActionResult> Details(int id)
    {
        var applicant = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations).ThenInclude(e => e.Evaluator)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (!CanAccessSport(allowed, applicant.SportId)) return Forbid();

        var myId = CurrentUserId;
        return View(new ApplicantDetailsViewModel
        {
            Applicant = applicant,
            Evaluations = applicant.Evaluations.OrderByDescending(e => e.EvaluatedAt).ToList(),
            MyEvaluationId = applicant.Evaluations.FirstOrDefault(e => e.EvaluatorId == myId)?.Id,
            CanDecide = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Coach),
            CanEvaluate = true,
            CanManage = IsAdmin
        });
    }

    // ---------- Admin: register on behalf of a student ----------
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ApplicantFormViewModel { ByStaff = true };
        await _forms.LoadOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create(ApplicantFormViewModel model)
    {
        model.ByStaff = true;
        await _forms.ValidateAsync(ModelState, model, null, null);
        if (!ModelState.IsValid)
        {
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        var applicant = new Applicant { RegisteredAt = DateTime.Now, Status = SelectionStatus.Pending };
        _forms.Apply(applicant, model);
        await _forms.SaveIdPicturesAsync(applicant, model);
        Db.Applicants.Add(applicant);

        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.StudentId), "This student ID is already registered for this sport.");
            Db.Entry(applicant).State = EntityState.Detached;
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        await _rt.ChangedAsync(RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Sports, RealtimeScopes.Reports);
        Success($"{applicant.DisplayName} was registered for {applicant.Sport?.Name}.");
        return RedirectToAction(nameof(Details), new { id = applicant.Id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var applicant = await Db.Applicants.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        var model = _forms.ToForm(applicant);
        model.ByStaff = true;
        await _forms.LoadOptionsAsync(model);
        return View(model);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ApplicantFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var applicant = await Db.Applicants.FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        model.ByStaff = true;
        await _forms.ValidateAsync(ModelState, model, applicant, null);
        _forms.ValidateIdPictures(ModelState, model);
        if (!ModelState.IsValid)
        {
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        _forms.Apply(applicant, model);
        await _forms.SaveIdPicturesAsync(applicant, model);
        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.StudentId), "This student ID is already registered for this sport.");
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        await _rt.ChangedAsync(RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Reports);
        Success("The applicant record was updated.");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Admin confirms or rejects the uploaded ID pictures of a student.</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Verify(int id, bool approved, string? remarks)
    {
        var applicant = await Db.Applicants.Include(a => a.Sport).FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        applicant.IdVerified = approved;
        applicant.VerifiedById = CurrentUserId;
        applicant.VerifiedAt = DateTime.Now;
        applicant.VerificationRemarks = string.IsNullOrWhiteSpace(remarks)
            ? (approved ? null : "ID pictures were not accepted. Please re-upload.")
            : remarks.Trim();

        // Verification belongs to the student, so it applies to all of their tryout entries.
        var siblings = await Db.Applicants.Where(a => a.UserId == applicant.UserId && a.Id != applicant.Id).ToListAsync();
        foreach (var s in siblings)
        {
            s.IdVerified = applicant.IdVerified;
            s.VerifiedById = applicant.VerifiedById;
            s.VerifiedAt = applicant.VerifiedAt;
            s.VerificationRemarks = applicant.VerificationRemarks;
        }

        await Db.SaveChangesAsync();

        if (applicant.UserId.HasValue)
        {
            await _rt.NotifyUserAsync(applicant.UserId.Value,
                approved ? "ID verified" : "ID needs re-upload",
                approved ? $"Your ID pictures for {applicant.Sport?.Name} tryouts were approved."
                         : "Your ID pictures were not accepted. Please upload new ones.",
                approved ? "patch-check-fill" : "exclamation-triangle-fill",
                new[] { RealtimeScopes.Registrations, RealtimeScopes.Applicants });
        }
        await _rt.ChangedAsync(RealtimeScopes.Applicants, RealtimeScopes.Dashboard);

        if (approved)
            Success($"The ID of {applicant.DisplayName} was verified.");
        else
            Failure($"The ID of {applicant.DisplayName} was marked as not verified. The student should re-upload.");

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var applicant = await Db.Applicants.FirstOrDefaultAsync(a => a.Id == id);
        if (applicant == null) return NotFound();

        Db.Applicants.Remove(applicant);
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Selection, RealtimeScopes.Screening, RealtimeScopes.Reports);
        Success($"The record for {applicant.DisplayName} was deleted, including their evaluations.");
        return RedirectToAction(nameof(Index));
    }
}
