using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

/// <summary>
/// A single capability granted to a staff account by an administrator. Administrators are
/// superadmins and never need rows here; students are managed by their own workflow.
/// </summary>
public class UserPermission
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>One of the keys in <see cref="Permissions"/>.</summary>
    [Required, StringLength(64)]
    public string Permission { get; set; } = string.Empty;
}
