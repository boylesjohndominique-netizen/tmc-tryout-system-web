using System.ComponentModel.DataAnnotations;
using TmcTryoutSystem.Helpers;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.Services;

namespace TmcTryoutSystem.ViewModels;

public class ApplicantFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the student ID.")]
    [StringLength(20, MinimumLength = 20, ErrorMessage = "The student ID must follow the format 24-020298(00-000000).")]
    [RegularExpression(UiHelpers.StudentIdPattern, ErrorMessage = "Use the format 24-020298(00-000000): two digits, a dash, six digits, then the campus code in parentheses.")]
    [Display(Name = "Student ID")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the first name."), StringLength(60)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Enter the last name."), StringLength(60)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a gender.")]
    public string Gender { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the birth date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Birth date")]
    public DateTime? BirthDate { get; set; }

    [Required(ErrorMessage = "Select your course.")]
    [Display(Name = "Course")]
    public string CourseOrGrade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select the year level.")]
    [Display(Name = "Year level")]
    public string YearLevel { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(120)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter a contact number."), StringLength(30)]
    [Display(Name = "Contact number")]
    public string ContactNumber { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm.")]
    [Display(Name = "Height (cm)")]
    public double? HeightCm { get; set; }

    [Range(30, 250, ErrorMessage = "Weight must be between 30 and 250 kg.")]
    [Display(Name = "Weight (kg)")]
    public double? WeightKg { get; set; }

    [StringLength(100)]
    [Display(Name = "Emergency contact name")]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    [Display(Name = "Emergency contact number")]
    public string? EmergencyContactNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "Medical notes")]
    public string? MedicalNotes { get; set; }

    [Required(ErrorMessage = "Select the sport you want to try out for.")]
    [Display(Name = "Sport to try out for")]
    public int? SportId { get; set; }

    // ----- ID pictures for student validation -----
    [Display(Name = "ID picture (front)")]
    public IFormFile? IdPictureFront { get; set; }

    [Display(Name = "ID picture (back)")]
    public IFormFile? IdPictureBack { get; set; }

    /// <summary>Paths of already-stored ID photos, so edits do not require re-uploading.</summary>
    public string? ExistingFrontPath { get; set; }
    public string? ExistingBackPath { get; set; }

    [Display(Name = "Tryout schedule")]
    public int? TryoutScheduleId { get; set; }

    // Dropdown data
    public List<Sport> Sports { get; set; } = new();
    public List<TryoutSchedule> Schedules { get; set; } = new();

    /// <summary>True when staff are filling the form on behalf of a student.</summary>
    public bool ByStaff { get; set; }
}

public class ApplicantListViewModel
{
    public List<Applicant> Applicants { get; set; } = new();
    public List<Sport> Sports { get; set; } = new();
    public List<TryoutSchedule> Schedules { get; set; } = new();

    public string? Search { get; set; }
    public int? SportId { get; set; }
    public int? ScheduleId { get; set; }
    public string? Result { get; set; }
    public string? Status { get; set; }
    public string? Verification { get; set; }

    public int TotalBeforeFilter { get; set; }
}

public class ApplicantDetailsViewModel
{
    public Applicant Applicant { get; set; } = null!;
    public List<Evaluation> Evaluations { get; set; } = new();
    public int? MyEvaluationId { get; set; }
    public bool CanDecide { get; set; }
    public bool CanEvaluate { get; set; }
    public bool CanManage { get; set; }
}

public class EvaluationListViewModel
{
    public List<Applicant> Applicants { get; set; } = new();
    public List<Sport> Sports { get; set; } = new();
    public HashSet<int> EvaluatedByMe { get; set; } = new();
    public string? Search { get; set; }
    public int? SportId { get; set; }
    public string? Show { get; set; }
}

public class EvaluationFormViewModel
{
    public int ApplicantId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string SportName { get; set; } = string.Empty;
    public string SportIcon { get; set; } = "trophy";
    public int QualifyingScore { get; set; }
    public bool IsEdit { get; set; }

    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Skill { get; set; } = 70;
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Speed { get; set; } = 70;
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Agility { get; set; } = 70;
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Endurance { get; set; } = 70;
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Teamwork { get; set; } = 70;
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int Discipline { get; set; } = 70;

