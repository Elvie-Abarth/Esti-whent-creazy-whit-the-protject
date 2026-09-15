using MarsvinWebExample.Pages;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages;

public class FoderlistePdfModelTests
{
    // This test instantiates FoderlistePdfModel directly rather than going
    // through WebApplicationFactory<Program>, so Program.cs's own
    // QuestPDF.Settings.License assignment never runs - set it here too
    // (idempotent) so this test doesn't depend on some other test class
    // happening to have booted the full app first.
    static FoderlistePdfModelTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Theory]
    [InlineData(false, "marsvins-foderliste.pdf")]
    [InlineData(true, "guinea-pig-food-list.pdf")]
    public void OnGet_ReturnsPdfFileWithExpectedName(bool en, string expectedFileName)
    {
        var model = new FoderlistePdfModel();

        var result = model.OnGet(en);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(expectedFileName, file.FileDownloadName);
        // %PDF is the magic number every valid PDF file starts with.
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4));
    }
}
