using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Controllers;

[Authorize(Roles = Roles.Staff)]
public class ReportsController : AppController
{
    public static readonly (string Key, string Title, string Description, string Icon)[] Catalog =
    {
        ("registered", "Registered applicants", "Every student who registered, with course, sport and status.", "people"),
        ("per-sport", "Applicants per sport", "Registration, evaluation and selection totals for each sport.", "bar-chart-line"),
        ("schedules", "Tryout schedules", "Dates, times, venues and how many applicants signed up.", "calendar-event"),
        ("evaluations", "Athlete evaluation results", "Average score for each criterion and the overall score.", "clipboard-data"),
        ("rankings", "Applicant rankings", "Applicants ranked by overall score within their sport.", "trophy"),
        ("qualified", "Qualified students", "Applicants whose overall score meets the sport's qualifying score.", "patch-check"),
        ("selected", "Selected athletes", "Applicants marked Selected for the team.", "check-circle"),
        ("not-selected", "Non-selected athletes", "Applicants marked Not Selected.", "x-circle")
    };

    public ReportsController(AppDbContext db) : base(db) { }

    public IActionResult Index() => View();

    public async Task<IActionResult> Run(string type, int? sportId, string? format)
    {
        var entry = Catalog.FirstOrDefault(c => c.Key == type);
        if (entry.Key == null) return NotFound();

        var allowed = await AllowedSportIdsAsync();
        if (sportId.HasValue && !CanAccessSport(allowed, sportId.Value)) return Forbid();

        var sports = await Db.Sports.ScopeTo(allowed).AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        var query = Db.Applicants
            .ScopeTo(allowed)
            .Include(a => a.Sport)
            .Include(a => a.Schedule)
            .Include(a => a.Evaluations)
            .AsNoTracking();
        if (sportId.HasValue) query = query.Where(a => a.SportId == sportId.Value);
        var applicants = await query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync();

        var report = new ReportViewModel
        {
            Type = type,
            Title = entry.Title,
            Description = entry.Description,
            SportId = sportId,
            SportName = sports.FirstOrDefault(s => s.Id == sportId)?.Name ?? "All sports",
            Sports = sports
        };

        switch (type)
        {
            case "registered":
                report.Columns = new() { "Student ID", "Name", "Course", "Year level", "Sport", "Tryout", "Registered on", "Status" };
                report.Rows = applicants
                    .OrderBy(a => a.Sport?.Name).ThenBy(a => a.LastName)
                    .Select(a => new[]
                    {
                        a.StudentId, a.FullName, a.CourseOrGrade, a.YearLevel, a.Sport?.Name ?? "",
                        a.Schedule?.Title ?? "Not assigned", a.RegisteredAt.ToString("MMM d, yyyy"), a.Status.DisplayName()
                    }).ToList();
                break;

            case "per-sport":
                report.Columns = new() { "Sport", "Registered", "Evaluated", "Qualified", "Selected", "Not selected", "For further evaluation", "Pending", "Roster slots" };
                report.Rows = sports
                    .Where(s => !sportId.HasValue || s.Id == sportId.Value)
                    .Select(s =>
                    {
                        var list = applicants.Where(a => a.SportId == s.Id).ToList();
                        return new[]
                        {
                            s.Name,
                            list.Count.ToString(),
                            list.Count(a => a.EvaluationCount > 0).ToString(),
                            list.Count(a => a.IsQualified).ToString(),
                            list.Count(a => a.Status == SelectionStatus.Selected).ToString(),
                            list.Count(a => a.Status == SelectionStatus.NotSelected).ToString(),
                            list.Count(a => a.Status == SelectionStatus.ForFurtherEvaluation).ToString(),
                            list.Count(a => a.Status == SelectionStatus.Pending).ToString(),
                            s.Slots?.ToString() ?? "-"
                        };
                    }).ToList();
                break;

            case "schedules":
                {
                    var scheduleQuery = Db.Schedules.ScopeTo(allowed).Include(t => t.Sport).AsNoTracking();
                    if (sportId.HasValue) scheduleQuery = scheduleQuery.Where(t => t.SportId == sportId.Value);
                    var schedules = await scheduleQuery.OrderBy(t => t.StartAt).ToListAsync();

                    var counts = await Db.Applicants
                        .Where(a => a.TryoutScheduleId != null)
                        .GroupBy(a => a.TryoutScheduleId!.Value)
                        .Select(g => new { g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.Key, x => x.Count);

                    report.Columns = new() { "Sport", "Title", "Date", "Time", "Venue", "Registered", "Status" };
                    report.Rows = schedules.Select(t => new[]
                    {
                        t.Sport?.Name ?? "", t.Title, t.StartAt.ToString("ddd, MMM d, yyyy"),
                        $"{t.StartAt:h:mm tt} - {t.EndAt:h:mm tt}", t.Venue,
                        (counts.TryGetValue(t.Id, out var c) ? c : 0).ToString(), t.Status.ToString()
                    }).ToList();
                    break;
                }

            case "evaluations":
                report.Columns = new() { "Student ID", "Name", "Sport", "Skill", "Speed", "Agility", "Endurance", "Teamwork", "Discipline", "Overall performance", "Overall score", "Evaluators" };
                report.Rows = ScreeningService.Build(applicants)
                    .Select(r => new[]
                    {
                        r.Applicant.StudentId, r.Applicant.FullName, r.Applicant.Sport?.Name ?? "",
                        Fmt(r.Skill), Fmt(r.Speed), Fmt(r.Agility), Fmt(r.Endurance), Fmt(r.Teamwork), Fmt(r.Discipline),
                        Fmt(r.OverallPerformance), Fmt(r.Overall), r.EvaluatorCount.ToString()
                    }).ToList();
                break;

            case "rankings":
                report.Columns = new() { "Rank", "Student ID", "Name", "Sport", "Overall score", "Required", "Result", "Status" };
                report.Rows = ScreeningService.Build(applicants)
                    .Select(r => new[]
                    {
                        r.Rank.ToString(), r.Applicant.StudentId, r.Applicant.FullName, r.Applicant.Sport?.Name ?? "",
                        Fmt(r.Overall), r.Required.ToString(), r.Qualified ? "Qualified" : "Below requirement",
                        r.Applicant.Status.DisplayName()
                    }).ToList();
                break;

            case "qualified":
                report.Columns = new() { "Rank", "Student ID", "Name", "Course", "Sport", "Overall score", "Required", "Status" };
                report.Rows = ScreeningService.Build(applicants)
                    .Where(r => r.Qualified)
                    .Select(r => new[]
                    {
                        r.Rank.ToString(), r.Applicant.StudentId, r.Applicant.FullName, r.Applicant.CourseOrGrade,
                        r.Applicant.Sport?.Name ?? "", Fmt(r.Overall), r.Required.ToString(), r.Applicant.Status.DisplayName()
                    }).ToList();
                break;

            case "selected":
            case "not-selected":
                {
                    var wanted = type == "selected" ? SelectionStatus.Selected : SelectionStatus.NotSelected;
                    report.Columns = new() { "Student ID", "Name", "Course", "Sport", "Overall score", "Decided on", "Remarks" };
                    report.Rows = applicants
                        .Where(a => a.Status == wanted)
                        .OrderBy(a => a.Sport?.Name).ThenBy(a => a.LastName)
                        .Select(a => new[]
                        {
                            a.StudentId, a.FullName, a.CourseOrGrade, a.Sport?.Name ?? "",
                            a.OverallScore.HasValue ? Fmt(a.OverallScore.Value) : "-",
                            a.DecidedAt?.ToString("MMM d, yyyy") ?? "-", a.StatusRemarks ?? ""
                        }).ToList();
                    break;
                }
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = BuildCsv(report);
            var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv", $"tmc-{type}-{DateTime.Now:yyyyMMdd}.csv");
        }

        return View(report);
    }

    private static string Fmt(double value) => value.ToString("0.0#");

    private static string BuildCsv(ReportViewModel report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", report.Columns.Select(Escape)));
        foreach (var row in report.Rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        // Stop spreadsheet apps from running formulas that begin with these characters.
        if (value.Length > 1 && "=+-@".IndexOf(value[0]) >= 0 && !double.TryParse(value, out _))
            value = "'" + value;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
