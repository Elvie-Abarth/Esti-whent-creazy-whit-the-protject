using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Cart;

// Buying is a Customer action - employees and admins have their own area
// (/Admin) and aren't meant to be shopping through the storefront.
[Authorize(Roles = "Customer")]
public class IndexModel(ICartStore cart, ICatalog catalog, IOrderStore orders) : PageModel
{
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];
    public decimal Total => Lines.Sum(l => l.LineTotal);

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet() => Lines = cart.GetLines(CurrentUserId);

    public IActionResult OnPostAdd(int productId, int quantity = 1, string? returnUrl = null)
    {
        var animal = catalog.FindAnimal(productId);
        if (animal is not null)
        {
            if (!animal.CanBeAddedToCart(1))
            {
                ErrorMessage = $"{animal.Name} kan ikke lægges i kurven lige nu.";
                return RedirectAfterAdd(returnUrl);
            }
            cart.AddOrIncrement(CurrentUserId, productId, 1);
            TempData["ToastMessage"] = $"{animal.Name} er lagt i kurven.";
            return RedirectAfterAdd(returnUrl);
        }

        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        if (product is null || quantity < 1)
        {
            ErrorMessage = "Varen findes ikke.";
            return RedirectAfterAdd(returnUrl);
        }
        if (!product.CanBeAddedToCart(quantity))
        {
            ErrorMessage = $"Der er ikke {quantity} styk tilbage af {product.Name}.";
            return RedirectAfterAdd(returnUrl);
        }

        cart.AddOrIncrement(CurrentUserId, productId, quantity);
        TempData["ToastMessage"] = $"{product.Name} er lagt i kurven.";
        return RedirectAfterAdd(returnUrl);
    }

    // "Add to cart" is posted to from the catalog and animal-profile pages, not just
    // from the cart itself - always bouncing back to /Cart/Index after every add made
    // it impossible to add several items without clicking back each time. Every "Læg i
    // kurv" form carries a returnUrl back to where it was submitted from, so browsing
    // continues from there; Url.IsLocalUrl guards against it being used to redirect
    // off-site (an attacker-crafted returnUrl on a link/form pointing here).
    private IActionResult RedirectAfterAdd(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();

    public IActionResult OnPostRemove(int productId)
    {
        cart.RemoveLine(CurrentUserId, productId);
        return RedirectToPage();
    }

    public IActionResult OnPostCheckout()
    {
        var result = orders.Checkout(CurrentUserId);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            return RedirectToPage();
        }

        return RedirectToPage("Confirmation", new { orderId = result.Order!.OrderId });
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
