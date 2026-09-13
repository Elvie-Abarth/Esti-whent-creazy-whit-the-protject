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

    // Read by _Layout.cshtml on whatever page the redirect after OnPostAdd lands
    // on - [TempData] (not a raw TempData["..."] write) so it still works when a
    // PageModel is unit-tested directly, the same as ErrorMessage above; a direct
    // dictionary write throws there because TempData isn't wired up outside a real
    // request.
    [TempData]
    public string? ToastMessage { get; set; }

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
            ToastMessage = $"{animal.Name} er lagt i kurven.";
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
        ToastMessage = $"{product.Name} er lagt i kurven.";
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

    public IActionResult OnPostUpdateQuantity(int productId, int quantity)
    {
        // A guinea pig is a unique item, not stock - there's no "quantity" for
        // it to change, so the cart page doesn't offer this form for animal
        // lines. Guard it here too rather than trusting that a form field
        // wasn't tampered with.
        var animal = catalog.FindAnimal(productId);
        if (animal is not null)
        {
            ErrorMessage = $"{animal.Name} er ét dyr - antallet kan ikke ændres.";
            return RedirectToPage();
        }

        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        if (product is null)
        {
            ErrorMessage = "Varen findes ikke.";
            return RedirectToPage();
        }

        if (quantity < 1)
        {
            cart.RemoveLine(CurrentUserId, productId);
            return RedirectToPage();
        }

        if (!product.CanBeAddedToCart(quantity))
        {
            ErrorMessage = $"Der er ikke {quantity} styk tilbage af {product.Name}.";
            return RedirectToPage();
        }

        cart.SetQuantity(CurrentUserId, productId, quantity);
        return RedirectToPage();
    }

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
