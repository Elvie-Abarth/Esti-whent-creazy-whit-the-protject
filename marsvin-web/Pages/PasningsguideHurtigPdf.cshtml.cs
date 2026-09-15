using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuestPDF.Fluent;

namespace MarsvinWebExample.Pages;

// Public, like /PasningsguideHurtig itself - no auth needed to read the guide.
public class PasningsguideHurtigPdfModel : PageModel
{
    public IActionResult OnGet(bool en = false)
    {
        var bytes = new QuickGuidePdfDocument(en).GeneratePdf();
        var fileName = en ? "guinea-pig-quick-guide.pdf" : "marsvins-lynguide.pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
