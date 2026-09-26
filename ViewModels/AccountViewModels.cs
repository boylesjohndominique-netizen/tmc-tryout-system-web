using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a username."), StringLength(30, MinimumLength = 4, ErrorMessage = "The username needs 4 to 30 characters.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Create a password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
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

    [Required(ErrorMessage = "Enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(120)]
    public string? Email { get; set; }
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
