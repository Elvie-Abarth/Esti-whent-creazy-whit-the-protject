using MarsvinWebExample.Pages;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages;

public class PasningsguideHurtigPdfModelTests
{
    // See FoderlistePdfModelTests for why this is needed here too.
    static PasningsguideHurtigPdfModelTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Theory]
    [InlineData(false, "marsvins-lynguide.pdf")]
    [InlineData(true, "guinea-pig-quick-guide.pdf")]
    public void OnGet_ReturnsPdfFileWithExpectedName(bool en, string expectedFileName)
    {
        var model = new PasningsguideHurtigPdfModel();

        var result = model.OnGet(en);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(expectedFileName, file.FileDownloadName);
        // %PDF is the magic number every valid PDF file starts with.
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4));
    }
}
