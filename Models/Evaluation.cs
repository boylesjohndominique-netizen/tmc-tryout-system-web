using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class Evaluation
{
    public int Id { get; set; }

    public int ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    public int EvaluatorId { get; set; }
    public AppUser? Evaluator { get; set; }

    [StringLength(600)]
    public string? Comments { get; set; }

    public DateTime EvaluatedAt { get; set; } = DateTime.Now;

    /// <summary>One row per criterion of the applicant's sport category. The evaluator's score is the mean.</summary>
    public ICollection<EvaluationScore> Scores { get; set; } = new List<EvaluationScore>();

    public double Average =>
        Scores.Count == 0 ? 0 : Math.Round(Scores.Average(s => (double)s.Value), 2);
}

/// <summary>A score an evaluator gave for one criterion on one evaluation.</summary>
public class EvaluationScore
{
    public int Id { get; set; }

    public int EvaluationId { get; set; }
    public Evaluation? Evaluation { get; set; }

    public int EvaluationCriterionId { get; set; }
    public EvaluationCriterion? Criterion { get; set; }

    [Range(0, 100)]
    public int Value { get; set; }
}
