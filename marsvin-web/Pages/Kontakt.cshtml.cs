using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages;

// A form anyone on the internet can post to: rate-limited per IP (the same
// "auth" policy as Login/Register) and behind the same optional reCAPTCHA, so
// it can't be used to flood the staff inbox. The message is stored and read
// by staff on Admin/Messages - deliberately NOT emailed anywhere: mailing a
// "we got your message" copy to whatever address was typed in would turn
// this form into a way to send mail to strangers through the shop's own
// mail account.
[EnableRateLimiting("auth")]
public class KontaktModel(IInboxStore inbox, IRecaptchaVerifier recaptcha) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(Name = "g-recaptcha-response")]
    public string? RecaptchaResponse { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet()
    {
        // Saves a signed-in customer retyping what the session already knows.
        if (User.Identity?.IsAuthenticated == true)
        {
            Input.Name = User.Identity.Name ?? "";
            Input.Email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (!await recaptcha.VerifyAsync(RecaptchaResponse))
        {
            ModelState.AddModelError(string.Empty, "Bekræft venligst, at du ikke er en robot.");
            return Page();
        }

        inbox.AddContactMessage(new ContactMessage
        {
            Name = Input.Name.Trim(),
            Email = Input.Email.Trim(),
            Topic = Input.Topic,
            OrderId = Input.OrderId,
            Message = Input.Message.Trim()
        });

        ToastMessage = new Bilingual(
            "Tak for din besked - vi svarer på e-mail inden for et par hverdage.",
            "Thanks for your message - we'll reply by email within a couple of weekdays.");
        // Redirect, not Page(): a refresh after sending must not send it again.
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Udfyld dit navn."), StringLength(200)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Udfyld din e-mail."), EmailAddress(ErrorMessage = "E-mailadressen ser forkert ud."), StringLength(256)]
        public string Email { get; set; } = "";

        [EnumDataType(typeof(ContactTopic))]
        public ContactTopic Topic { get; set; } = ContactTopic.Other;

        [Range(1, int.MaxValue, ErrorMessage = "Ordrenummeret ser forkert ud.")]
        public int? OrderId { get; set; }

        [Required(ErrorMessage = "Skriv en besked."), StringLength(2000, MinimumLength = 10, ErrorMessage = "Beskeden skal være mellem 10 og 2000 tegn.")]
        public string Message { get; set; } = "";
    }
}
