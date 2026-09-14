using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Cart;

// A demo-only stand-in for a real payment step (see the note on Payment.cshtml
// for why this project can't and shouldn't take real card details). Buying is a
// Customer action - employees and admins have their own area (/Admin) and
// aren't meant to be shopping through the storefront.
[Authorize(Roles = "Customer")]
public class PaymentModel(ICartStore cart, IOrderStore orders) : PageModel
{
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];
    public decimal Total => Lines.Sum(l => l.LineTotal);

    // Guinea pigs can't go in a parcel - shipping is only ever offered when
    // nothing in the cart is an animal. Re-checked server-side in
    // SqlOrderStore.Checkout too, not just hidden in the UI.
    public bool CanShip => !Lines.Any(l => l.IsAnimal);

    [BindProperty]
    public PaymentInputModel Input { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        Lines = cart.GetLines(CurrentUserId);
        return Lines.Count == 0 ? RedirectToPage("Index") : Page();
    }

    public IActionResult OnPost()
    {
        Lines = cart.GetLines(CurrentUserId);
        if (Lines.Count == 0) return RedirectToPage("Index");

        if (Input.DeliveryMethod == DeliveryMethod.Shipping)
        {
            if (!CanShip)
            {
                ModelState.AddModelError(string.Empty, "Marsvin kan ikke sendes med fragt - vælg afhentning.");
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
        var result = orders.Checkout(CurrentUserId, Input.DeliveryMethod, Input.ShippingAddress);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            return RedirectToPage("Index");
        }

        return RedirectToPage("Confirmation", new { orderId = result.Order!.OrderId });
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
