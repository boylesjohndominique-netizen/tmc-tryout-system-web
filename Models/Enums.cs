using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public enum SelectionStatus
{
    [Display(Name = "Pending")] Pending = 0,
    [Display(Name = "Selected")] Selected = 1,
    [Display(Name = "Not Selected")] NotSelected = 2,
    [Display(Name = "For Further Evaluation")] ForFurtherEvaluation = 3
}

public enum ScheduleStatus
{
    Upcoming,
    Ongoing,
    Completed,
    Cancelled
}
