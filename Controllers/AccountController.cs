using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

public class AccountController : AppController
{
    private readonly IRealtimeNotifier _rt;

    public AccountController(AppDbContext db, IRealtimeNotifier rt) : base(db)
    {
        _rt = rt;
    }

    // ---------- Login ----------
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        LoadHeroStats();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var username = model.Username.Trim().ToLowerInvariant();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user == null || !PasswordService.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "The username or password is incorrect. Check both and try again.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account is deactivated. Contact the athletics office to restore access.");
            return View(model);
        }

        await SignInAsync(user, model.RememberMe);
        user.LastLoginAt = DateTime.Now;
        await Db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ---------- Student sign-up ----------
    [AllowAnonymous, HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        LoadHeroStats();
        return View(new RegisterViewModel());
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var passwordError = PasswordService.Validate(model.Password);
        if (passwordError != null) ModelState.AddModelError(nameof(model.Password), passwordError);

        var username = (model.Username ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(username) && await Db.Users.AnyAsync(u => u.Username == username))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken. Try another one.");

        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            Username = username,
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim(),
            Role = Roles.Student,
            PasswordHash = PasswordService.Hash(model.Password),
            IsActive = true,
            LastLoginAt = DateTime.Now
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        await SignInAsync(user, false);
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);
        Success("Your account is ready. Complete your athlete registration to join a tryout.");
        return RedirectToAction("Create", "Registration");
    }

    // ---------- Logout ----------
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------- Profile ----------
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));
        return View(new ProfileViewModel
        {
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email
        });
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));

        model.Username = user.Username;
        model.Role = user.Role;
        if (!ModelState.IsValid) return View(model);

        user.FullName = model.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users);

        // Refresh the cookie so the new name shows immediately.
        var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInAsync(user, current.Properties?.IsPersistent ?? false);

        Success("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    // ---------- Change password ----------
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));

        if (!string.IsNullOrEmpty(model.CurrentPassword) && !PasswordService.Verify(model.CurrentPassword, user.PasswordHash))
            ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is not correct.");

        var passwordError = PasswordService.Validate(model.NewPassword);
        if (passwordError != null) ModelState.AddModelError(nameof(model.NewPassword), passwordError);

        if (!ModelState.IsValid) return View(model);

        user.PasswordHash = PasswordService.Hash(model.NewPassword);
        await Db.SaveChangesAsync();

        Success("Your password has been changed.");
        return RedirectToAction(nameof(ChangePassword));
    }

    // ---------- Hero stats for the auth pages ----------
    /// <summary>Loads figures from the most recent completed tryout into ViewData for the auth hero panel.</summary>
    private void LoadHeroStats()
    {
        try
        {
            var now = DateTime.Now;
            var schedule = Db.Schedules.AsNoTracking()
                .Include(s => s.Sport)
                .Where(s => !s.IsCancelled && s.EndAt < now)
                .OrderByDescending(s => s.EndAt)
                .FirstOrDefault();
            if (schedule == null) return;

            var applicants = Db.Applicants.AsNoTracking()
                .Include(a => a.Evaluations)
                .Where(a => a.TryoutScheduleId == schedule.Id)
                .ToList();
            var evaluations = applicants.SelectMany(a => a.Evaluations).ToList();
            if (evaluations.Count == 0) return;

            double Avg(Func<Evaluation, int> pick) => evaluations.Average(e => pick(e));

            ViewData["HeroStats"] = new AuthHeroStatsViewModel
            {
                SportName = schedule.Sport?.Name ?? "Tryout",
                SportIcon = schedule.Sport?.Icon ?? "trophy",
                ScheduleTitle = schedule.Title,
                ScheduleDate = schedule.StartAt.ToString("MMM d, yyyy"),
                Applicants = applicants.Count,
                Evaluators = evaluations.Select(e => e.EvaluatorId).Distinct().Count(),
                Evaluations = evaluations.Count,
                Selected = applicants.Count(a => a.Status == SelectionStatus.Selected),
                Slots = schedule.Sport?.Slots ?? 0,
                QualifyingScore = schedule.Sport?.QualifyingScore ?? 75,
                TopScore = Math.Round(evaluations.Max(e => e.Average), 1),
                Criteria =
                [
                    ("Skill", Math.Round(Avg(e => e.Skill))),
                    ("Speed", Math.Round(Avg(e => e.Speed))),
                    ("Agility", Math.Round(Avg(e => e.Agility))),
                    ("Endurance", Math.Round(Avg(e => e.Endurance))),
                    ("Teamwork", Math.Round(Avg(e => e.Teamwork))),
                    ("Discipline", Math.Round(Avg(e => e.Discipline)))
                ]
            };
        }
        catch
        {
            // The hero panel is decorative; never block the login or register page.
        }
    }

    private async Task SignInAsync(AppUser user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(remember ? 24 * 7 : 8)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
    }
}
