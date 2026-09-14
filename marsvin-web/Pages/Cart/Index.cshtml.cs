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
public class IndexModel(ICartStore cart, ICatalog catalog) : PageModel
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

    public IActionResult OnPostAdd(
        int productId, int quantity = 1, string? returnUrl = null,
        bool confirmNotAlone = false, string? companionNote = null)
    {
        var animal = catalog.FindAnimal(productId);
        if (animal is not null)
        {
            if (!animal.CanBeAddedToCart(1))
            {
                ErrorMessage = new Bilingual(
                    $"{animal.Name} kan ikke lægges i kurven lige nu.",
                    $"{animal.Name} can't be added to the cart right now.");
                return RedirectAfterAdd(returnUrl);
            }
            // Guinea pigs are herd animals. A bonded animal (BondedWithId set) already
            // comes with its partner; an unbonded one is only sold once the buyer
            // confirms it's joining a guinea pig or herd they already have - checked
            // server-side too, since a checkbox and text field in the HTML are exactly
            // as trustworthy as no checkbox at all.
            if (animal.BondedWithId is null && (!confirmNotAlone || string.IsNullOrWhiteSpace(companionNote)))
            {
                ErrorMessage = new Bilingual(
                    $"{animal.Name} sælges kun enkeltvis, hvis du bekræfter det ikke skal bo alene.",
                    $"{animal.Name} is only sold alone if you confirm it won't be living alone.");
                return RedirectAfterAdd(returnUrl);
            }
            cart.AddOrIncrement(CurrentUserId, productId, 1);
            ToastMessage = new Bilingual($"{animal.Name} er lagt i kurven.", $"{animal.Name} has been added to the cart.");
            return RedirectAfterAdd(returnUrl);
        }

        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        if (product is null || quantity < 1)
        {
            ErrorMessage = new Bilingual("Varen findes ikke.", "This item doesn't exist.");
            return RedirectAfterAdd(returnUrl);
        }
        if (!product.CanBeAddedToCart(quantity))
        {
            ErrorMessage = new Bilingual(
                $"Der er ikke {quantity} styk tilbage af {product.Name}.",
                $"There aren't {quantity} left of {product.Name}.");
            return RedirectAfterAdd(returnUrl);
        }

        cart.AddOrIncrement(CurrentUserId, productId, quantity);
        ToastMessage = new Bilingual($"{product.Name} er lagt i kurven.", $"{product.Name} has been added to the cart.");
        return RedirectAfterAdd(returnUrl);
    }

    // "Add to cart" is posted to from the catalog and animal-profile pages, not just
    // from the cart itself - always bouncing back to /Cart/Index after every add made
    // it impossible to add several items without clicking back each time. Every "Læg i
    // kurv" form carries a returnUrl back to where it was submitted from, so browsing
    // continues from there; IsSafeLocalUrl guards against it being used to redirect
    // off-site (an attacker-crafted returnUrl on a link/form pointing here).
    private IActionResult RedirectAfterAdd(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && IsSafeLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();

    // Deliberately not PageModel.Url.IsLocalUrl: that needs an IUrlHelper wired up
    // through the full request pipeline, which a PageModel constructed directly in
    // a unit test doesn't have (Url is null there), so it throws where this doesn't.
    // Same local-path shape Url.IsLocalUrl checks: exactly one leading slash - not
    // "//host/evil" or "/\host/evil", either of which a browser can treat as
    // protocol-relative and follow off-site.
    private static bool IsSafeLocalUrl(string url) =>
        url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    public IActionResult OnPostUpdateQuantity(int productId, int quantity)
    {
        // A guinea pig is a unique item, not stock - there's no "quantity" for
        // it to change, so the cart page doesn't offer this form for animal
        // lines. Guard it here too rather than trusting that a form field
        // wasn't tampered with.
        var animal = catalog.FindAnimal(productId);
        if (animal is not null)
        {
            ErrorMessage = new Bilingual(
                $"{animal.Name} er ét dyr - antallet kan ikke ændres.",
                $"{animal.Name} is one animal - the quantity can't be changed.");
            return RedirectToPage();
        }

        var product = catalog.Accessories.FirstOrDefault(p => p.ProductId == productId);
        if (product is null)
        {
            ErrorMessage = new Bilingual("Varen findes ikke.", "This item doesn't exist.");
            return RedirectToPage();
        }

        if (quantity < 1)
        {
            cart.RemoveLine(CurrentUserId, productId);
            return RedirectToPage();
        }

        if (!product.CanBeAddedToCart(quantity))
        {
            ErrorMessage = new Bilingual(
                $"Der er ikke {quantity} styk tilbage af {product.Name}.",
                $"There aren't {quantity} left of {product.Name}.");
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

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
