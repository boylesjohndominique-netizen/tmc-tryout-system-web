using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class AppUser
{
    public int Id { get; set; }

    /// <summary>
    /// Staff sign in with a short username. Students have no username of their own, so their email
    /// doubles as this login key — hence the column is wide enough to hold a full email address.
    /// </summary>
    [Required, StringLength(120)]
    public string Username { get; set; } = string.Empty;

    /// <summary>Wide enough for a first, middle and last name of up to 60 characters each.</summary>
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    // ---------- Structured name (students; staff may leave blank) ----------
    [StringLength(60)]
    public string? FirstName { get; set; }

    [StringLength(60)]
    public string? MiddleName { get; set; }

    [StringLength(60)]
    public string? LastName { get; set; }

    /// <summary>TMC student number in the 00-000000 format. Unique across student accounts.</summary>
    [StringLength(20)]
    public string? StudentId { get; set; }

    /// <summary>
    /// Program the student is enrolled in, stored as the full label
    /// (e.g. "BSIT (Bachelor of Science in Information Technology)"). Captured at sign-up.
    /// </summary>
    [StringLength(80)]
    public string? CourseOrGrade { get; set; }

    /// <summary>Year level, 1st to 4th year. Captured at sign-up.</summary>
    [StringLength(20)]
    public string? YearLevel { get; set; }

    [StringLength(120)]
    public string? Email { get; set; }

    // ---------- Email (OTP) verification ----------
    [Display(Name = "Email verified")]
    public bool EmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>PBKDF2 hash of the current one-time code. Never stored in plain text.</summary>
    [StringLength(200)]
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public DateTime? OtpSentAt { get; set; }
    public int OtpAttempts { get; set; }

    // ---------- Student ID pictures + admin verification ----------
    [StringLength(260)]
    public string? IdPictureFrontPath { get; set; }

    [StringLength(260)]
    public string? IdPictureBackPath { get; set; }

    [Display(Name = "ID verified")]
    public bool IdVerified { get; set; }

    public int? VerifiedById { get; set; }
    public DateTime? IdVerifiedAt { get; set; }

    [StringLength(300)]
    public string? VerificationRemarks { get; set; }

    // ---------- Profile picture ----------
    /// <summary>Web path to the user's optional profile photo, shown in the app shell and on the profile page.</summary>
    [StringLength(260)]
    public string? ProfilePicturePath { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; set; } = Roles.Student;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<SportAssignment> SportAssignments { get; set; } = new List<SportAssignment>();
    public ICollection<Applicant> Registrations { get; set; } = new List<Applicant>();

    /// <summary>True once the email OTP and the admin ID check have both passed.</summary>
    public bool IsFullyVerified => EmailVerified && IdVerified;

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;

    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            return parts.Length == 1
                ? char.ToUpperInvariant(parts[0][0]).ToString()
                : $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }
    }
}
