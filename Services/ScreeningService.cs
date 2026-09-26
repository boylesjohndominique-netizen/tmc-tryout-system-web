using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Services;

public class ScreeningRow
{
    public Applicant Applicant { get; set; } = null!;
    public double Skill { get; set; }
    public double Speed { get; set; }
    public double Agility { get; set; }
    public double Endurance { get; set; }
    public double Teamwork { get; set; }
    public double Discipline { get; set; }
    public double OverallPerformance { get; set; }
    public double Overall { get; set; }
    public int EvaluatorCount { get; set; }
    public int Rank { get; set; }
    public int Required { get; set; }
    public bool Qualified => Overall >= Required;
}

public static class ScreeningService
{
    /// <summary>
    /// Builds ranked rows for every applicant that has at least one evaluation.
    /// Applicants are ranked inside their own sport; ties share the same rank.
    /// Applicants must be loaded with Sport and Evaluations.
    /// </summary>
    public static List<ScreeningRow> Build(IEnumerable<Applicant> applicants)
    {
        var rows = new List<ScreeningRow>();

        foreach (var a in applicants)
        {
            if (a.Evaluations == null || a.Evaluations.Count == 0) continue;
            var ev = a.Evaluations;
            rows.Add(new ScreeningRow
            {
                Applicant = a,
                Skill = Math.Round(ev.Average(e => (double)e.Skill), 1),
                Speed = Math.Round(ev.Average(e => (double)e.Speed), 1),
                Agility = Math.Round(ev.Average(e => (double)e.Agility), 1),
                Endurance = Math.Round(ev.Average(e => (double)e.Endurance), 1),
                Teamwork = Math.Round(ev.Average(e => (double)e.Teamwork), 1),
                Discipline = Math.Round(ev.Average(e => (double)e.Discipline), 1),
                OverallPerformance = Math.Round(ev.Average(e => (double)e.OverallPerformance), 1),
                Overall = Math.Round(ev.Average(e => e.Average), 2),
                EvaluatorCount = ev.Count,
                Required = a.Sport?.QualifyingScore ?? 0
            });
        }

        foreach (var group in rows.GroupBy(r => r.Applicant.SportId))
        {
            var ordered = group.OrderByDescending(r => r.Overall)
                               .ThenBy(r => r.Applicant.LastName)
                               .ThenBy(r => r.Applicant.FirstName)
                               .ToList();
            int position = 0;
            int currentRank = 0;
            double? previous = null;
            foreach (var r in ordered)
            {
                position++;
                if (previous == null || r.Overall != previous.Value) currentRank = position;
                r.Rank = currentRank;
                previous = r.Overall;
            }
        }

        return rows
            .OrderBy(r => r.Applicant.Sport?.Name)
            .ThenBy(r => r.Rank)
            .ThenBy(r => r.Applicant.LastName)
            .ToList();
    }
}
