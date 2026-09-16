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
    private static FormFile MakeFile(byte[] bytes, string contentType, string fileName = "upload.bin")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "PhotoFile", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    [Fact]
    public async Task SaveAsync_ValidJpeg_SavesFileAndReturnsUrlUnderSubfolder()
    {
        using var env = new FakeWebHostEnvironment();
        var file = MakeFile(Encoding.UTF8.GetBytes("not really a jpeg but that's fine for this check"), "image/jpeg");
        var modelState = new ModelStateDictionary();

        var url = await PhotoUploadHelper.SaveAsync(file, "animals", env, modelState);

        Assert.NotNull(url);
        Assert.StartsWith("/img/animals/", url);
        Assert.EndsWith(".jpg", url);
        Assert.True(modelState.IsValid);
        Assert.True(File.Exists(Path.Combine(env.WebRootPath, "img", "animals", Path.GetFileName(url))));
    }

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

        var url1 = await PhotoUploadHelper.SaveAsync(MakeFile([1, 2, 3], "image/png"), "animals", env, modelState);
        var url2 = await PhotoUploadHelper.SaveAsync(MakeFile([1, 2, 3], "image/png"), "animals", env, modelState);

        Assert.NotEqual(url1, url2);
    }
}
