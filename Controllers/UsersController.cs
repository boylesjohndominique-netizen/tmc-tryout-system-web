using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public UsersController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    public async Task<IActionResult> Index(string? search, string? role)
    {
        var query = Db.Users
            .Include(u => u.SportAssignments).ThenInclude(x => x.Sport)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term)
                                     || u.Username.ToLower().Contains(term)
                                     || (u.Email != null && u.Email.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        var users = await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
        return View(new UserListViewModel { Users = users, Search = search, Role = role });
    }

    [HttpGet]
    public IActionResult Create() => View(new UserFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        var username = (model.Username ?? string.Empty).Trim().ToLowerInvariant();

        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Choose a valid role.");

        if (string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Enter a password for the new account.");
        else
        {
            var pwError = PasswordService.Validate(model.Password);
            if (pwError != null) ModelState.AddModelError(nameof(model.Password), pwError);
        }

        if (!string.IsNullOrEmpty(username) && await Db.Users.AnyAsync(u => u.Username == username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

        if (!ModelState.IsValid) return View(model);

        Db.Users.Add(new AppUser
        {
            Username = username,
            FullName = model.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            Role = model.Role,
            IsActive = model.IsActive,
            PasswordHash = PasswordService.Hash(model.Password!)
        });
        await Db.SaveChangesAsync();

        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);
        Success($"Account for {model.FullName.Trim()} was created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        return View(new UserFormViewModel
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UserFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        // The password is managed through "Reset password", not this form.
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));

        model.Username = user.Username;
        ModelState.Remove(nameof(model.Username));

        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Choose a valid role.");

        bool isSelf = user.Id == CurrentUserId;
        if (isSelf)
        {
            // Nobody can lock themselves out by changing their own role or status.
            model.Role = user.Role;
            model.IsActive = true;
        }

        if (user.Role == Roles.Admin && (model.Role != Roles.Admin || !model.IsActive))
        {
            var otherAdmins = await Db.Users.CountAsync(u => u.Role == Roles.Admin && u.IsActive && u.Id != user.Id);
            if (otherAdmins == 0)
                ModelState.AddModelError(nameof(model.Role), "At least one active administrator is required.");
        }

        if (!ModelState.IsValid) return View(model);

        // Staff assignments only make sense for coaches and evaluators.
        if (user.Role != model.Role && model.Role != Roles.Coach && model.Role != Roles.Evaluator)
        {
            var assignments = await Db.SportAssignments.Where(x => x.UserId == user.Id).ToListAsync();
            Db.SportAssignments.RemoveRange(assignments);
        }

        user.FullName = model.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);

        Success($"Changes to {user.FullName} were saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        return View(new ResetPasswordViewModel { Id = user.Id, FullName = user.FullName, Username = user.Username });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        model.FullName = user.FullName;
        model.Username = user.Username;

        var pwError = PasswordService.Validate(model.NewPassword);
        if (pwError != null) ModelState.AddModelError(nameof(model.NewPassword), pwError);
        if (!ModelState.IsValid) return View(model);

        user.PasswordHash = PasswordService.Hash(model.NewPassword);
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users);

        Success($"The password for {user.FullName} was reset.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == CurrentUserId)
        {
            Failure("You cannot deactivate your own account.");
            return RedirectToAction(nameof(Index));
        }

        if (user.IsActive && user.Role == Roles.Admin)
        {
            var otherAdmins = await Db.Users.CountAsync(u => u.Role == Roles.Admin && u.IsActive && u.Id != user.Id);
            if (otherAdmins == 0)
            {
                Failure("At least one active administrator is required.");
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsActive = !user.IsActive;
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);
        Success(user.IsActive ? $"{user.FullName} can sign in again." : $"{user.FullName} was deactivated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (user.Id == CurrentUserId)
        {
            Failure("You cannot delete your own account.");
            return RedirectToAction(nameof(Index));
        }

        if (user.Role == Roles.Admin)
        {
            var otherAdmins = await Db.Users.CountAsync(u => u.Role == Roles.Admin && u.IsActive && u.Id != user.Id);
            if (otherAdmins == 0)
            {
                Failure("At least one active administrator is required.");
                return RedirectToAction(nameof(Index));
            }
        }

        if (await Db.Evaluations.AnyAsync(e => e.EvaluatorId == user.Id))
        {
            Failure($"{user.FullName} has recorded evaluations, so the account cannot be deleted. Deactivate it instead.");
            return RedirectToAction(nameof(Index));
        }

        // Keep the student's registration; it simply loses its account link.
        var registrations = await Db.Applicants.Where(a => a.UserId == user.Id).ToListAsync();
        foreach (var r in registrations) r.UserId = null;

        Db.Users.Remove(user);
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);
        Success($"The account for {user.FullName} was deleted.");
        return RedirectToAction(nameof(Index));
    }
}
