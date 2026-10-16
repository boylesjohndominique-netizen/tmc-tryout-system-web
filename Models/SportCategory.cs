using System.ComponentModel.DataAnnotations;

namespace TmcTryoutSystem.Models;

/// <summary>
/// Groups sports that share the same way of evaluating performance (Physical, Mind,
/// Esports, ...). Each category owns its own set of evaluation criteria, and every sport
/// belongs to one category.
/// </summary>
public class SportCategory
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the category name."), StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    [StringLength(40)]
    public string Icon { get; set; } = "collection";

    [Display(Name = "Display order")]
    [Range(0, 999)]
    public int DisplayOrder { get; set; }

    public ICollection<EvaluationCriterion> Criteria { get; set; } = new List<EvaluationCriterion>();
    public ICollection<Sport> Sports { get; set; } = new List<Sport>();
}

/// <summary>A single performance criterion that belongs to a sport category.</summary>
public class EvaluationCriterion
{
    public int Id { get; set; }

    public int SportCategoryId { get; set; }
    public SportCategory? Category { get; set; }

    [Required(ErrorMessage = "Enter the criterion name."), StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string Icon { get; set; } = "star";

    [Display(Name = "Display order")]
    [Range(0, 999)]
    public int DisplayOrder { get; set; }
}
