using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class Sport
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the sport name."), StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(40)]
    public string Icon { get; set; } = "trophy";

    [Display(Name = "Qualifying score")]
    [Range(0, 100, ErrorMessage = "The qualifying score must be between 0 and 100.")]
    public int QualifyingScore { get; set; } = 75;

    [Display(Name = "Roster slots")]
    [Range(1, 500, ErrorMessage = "Roster slots must be between 1 and 500.")]
    public int? Slots { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SportAssignment> Staff { get; set; } = new List<SportAssignment>();
    public ICollection<TryoutSchedule> Schedules { get; set; } = new List<TryoutSchedule>();
    public ICollection<Applicant> Applicants { get; set; } = new List<Applicant>();
}

/// <summary>Links a coach or evaluator to a sport.</summary>
public class SportAssignment
{
    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }
}
