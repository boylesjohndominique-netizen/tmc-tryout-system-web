using System.ComponentModel.DataAnnotations;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.ViewModels;

public class UserListViewModel
{
    public List<AppUser> Users { get; set; } = new();
    public string? Search { get; set; }
    public string? Role { get; set; }
}

public class UserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter a username."), StringLength(30, MinimumLength = 4, ErrorMessage = "The username needs 4 to 30 characters.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(120)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Choose a role.")]
    public string Role { get; set; } = Roles.Coach;

    public bool IsActive { get; set; } = true;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }
}

public class ResetPasswordViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class SportFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the sport name."), StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string Icon { get; set; } = "trophy";

    [Display(Name = "Qualifying score")]
    [Range(0, 100, ErrorMessage = "The qualifying score must be between 0 and 100.")]
    public int QualifyingScore { get; set; } = 75;

    [Display(Name = "Roster slots")]
    [Range(1, 500, ErrorMessage = "Roster slots must be between 1 and 500.")]
    public int? Slots { get; set; }

    public bool IsActive { get; set; } = true;

    public List<int> StaffIds { get; set; } = new();
    public List<AppUser> AvailableStaff { get; set; } = new();
}

public class SportDetailsViewModel
{
    public Sport Sport { get; set; } = null!;
    public List<AppUser> Staff { get; set; } = new();
    public List<TryoutSchedule> Schedules { get; set; } = new();
    public int ApplicantCount { get; set; }
    public int SelectedCount { get; set; }
}

public class ScheduleListViewModel
{
    public List<TryoutSchedule> Schedules { get; set; } = new();
    public List<Sport> Sports { get; set; } = new();
    public Dictionary<int, int> RegisteredCounts { get; set; } = new();
    public string View { get; set; } = "upcoming";
    public int? SportId { get; set; }
    public int UpcomingCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int AllCount { get; set; }
}

public class ScheduleFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Choose a sport.")]
    [Display(Name = "Sport")]
    public int? SportId { get; set; }

    [Required(ErrorMessage = "Enter a title for the tryout."), StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the tryout date.")]
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Set the start time.")]
    [Display(Name = "Start time")]
    public TimeSpan? StartTime { get; set; }

    [Required(ErrorMessage = "Set the end time.")]
    [Display(Name = "End time")]
    public TimeSpan? EndTime { get; set; }

    [Required(ErrorMessage = "Enter the venue."), StringLength(150)]
    public string Venue { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public List<Sport> Sports { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime.HasValue && EndTime.HasValue && EndTime.Value <= StartTime.Value)
            yield return new ValidationResult("The end time must be after the start time.", new[] { nameof(EndTime) });
    }
}

public class SportListViewModel
{
    public List<Sport> Sports { get; set; } = new();
    public Dictionary<int, int> ApplicantCounts { get; set; } = new();
    public Dictionary<int, int> ScheduleCounts { get; set; } = new();
}
