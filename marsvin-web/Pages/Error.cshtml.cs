using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages;

// Where every response with an error status and no body ends up (see
// UseStatusCodePagesWithReExecute in Program.cs, which passes the status
// along as ?code=). One page, but it says what actually happened: a missing
// page, a form that had gone stale, too many attempts, or no access - not
// "this page doesn't exist" for all of them.
//
// The failed request is re-run here with its original method, so a rejected
// POST arrives as a POST: hence OnPost, and no antiforgery check on this
// page (it shows a message and changes nothing - and a stale token is
// exactly one of the reasons to land here).
[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    /// <summary>The status being explained. Anything unexpected is treated as "not found".</summary>
    public int Code { get; private set; } = StatusCodes.Status404NotFound;

    public void OnGet(int? code = null) => Code = code switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status403Forbidden or StatusCodes.Status429TooManyRequests => code.Value,
        _ => StatusCodes.Status404NotFound
    };

    public void OnPost(int? code = null) => OnGet(code);
}
