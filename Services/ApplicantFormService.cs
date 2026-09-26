using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TmcTryoutSystem.Data;
using TmcTryoutSystem.Models;
using TmcTryoutSystem.ViewModels;

namespace TmcTryoutSystem.Services;

/// <summary>Shared logic for the registration form used by students and by staff.</summary>
public class ApplicantFormService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private const int MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    public ApplicantFormService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public static string NormalizeStudentId(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Loads the sport and schedule dropdown options.</summary>
    public async Task LoadOptionsAsync(ApplicantFormViewModel model)
    {
        var now = DateTime.Now;
        int currentSport = model.SportId ?? 0;
        int currentSchedule = model.TryoutScheduleId ?? 0;

        model.Sports = await _db.Sports
            .AsNoTracking()
            .Where(s => s.IsActive || s.Id == currentSport)
            .OrderBy(s => s.Name)
            .ToListAsync();

        model.Schedules = await _db.Schedules
            .AsNoTracking()
            .Where(t => (!t.IsCancelled && t.EndAt >= now) || t.Id == currentSchedule)
            .OrderBy(t => t.StartAt)
            .ToListAsync();
    }

    /// <summary>
    /// Adds model errors for invalid details, duplicate registrations (one tryout entry
    /// per student per sport) and invalid sport or schedule choices.
    /// </summary>
    public async Task ValidateAsync(
        ModelStateDictionary state,
        ApplicantFormViewModel model,
        Applicant? existing,
        int? ownerUserId)
    {
        int existingId = existing?.Id ?? 0;
        var studentId = NormalizeStudentId(model.StudentId);

        // Students can try out for several sports, so the same student ID is allowed
        // once per sport instead of once overall.
        if (studentId.Length > 0 && model.SportId.HasValue &&
            await _db.Applicants.AnyAsync(a => a.StudentId == studentId
                                               && a.SportId == model.SportId.Value
                                               && a.Id != existingId))
        {
            state.AddModelError(nameof(model.StudentId),
                "This student ID is already registered for this sport. A student gets one tryout entry per sport.");
        }

        if (ownerUserId.HasValue && model.SportId.HasValue &&
            await _db.Applicants.AnyAsync(a => a.UserId == ownerUserId.Value
                                               && a.SportId == model.SportId.Value
                                               && a.Id != existingId))
        {
            state.AddModelError(string.Empty,
                "You already registered for this sport. You can try out for several sports, but only once per sport.");
        }

        if (model.BirthDate.HasValue)
        {
            var birth = model.BirthDate.Value.Date;
            if (birth >= DateTime.Today || birth.Year < 1950)
                state.AddModelError(nameof(model.BirthDate), "Enter a valid birth date.");
        }

        if (model.SportId.HasValue)
        {
            var sport = await _db.Sports.AsNoTracking().FirstOrDefaultAsync(s => s.Id == model.SportId.Value);
            if (sport == null)
                state.AddModelError(nameof(model.SportId), "Select a sport from the list.");
            else if (!sport.IsActive && existing?.SportId != sport.Id)
                state.AddModelError(nameof(model.SportId), "This sport is not accepting registrations right now.");
        }

        if (model.TryoutScheduleId.HasValue)
        {
            var schedule = await _db.Schedules.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TryoutScheduleId.Value);
            bool unchanged = existing?.TryoutScheduleId == model.TryoutScheduleId;

            if (schedule == null)
                state.AddModelError(nameof(model.TryoutScheduleId), "Select a schedule from the list.");
            else if (model.SportId.HasValue && schedule.SportId != model.SportId.Value)
                state.AddModelError(nameof(model.TryoutScheduleId), "This schedule belongs to a different sport.");
            else if (!unchanged && (schedule.IsCancelled || schedule.EndAt < DateTime.Now))
                state.AddModelError(nameof(model.TryoutScheduleId), "Choose an upcoming tryout schedule.");
        }
    }

    /// <summary>Checks the two ID pictures for type and size.</summary>
    public void ValidateIdPictures(ModelStateDictionary state, ApplicantFormViewModel model)
    {
        ValidateImage(state, model.IdPictureFront, nameof(model.IdPictureFront));
        ValidateImage(state, model.IdPictureBack, nameof(model.IdPictureBack));
    }

    private static void ValidateImage(ModelStateDictionary state, IFormFile? file, string key)
    {
        if (file == null || file.Length == 0) return;
        if (file.Length > MaxImageBytes)
        {
            state.AddModelError(key, "Each ID picture must be 5 MB or smaller.");
            return;
        }
        if (AllowedImageTypes.All(t => !string.Equals(t, file.ContentType, StringComparison.OrdinalIgnoreCase)))
            state.AddModelError(key, "ID pictures must be a JPG, PNG or WEBP image.");
    }

    public void Apply(Applicant applicant, ApplicantFormViewModel model)
    {
        applicant.StudentId = NormalizeStudentId(model.StudentId);
        applicant.FirstName = model.FirstName.Trim();
        applicant.MiddleName = Clean(model.MiddleName);
        applicant.LastName = model.LastName.Trim();
        applicant.Gender = model.Gender;
        applicant.BirthDate = model.BirthDate!.Value.Date;
        applicant.CourseOrGrade = model.CourseOrGrade.Trim();
        applicant.YearLevel = model.YearLevel;
        applicant.Email = Clean(model.Email);
        applicant.ContactNumber = model.ContactNumber.Trim();
        applicant.Address = Clean(model.Address);
        applicant.HeightCm = model.HeightCm;
        applicant.WeightKg = model.WeightKg;
        applicant.EmergencyContactName = Clean(model.EmergencyContactName);
        applicant.EmergencyContactNumber = Clean(model.EmergencyContactNumber);
        applicant.MedicalNotes = Clean(model.MedicalNotes);
        applicant.SportId = model.SportId!.Value;
        applicant.TryoutScheduleId = model.TryoutScheduleId;
    }

    /// <summary>Saves uploaded ID pictures. New photos reset the verification status.</summary>
    public Task SaveIdPicturesAsync(Applicant applicant, ApplicantFormViewModel model)
    {
        var front = StoreImage(model.IdPictureFront, applicant.StudentId, "front", applicant.IdPictureFrontPath);
        if (front != null) applicant.IdPictureFrontPath = front;

        var back = StoreImage(model.IdPictureBack, applicant.StudentId, "back", applicant.IdPictureBackPath);
        if (back != null) applicant.IdPictureBackPath = back;

        if (front != null || back != null)
        {
            applicant.IdVerified = false;
            applicant.VerifiedById = null;
            applicant.VerifiedAt = null;
            applicant.VerificationRemarks = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Shares the student's identity fields (name, student number, ID photos, verification)
    /// with their other tryout entries, because those belong to the student rather than
    /// to a single sport.
    /// </summary>
    public async Task SyncProfileAsync(Applicant applicant, int? ownerUserId)
    {
        if (!ownerUserId.HasValue) return;

        var siblings = await _db.Applicants
            .Where(a => a.UserId == ownerUserId.Value && a.Id != applicant.Id)
            .ToListAsync();
        if (siblings.Count == 0) return;

        foreach (var s in siblings)
        {
            s.StudentId = applicant.StudentId;
            s.FirstName = applicant.FirstName;
            s.MiddleName = applicant.MiddleName;
            s.LastName = applicant.LastName;
            s.Gender = applicant.Gender;
            s.BirthDate = applicant.BirthDate;
            s.Email = applicant.Email;
            s.ContactNumber = applicant.ContactNumber;
            s.Address = applicant.Address;
            s.IdPictureFrontPath = applicant.IdPictureFrontPath;
            s.IdPictureBackPath = applicant.IdPictureBackPath;
            s.IdVerified = applicant.IdVerified;
            s.VerifiedById = applicant.VerifiedById;
            s.VerifiedAt = applicant.VerifiedAt;
            s.VerificationRemarks = applicant.VerificationRemarks;
        }
    }

    private string? StoreImage(IFormFile? file, string studentId, string side, string? previousPath)
    {
        if (file == null || file.Length == 0) return null;

        var folder = Path.Combine(_env.WebRootPath, "uploads", "id-pictures");
        Directory.CreateDirectory(folder);

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) ext = ".jpg";

        string fileName = $"{studentId}-{side}-{DateTime.Now:yyyyMMddHHmmss}{ext}";
        string fullPath = Path.Combine(folder, fileName);

        using (var stream = File.Create(fullPath))
        {
            file.CopyTo(stream);
        }

        if (!string.IsNullOrWhiteSpace(previousPath))
            DeleteImage(previousPath);

        return $"/uploads/id-pictures/{fileName}";
    }

    private void DeleteImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }

    public ApplicantFormViewModel ToForm(Applicant a) => new()
    {
        Id = a.Id,
        StudentId = a.StudentId,
        FirstName = a.FirstName,
        MiddleName = a.MiddleName,
        LastName = a.LastName,
        Gender = a.Gender,
        BirthDate = a.BirthDate,
        CourseOrGrade = a.CourseOrGrade,
        YearLevel = a.YearLevel,
        Email = a.Email,
        ContactNumber = a.ContactNumber,
        Address = a.Address,
        HeightCm = a.HeightCm,
        WeightKg = a.WeightKg,
        EmergencyContactName = a.EmergencyContactName,
        EmergencyContactNumber = a.EmergencyContactNumber,
        MedicalNotes = a.MedicalNotes,
        SportId = a.SportId,
        TryoutScheduleId = a.TryoutScheduleId,
        ExistingFrontPath = a.IdPictureFrontPath,
        ExistingBackPath = a.IdPictureBackPath
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
