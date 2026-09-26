using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Helpers;

public static class UiHelpers
{
    public static readonly string[] SportIcons =
    {
        "trophy", "dribbble", "people", "stopwatch", "lightning-charge", "water",
        "bullseye", "bicycle", "activity", "award", "flag", "star"
    };

    /// <summary>The year levels Trinidad Municipal College offers: college only, 1st to 4th year.</summary>
    public static readonly string[] YearLevels =
    {
        "1st Year", "2nd Year", "3rd Year", "4th Year"
    };

    /// <summary>The degree programs TMC offers. Students must pick one of these.</summary>
    public static readonly string[] Courses =
    {
        "BSIT", "BSOA", "BSED", "BSPolSci", "BSCrim"
    };

    /// <summary>Course names shown as suggestions (includes common full names).</summary>
    public static readonly string[] CourseSuggestions =
    {
        "BSIT", "BSOA", "BSED", "BSPolSci", "BSCrim"
    };

    /// <summary>
    /// TMC student number format, e.g. 24-020298(00-000000): two-digit year, six digits,
    /// then the campus/program code in parentheses.
    /// </summary>
    public const string StudentIdPattern = @"^\d{2}-\d{6}\(\d{2}-\d{6}\)$";

    public const string StudentIdExample = "24-020298(00-000000)";

    public static string StudentIdHint =>
        $"Format: {StudentIdExample} — year and number, then the campus code in parentheses.";

    public static string StatusBadge(SelectionStatus s) => s switch
    {
        SelectionStatus.Selected => "badge-soft-success",
        SelectionStatus.NotSelected => "badge-soft-danger",
        SelectionStatus.ForFurtherEvaluation => "badge-soft-warning",
        _ => "badge-soft-muted"
    };

    public static string StatusIcon(SelectionStatus s) => s switch
    {
        SelectionStatus.Selected => "check-circle-fill",
        SelectionStatus.NotSelected => "x-circle-fill",
        SelectionStatus.ForFurtherEvaluation => "hourglass-split",
        _ => "clock"
    };

    public static string ScheduleBadge(ScheduleStatus s) => s switch
    {
        ScheduleStatus.Upcoming => "badge-soft-info",
        ScheduleStatus.Ongoing => "badge-soft-warning",
        ScheduleStatus.Completed => "badge-soft-success",
        _ => "badge-soft-danger"
    };

    public static string ScoreClass(double score, int required) =>
        score >= required ? "score-good" : (score >= required - 10 ? "score-mid" : "score-low");

    public static string RoleIcon(string role) => role switch
    {
        Roles.Admin => "shield-lock-fill",
        Roles.Coach => "clipboard2-pulse-fill",
        Roles.Evaluator => "clipboard-check-fill",
        _ => "mortarboard-fill"
    };
}
