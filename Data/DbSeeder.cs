using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;

namespace TmcTryoutSystem.Data;

/// <summary>
/// Creates the administrator account, sports, schedules and a small set of demo
/// applicants the first time the database is created. Delete the demo sections
/// for production use.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        // ---------- Account ----------
        db.Users.Add(new AppUser
        {
            Username = "admin",
            FullName = "Athletics Administrator",
            Email = "admin@tmc.edu.ph",
            Role = Roles.Admin,
            PasswordHash = PasswordService.Hash("Admin@123"),
            IsActive = true,
            CreatedAt = DateTime.Now
        });

        // A coach account so tryout staff features are demoable right away.
        db.Users.Add(new AppUser
        {
            Username = "coach",
            FullName = "Coach Demo",
            Email = "coach@tmc.edu.ph",
            Role = Roles.Coach,
            PasswordHash = PasswordService.Hash("Coach@123"),
            IsActive = true,
            CreatedAt = DateTime.Now
        });

        // ---------- Sports ----------
        var basketball = new Sport { Name = "Basketball", Icon = "dribbble", QualifyingScore = 75, Slots = 15, Description = "Varsity team for the college intramural and municipal leagues." };
        var volleyball = new Sport { Name = "Volleyball", Icon = "people", QualifyingScore = 75, Slots = 12, Description = "Men's and women's varsity volleyball." };
        var athletics = new Sport { Name = "Athletics", Icon = "stopwatch", QualifyingScore = 70, Slots = 20, Description = "Track and field events including sprints, distance runs, jumps and throws." };
        var badminton = new Sport { Name = "Badminton", Icon = "lightning-charge", QualifyingScore = 72, Slots = 10, Description = "Singles and doubles badminton." };
        var swimming = new Sport { Name = "Swimming", Icon = "water", QualifyingScore = 70, Slots = 8, Description = "Freestyle, backstroke, breaststroke and relay events." };
        var sports = new[] { basketball, volleyball, athletics, badminton, swimming };
        db.Sports.AddRange(sports);
        db.SaveChanges();

        // Assign the demo coach to basketball and volleyball.
        var coachUser = db.Users.Local.First(u => u.Username == "coach");
        db.SportAssignments.AddRange(
            new SportAssignment { SportId = basketball.Id, UserId = coachUser.Id },
            new SportAssignment { SportId = volleyball.Id, UserId = coachUser.Id });

        // ---------- Schedules (one finished, one coming up per sport) ----------
        var venues = new Dictionary<string, string>
        {
            ["Basketball"] = "College Gymnasium",
            ["Volleyball"] = "College Gymnasium",
            ["Athletics"] = "Municipal Oval",
            ["Badminton"] = "Covered Court",
            ["Swimming"] = "Municipal Swimming Pool"
        };
        var today = DateTime.Today;
        var pastSchedules = new Dictionary<int, TryoutSchedule>();
        int offset = 0;
        foreach (var sport in sports)
        {
            var past = new TryoutSchedule
            {
                SportId = sport.Id,
                Title = $"{sport.Name} Tryouts - Round 1",
                StartAt = today.AddDays(-9 + offset).AddHours(8),
                EndAt = today.AddDays(-9 + offset).AddHours(11),
                Venue = venues[sport.Name],
                Notes = "Bring your school ID, sports attire and drinking water."
            };
            var next = new TryoutSchedule
            {
                SportId = sport.Id,
                Title = $"{sport.Name} Tryouts - Final Round",
                StartAt = today.AddDays(5 + offset).AddHours(14),
                EndAt = today.AddDays(5 + offset).AddHours(17),
                Venue = venues[sport.Name],
                Notes = "Final round for shortlisted and newly registered applicants."
            };
            pastSchedules[sport.Id] = past;
            db.Schedules.AddRange(past, next);
            offset++;
        }
        db.SaveChanges();

        // ---------- Demo applicants (college students, some trying out for two sports) ----------
        var rng = new Random(2026);
        var firstNames = new[] { "Mark", "Jasmine", "Carlo", "Angel", "Paolo", "Kristine", "Rey", "Bea", "Lance", "Maricel", "Joshua", "Trisha", "Nico", "Hazel", "Ivan", "Camille" };
        var lastNames = new[] { "Garcia", "Villanueva", "Bautista", "Ramos", "Mendoza", "Castillo", "Aquino", "Flores", "Navarro", "Torres", "Domingo", "Salazar", "Pascual", "Lim", "Cabrera", "Uy" };
        var courses = new[] { "BSIT", "BSOA", "BSED", "BSPolSci", "BSCrim" };
        var years = new[] { "1st Year", "2nd Year", "3rd Year", "4th Year" };

        var applicants = new List<Applicant>();
        // 12 distinct students; the last four try out for a second sport as well.
        for (int i = 0; i < 16; i++)
        {
            int studentIndex = i % 12;
            var sport = sports[i % sports.Length];
            bool female = studentIndex % 2 == 1;
            string lastName = lastNames[(studentIndex * 5 + 3) % lastNames.Length];

            var a = new Applicant
            {
                // TMC student number format: 24-020298(00-000000)
                StudentId = $"24-{020200 + studentIndex * 17:000000}(00-{100000 + studentIndex * 31:000000})",
                FirstName = firstNames[studentIndex],
                MiddleName = null,
                LastName = lastName,
                Gender = female ? "Female" : "Male",
                BirthDate = new DateTime(2003 + rng.Next(0, 4), rng.Next(1, 13), rng.Next(1, 28)),
                CourseOrGrade = courses[studentIndex % courses.Length],
                YearLevel = years[studentIndex % years.Length],
                Email = $"{firstNames[studentIndex].ToLower()}.{lastName.ToLower()}@example.com",
                ContactNumber = $"0917-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
                Address = "Poblacion, Trinidad, Bohol",
                HeightCm = 150 + rng.Next(0, 35),
                WeightKg = 45 + rng.Next(0, 35),
                EmergencyContactName = "Parent / Guardian",
                EmergencyContactNumber = $"0928-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
                SportId = sport.Id,
                RegisteredAt = DateTime.Now.AddDays(-rng.Next(10, 20)),
                Schedule = null,
                IdVerified = studentIndex % 3 != 2 // two of every three students already verified
            };
            a.TryoutScheduleId = pastSchedules[sport.Id].Id;
            applicants.Add(a);
        }

        db.Applicants.AddRange(applicants);
        db.SaveChanges();
    }
}
