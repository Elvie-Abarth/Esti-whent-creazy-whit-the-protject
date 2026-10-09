using System.Text;
using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.FileProviders;

namespace MarsvinWebExample.Tests.Data;

/// <summary>Minimal IWebHostEnvironment backed by a real temp directory, so PhotoUploadHelper can actually write a file.</summary>
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment, IDisposable
{
    public string WebRootPath { get; set; } = Directory.CreateTempSubdirectory("marsvin-webroot-").FullName;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public string EnvironmentName { get; set; } = "Test";

    public void Dispose() => Directory.Delete(WebRootPath, recursive: true);
}

public class PhotoUploadHelperTests
{
    // What every PNG file begins with - the upload is checked against it.
    private static readonly byte[] PngStart = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private static FormFile MakeFile(byte[] bytes, string contentType, string fileName = "upload.bin")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "PhotoFile", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    [Fact]
    public async Task SaveAsync_ValidJpeg_SavesFileAndReturnsUrlUnderSubfolder()
    {
        using var env = new FakeWebHostEnvironment();
        var file = MakeFile([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01], "image/jpeg");
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(file, "animals", env, modelState);

        Assert.NotNull(url);
        Assert.StartsWith("/img/animals/", url);
        Assert.EndsWith(".jpg", url);
        Assert.True(modelState.IsValid);
        Assert.True(File.Exists(Path.Combine(env.WebRootPath, "img", "animals", Path.GetFileName(url))));
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>", "image/jpeg")]   // a page calling itself a photo
    [InlineData("GIF89a and then whatever", "image/png")]                   // a real image type, but not the one claimed
    [InlineData("", "image/gif")]                                           // nothing at all
    public async Task SaveAsync_ContentThatIsNotTheClaimedImageType_IsRejectedWithoutWritingAnything(string content, string claimedType)
    {
        using var env = new FakeWebHostEnvironment();
        var file = MakeFile(Encoding.ASCII.GetBytes(content), claimedType, "photo.jpg");
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(file, "animals", env, modelState);

        Assert.Null(url);
        Assert.False(modelState.IsValid);
        Assert.False(Directory.Exists(Path.Combine(env.WebRootPath, "img", "animals")));
    }

    [Theory]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }, ".png")]
    [InlineData("image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0, 0 }, ".gif")]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    public async Task SaveAsync_EachAllowedImageType_IsRecognisedByItsOwnFirstBytes(string contentType, byte[] start, string extension)
    {
        using var env = new FakeWebHostEnvironment();
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(MakeFile(start, contentType), "products", env, modelState);

        Assert.NotNull(url);
        Assert.EndsWith(extension, url);
        // The whole file is written, not just what was left after peeking at its start.
        Assert.Equal(start, File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "products", Path.GetFileName(url))));
    }

    [Theory]
    [InlineData("/img/animals/cotton.jpg", true)]
    [InlineData("/img/products/hay-bag.jpg", true)]
    [InlineData("/img/animals/0f3c9a.webp", true)]
    [InlineData("https://evil.example/tracker.jpg", false)]   // another site
    [InlineData("//evil.example/x.jpg", false)]               // another site, without the scheme
    [InlineData("/img/../appsettings.json", false)]           // out of the image folder
    [InlineData("/img/animals/x.jpg?v=<script>", false)]      // a query string
    [InlineData("/img/brands/logo.svg", false)]               // an SVG can carry script
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/Admin/Users", false)]
    public void ATypedPhotoAddress_MustPointAtAnImageInTheSitesOwnImageFolder(string address, bool allowed) =>
        Assert.Equal(allowed, System.Text.RegularExpressions.Regex.IsMatch(address, PhotoUploadHelper.LocalPhotoUrlPattern));

    [Fact]
    public async Task SaveAsync_DisallowedContentType_RejectsWithoutWritingAnything()
    {
        using var env = new FakeWebHostEnvironment();
        var file = MakeFile("<svg onload=alert(1)></svg>"u8.ToArray(), "image/svg+xml");
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(file, "animals", env, modelState);

        Assert.Null(url);
        Assert.False(modelState.IsValid);
        Assert.False(Directory.Exists(Path.Combine(env.WebRootPath, "img", "animals")));
    }

    [Fact]
    public async Task SaveAsync_TooLarge_RejectsWithoutWritingAnything()
    {
        using var env = new FakeWebHostEnvironment();
        var file = MakeFile(new byte[6 * 1024 * 1024], "image/png");
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(file, "animals", env, modelState);

        Assert.Null(url);
        Assert.False(modelState.IsValid);
    }

    [Fact]
    public async Task SaveAsync_TwoUploads_GetDistinctFilenames()
    {
        // Always a fresh GUID name - never the browser-supplied one - so two
        // admins uploading a file with the same original name (e.g. both
        // named "photo.png" on their own laptops) never collide or overwrite
        // each other on the server.
        using var env = new FakeWebHostEnvironment();
        var modelState = new ModelStateDictionary();

        var url1 = await PhotoUploadHelper.SaveAsync(MakeFile(PngStart, "image/png"), "animals", env, modelState);
        var url2 = await PhotoUploadHelper.SaveAsync(MakeFile(PngStart, "image/png"), "animals", env, modelState);

        Assert.NotEqual(url1, url2);
    }
}
