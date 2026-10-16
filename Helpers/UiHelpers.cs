using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Helpers;

public static class UiHelpers
{
    public static readonly string[] SportIcons =
    {
        "trophy", "dribbble", "people", "stopwatch", "lightning-charge", "water",
        "bullseye", "bicycle", "activity", "award", "flag", "star"
    };

    /// <summary>Icons the administrator can pick for a sport category.</summary>
    public static readonly string[] CategoryIcons =
    {
        "collection", "activity", "people", "lightning-charge", "trophy", "cpu",
        "controller", "diagram-3", "globe", "star", "award", "flag"
    };

    /// <summary>Icons the administrator can pick for an evaluation criterion.</summary>
    public static readonly string[] CriterionIcons =
    {
        "star", "bullseye", "lightning-charge", "arrow-repeat", "activity", "people",
        "shield-check", "clock", "diagram-3", "lightbulb", "map", "chat-dots", "eye",
        "heart-pulse", "gem", "puzzle", "book", "calculator", "translate", "trophy",
        "stopwatch", "speedometer2", "wind", "fire", "crosshair", "hand-index",
        "graph-up", "clipboard-check", "joystick"
    };

    /// <summary>The year levels Trinidad Municipal College offers: college only, 1st to 4th year.</summary>
    public static readonly string[] YearLevels =
    {
        "1st Year", "2nd Year", "3rd Year", "4th Year"
    };

    /// <summary>
    /// The degree programs TMC offers, shown as "acronym (full program name)" so students see the
    /// program they are enrolling in without losing the shorthand they know. Students must pick one.
    /// The label is also the value that gets stored, so keep the wording stable once it is in use.
    /// </summary>
    public static readonly string[] Courses =
    {
        "BSIT (Bachelor of Science in Information Technology)",
        "BSOA (Bachelor of Science in Office Administration)",
        "BSED (Bachelor of Secondary Education)",
        "BSPolSci (Bachelor of Science in Political Science)",
        "BSCrim (Bachelor of Science in Criminology)"
    };

    /// <summary>Course names shown as suggestions. Same labels as <see cref="Courses"/>.</summary>
    public static readonly string[] CourseSuggestions =
    {
        "BSIT (Bachelor of Science in Information Technology)",
        "BSOA (Bachelor of Science in Office Administration)",
        "BSED (Bachelor of Secondary Education)",
        "BSPolSci (Bachelor of Science in Political Science)",
        "BSCrim (Bachelor of Science in Criminology)"
    };

    /// <summary>The short acronym of a stored course label, e.g. "BSIT". Blank becomes "All courses".</summary>
    public static string CourseShort(string? course)
    {
        if (string.IsNullOrWhiteSpace(course)) return "All courses";
        var idx = course.IndexOf(" (", StringComparison.Ordinal);
        return idx > 0 ? course[..idx] : course;
    }

    /// <summary>
    /// TMC student number format, e.g. 24-000123: two-digit year, a dash, then six digits.
    /// </summary>
    public const string StudentIdPattern = @"^\d{2}-\d{6}$";

    public const string StudentIdExample = "24-000123";

    public static string StudentIdHint =>
        $"Format: {StudentIdExample} — two-digit year, a dash, then six digits.";

    /// <summary>Trims and upper-cases a student ID so lookups and storage always match.</summary>
    public static string NormalizeStudentId(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

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
