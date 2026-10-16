using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

public class AccountController : AppController
{
    private readonly IRealtimeNotifier _rt;
    private readonly VerificationCodeService _codes;
    private readonly AccountIdPictureService _pictures;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppDbContext db,
        IRealtimeNotifier rt,
        VerificationCodeService codes,
        AccountIdPictureService pictures,
        IWebHostEnvironment env,
        ILogger<AccountController> logger) : base(db)
    {
        _rt = rt;
        _codes = codes;
        _pictures = pictures;
        _env = env;
        _logger = logger;
    }

    private const string DevOtpKey = "DevOtp";

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

        var key = model.Identifier.Trim();
        var lowered = key.ToLowerInvariant();

        // Students have no username: they sign in with the verified email or their student ID.
        // Staff sign in with their username (or email).
        var user = await Db.Users.FirstOrDefaultAsync(u =>
            u.Username == lowered ||
            (u.Email != null && u.Email.ToLower() == lowered) ||
            (u.StudentId != null && u.StudentId == key.ToUpper()));

        if (user == null || !PasswordService.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Those sign-in details are not correct. Check them and try again.");
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

        var studentId = UiHelpers.NormalizeStudentId(model.StudentId);
        model.StudentId = studentId;

        if (studentId.Length > 0 && await Db.Users.AnyAsync(u => u.StudentId == studentId))
            ModelState.AddModelError(nameof(model.StudentId), "That student ID is already used by another account.");

        var email = (model.Email ?? string.Empty).Trim();
        var loweredEmail = email.ToLowerInvariant();
        if (email.Length > 0 && await Db.Users.AnyAsync(u =>
                (u.Email != null && u.Email.ToLower() == loweredEmail) || u.Username == loweredEmail))
            ModelState.AddModelError(nameof(model.Email), "That email address is already registered. Sign in instead.");

        _pictures.Validate(model.IdPictureFront, nameof(model.IdPictureFront), "front", ModelState);
        _pictures.Validate(model.IdPictureBack, nameof(model.IdPictureBack), "back", ModelState);

        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            // Students have no username of their own; the email doubles as the unique login key.
            Username = loweredEmail,
            FullName = model.FullName,
            FirstName = model.FirstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim(),
            LastName = model.LastName.Trim(),
            StudentId = studentId,
            CourseOrGrade = model.CourseOrGrade.Trim(),
            YearLevel = model.YearLevel.Trim(),
            Email = email,
            Role = Roles.Student,
            PasswordHash = PasswordService.Hash(model.Password),
            IsActive = true,
            EmailVerified = false,
            IdVerified = false
        };

        try
        {
            user.IdPictureFrontPath = await _pictures.SaveAsync(model.IdPictureFront!, studentId, "front");
            user.IdPictureBackPath = await _pictures.SaveAsync(model.IdPictureBack!, studentId, "back");
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "We could not save your ID photos. Please try again.");
            return View(model);
        }

        var code = _codes.Issue(user);
        Db.Users.Add(user);

        try
        {
            await Db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _pictures.Delete(user.IdPictureFrontPath);
            _pictures.Delete(user.IdPictureBackPath);
            ModelState.AddModelError(string.Empty,
                "That email address or student ID is already in use. Sign in instead, or contact the athletics office.");
            return View(model);
        }

        await SendCodeAsync(user, code);

        // Sign in straight away, but the account-status middleware keeps them on the
        // verification page until the email code is confirmed.
        await SignInAsync(user, false);
        await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);

        Success($"We emailed a 6-digit code to {email}. Enter it to activate your account.");
        return RedirectToAction(nameof(VerifyEmail));
    }

    // ---------- Email OTP verification ----------
    [Authorize, HttpGet]
    public async Task<IActionResult> VerifyEmail()
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));
        if (user.EmailVerified) return RedirectToAction("Index", "Home");

        return View(new VerifyEmailViewModel
        {
            Email = user.Email,
            SecondsToResend = _codes.SecondsUntilResendAllowed(user),
            DevCode = TakeDevCode()
        });
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));
        if (user.EmailVerified) return RedirectToAction("Index", "Home");

        model.Email = user.Email;
        model.SecondsToResend = _codes.SecondsUntilResendAllowed(user);

        if (ModelState.IsValid)
        {
            var result = _codes.Check(user, model.Code);
            await Db.SaveChangesAsync();

            if (result == VerificationCodeService.CheckResult.Success)
            {
                await _rt.ChangedAsync(RealtimeScopes.Users, RealtimeScopes.Dashboard);
                Success("Your email is verified. Your student ID is now with the athletics office for approval — you can browse sports and schedules while you wait.");
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(nameof(model.Code), result switch
            {
                VerificationCodeService.CheckResult.Expired => "That code has expired. Ask for a new one.",
                VerificationCodeService.CheckResult.TooManyAttempts => "Too many incorrect tries. Request a new code.",
                VerificationCodeService.CheckResult.Missing => "There is no active code for this account. Request a new one.",
                _ => "That code is not correct. Check it and try again."
            });
        }

        model.DevCode = TakeDevCode();
        return View(model);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> ResendOtp()
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));
        if (user.EmailVerified) return RedirectToAction("Index", "Home");

        var wait = _codes.SecondsUntilResendAllowed(user);
        if (wait > 0)
        {
            Warning($"Please wait {wait} more second{(wait == 1 ? "" : "s")} before requesting another code.");
            return RedirectToAction(nameof(VerifyEmail));
        }

        var code = _codes.Issue(user);
        await Db.SaveChangesAsync();
        await SendCodeAsync(user, code);

        Success($"A new code is on its way to {user.Email}.");
        return RedirectToAction(nameof(VerifyEmail));
    }

    // ---------- Account verification status (email verified, waiting on admin) ----------
    [Authorize(Roles = Roles.Student)]
    [HttpGet]
    public async Task<IActionResult> VerificationStatus()
    {
        var user = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));

        if (!user.EmailVerified) return RedirectToAction(nameof(VerifyEmail));

        return View(new AccountStatusViewModel
        {
            FullName = user.DisplayName,
            StudentId = user.StudentId,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            IdVerified = user.IdVerified,
            IdVerifiedAt = user.IdVerifiedAt,
            VerificationRemarks = user.VerificationRemarks,
            IdPictureFrontPath = user.IdPictureFrontPath,
            IdPictureBackPath = user.IdPictureBackPath
        });
    }

    /// <summary>Lets a student replace the ID photos the admin rejected.</summary>
    [Authorize(Roles = Roles.Student), HttpPost]
    public async Task<IActionResult> ReuploadId(IFormFile? idPictureFront, IFormFile? idPictureBack)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));
        if (!user.EmailVerified) return RedirectToAction(nameof(VerifyEmail));
        if (user.IdVerified)
        {
            Warning("Your account is already verified.");
            return RedirectToAction(nameof(VerificationStatus));
        }

        _pictures.Validate(idPictureFront, nameof(idPictureFront), "front", ModelState);
        _pictures.Validate(idPictureBack, nameof(idPictureBack), "back", ModelState);
        if (!ModelState.IsValid)
        {
            Failure(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(VerificationStatus));
        }

        var studentId = user.StudentId ?? "student";
        var front = await _pictures.SaveAsync(idPictureFront!, studentId, "front");
        var back = await _pictures.SaveAsync(idPictureBack!, studentId, "back");
        _pictures.Delete(user.IdPictureFrontPath);
        _pictures.Delete(user.IdPictureBackPath);

        user.IdPictureFrontPath = front;
        user.IdPictureBackPath = back;
        user.IdVerified = false;
        user.VerifiedById = null;
        user.IdVerifiedAt = null;
        user.VerificationRemarks = null;
        await Db.SaveChangesAsync();
        await _rt.ChangedAsync(RealtimeScopes.Users);

        Success("Your new ID photos were uploaded. The athletics office will review them again.");
        return RedirectToAction(nameof(VerificationStatus));
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
        return View(ToProfile(user));
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return RedirectToAction(nameof(Login));

        model.Username = user.Username;
        model.Role = user.Role;
        model.IsStudent = user.Role == Roles.Student;
        model.StudentId = user.StudentId;
        model.EmailVerified = user.EmailVerified;
        model.IdVerified = user.IdVerified;
        model.VerificationRemarks = user.VerificationRemarks;

        // A student's email is the verified sign-in key, so it is changed only through support.
        if (model.IsStudent) model.Email = user.Email;

        // Course and year live on the student account, so they are required for students only.
        if (model.IsStudent)
        {
            if (string.IsNullOrWhiteSpace(model.CourseOrGrade))
                ModelState.AddModelError(nameof(model.CourseOrGrade), "Select your course.");
            if (string.IsNullOrWhiteSpace(model.YearLevel))
                ModelState.AddModelError(nameof(model.YearLevel), "Select your year level.");
        }

        if (!ModelState.IsValid) return View(model);

        user.FullName = model.FullName.Trim();
        if (model.IsStudent)
        {
            user.CourseOrGrade = string.IsNullOrWhiteSpace(model.CourseOrGrade) ? null : model.CourseOrGrade.Trim();
            user.YearLevel = string.IsNullOrWhiteSpace(model.YearLevel) ? null : model.YearLevel.Trim();
        }
        if (!model.IsStudent)
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

    // ---------- Helpers ----------
    private static ProfileViewModel ToProfile(AppUser user) => new()
    {
        Username = user.Username,
        Role = user.Role,
        FullName = user.FullName,
        Email = user.Email,
        IsStudent = user.Role == Roles.Student,
        StudentId = user.StudentId,
        CourseOrGrade = user.CourseOrGrade,
        YearLevel = user.YearLevel,
        EmailVerified = user.EmailVerified,
        IdVerified = user.IdVerified,
        VerificationRemarks = user.VerificationRemarks
    };

    /// <summary>
    /// Sends the code and, when SMTP is not configured, surfaces it in Development so sign-up stays testable.
    /// A delivery failure never throws: the account already exists and the student can ask for a new code.
    /// </summary>
    private async Task SendCodeAsync(AppUser user, string code)
    {
        if (_codes.EmailConfigured)
        {
            try
            {
                await _codes.SendCodeAsync(user, code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not email the verification code to {Email}.", user.Email);
                Failure($"Your account was created, but we could not send the code to {user.Email} just now. " +
                        "Tap \"Resend the code\", or contact the athletics office if it keeps failing.");
            }
            return;
        }

        if (_env.IsDevelopment())
        {
            TempData[DevOtpKey] = code;
            Warning($"Email is not configured yet, so the verification code for {user.Email} is {code}. (Development only.)");
        }
        await _codes.SendCodeAsync(user, code); // logs a warning when unconfigured
    }

    private string? TakeDevCode()
    {
        if (!_env.IsDevelopment()) return null;
        var code = TempData[DevOtpKey] as string;
        if (code != null) TempData.Keep(DevOtpKey);
        return code;
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
            new(ClaimTypes.Role, user.Role),
            new(VerificationClaims.EmailVerified, user.EmailVerified.ToString()),
            new(VerificationClaims.IdVerified, user.IdVerified.ToString())
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
