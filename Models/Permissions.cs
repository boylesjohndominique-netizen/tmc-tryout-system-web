namespace TmcTryoutSystem.Models;

/// <summary>
/// Fine-grained capabilities an administrator can grant to Coach and Evaluator accounts.
/// Administrators are superadmins: they always hold every permission and cannot be restricted.
/// Each module exposes a "view" toggle and, where the module can be edited, a "manage" toggle.
/// </summary>
public static class Permissions
{
    /// <summary>Cookie claim type that carries one granted permission key.</summary>
    public const string ClaimType = "permission";

    // Applicant records
    public const string ApplicantsView = "applicants.view";
    public const string ApplicantsManage = "applicants.manage";
    // Tryout schedules
    public const string SchedulesView = "schedules.view";
    public const string SchedulesManage = "schedules.manage";
    // Evaluations
    public const string EvaluationsView = "evaluations.view";
    public const string EvaluationsManage = "evaluations.manage";
    // Screening and ranking
    public const string ScreeningView = "screening.view";
    // Selection decisions
    public const string SelectionView = "selection.view";
    public const string SelectionManage = "selection.manage";
    // Reports
    public const string ReportsView = "reports.view";
    // Sports and categories
    public const string SportsView = "sports.view";
    public const string SportsManage = "sports.manage";

    public static readonly string[] All =
    {
        ApplicantsView, ApplicantsManage,
        SchedulesView, SchedulesManage,
        EvaluationsView, EvaluationsManage,
        ScreeningView,
        SelectionView, SelectionManage,
        ReportsView,
        SportsView, SportsManage
    };

    /// <summary>A grantable module shown on the Permissions screen. ManageKey is null for view-only areas.</summary>
    public sealed record Area(string Name, string Icon, string ViewKey, string? ManageKey, string ManageLabel);

    public static readonly Area[] Catalog =
    {
        new("Applicant records", "people-fill", ApplicantsView, ApplicantsManage, "Approve student accounts and edit records"),
        new("Tryout schedules", "calendar-event-fill", SchedulesView, SchedulesManage, "Create and edit schedules"),
        new("Evaluations", "clipboard2-pulse-fill", EvaluationsView, EvaluationsManage, "Record and edit evaluations"),
        new("Screening and ranking", "bar-chart-steps", ScreeningView, null, ""),
        new("Selection decisions", "patch-check-fill", SelectionView, SelectionManage, "Record the final selected list"),
        new("Reports", "file-earmark-bar-graph-fill", ReportsView, null, ""),
        new("Sports and categories", "dribbble", SportsView, SportsManage, "Create and edit sports and categories")
    };

    /// <summary>
    /// Starting permissions for a newly created staff account. Views mirror what staff could already
    /// reach; management defaults follow the rules that were in force before permissions existed.
    /// </summary>
    public static IReadOnlyCollection<string> DefaultsFor(string role) => role switch
    {
        Roles.Coach => new[]
        {
            ApplicantsView, SchedulesView, SchedulesManage, EvaluationsView, EvaluationsManage,
            ScreeningView, SelectionView, SelectionManage, ReportsView, SportsView
        },
        Roles.Evaluator => new[]
        {
            ApplicantsView, SchedulesView, EvaluationsView, EvaluationsManage,
            ScreeningView, ReportsView, SportsView
        },
        Roles.Admin => All,
        _ => Array.Empty<string>()
    };
}
