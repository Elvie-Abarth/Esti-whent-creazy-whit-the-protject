using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Cart;

// A demo-only stand-in for a real payment step (see the note on Payment.cshtml
// for why this project can't and shouldn't take real card details). Buying is a
// Customer action - employees and admins have their own area (/Admin) and
// aren't meant to be shopping through the storefront.
[Authorize(Roles = "Customer")]
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

    [BindProperty]
    public PaymentInputModel Input { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    private static readonly Regex CardNumberPattern = new(@"^[0-9 ]{12,19}$", RegexOptions.Compiled);
    private static readonly Regex ExpiryPattern = new(@"^(0[1-9]|1[0-2])\/[0-9]{2}$", RegexOptions.Compiled);
    private static readonly Regex CvcPattern = new(@"^[0-9]{3,4}$", RegexOptions.Compiled);

    public IActionResult OnGet()
    {
        Lines = cart.GetLines(this.CurrentUserId());
        return Lines.Count == 0 ? RedirectToPage("Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Lines = cart.GetLines(this.CurrentUserId());
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
            if (!CardNumberPattern.IsMatch(Input.CardNumber))
                ModelState.AddModelError("Input.CardNumber", "Kortnummeret ser forkert ud.");
            if (!ExpiryPattern.IsMatch(Input.Expiry))
                ModelState.AddModelError("Input.Expiry", "Brug formatet MM/ÅÅ.");
            if (!CvcPattern.IsMatch(Input.Cvc))
                ModelState.AddModelError("Input.Cvc", "CVC skal være 3-4 cifre.");
        }

        if (!ModelState.IsValid) return Page();

        // The "payment" above is never actually processed - the demo card details
        // aren't read past validating their shape. Checkout re-validates stock,
        // animal availability, and the shipping/animal rule itself (see
        // SqlOrderStore.Checkout), same as it did before this page existed.
        var result = orders.Checkout(this.CurrentUserId(), Input.DeliveryMethod, Input.ShippingAddress,
            Input.DeliveryMethod == DeliveryMethod.Shipping ? Input.ShippingCarrier : null, Input.PaymentMethod);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            return RedirectToPage("Index");
        }

        await SendConfirmationEmailAsync(result.Order!);

        return RedirectToPage("Confirmation", new { orderId = result.Order!.OrderId });
    }

    private async Task SendConfirmationEmailAsync(Order order)
    {
        var buyer = users.FindById(this.CurrentUserId());
        if (buyer is null) return;

        var itemLines = string.Join("\n", order.Items.Select(i =>
            $"- {i.ProductName} x{i.Quantity}: {i.LineTotal:N0} kr."));
        var deliveryLine = order.DeliveryMethod == DeliveryMethod.Shipping
            ? $"Sendes til: {order.ShippingAddress} ({order.ShippingCarrier?.DisplayName()})" +
              (order.Items.Any(i => i.IsAnimal)
                  ? $"\n{string.Join(" og ", order.Items.Where(i => i.IsAnimal).Select(i => i.ProductName))} afhentes i butikken separat."
                  : "")
            : "Afhentes i butikken.";

        await emailSender.SendAsync(buyer.Email, $"Ordrebekræftelse #{order.OrderId}",
            $"""
            Hej {buyer.DisplayName},

            Tak for din ordre #{order.OrderId}:

            {itemLines}

            I alt: {order.TotalPrice:N0} kr.

            {deliveryLine}

            Se ordren under Min konto.

            Venlig hilsen
            Marsvin
            """);
    }

    public sealed class PaymentInputModel
    {
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Pickup;

        [StringLength(500)]
        public string? ShippingAddress { get; set; }

        public ShippingCarrier ShippingCarrier { get; set; } = ShippingCarrier.PostNord;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;

        // No [Required]/[RegularExpression] here - these only apply when
        // PaymentMethod is Card, checked by hand in OnPostAsync, since
        // DataAnnotations has no clean "required if" for a sibling property.
        [StringLength(200)]
        public string CardHolder { get; set; } = "";

        public string CardNumber { get; set; } = "";

        public string Expiry { get; set; } = "";

        public string Cvc { get; set; } = "";
    }
}
