# TMC Sports Tryout System

An ASP.NET Core MVC (C# / .NET 8) web application for managing student-athlete
tryouts at Trinidad Municipal College: registration, sport management, tryout
scheduling, applicant records, evaluation, screening/ranking, final selection,
search & filtering, and reports.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL Server 8.x (MySQL Workbench is a client for this — see below)
- An SMTP account for the student verification codes (optional in development, see below)

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
   and adds the administrator account automatically — no SQL script to run by
   hand. Refresh the schema in Workbench afterward to see the tables.

## Run it

```bash
cd TmcTryoutSystem
dotnet restore
dotnet run
```

Then open the URL shown in the console (e.g. `http://localhost:5254`).

The database is created automatically on first launch. Only the administrator
account is added, so the system starts clean: create your sports, tryout
schedules and staff accounts from inside the app.

## Administrator account

| Role  | Username | Password    |
|-------|----------|-------------|
| Admin | `admin`  | `Admin@123` |

You can also register a brand-new student account from the login page.

## Student sign-up and verification

Students create their own account with **first, middle and last name, student ID
(`00-0000`), photos of the front and back of their student ID, email and
password**. There is no username — students sign in with their **email or
student ID**, while staff keep signing in with their username.

Two checks run after sign-up, in this order:

1. **Email (OTP).** A 6-digit code is emailed and is valid for 10 minutes. Until
   it is entered, the account cannot open any page other than the verification
   screen. The code is stored only as a hash, and a new one can be requested
   once a minute.
2. **Student ID (admin).** The athletics office reviews the uploaded photos on
   **Account verification**. Until they approve the account, the student can sign
   in, browse sports and schedules, and see their status — but cannot submit a
   tryout entry. Rejected photos can be re-uploaded from the student's
   verification page.

While a student is unverified, the app shell and dashboard label the account as
not verified, and tryout submissions are blocked server-side.

### Configuring the OTP email (SMTP)

Fill in the `Smtp` section of `appsettings.json` (or the matching environment
variables, e.g. `Smtp__Host`) with your mail provider:

```json
"Smtp": {
  "Host": "smtp.example.com",
  "Port": 587,
  "UserName": "you@example.com",
  "Password": "your-app-password",
  "FromEmail": "no-reply@tmc.edu.ph",
  "FromName": "TMC Tryouts",
  "EnableSsl": true
}
```

When SMTP is left blank the code is logged and, **in Development only**, shown
on the verification page so the flow can be tested without a mail server. It is
never displayed outside Development.

### Notes on the ID photos

The browser offers **Take photo** (rear camera on a phone) and **Choose from
gallery**. A photo is rejected when it is too small, too blurry, or black-and-white
/ filtered — blur and filters are detected in the browser before upload, and the
server independently rejects photos below 320x480 px. Photos are stored under
`wwwroot/uploads/account-id`.

### Existing accounts

Student accounts created before these checks existed have no code on file and no
ID photos, so the `GrandfatherExistingStudentAccounts` migration marks them
verified instead of locking them out.

## Project structure

- `Controllers/` — MVC controllers (Account, Home, Users, Sports, Schedules,
  Registration, Applicants, Evaluations, Screening, Selection, Reports)
- `Middleware/` — `AccountStatusMiddleware`, which enforces the email-OTP and
  student-ID gates described above
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
- **Student** — self-service sign-up with email and student-ID verification,
  then registration for one or more sports, own schedule, evaluation status and
  final result.

## Notes

- Passwords are hashed with PBKDF2-SHA256 (no external identity package
  required); email verification codes are hashed the same way.
- Duplicate registrations are prevented both in the form (student ID / one
  registration per account) and at the database level with unique indexes.
  Student IDs are unique per account, so a student ID doubles as a sign-in name.
- Student ID format is `00-0000` everywhere (accounts and applicant records).
- Email is sent with the built-in `System.Net.Mail.SmtpClient`, so no extra
  package is required.
- Reports can be viewed on-screen, printed, or exported to CSV.
- No sample records are seeded: `Data/DbSeeder.cs` creates the single
  administrator account and nothing else, so the database starts empty apart
  from that account.
