using System.ComponentModel.DataAnnotations;
using TmcTryoutSystem.Models;

namespace TmcTryoutSystem.ViewModels;

/// <summary>The student's notification list, optionally filtered to unread items.</summary>
public class NotificationListViewModel
{
    public List<Notification> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public bool UnreadOnly { get; set; }
}

/// <summary>Form staff use to send a custom notification to students.</summary>
public class ComposeNotificationViewModel
{
    [Required(ErrorMessage = "Enter a title."), StringLength(120)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a message."), StringLength(500)]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Icon")]
    public string Icon { get; set; } = "bell-fill";

    /// <summary>One of: all, course, sport, student.</summary>
    [Required]
    [Display(Name = "Send to")]
    public string Audience { get; set; } = "all";

    [Display(Name = "Course")]
    public string? CourseOrGrade { get; set; }

    [Display(Name = "Sport")]
    public int? SportId { get; set; }

    [Display(Name = "Student")]
    public int? TargetUserId { get; set; }

    [StringLength(260)]
    [Display(Name = "Link (optional)")]
    public string? Url { get; set; }

    // Dropdown data
    public List<Sport> Sports { get; set; } = new();
    public List<StudentOption> Students { get; set; } = new();
    public List<string> Courses { get; set; } = new();
}

public class StudentOption
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? StudentId { get; set; }
}
