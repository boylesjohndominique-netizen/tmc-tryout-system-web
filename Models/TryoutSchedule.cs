using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class TryoutSchedule
{
    public int Id { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    [Required, StringLength(150)]
    public string Venue { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public ICollection<Applicant> Applicants { get; set; } = new List<Applicant>();

    public ScheduleStatus Status
    {
        get
        {
            if (IsCancelled) return ScheduleStatus.Cancelled;
            var now = DateTime.Now;
            if (EndAt < now) return ScheduleStatus.Completed;
            if (StartAt <= now) return ScheduleStatus.Ongoing;
            return ScheduleStatus.Upcoming;
        }
    }

    public string Label => $"{Title} - {StartAt:MMM d, yyyy} {StartAt:h:mm tt}";
}
