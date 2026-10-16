using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Middleware;

/// <summary>
/// Enforces the two student verification stages:
/// <list type="bullet">
///   <item>Email must be verified with the OTP before any page loads.</item>
///   <item>Until an admin verifies the student ID pictures, tryout submissions are blocked
///         (browsing sports and schedules stays open).</item>
/// </list>
/// </summary>
public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;

    public AccountStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Only these paths stay reachable while the email is unverified.</summary>
    private static readonly string[] EmailGateAllowed =
    {
        "/account/verifyemail",
        "/account/resendotp",
        "/account/verificationstatus",
        "/account/logout",
        "/account/login",
        "/account/accessdenied",
        "/home/status",
        "/home/error"
    };

    /// <summary>Student routes that count as submitting a tryout entry.</summary>
    private static readonly string[] TryoutSubmitPaths =
    {
        "/registration/create",
        "/registration/edit",
        "/registration/withdraw"
    };

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(Roles.Student))
        {
            var idText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idText, out var userId))
            {
                var status = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.EmailVerified, u.IdVerified, u.IsActive })
                    .FirstOrDefaultAsync();

                if (status is { IsActive: true })
                {
                    var path = (context.Request.Path.Value ?? string.Empty).ToLowerInvariant();

                    if (!status.EmailVerified)
                    {
                        if (!EmailGateAllowed.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
                        {
                            context.Response.Redirect("/Account/VerifyEmail");
                            return;
                        }
                    }
                    else if (!status.IdVerified && TryoutSubmitPaths.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
                    {
                        context.Response.Redirect("/Account/VerificationStatus");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
