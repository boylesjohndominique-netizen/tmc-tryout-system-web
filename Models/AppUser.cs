using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; set; } = Roles.Student;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<SportAssignment> SportAssignments { get; set; } = new List<SportAssignment>();
    public ICollection<Applicant> Registrations { get; set; } = new List<Applicant>();
}
