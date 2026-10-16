# AGENTS.md — TMC Sports Tryout System

ASP.NET Core MVC, **.NET 8** (`net8.0`), Pomelo EF Core + MySQL. Solution: `TmcTryoutSystem.slnx`.
This file is the single source of project rules. Keep it short; extend only when a rule is proven necessary.

## Token & context efficiency (highest priority)
- Never re-read a file you already read this session; reuse the content in context.
- Use `Grep`/`Glob` with a narrow path and pattern before opening files. Do not read whole directories or `obj`/`bin`/`Migrations/*.Designer.cs` unless the task requires it.
- Batch independent tool calls in one message; run dependent steps sequentially.
- Prefer one precise search over repeated exploratory scans. Stop searching once the target symbol/file is found.
- Do not run the app, `dotnet restore`, or full builds "just to check"; use the targeted commands below.
- Keep responses short: no restating the request, no narrating each tool call, no summaries of unchanged files.

## Workflow
1. Locate the smallest set of files that own the behavior (`Grep` first).
2. State the root cause or the exact change needed in one or two lines before editing.
3. Reuse existing patterns/services; do not introduce new abstractions, packages, or files unless required.
4. Make the minimal edit. Match surrounding style exactly.
5. Verify with a targeted build/test (below), then report changed files + result.

## Architecture (follow existing conventions)
- `Controllers/` — MVC controllers. Inherit `AppController` (`Db`, `CurrentUserId`, `IsAdmin`, `AllowedSportIdsAsync`, `MatchesCourse`, `Success/Warning/Failure`).
- `Models/` — EF Core entities. `AppUser`, `Sport`, `SportCategory`, `TryoutSchedule`, `Applicant`, `Evaluation`. Roles/enums live in `Roles.cs`/`Enums.cs`.
- `ViewModels/` — per-page form/view models with DataAnnotations; validation lives here, controller-specific checks in the action.
- `Views/` — Razor (`.cshtml`). Shared layout `_Layout.cshtml`; auth pages use `_AuthLayout.cshtml`. Bootstrap 5 + Bootstrap Icons; theme in `wwwroot/css/site.css`. Use existing classes (`.card-x`, `.badge-soft`, `.avatar`, `.btn-gold`).
- `Services/` — business logic (`PasswordService`, `ScreeningService`, `AccountIdPictureService`, `VerificationCodeService`, `IRealtimeNotifier`). Register new services in `Program.cs` DI.
- `Data/` — `AppDbContext`, `DbSeeder`. `Middleware/` — `AccountStatusMiddleware`. `Helpers/` — `UiHelpers`, `ImageProbe`, `QueryExtensions`.

## Data, migrations & safety
- Schema changes: add the property to the entity, then run `dotnet ef migrations add <Name>`. Never edit generated migration/Designer/snapshot files by hand.
- Preserve existing migrations. Never delete or rewrite an applied migration.
- Parameterized EF queries only; never string-concatenate SQL/paths with user input. Use `AsNoTracking()` for reads.
- Never commit secrets. Connection strings and SMTP credentials: `appsettings.json` placeholders / env vars (`ConnectionStrings__Default`, `Smtp__*`) / user-secrets. `appsettings.Development.json` is git-ignored.
- User uploads live under `wwwroot/uploads/` and are runtime data — do not commit or delete them.

## Validation, auth & authorization
- Server-side validation is mandatory; client validation is a convenience. Add DataAnnotations on view models and validate uploads in the service (see `AccountIdPictureService`).
- Every page requires auth by default. Mark exceptions `[AllowAnonymous]`. Restrict by role with `[Authorize(Roles = Roles.Admin)]` etc.
- Enforce ownership/course scope server-side (`AllowedSportIdsAsync`, `CurrentUserCourseAsync`, `MatchesCourse`), not just in the UI.
- Hash passwords/codes with `PasswordService`; never store or log plaintext credentials or OTPs.

## Verify (targeted only)
- Compile: `dotnet build TmcTryoutSystem.csproj --nologo --verbosity quiet` (stop the running app first if the output file is locked).
- The project has no test project; verify logic by build + reasoning, or `dotnet ef migrations list` for schema.
- Do not modify app code solely to make a check pass. If blocked (e.g. locked `.exe`), report it instead of killing processes.

## Final report (concise)
- Changed files (with `path:line` for key spots).
- Verification command(s) run and exact result.
- Any unresolved issue or limitation, stated plainly.
