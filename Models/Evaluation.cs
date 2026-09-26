using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class Evaluation
{
    public int Id { get; set; }

    public int ApplicantId { get; set; }
    public Applicant? Applicant { get; set; }

    public int EvaluatorId { get; set; }
    public AppUser? Evaluator { get; set; }

    [Range(0, 100)] public int Skill { get; set; }
    [Range(0, 100)] public int Speed { get; set; }
    [Range(0, 100)] public int Agility { get; set; }
    [Range(0, 100)] public int Endurance { get; set; }
    [Range(0, 100)] public int Teamwork { get; set; }
    [Range(0, 100)] public int Discipline { get; set; }

    [Display(Name = "Overall performance")]
    [Range(0, 100)] public int OverallPerformance { get; set; }

    [StringLength(600)]
    public string? Comments { get; set; }

    public DateTime EvaluatedAt { get; set; } = DateTime.Now;

    /// <summary>The evaluator's overall score: the mean of all seven criteria.</summary>
    public double Average =>
        Math.Round((Skill + Speed + Agility + Endurance + Teamwork + Discipline + OverallPerformance) / 7.0, 2);
}
