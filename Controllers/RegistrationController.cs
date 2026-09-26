using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

/// <summary>A student's own athlete registrations (one entry per sport) and selection results.</summary>
[Authorize(Roles = Roles.Student)]
public class RegistrationController : AppController
{
    private readonly ApplicantFormService _forms;
    private readonly IRealtimeNotifier _rt;

    public RegistrationController(AppDbContext db, ApplicantFormService forms, IRealtimeNotifier rt) : base(db)
    {
        _forms = forms;
        _rt = rt;
    }

    public async Task<IActionResult> Index()
    {
        var entries = await LoadMineAsync();
        var registeredSportIds = entries.Select(e => e.SportId).ToHashSet();
        var openSports = await Db.Sports.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
        openSports = openSports.Where(s => !registeredSportIds.Contains(s.Id)).ToList();

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

        return View(new StudentRegistrationsViewModel
        {
            Entries = entries,
            OpenSports = openSports,
            SchedulesPerEntry = schedulesPerEntry
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ApplicantFormViewModel { Email = (await CurrentUserAsync())?.Email };
        await _forms.LoadOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ApplicantFormViewModel model)
    {
        model.ExistingFrontPath = null;
        model.ExistingBackPath = null;

        await _forms.ValidateAsync(ModelState, model, null, CurrentUserId);
        _forms.ValidateIdPictures(ModelState, model);
        if (!ModelState.IsValid)
        {
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        var applicant = new Applicant { UserId = CurrentUserId, RegisteredAt = DateTime.Now, Status = SelectionStatus.Pending };
        _forms.Apply(applicant, model);
        await _forms.SaveIdPicturesAsync(applicant, model);
        Db.Applicants.Add(applicant);

        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two submissions at once can still hit the unique index; treat it as a duplicate.
            ModelState.AddModelError(string.Empty, "You are already registered for this sport. A student gets one tryout entry per sport.");
            Db.Entry(applicant).State = EntityState.Detached;
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        await _rt.ChangedAsync(RealtimeScopes.Registrations, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Sports, RealtimeScopes.Reports);
        Success("You are registered. You can try out for several sports — add another from My registration.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var mine = await Db.Applicants.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);
        if (mine == null) return RedirectToAction(nameof(Index));

        if (!IsEditable(mine))
        {
            Warning("This registration is locked because evaluation has started. Contact the athletics office for changes.");
            return RedirectToAction(nameof(Index));
        }

        var model = _forms.ToForm(mine);
        await _forms.LoadOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ApplicantFormViewModel model)
    {
        var mine = await Db.Applicants.Include(a => a.Evaluations).FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);
        if (mine == null) return RedirectToAction(nameof(Index));

        if (!IsEditable(mine))
        {
            Warning("This registration is locked because evaluation has started.");
            return RedirectToAction(nameof(Index));
        }

        model.Id = mine.Id;
        model.SportId = mine.SportId; // The sport of an entry cannot change; register again to add another sport.

        await _forms.ValidateAsync(ModelState, model, mine, CurrentUserId);
        _forms.ValidateIdPictures(ModelState, model);
        if (!ModelState.IsValid)
        {
            await _forms.LoadOptionsAsync(model);
            return View(model);
        }

        _forms.Apply(mine, model);
        await _forms.SaveIdPicturesAsync(mine, model);
        await _forms.SyncProfileAsync(mine, CurrentUserId);

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

        await _rt.ChangedAsync(RealtimeScopes.Registrations, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Reports);
        Success("Your registration was updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id)
    {
        var mine = await Db.Applicants.Include(a => a.Evaluations).FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);
        if (mine == null) return RedirectToAction(nameof(Index));

        if (!IsEditable(mine))
        {
            Failure("You can no longer withdraw online because evaluation has started. Contact the athletics office.");
            return RedirectToAction(nameof(Index));
        }

        Db.Applicants.Remove(mine);
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Registrations, RealtimeScopes.Applicants, RealtimeScopes.Dashboard, RealtimeScopes.Explore, RealtimeScopes.Sports, RealtimeScopes.Reports);
        Success("That tryout registration was withdrawn. You can register again at any time.");
        return RedirectToAction(nameof(Index));
    }

    private Task<AppUser?> CurrentUserAsync()
    {
        var userId = CurrentUserId;
        return Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    private async Task<List<Applicant>> LoadMineAsync()
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

        return entries;
    }

    private static bool IsEditable(Applicant a) =>
        a.Status == SelectionStatus.Pending && a.Evaluations.Count == 0;
}