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

    /// <summary>
    /// Program this tryout is held for, stored as the full course label
    /// (e.g. "BSIT (Bachelor of Science in Information Technology)"). A student may only
    /// register for schedules of their own course. Null means the tryout is open to every course.
    /// </summary>
    [StringLength(80)]
    [Display(Name = "Course")]
    public string? CourseOrGrade { get; set; }

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
