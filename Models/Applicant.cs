using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class Applicant
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(30)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>Stored wwwroot-relative path of the uploaded front-of-ID photo, used to validate the student.</summary>
    [StringLength(260)]
    public string? IdPictureFrontPath { get; set; }

    /// <summary>Stored wwwroot-relative path of the uploaded back-of-ID photo, used to validate the student.</summary>
    [StringLength(260)]
    public string? IdPictureBackPath { get; set; }

    [Display(Name = "ID verified")]
    public bool IdVerified { get; set; }

    public int? VerifiedById { get; set; }
    public AppUser? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }

    [StringLength(300)]
    public string? VerificationRemarks { get; set; }

    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(60)]
    public string? MiddleName { get; set; }

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Gender { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    [Required, StringLength(20)]
    public string CourseOrGrade { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string YearLevel { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Email { get; set; }

    [Required, StringLength(30)]
    public string ContactNumber { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    public double? HeightCm { get; set; }
    public double? WeightKg { get; set; }

    [StringLength(100)]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    public string? EmergencyContactNumber { get; set; }

    [StringLength(500)]
    public string? MedicalNotes { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public int? TryoutScheduleId { get; set; }
    public TryoutSchedule? Schedule { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.Now;

    public SelectionStatus Status { get; set; } = SelectionStatus.Pending;

    [StringLength(500)]
    public string? StatusRemarks { get; set; }

    public int? DecidedById { get; set; }
    public DateTime? DecidedAt { get; set; }

    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();

    public string FullName =>
        string.IsNullOrWhiteSpace(MiddleName)
            ? $"{LastName}, {FirstName}"
            : $"{LastName}, {FirstName} {MiddleName}";

    public string DisplayName => $"{FirstName} {LastName}";

    public string Initials =>
        $"{(FirstName.Length > 0 ? FirstName[0] : '?')}{(LastName.Length > 0 ? LastName[0] : '?')}".ToUpperInvariant();

    public int EvaluationCount => Evaluations?.Count ?? 0;

    /// <summary>Average of every evaluator's overall score, or null when nobody has evaluated yet.</summary>
    public double? OverallScore =>
        Evaluations != null && Evaluations.Count > 0
            ? Math.Round(Evaluations.Average(e => e.Average), 2)
            : null;

    public bool IsQualified =>
        OverallScore.HasValue && Sport != null && OverallScore.Value >= Sport.QualifyingScore;
}
