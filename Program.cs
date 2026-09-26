using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TmcTryoutSystem.Data;
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
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddSignalR();

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
                var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                bool valid = false;
                if (int.TryParse(idText, out var userId))
                {
                    var user = await db.Users.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.IsActive, u.Role })
                        .FirstOrDefaultAsync();
                    valid = user != null && user.IsActive && user.Role == role;
                }

                if (!valid)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

// Every page requires a signed-in user unless it is marked [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<TryoutHub>("/hubs/tryout");

app.Run();
