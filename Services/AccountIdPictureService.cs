using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TmcTryoutSystem.Helpers;

namespace TmcTryoutSystem.Services;

/// <summary>Stores and validates the front/back student-ID photos uploaded during sign-up.</summary>
public class AccountIdPictureService
{
    private readonly IWebHostEnvironment _env;

    public const int MaxBytes = 5 * 1024 * 1024; // 5 MB
    private const int MinShortSide = 320;
    private const int MinLongSide = 480;

    private static readonly string[] AllowedTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public AccountIdPictureService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public bool IsSupportedContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType) &&
        AllowedTypes.Any(t => string.Equals(t, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a model error when the picture is missing, too large, the wrong type or too low resolution.</summary>
    public void Validate(IFormFile? file, string field, string label, ModelStateDictionary state)
    {
        if (file == null || file.Length == 0)
        {
            state.AddModelError(field, $"Upload a clear photo of the {label} of your student ID.");
            return;
        }

        if (file.Length > MaxBytes)
        {
            state.AddModelError(field, $"The {label} photo must be 5 MB or smaller.");
            return;
        }

        if (!IsSupportedContentType(file.ContentType))
        {
            state.AddModelError(field, $"The {label} photo must be a JPG, PNG, WEBP or GIF image.");
            return;
        }

        if (!HasEnoughDetail(file, out var width, out var height))
        {
            state.AddModelError(field,
                width == 0
                    ? $"We could not read the {label} photo. Please pick a JPG or PNG straight from your camera or gallery."
                    : $"The {label} photo is only {width}x{height}px and would be too blurry to read. Take a closer, sharper photo of at least {MinShortSide}x{MinLongSide}px.");
        }
    }

    private static bool HasEnoughDetail(IFormFile file, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var stream = file.OpenReadStream();
            if (!ImageProbe.TryGetDimensions(stream, out width, out height)) return false;
        }
        catch (Exception)
        {
            return false;
        }

        int shortSide = Math.Min(width, height);
        int longSide = Math.Max(width, height);
        return shortSide >= MinShortSide && longSide >= MinLongSide;
    }

    /// <summary>Saves the picture under wwwroot/uploads/account-id and returns its web path.</summary>
    public async Task<string> SaveAsync(IFormFile file, string studentId, string side)
    {
        var folder = Path.Combine(_env.WebRootPath, "uploads", "account-id");
        Directory.CreateDirectory(folder);

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) ext = ".jpg";

        string safeId = new string(studentId.Where(char.IsLetterOrDigit).ToArray());
        string fileName = $"{(safeId.Length == 0 ? "student" : safeId)}-{side}-{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
        string fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/account-id/{fileName}";
    }

    public void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }
}
