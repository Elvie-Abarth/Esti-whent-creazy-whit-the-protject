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
    /// A photo address typed by hand has to point into the site's own image
    /// folder: a plain path under /img/, ending in an image extension. No
    /// other site, no "..", no query string - so the field can't be used to
    /// make every visitor's browser fetch something from somewhere else, or
    /// to point at a file that isn't a picture.
    /// </summary>
    public const string LocalPhotoUrlPattern = @"^/img/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.(?:jpg|jpeg|png|webp|gif)$";

    public const string LocalPhotoUrlMessage =
        "Foto-adressen skal pege på et billede i sidens egen billedmappe, f.eks. /img/animals/navn.jpg.";

    // The first bytes every file of that type starts with. The Content-Type
    // header is only what the browser *says* the file is - anyone can send
    // "image/jpeg" in front of something else - so the file's own opening
    // bytes have to agree with it before anything is written to disk.
    private static bool LooksLike(string contentType, ReadOnlySpan<byte> start) => contentType switch
    {
        "image/jpeg" => start.StartsWith<byte>([0xFF, 0xD8, 0xFF]),
        "image/png" => start.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "image/gif" => start.StartsWith("GIF87a"u8) || start.StartsWith("GIF89a"u8),
        // RIFF....WEBP - four bytes of length sit between the two markers.
        "image/webp" => start.Length >= 12 && start.StartsWith("RIFF"u8) && start[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };

    /// <summary>
    /// Returns the new photo's URL on success, or null (with a validation
    /// error added to modelState) if the file isn't an image or is too big.
    /// "Is an image" means both that the browser says so and that the file's
    /// own first bytes are those of that image type.
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

        var start = new byte[12];
        int read;
        await using (var peek = file.OpenReadStream())
        {
            read = await peek.ReadAtLeastAsync(start, start.Length, throwOnEndOfStream: false);
        }
        if (!LooksLike(file.ContentType, start.AsSpan(0, read)))
        {
            modelState.AddModelError(string.Empty, "Filen er ikke et gyldigt billede af den angivne type.");
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