    [Display(Name = "Overall performance")]
    [Range(0, 100, ErrorMessage = "Enter a score from 0 to 100.")] public int OverallPerformance { get; set; } = 70;

    [StringLength(600)]
    public string? Comments { get; set; }
}

public class ScreeningViewModel
{
    public List<ScreeningRow> Rows { get; set; } = new();
    public List<Sport> Sports { get; set; } = new();
    public int? SportId { get; set; }
    public string? Filter { get; set; }
    public string? Search { get; set; }
    public int NotEvaluated { get; set; }
    public int QualifiedCount { get; set; }
}

public class CompareViewModel
{
    public List<ScreeningRow> Rows { get; set; } = new();
}

public class SelectionListViewModel
{
    public List<ScreeningRowLite> Items { get; set; } = new();
    public List<Sport> Sports { get; set; } = new();
    public string? Search { get; set; }
    public int? SportId { get; set; }
    public string? Status { get; set; }
    public Dictionary<int, int> SelectedPerSport { get; set; } = new();
}

public class ScreeningRowLite
{
    public Applicant Applicant { get; set; } = null!;
    public double? Score { get; set; }
    public int? Rank { get; set; }
    public bool Qualified { get; set; }
}

public class ReportViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();
    public int? SportId { get; set; }
    public string SportName { get; set; } = "All sports";
    public List<Sport> Sports { get; set; } = new();
    public bool SupportsSportFilter { get; set; } = true;
}

public class DashboardViewModel
{
    public int TotalApplicants { get; set; }
    public int SportCount { get; set; }
    public int UpcomingTryouts { get; set; }
    public int AwaitingEvaluation { get; set; }
    public int Selected { get; set; }
    public int Qualified { get; set; }
    public int UserCount { get; set; }
    public List<(string Name, string Icon, int Count)> PerSport { get; set; } = new();
    public List<TryoutSchedule> Upcoming { get; set; } = new();
    public List<Applicant> Recent { get; set; } = new();
    public Dictionary<int, int> RegisteredCounts { get; set; } = new();
}

public class StudentDashboardViewModel
{
    /// <summary>Kept for compatibility with older views; the first tryout entry, if any.</summary>
    public Applicant? Applicant { get; set; }
    public List<TryoutSchedule> Schedules { get; set; } = new();

    /// <summary>Every tryout the student signed up for, one per sport.</summary>
    public List<Applicant> TryoutEntries { get; set; } = new();

    public Dictionary<int, List<TryoutSchedule>> SchedulesPerEntry { get; set; } = new();
}

/// <summary>A student's registration overview: profile fields (shared) plus one entry per sport.</summary>
public class StudentRegistrationsViewModel
{
    public List<Applicant> Entries { get; set; } = new();
    public List<Sport> OpenSports { get; set; } = new();
    public Dictionary<int, List<TryoutSchedule>> SchedulesPerEntry { get; set; } = new();
}

public class ExploreSportsViewModel
{
    public List<ExploreSportCard> Sports { get; set; } = new();
    public HashSet<int> MyTryoutSportIds { get; set; } = new();
}

public class ExploreSportCard
{
    public Sport Sport { get; set; } = null!;
    public int TryoutCount { get; set; }
    public int PlayerCount { get; set; }
    public int NextScheduleId { get; set; }
}

public class ExploreSportDetailsViewModel
{
    public Sport Sport { get; set; } = null!;
    public List<AppUser> Coaches { get; set; } = new();
    public List<AppUser> Evaluators { get; set; } = new();
    public List<PlayerCard> CurrentPlayers { get; set; } = new();
    public List<PlayerCard> RecentPlayers { get; set; } = new();
    public List<TryoutSchedule> UpcomingSchedules { get; set; } = new();
    public List<TryoutSchedule> PastSchedules { get; set; } = new();
    public bool AlreadyTryingOut { get; set; }
}

public class PlayerCard
{
    public Applicant Applicant { get; set; } = null!;
    public double? Score { get; set; }
    public string Status { get; set; } = string.Empty;
}
