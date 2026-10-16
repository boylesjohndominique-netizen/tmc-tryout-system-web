using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;

namespace TmcTryoutSystem.Data;

/// <summary>
/// Creates the single account the system needs to start: the main administrator.
/// Sports, tryout schedules, staff accounts and student accounts are all created
/// through the application, so nothing else is seeded.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            db.Users.Add(new AppUser
            {
                Username = "admin",
                FullName = "Athletics Administrator",
                Email = "admin@tmc.edu.ph",
                Role = Roles.Admin,
                PasswordHash = PasswordService.Hash("Admin@123"),
                IsActive = true,
                EmailVerified = true,
                IdVerified = true,
                CreatedAt = DateTime.Now
            });

            db.SaveChanges();
        }

        BackfillStaffPermissions(db);
    }

    /// <summary>
    /// One-time gap-fill for staff accounts created before permissions existed. Guarded by an empty
    /// permissions table so an administrator clearing a single account is never overwritten on restart.
    /// </summary>
    private static void BackfillStaffPermissions(AppDbContext db)
    {
        if (db.UserPermissions.Any()) return;

        var staff = db.Users
            .Where(u => u.Role == Roles.Coach || u.Role == Roles.Evaluator)
            .ToList();
        if (staff.Count == 0) return;

        foreach (var user in staff)
            foreach (var key in Permissions.DefaultsFor(user.Role))
                db.UserPermissions.Add(new UserPermission { UserId = user.Id, Permission = key });

        db.SaveChanges();
    }
}
