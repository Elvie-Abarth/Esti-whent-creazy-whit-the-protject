using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MarsvinWebExample.Data;

/// <summary>
/// Saves an admin-uploaded photo under wwwroot/img and returns the URL to
/// store on the Animal/StockProduct - lets Admin/Animals/Edit and
/// Admin/Products/Edit offer "upload a file" as an alternative to typing a
/// PhotoUrl by hand, which was the only option before.
/// </summary>
public static class PhotoUploadHelper
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedExtensionsByContentType = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    /// <summary>
    /// Returns the new photo's URL on success, or null (with a validation
    /// error added to modelState) if the file isn't an image or is too big.
    /// The filename is always a fresh GUID - never the browser-supplied
    /// name - so nothing about the original filename (including its
    /// extension) reaches the filesystem unvalidated.
    /// </summary>
    public static async Task<string?> SaveAsync(
        IFormFile file, string subfolder, IWebHostEnvironment environment, ModelStateDictionary modelState)
    {
        if (!AllowedExtensionsByContentType.TryGetValue(file.ContentType, out var extension))
        {
            modelState.AddModelError(string.Empty, "Billedet skal være JPEG, PNG, WebP eller GIF.");
            return null;
        }

        if (file.Length > MaxBytes)
        {
            modelState.AddModelError(string.Empty, "Billedet må højst være 5 MB.");
            return null;
        }

        var folder = Path.Combine(environment.WebRootPath, "img", subfolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/img/{subfolder}/{fileName}";
    }
}
