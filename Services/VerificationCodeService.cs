using System.Security.Cryptography;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Services;

/// <summary>
/// Issues and checks the 6-digit email verification codes used while a student
/// creates an account. Codes are stored only as a PBKDF2 hash.
/// </summary>
public class VerificationCodeService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxAttempts = 5;

    private readonly IEmailSender _email;
    private readonly ILogger<VerificationCodeService> _logger;

    public VerificationCodeService(IEmailSender email, ILogger<VerificationCodeService> logger)
    {
        _email = email;
        _logger = logger;
    }

    /// <summary>True when mail can actually be delivered; otherwise callers may show the code on screen in development.</summary>
    public bool EmailConfigured => _email.IsConfigured;

    /// <summary>Creates a fresh code on the user and stores its hash. Returns the plain code so it can be emailed.</summary>
    public string Issue(AppUser user)
    {
        var code = GenerateCode();
        user.OtpHash = PasswordService.Hash(code);
        user.OtpExpiresAt = DateTime.Now.Add(Lifetime);
        user.OtpSentAt = DateTime.Now;
        user.OtpAttempts = 0;
        return code;
    }

    /// <summary>Seconds the caller still has to wait before a new code may be requested.</summary>
    public int SecondsUntilResendAllowed(AppUser user)
    {
        if (!user.OtpSentAt.HasValue) return 0;
        var remaining = ResendCooldown - (DateTime.Now - user.OtpSentAt.Value);
        return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : 0;
    }

    public enum CheckResult
    {
        Success,
        Missing,
        Expired,
        TooManyAttempts,
        Incorrect
    }

    /// <summary>Validates a submitted code and updates the attempt counter. Does not save changes.</summary>
    public CheckResult Check(AppUser user, string? submitted)
    {
        if (string.IsNullOrWhiteSpace(user.OtpHash) || !user.OtpExpiresAt.HasValue)
            return CheckResult.Missing;

        if (user.OtpAttempts >= MaxAttempts)
            return CheckResult.TooManyAttempts;

        var code = (submitted ?? string.Empty).Trim();
        if (code.Length == 0)
            return CheckResult.Incorrect;

        if (user.OtpExpiresAt.Value < DateTime.Now)
            return CheckResult.Expired;

        if (!PasswordService.Verify(code, user.OtpHash))
        {
            user.OtpAttempts++;
            return user.OtpAttempts >= MaxAttempts ? CheckResult.TooManyAttempts : CheckResult.Incorrect;
        }

        // Code accepted: burn it so it cannot be replayed.
        user.OtpHash = null;
        user.OtpExpiresAt = null;
        user.OtpAttempts = 0;
        user.EmailVerified = true;
        user.EmailVerifiedAt = DateTime.Now;
        return CheckResult.Success;
    }

    public Task SendCodeAsync(AppUser user, string code) =>
        _email.SendAsync(user.Email!, "Your TMC Tryouts verification code", BuildBody(user, code));

    private static string BuildBody(AppUser user, string code) =>
        $"""
         <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;padding:24px;">
           <div style="background:#0b2559;color:#fff;border-radius:14px 14px 0 0;padding:18px 24px;">
             <strong style="font-size:18px;">TMC Tryouts</strong>
             <div style="opacity:.8;font-size:12px;">Trinidad Municipal College</div>
           </div>
           <div style="border:1px solid #e3e8f2;border-top:none;border-radius:0 0 14px 14px;padding:24px;">
             <p style="margin-top:0;">Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
             <p>Use this code to verify your email address and finish creating your student account:</p>
             <p style="text-align:center;margin:28px 0;">
               <span style="display:inline-block;font-size:32px;letter-spacing:8px;font-weight:700;color:#0b2559;background:#f3f6fc;border:1px solid #e3e8f2;border-radius:12px;padding:12px 20px;">{code}</span>
             </p>
             <p>The code expires in {(int)Lifetime.TotalMinutes} minutes. If you did not try to create an account, you can ignore this email.</p>
           </div>
         </div>
         """;

    private static string GenerateCode()
    {
        // 000000-999999, uniformly random and not predictable from the clock.
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }
}
