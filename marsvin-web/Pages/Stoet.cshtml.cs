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
public class StoetModel(
    IInboxStore inbox, IRecaptchaVerifier recaptcha, IEmailSender emailSender, ILogger<StoetModel> logger) : PageModel
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
        var donation = new Donation
        {
            Kind = Input.Kind,
            // Only the fields that belong to the chosen kind are kept, whatever else the form posted.
            AmountKr = isMoney ? Input.AmountKr : null,
            ItemDescription = isMoney ? null : Input.ItemDescription!.Trim(),
            DonorName = string.IsNullOrWhiteSpace(Input.DonorName) ? null : Input.DonorName.Trim(),
            DonorEmail = string.IsNullOrWhiteSpace(Input.DonorEmail) ? null : Input.DonorEmail.Trim(),
            Message = string.IsNullOrWhiteSpace(Input.Message) ? null : Input.Message.Trim()
        };
        inbox.AddDonation(donation);

        await SendThankYouEmailAsync(donation, this.IsEnglish());

        ToastMessage = isMoney
            ? new Bilingual(
                $"Tak for din donation på {Input.AmountKr:N0} kr. (demo - der er ikke trukket noget).",
                $"Thank you for your donation of {Input.AmountKr:N0} kr. (demo - nothing was charged).")
            : new Bilingual(
                "Tak! Vi skriver til dig og aftaler, hvornår du kan aflevere det i butikken.",
                "Thank you! We'll write to you to arrange when you can hand it in at the shop.");
        return RedirectToPage();
    }

    // The thank-you and "what happens now" mail. Only sent when the donor
    // gave an address - an anonymous money donation has nobody to write to.
    // Nothing the donor typed as free text (what they offer, their greeting)
    // is repeated in it: this form is open to guests, and the address is
    // whatever was typed in, so echoing free text would let anyone use the
    // shop to send their own wording to somebody else's inbox. The name is
    // kept to one line for the same reason. Written in the language the
    // donor had the page in when they sent the form.
    private async Task SendThankYouEmailAsync(Donation donation, bool english)
    {
        if (donation.DonorEmail is null) return;

        var name = donation.DonorName?.ReplaceLineEndings(" ");
        var isMoney = donation.Kind == DonationKind.Money;
        string subject, body;
        if (english)
        {
            var details = isMoney
                ? $"""
                   We have recorded your donation of {donation.AmountKr:N0} kr.
                   This is an example project, so no money has been taken.

                   The money goes to the guinea pigs the shop takes back and rehomes: food, hay, bedding and vet visits.
                   """
                : """
                  We have received your offer to donate food or equipment.
                  We will write to you at this address to agree a day when you can hand it in.

                  Hand it in at the shop: Marsvin, Havnegade 12, 6700 Esbjerg.
                  Opening hours: Thursday and Friday 14-18, Saturday 10-14.
                  """;
            subject = "Thank you for your donation to Marsvin";
            body = $"""
                {(name is null ? "Hello," : $"Hello {name},")}

                Thank you so much for supporting guinea pigs in need - it means a lot to the animals and to us.

                {details}

                If you have any questions, you are welcome to reply to this email.

                Kind regards
                Marsvin
                """;
        }
        else
        {
            var details = isMoney
                ? $"""
                   Vi har registreret din donation på {donation.AmountKr:N0} kr.
                   Det her er et eksempelprojekt, så der er ikke trukket noget beløb.

                   Pengene går til de marsvin, butikken tager tilbage og finder nye hjem til: foder, hø, strøelse og dyrlæge.
                   """
                : """
                  Vi har modtaget dit tilbud om at donere foder eller udstyr.
                  Vi skriver til dig på denne adresse og aftaler en dag, hvor du kan aflevere det.

                  Afleveres i butikken: Marsvin, Havnegade 12, 6700 Esbjerg.
                  Åbningstider: torsdag og fredag 14-18, lørdag 10-14.
                  """;
            subject = "Tak for din donation til Marsvin";
            body = $"""
                {(name is null ? "Hej," : $"Hej {name},")}

                Tusind tak for din støtte til marsvin i nød - det betyder meget for dyrene og for os.

                {details}

                Har du spørgsmål, er du velkommen til at svare på denne mail.

                Venlig hilsen
                Marsvin
                """;
        }

        try
        {
            await emailSender.SendAsync(donation.DonorEmail, subject, body, english);
        }
        catch (Exception ex)
        {
            // The donation is already saved - a mail that could not be sent
            // must not turn it into an error page (and a second attempt
            // into a second donation).
            logger.LogWarning(ex, "Could not send the donation thank-you email.");
        }
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
