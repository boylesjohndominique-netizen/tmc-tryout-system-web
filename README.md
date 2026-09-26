# TMC Sports Tryout System

An ASP.NET Core MVC (C# / .NET 8) web application for managing student-athlete
tryouts at Trinidad Municipal College: registration, sport management, tryout
scheduling, applicant records, evaluation, screening/ranking, final selection,
search & filtering, and reports.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL Server 8.x (MySQL Workbench is a client for this — see below)

## Database setup (MySQL Workbench)

1. Open MySQL Workbench and connect to your MySQL server.
2. Create an empty schema, e.g. `tmc_tryout`
   (Server menu or the "Create Schema" icon → name it `tmc_tryout` → Apply).
3. Open `appsettings.json` and set the `ConnectionStrings:Default` value to
   match your server:
   ```
   Server=localhost;Port=3306;Database=tmc_tryout;User=root;Password=YOUR_MYSQL_PASSWORD;TreatTinyAsBoolean=true;
   ```
4. Run the app (below). On first launch it creates every table in that schema
   and seeds the demo data automatically — no SQL script to run by hand.
   Refresh the schema in Workbench afterward to see the tables.

## Run it

```bash
cd TmcTryoutSystem
dotnet restore
dotnet run
```

Then open the URL shown in the console (e.g. `http://localhost:5254`).

The database is created automatically on first launch and seeded with the
admin account, sports, schedules and sample applicants so you can explore every
screen immediately.

## Demo account

| Role  | Username | Password    |
|-------|----------|-------------|
| Admin | `admin`  | `Admin@123` |

You can also register a brand-new student account from the login page.

## Project structure

- `Controllers/` — MVC controllers (Account, Home, Users, Sports, Schedules,
  Registration, Applicants, Evaluations, Screening, Selection, Reports)
- `Models/` — EF Core entities (`AppUser`, `Sport`, `TryoutSchedule`,
  `Applicant`, `Evaluation`, roles and enums)
- `ViewModels/` — form and page view models with validation
- `Data/` — `AppDbContext` and `DbSeeder`
- `Services/` — password hashing, screening/ranking logic, shared
  registration-form logic
- `Views/` — Razor views, Bootstrap 5 + Bootstrap Icons, custom
  blue/yellow/white theme in `wwwroot/css/site.css`

## Roles and access

- **Admin** — full access: user accounts, sports, schedules, applicants,
  evaluations, screening, selection, reports.
- **Coach** — assigned sports only: schedules, applicants, evaluations,
  screening, final selection, reports for those sports.
- **Evaluator** — assigned sports only: applicants, evaluations, screening,
  reports (cannot record final selection).
- **Student** — self-service registration for one sport, view own schedule,
  evaluation status and final result.

## Notes

- Passwords are hashed with PBKDF2-SHA256 (no external identity package
  required).
- Duplicate registrations are prevented both in the form (student ID / one
  registration per account) and at the database level with unique indexes.
- Reports can be viewed on-screen, printed, or exported to CSV.
- To start with an empty database, delete `tryout.db` (and remove/trim the
  demo section in `Data/DbSeeder.cs`) before running.
