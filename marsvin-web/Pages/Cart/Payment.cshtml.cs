using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Cart;

// A demo-only stand-in for a real payment step (see the note on Payment.cshtml
// for why this project can't and shouldn't take real card details). No
// [Authorize] - guest checkout is allowed (see Input.GuestName/GuestEmail,
// only required when not signed in); staff accounts still can't buy, checked
// by hand below since [Authorize(Roles = "Customer")] no longer does it.
public class PaymentModel(ICartStore cart, IOrderStore orders, IUserAccountStore users, IEmailSender emailSender) : PageModel
{
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];
    public decimal Total => Lines.Sum(l => l.LineTotal);

    // Guinea pigs can't go in a parcel, but an order can still mix an animal
    // with accessories - shipping is offered as long as there's at least one
    // shippable (non-animal) line; HasAnimal then drives the "the guinea pig
    // still needs pickup" wording on both this page and the confirmation
    // page. Re-checked server-side in SqlOrderStore.Checkout too, not just
    // decided here in the UI.
    public bool CanShip => Lines.Any(l => !l.IsAnimal);
    public bool HasAnimal => Lines.Any(l => l.IsAnimal);

    // What the buyer is shown before choosing a carrier: price, parcel count
    // and estimated delivery for each. Display only - SqlOrderStore.Checkout
    // runs the same calculation itself and never reads a price off the form.
    public int ShippableWeightGrams => ShippingCalculator.ShippableWeightGrams(Lines);
    public int ParcelCount => ShippingCalculator.ParcelCount(ShippableWeightGrams);
    public IReadOnlyList<ShippingQuote> ShippingOptions =>
        Enum.GetValues<ShippingCarrier>()
            .Select(c => ShippingCalculator.Quote(c, ShippableWeightGrams, DateOnly.FromDateTime(DateTime.UtcNow)))
            .ToList();

    [BindProperty]
    public PaymentInputModel Input { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    private static readonly Regex CardNumberPattern = new(@"^[0-9 ]{12,19}$", RegexOptions.Compiled);
    private static readonly Regex ExpiryPattern = new(@"^(0[1-9]|1[0-2])\/[0-9]{2}$", RegexOptions.Compiled);
    private static readonly Regex CvcPattern = new(@"^[0-9]{3,4}$", RegexOptions.Compiled);
    private static readonly Regex GuestEmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public bool IsGuest => User.Identity?.IsAuthenticated != true;

    public IActionResult OnGet()
    {
        if (User.IsInRole("Employee") || User.IsInRole("Admin")) return RedirectToPage("/Index");

        Lines = cart.GetLines(this.CurrentUserIdOrZero());
        return Lines.Count == 0 ? RedirectToPage("Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.IsInRole("Employee") || User.IsInRole("Admin")) return RedirectToPage("/Index");

        Lines = cart.GetLines(this.CurrentUserIdOrZero());
        if (Lines.Count == 0) return RedirectToPage("Index");

        if (Input.DeliveryMethod == DeliveryMethod.Shipping)
        {
            if (!CanShip)
            {
                ModelState.AddModelError(string.Empty, "Der er intet at sende med fragt i denne ordre - vælg afhentning.");
            }
            else if (string.IsNullOrWhiteSpace(Input.ShippingAddress))
            {
                ModelState.AddModelError("Input.ShippingAddress", "Angiv en leveringsadresse.");
            }
        }

        // Card details only matter (and are only required) when Card is the
        // chosen method - MobilePay needs nothing further from this form, the
        // same way a real MobilePay checkout would just send a payment request
        // to the buyer's phone instead of asking for card details at all.
        if (Input.PaymentMethod == PaymentMethod.Card)
        {
            if (string.IsNullOrWhiteSpace(Input.CardHolder))
                ModelState.AddModelError("Input.CardHolder", "Udfyld navnet på kortet.");
            if (!CardNumberPattern.IsMatch(Input.CardNumber ?? ""))
                ModelState.AddModelError("Input.CardNumber", "Kortnummeret ser forkert ud.");
            if (!ExpiryPattern.IsMatch(Input.Expiry ?? ""))
                ModelState.AddModelError("Input.Expiry", "Brug formatet MM/ÅÅ.");
            if (!CvcPattern.IsMatch(Input.Cvc ?? ""))
                ModelState.AddModelError("Input.Cvc", "CVC skal være 3-4 cifre.");
        }

        // Name/email only matter (and are only required) for a guest - a
        // signed-in customer's own account already has both.
        if (IsGuest)
        {
            if (string.IsNullOrWhiteSpace(Input.GuestName))
                ModelState.AddModelError("Input.GuestName", "Udfyld dit navn.");
            if (string.IsNullOrWhiteSpace(Input.GuestEmail))
                ModelState.AddModelError("Input.GuestEmail", "Udfyld din e-mail.");
            else if (!GuestEmailPattern.IsMatch(Input.GuestEmail))
                ModelState.AddModelError("Input.GuestEmail", "E-mailadressen ser forkert ud.");
        }

        if (!ModelState.IsValid) return Page();

        // The "payment" above is never actually processed - the demo card details
        // aren't read past validating their shape. Checkout re-validates stock,
        // animal availability, and the shipping/animal rule itself (see
        // SqlOrderStore.Checkout), same as it did before this page existed.
        // guestLines is the guest's already-resolved session cart, passed
        // straight through since a guest checkout has no dbo.CartItems row
        // for Checkout to load itself (see IOrderStore.Checkout's own doc
        // comment) - ignored (and harmless to pass) for a signed-in customer.
        var result = orders.Checkout(this.CurrentUserIdOrNull(), Input.DeliveryMethod, Input.ShippingAddress,
            Input.DeliveryMethod == DeliveryMethod.Shipping ? Input.ShippingCarrier : null, Input.PaymentMethod,
            IsGuest ? Input.GuestName : null, IsGuest ? Input.GuestEmail : null, Lines);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            return RedirectToPage("Index");
        }

        cart.Clear(this.CurrentUserIdOrZero());

        if (IsGuest)
        {
            // The one-time "receipt pass" Cart/Confirmation checks for a guest:
            // proves it's really this same browser that just placed this exact
            // order, without needing any account to look the order up by.
            // Nothing else in this session is ever allowed to read any other
            // order just by guessing its id - see ConfirmationModel.OnGet.
            HttpContext.Session.SetInt32("GuestOrderId", result.Order!.OrderId);
        }

        await SendConfirmationEmailAsync(result.Order!);

        return RedirectToPage("Confirmation", new { orderId = result.Order!.OrderId });
    }

    private async Task SendConfirmationEmailAsync(Order order)
    {
        // A signed-in customer's name/email come from their account; a
        // guest's are exactly what they just typed into Input.GuestName/
        // GuestEmail (already copied onto the order itself by Checkout -
        // read back from there rather than from Input, so this always
        // reflects what was actually saved).
        string toEmail, toName;
        if (IsGuest)
        {
            toEmail = order.GuestEmail!;
            toName = order.GuestName!;
        }
        else
        {
            var buyer = users.FindById(this.CurrentUserId());
            if (buyer is null) return;
            toEmail = buyer.Email;
            toName = buyer.DisplayName;
        }

        var itemLines = string.Join("\n", order.Items.Select(i =>
            $"- {i.ProductName} x{i.Quantity}: {i.LineTotal:N0} kr."));
        var deliveryLine = order.DeliveryMethod == DeliveryMethod.Shipping
            ? $"Fragt ({order.ShippingCarrier?.DisplayName()}): {order.ShippingCost:N0} kr. - allerede med i beløbet ovenfor.\n" +
              $"Leveres til: {order.ShippingCarrier?.DestinationText(order.ShippingAddress)}\n" +
              (order.ExpectedDelivery is var (earliest, latest)
                  ? $"Forventet levering: {ShippingCalculator.FormatWindow(earliest, latest)}\n" : "") +
              (order.ShippingWeightGrams is int grams && order.ParcelCount is int parcels
                  ? $"Samlet vægt: {ShippingCalculator.FormatWeight(grams)}, sendes i {parcels} {(parcels == 1 ? "pakke" : "pakker")}." : "") +
              (order.Items.Any(i => i.IsAnimal)
                  ? $"\n{string.Join(" og ", order.Items.Where(i => i.IsAnimal).Select(i => i.ProductName))} afhentes i butikken separat."
                  : "")
            : "Afhentes i butikken.";
        // A guest has no "Min konto" order history to point back to - this
        // email (and, for as long as the browser session lasts, the
        // confirmation page itself) is the only receipt they get.
        var seeOrderLine = IsGuest
            ? "Gem denne mail som din kvittering."
            : "Se ordren under Min konto.";

        await emailSender.SendAsync(toEmail, $"Ordrebekræftelse #{order.OrderId}",
            $"""
            Hej {toName},

            Tak for din ordre #{order.OrderId}:

            {itemLines}

            I alt: {order.TotalPrice:N0} kr.

            {deliveryLine}

            {seeOrderLine}

            Venlig hilsen
            Marsvin
            """);
    }

    public sealed class PaymentInputModel
    {
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Pickup;

        // No [Required] here either, same reasoning as the card fields below -
        // only required for a guest, checked by hand in OnPostAsync.
        [StringLength(200)]
        public string? GuestName { get; set; }

        [StringLength(256)]
        public string? GuestEmail { get; set; }

        [StringLength(500)]
        public string? ShippingAddress { get; set; }

        public ShippingCarrier ShippingCarrier { get; set; } = ShippingCarrier.PostNord;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;

        // No [Required]/[RegularExpression] here - these only apply when
        // PaymentMethod is Card, checked by hand in OnPostAsync, since
        // DataAnnotations has no clean "required if" for a sibling property.
        // Nullable on purpose: a blank form field binds as null, not "", and
        // a non-nullable string would get an implicit [Required] from MVC -
        // rejecting a MobilePay checkout for leaving the card fields empty.
        [StringLength(200)]
        public string? CardHolder { get; set; }

        public string? CardNumber { get; set; }

        public string? Expiry { get; set; }

        public string? Cvc { get; set; }
    }
}
