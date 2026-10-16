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
    private readonly NotificationService _notifications;

    public ApplicantsController(AppDbContext db, ApplicantFormService forms, IRealtimeNotifier rt, NotificationService notifications) : base(db)
    {
        _forms = forms;
        _rt = rt;
        _notifications = notifications;
    }

    // ---------- Applicant records: the student accounts ----------
    /// <summary>
    /// One row per student account, so the athletics office can verify a student the moment they
    /// sign up — before the student is allowed to submit a tryout entry. Staff accounts are not
    /// listed here; they live on the Users page.
    /// </summary>
    [Authorize(Policy = Permissions.ApplicantsView)]
    public async Task<IActionResult> Index(string? search, string? verification, string? course)
    {
        var students = Db.Users.AsNoTracking().Where(u => u.Role == Roles.Student);
        int total = await students.CountAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            students = students.Where(u => u.FullName.ToLower().Contains(term)
                                           || (u.StudentId != null && u.StudentId.ToLower().Contains(term))
                                           || (u.Email != null && u.Email.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(course))
            students = students.Where(u => u.CourseOrGrade == course);

        students = verification switch
        {
            "verified" => students.Where(u => u.IdVerified),
            "pending" => students.Where(u => u.EmailVerified && !u.IdVerified),
            "email" => students.Where(u => !u.EmailVerified),
            _ => students
        };

        var list = await students.OrderBy(u => u.FullName).ToListAsync();

        var tryoutCounts = await Db.Applicants
            .Where(a => a.UserId != null)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return View(new StudentRecordListViewModel
        {
            Rows = list.Select(u => new StudentRecordRow
            {
                Student = u,
                TryoutCount = tryoutCounts.TryGetValue(u.Id, out var c) ? c : 0
            }).ToList(),
            Search = search,
            Verification = verification,
            Course = course,
            Courses = await Db.Users.AsNoTracking()
                .Where(u => u.Role == Roles.Student && u.CourseOrGrade != null && u.CourseOrGrade != "")
                .Select(u => u.CourseOrGrade!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync(),
            TotalBeforeFilter = total,
            AwaitingApproval = await Db.Users.CountAsync(u => u.Role == Roles.Student && u.EmailVerified && !u.IdVerified),
            VerifiedCount = await Db.Users.CountAsync(u => u.Role == Roles.Student && u.IdVerified)
        });
    }

    /// <summary>A single student account: identity, ID photos for approval and their tryout entries.</summary>
    [Authorize(Policy = Permissions.ApplicantsView)]
    public async Task<IActionResult> StudentAccount(int id)
    {
        var student = await Db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Student);
        if (student == null) return NotFound();

        var registrations = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations)
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderBy(a => a.Sport!.Name)
            .ToListAsync();

        return View(new StudentAccountViewModel
        {
            Student = student,
            Registrations = registrations,
            CanVerify = !student.IdVerified
        });
    }

    /// <summary>Staff confirm or reject the student ID photos uploaded at sign-up.</summary>
    [Authorize(Policy = Permissions.ApplicantsManage)]
    [HttpPost]
    public async Task<IActionResult> VerifyAccount(int id, bool approved, string? remarks)
    {
        var student = await Db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Student);
        if (student == null) return NotFound();

        student.IdVerified = approved;
        student.VerifiedById = CurrentUserId;
        student.IdVerifiedAt = DateTime.Now;
        student.VerificationRemarks = string.IsNullOrWhiteSpace(remarks)
            ? (approved ? null : "The ID photos were not accepted. Please upload clearer photos of the front and back of your student ID.")
            : remarks.Trim();

        await Db.SaveChangesAsync();

        await _notifications.CreateAsync(student.Id,
            approved ? "Account verified" : "Student ID needs new photos",
            approved
                ? "Your student ID was approved. You can now register for tryouts."
                : "Your student ID photos were not accepted. Upload new photos from the Account verification page.",
            approved ? "patch-check-fill" : "exclamation-triangle-fill",
            Url.Action("VerificationStatus", "Account"),
            NotificationCategories.Verification);

        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Applicants, RealtimeScopes.Dashboard);

        if (approved) Success($"The account of {student.DisplayName} was verified.");
        else Failure($"The student ID of {student.DisplayName} was marked as not verified.");

        return RedirectToAction(nameof(StudentAccount), new { id });
    }

    /// <summary>
    /// Deactivate or reactivate a student account. Student accounts are no longer on the Users
    /// page, so this is where an administrator blocks (or restores) a student's access.
    /// </summary>
    [Authorize(Policy = Permissions.ApplicantsManage)]
    [HttpPost]
    public async Task<IActionResult> ToggleStudentActive(int id)
    {
        var student = await Db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Student);
        if (student == null) return NotFound();

        student.IsActive = !student.IsActive;
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);

        Success(student.IsActive
            ? $"{student.DisplayName} can sign in again."
            : $"{student.DisplayName} was deactivated.");
        return RedirectToAction(nameof(StudentAccount), new { id });
    }

    // ---------- Details ----------
    [Authorize(Policy = Permissions.ApplicantsView)]
    public async Task<IActionResult> Details(int id)
    {
        var applicant = await Db.Applicants
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations).ThenInclude(e => e.Evaluator)
            .Include(a => a.Evaluations).ThenInclude(e => e.Scores).ThenInclude(s => s.Criterion)
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
            CanDecide = HasPermission(Permissions.SelectionManage),
            CanEvaluate = true,
            CanManage = HasPermission(Permissions.ApplicantsManage)
        });
    }

    // ---------- Admin: register on behalf of a student ----------
    [Authorize(Policy = Permissions.ApplicantsManage)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ApplicantFormViewModel { ByStaff = true };
        await _forms.LoadOptionsAsync(model);
        return View(model);
    }

    [Authorize(Policy = Permissions.ApplicantsManage)]
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

    [Authorize(Policy = Permissions.ApplicantsManage)]
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

    [Authorize(Policy = Permissions.ApplicantsManage)]
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
    [Authorize(Policy = Permissions.ApplicantsManage)]
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
            await _notifications.CreateAsync(applicant.UserId.Value,
                approved ? "ID verified" : "ID needs re-upload",
                approved ? $"Your ID pictures for {applicant.Sport?.Name} tryouts were approved."
                         : "Your ID pictures were not accepted. Please upload new ones.",
                approved ? "patch-check-fill" : "exclamation-triangle-fill",
                Url.Action("VerificationStatus", "Account"),
                NotificationCategories.Verification);
        }
        await _rt.ChangedAsync(RealtimeScopes.Applicants, RealtimeScopes.Dashboard);

        if (approved)
            Success($"The ID of {applicant.DisplayName} was verified.");
        else
            Failure($"The ID of {applicant.DisplayName} was marked as not verified. The student should re-upload.");

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Permissions.ApplicantsManage)]
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
