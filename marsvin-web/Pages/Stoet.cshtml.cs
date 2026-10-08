using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages;

// Donations towards the guinea pigs the shop takes back and rehomes: an
// amount, or an offer of food/hay/equipment. Same shape as the demo payment
// step - a money donation is recorded, never charged (see BetalingOgLevering
// for why this project takes no real payments). Open to guests, so
// rate-limited and behind the optional reCAPTCHA like the contact form.
[EnableRateLimiting("auth")]
public class StoetModel(IInboxStore inbox, IRecaptchaVerifier recaptcha) : PageModel
{
    public const int MinAmount = 10;
    public const int MaxAmount = 10_000;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(Name = "g-recaptcha-response")]
    public string? RecaptchaResponse { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        // Which fields are required depends on the kind of donation - the same
        // "required if" situation as the card fields on Cart/Payment, checked
        // by hand for the same reason.
        if (Input.Kind == DonationKind.Money)
        {
            if (Input.AmountKr is null or < MinAmount or > MaxAmount)
                ModelState.AddModelError("Input.AmountKr", $"Vælg et beløb mellem {MinAmount} og {MaxAmount} kr.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Input.ItemDescription))
                ModelState.AddModelError("Input.ItemDescription", "Skriv hvad du gerne vil donere.");
            if (string.IsNullOrWhiteSpace(Input.DonorName))
                ModelState.AddModelError("Input.DonorName", "Udfyld dit navn, så vi ved hvem der kommer.");
            if (string.IsNullOrWhiteSpace(Input.DonorEmail))
                ModelState.AddModelError("Input.DonorEmail", "Udfyld din e-mail, så vi kan aftale afleveringen.");
        }

        if (!ModelState.IsValid) return Page();

        if (!await recaptcha.VerifyAsync(RecaptchaResponse))
        {
            ModelState.AddModelError(string.Empty, "Bekræft venligst, at du ikke er en robot.");
            return Page();
        }

        var isMoney = Input.Kind == DonationKind.Money;
        inbox.AddDonation(new Donation
        {
            Kind = Input.Kind,
            // Only the fields that belong to the chosen kind are kept, whatever else the form posted.
            AmountKr = isMoney ? Input.AmountKr : null,
            ItemDescription = isMoney ? null : Input.ItemDescription!.Trim(),
            DonorName = string.IsNullOrWhiteSpace(Input.DonorName) ? null : Input.DonorName.Trim(),
            DonorEmail = string.IsNullOrWhiteSpace(Input.DonorEmail) ? null : Input.DonorEmail.Trim(),
            Message = string.IsNullOrWhiteSpace(Input.Message) ? null : Input.Message.Trim()
        });

        ToastMessage = isMoney
            ? new Bilingual(
                $"Tak for din donation på {Input.AmountKr:N0} kr. (demo - der er ikke trukket noget).",
                $"Thank you for your donation of {Input.AmountKr:N0} kr. (demo - nothing was charged).")
            : new Bilingual(
                "Tak! Vi skriver til dig og aftaler, hvornår du kan aflevere det i butikken.",
                "Thank you! We'll write to you to arrange when you can hand it in at the shop.");
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [EnumDataType(typeof(DonationKind))]
        public DonationKind Kind { get; set; } = DonationKind.Money;

        public int? AmountKr { get; set; } = 50;

        [StringLength(500)]
        public string? ItemDescription { get; set; }

        [StringLength(200)]
        public string? DonorName { get; set; }

        [EmailAddress(ErrorMessage = "E-mailadressen ser forkert ud."), StringLength(256)]
        public string? DonorEmail { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }
    }
}
