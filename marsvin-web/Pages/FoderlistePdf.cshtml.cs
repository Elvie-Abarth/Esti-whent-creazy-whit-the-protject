using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuestPDF.Fluent;

namespace MarsvinWebExample.Pages;

// Public, like /Foderliste itself - no auth needed to read the food list.
public class FoderlistePdfModel : PageModel
{
    public IActionResult OnGet(bool en = false)
    {
        var bytes = new FoodListPdfDocument(en).GeneratePdf();
        var fileName = en ? "guinea-pig-food-list.pdf" : "marsvins-foderliste.pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
