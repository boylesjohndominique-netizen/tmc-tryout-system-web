using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TmcTryoutSystem.Helpers;

namespace TmcTryoutSystem.Services;

/// <summary>
/// Stores and validates the optional profile photo any signed-in user (students and staff) may add.
/// Kept separate from the student-ID pictures: profile photos are cosmetic and never reviewed.
/// </summary>
public class ProfilePictureService
{
    private readonly IWebHostEnvironment _env;

    public const int MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly string[] AllowedTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public ProfilePictureService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public bool IsSupportedContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType) &&
        AllowedTypes.Any(t => string.Equals(t, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a model error when the picture is missing, too large, the wrong type or unreadable.</summary>
    public void Validate(IFormFile? file, string field, ModelStateDictionary state)
    {
        if (file == null || file.Length == 0)
        {
            state.AddModelError(field, "Choose a picture to upload.");
            return;
        }

        if (file.Length > MaxBytes)
        {
            state.AddModelError(field, "Your profile picture must be 5 MB or smaller.");
            return;
        }

        if (!IsSupportedContentType(file.ContentType))
        {
            state.AddModelError(field, "Your profile picture must be a JPG, PNG, WEBP or GIF image.");
            return;
        }

        if (!IsReadableImage(file))
        {
            state.AddModelError(field, "We could not read that picture. Please pick a JPG, PNG, WEBP or GIF image.");
        }
    }

    private static bool IsReadableImage(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            return ImageProbe.TryGetDimensions(stream, out var width, out var height) && width > 0 && height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Saves the picture under wwwroot/uploads/profile-pictures and returns its web path.</summary>
    public async Task<string> SaveAsync(IFormFile file, int userId)
    {
        var folder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
        Directory.CreateDirectory(folder);

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) ext = ".jpg";

        string fileName = $"user-{userId}-{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
        string fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/profile-pictures/{fileName}";
    }

    public void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }
}
