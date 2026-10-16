using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Middleware;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Vercel / container port binding ----------
// Vercel injects the PORT env var; honour it so the container receives traffic.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddControllersWithViews(options =>
{
    // Non-nullable strings are not implicitly required; validation is declared explicitly on each model.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is missing. "
        + "Set it via appsettings.json (local dev) or the ConnectionStrings__Default environment variable (production).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddScoped<ApplicantFormService>();
builder.Services.AddScoped<AccountIdPictureService>();
builder.Services.AddScoped<ProfilePictureService>();
builder.Services.AddScoped<VerificationCodeService>();
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddSignalR();

// ---------- Outgoing mail (student email verification codes) ----------
var smtpOptions = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// ---------- Forwarded headers (Vercel terminates TLS) ----------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust all proxies in a container environment.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "TmcTryout.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Keep the cookie in sync with the database: deactivated accounts or changed roles sign out.
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var principal = context.Principal;
                var idText = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = principal?.FindFirstValue(ClaimTypes.Role);
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                bool valid = false;
                bool emailVerified = false, idVerified = false;
                var permissions = new List<string>();
                if (int.TryParse(idText, out var userId))
                {
                    var user = await db.Users.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new
                        {
                            u.IsActive,
                            u.Role,
                            u.EmailVerified,
                            u.IdVerified,
                            Granted = u.Permissions.Select(p => p.Permission).ToList()
                        })
                        .FirstOrDefaultAsync();
                    valid = user != null && user.IsActive && user.Role == role;
                    if (user != null)
                    {
                        emailVerified = user.EmailVerified;
                        idVerified = user.IdVerified;
                        permissions = user.Role == Roles.Admin ? Permissions.All.ToList() : user.Granted;
                    }
                }

                if (!valid)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // Keep the verification badges and the granted permissions in the app shell accurate
                // after an administrator approves an account or changes what a staff member may do.
                bool verificationChanged = principal != null &&
                    (principal.FindFirst(VerificationClaims.EmailVerified)?.Value != emailVerified.ToString() ||
                     principal.FindFirst(VerificationClaims.IdVerified)?.Value != idVerified.ToString());

                var currentPermissions = principal?.Claims
                    .Where(c => c.Type == Permissions.ClaimType)
                    .Select(c => c.Value)
                    .OrderBy(v => v)
                    .ToList() ?? new List<string>();
                bool permissionsChanged = !currentPermissions.SequenceEqual(permissions.OrderBy(v => v));

                if (principal != null && (verificationChanged || permissionsChanged))
                {
                    var claims = principal.Claims
                        .Where(c => c.Type != VerificationClaims.EmailVerified
                                    && c.Type != VerificationClaims.IdVerified
                                    && c.Type != Permissions.ClaimType)
                        .ToList();
                    claims.Add(new Claim(VerificationClaims.EmailVerified, emailVerified.ToString()));
                    claims.Add(new Claim(VerificationClaims.IdVerified, idVerified.ToString()));
                    foreach (var permission in permissions)
                        claims.Add(new Claim(Permissions.ClaimType, permission));
                    var identity = new ClaimsIdentity(claims, principal.Identity?.AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
                    context.ReplacePrincipal(new ClaimsPrincipal(identity));
                    context.ShouldRenew = true;
                }
            }
        };
    });

// Every page requires a signed-in user unless it is marked [AllowAnonymous].
// Each grantable capability is also registered as a policy so actions can require it directly.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(Permissions.ClaimType, permission));
});

var app = builder.Build();

// ---------- Database migration ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Forwarded headers must come first so scheme detection is correct.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS is safe: Vercel already serves HTTPS externally.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<TryoutHub>("/hubs/tryout");

app.Run();
