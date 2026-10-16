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
        if (db.Users.Any()) return;

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
}
