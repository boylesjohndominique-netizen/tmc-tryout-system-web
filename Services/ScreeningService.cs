using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.Services;

/// <summary>An applicant's average for one criterion, across every evaluator.</summary>
public class CriterionAverage
{
    public int CriterionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "star";
    public int DisplayOrder { get; set; }
    public double Value { get; set; }
}

public class ScreeningRow
{
    public Applicant Applicant { get; set; } = null!;

    /// <summary>Average per criterion of the applicant's sport category; criteria may differ by sport.</summary>
    public List<CriterionAverage> Criteria { get; set; } = new();

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
    /// Applicants must be loaded with Sport, Evaluations, their Scores and each score's Criterion.
    /// </summary>
    public static List<ScreeningRow> Build(IEnumerable<Applicant> applicants)
    {
        var rows = new List<ScreeningRow>();

        foreach (var a in applicants)
        {
            if (a.Evaluations == null || a.Evaluations.Count == 0) continue;
            var ev = a.Evaluations;

            var criteria = ev
                .SelectMany(e => e.Scores)
                .GroupBy(s => new
                {
                    s.EvaluationCriterionId,
                    Name = s.Criterion?.Name ?? "Criterion",
                    Icon = s.Criterion?.Icon ?? "star",
                    Order = s.Criterion?.DisplayOrder ?? 0
                })
                .Select(g => new CriterionAverage
                {
                    CriterionId = g.Key.EvaluationCriterionId,
                    Name = g.Key.Name,
                    Icon = g.Key.Icon,
                    DisplayOrder = g.Key.Order,
                    Value = Math.Round(g.Average(s => (double)s.Value), 1)
                })
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .ToList();

            rows.Add(new ScreeningRow
            {
                Applicant = a,
                Criteria = criteria,
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
