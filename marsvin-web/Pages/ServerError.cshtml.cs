using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages;

// What a visitor sees when the app itself crashes (an unhandled exception),
// outside Development - wired up with UseExceptionHandler in Program.cs. A
// plain "something went wrong": no exception text, no stack trace, nothing
// about the database. Separate from Error, which explains missing pages and
// rejected requests. Never cached, so a later visit doesn't show a stale
// error.
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ServerErrorModel : PageModel
{
    public void OnGet() { }
}
