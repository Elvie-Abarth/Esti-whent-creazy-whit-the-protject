using System.ComponentModel.DataAnnotations;
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
                ModelState.AddModelError(nameof(Input.ShippingAddress), "Angiv en leveringsadresse.");
            }
        }
        if (!ModelState.IsValid) return Page();

        // The "payment" above is never actually processed - the demo card details
        // aren't read past validating their shape. Checkout re-validates stock,
        // animal availability, and the shipping/animal rule itself (see
        // SqlOrderStore.Checkout), same as it did before this page existed.
        var result = orders.Checkout(this.CurrentUserId(), Input.DeliveryMethod, Input.ShippingAddress);
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
            ? $"Sendes til: {order.ShippingAddress}" +
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

        [Required(ErrorMessage = "Udfyld navnet på kortet.")]
        [StringLength(200)]
        public string CardHolder { get; set; } = "";

        [Required(ErrorMessage = "Udfyld kortnummeret.")]
        [RegularExpression(@"^[0-9 ]{12,19}$", ErrorMessage = "Kortnummeret ser forkert ud.")]
        public string CardNumber { get; set; } = "";

        [Required(ErrorMessage = "Udfyld udløbsdatoen.")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/[0-9]{2}$", ErrorMessage = "Brug formatet MM/ÅÅ.")]
        public string Expiry { get; set; } = "";

        [Required(ErrorMessage = "Udfyld CVC.")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVC skal være 3-4 cifre.")]
        public string Cvc { get; set; } = "";
    }
}
