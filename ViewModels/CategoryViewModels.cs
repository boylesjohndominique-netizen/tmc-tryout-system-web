using System.ComponentModel.DataAnnotations;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.ViewModels;

public class CategoryListViewModel
{
    public List<CategoryRow> Categories { get; set; } = new();
}

public class CategoryRow
{
    public SportCategory Category { get; set; } = null!;
    public int SportCount { get; set; }
}

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the category name."), StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public string Icon { get; set; } = "collection";

    [Display(Name = "Display order")]
    [Range(0, 999)]
    public int DisplayOrder { get; set; }

    public List<CriterionFormItem> Criteria { get; set; } = new();

    /// <summary>How many sports already use this category; shown on the edit page.</summary>
    public int SportCount { get; set; }
}

public class CriterionFormItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the criterion name."), StringLength(60)]
    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = "star";

    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }
}
