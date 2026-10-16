using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email, student ID or username.")]
    [Display(Name = "Email, student ID or username")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>Student sign-up: name, student ID, ID pictures, email and password (no username).</summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your first name."), StringLength(60)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Enter your last name."), StringLength(60)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your student ID.")]
    [RegularExpression(Helpers.UiHelpers.StudentIdPattern, ErrorMessage = "Use the format 00-0000: two digits, a dash, then four digits (for example 24-0202).")]
    [Display(Name = "Student ID")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select your course.")]
    [Display(Name = "Course")]
    public string CourseOrGrade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select your year level.")]
    [Display(Name = "Year level")]
    public string YearLevel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Upload a photo of the front of your student ID.")]
    [Display(Name = "Student ID - front")]
    public IFormFile? IdPictureFront { get; set; }

    [Required(ErrorMessage = "Upload a photo of the back of your student ID.")]
    [Display(Name = "Student ID - back")]
    public IFormFile? IdPictureBack { get; set; }

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(120)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Create a password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string FullName =>
        string.Join(' ', new[] { FirstName, MiddleName, LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim()));
}

public class VerifyEmailViewModel
{
    [Required(ErrorMessage = "Enter the 6-digit code we emailed you.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The code is 6 digits.")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

    public string? Email { get; set; }
    public bool CanResend { get; set; } = true;
    public int SecondsToResend { get; set; }

    /// <summary>Development-only hint shown when SMTP is not configured.</summary>
    public string? DevCode { get; set; }
}

/// <summary>Account verification overview shown to a student while the admin reviews their ID.</summary>
public class AccountStatusViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public bool IdVerified { get; set; }
    public DateTime? IdVerifiedAt { get; set; }
    public string? VerificationRemarks { get; set; }
    public string? IdPictureFrontPath { get; set; }
    public string? IdPictureBackPath { get; set; }
    public bool HasIdPictures => !string.IsNullOrEmpty(IdPictureFrontPath) && !string.IsNullOrEmpty(IdPictureBackPath);
    public bool IsFullyVerified => EmailVerified && IdVerified;
}

/// <summary>Real figures from the most recent tryout, shown on the auth pages' hero panel.</summary>
public class AuthHeroStatsViewModel
{
    public string SportName { get; set; } = string.Empty;
    public string SportIcon { get; set; } = "trophy";
    public string ScheduleTitle { get; set; } = string.Empty;
    public string ScheduleDate { get; set; } = string.Empty;
    public int Applicants { get; set; }
    public int Evaluators { get; set; }
    public int Evaluations { get; set; }
    public int Selected { get; set; }
    public int Slots { get; set; }
    public int QualifyingScore { get; set; }
    public double? TopScore { get; set; }

    /// <summary>Per-criterion averages (0-100) across all evaluations of the tryout.</summary>
    public List<(string Label, double Avg)> Criteria { get; set; } = new();

    public bool HasData => Evaluations > 0;
}

public class ProfileViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your full name."), StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(120)]
    public string? Email { get; set; }

    // ---------- Student-only summary ----------
    public bool IsStudent { get; set; }
    public string? StudentId { get; set; }

    [Display(Name = "Course")]
    public string? CourseOrGrade { get; set; }

    [Display(Name = "Year level")]
    public string? YearLevel { get; set; }

    public bool EmailVerified { get; set; }
    public bool IdVerified { get; set; }
    public string? VerificationRemarks { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
