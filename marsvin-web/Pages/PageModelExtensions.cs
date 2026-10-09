using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages;

/// <summary>
/// Small pieces every authenticated PageModel needed a copy of - collapsed
/// here instead of staying duplicated across Register/ConfirmLogin/Profile
/// (sign-in) and six page models (CurrentUserId).
/// </summary>
public static class PageModelExtensions
{
    /// <summary>
    /// The signed-in user's id, from the same claim SignInAsync below sets.
    /// Only ever called on a page that requires authentication, so the claim
    /// is guaranteed present - same as every call site already assumed.
    /// </summary>
    public static int CurrentUserId(this PageModel page) =>
        int.Parse(page.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// CurrentUserId, but safe to call on a page a guest can also reach
    /// (Cart/*): null rather than throwing when nobody's signed in. Pass
    /// straight through to IOrderStore.Checkout's own nullable userId -
    /// never used as a real id anywhere, since nothing is actually "the
    /// guest's account".
    /// </summary>
    public static int? CurrentUserIdOrNull(this PageModel page) =>
        page.User.Identity?.IsAuthenticated == true ? page.CurrentUserId() : null;

    /// <summary>
    /// CurrentUserId, but 0 rather than throwing when nobody's signed in -
    /// for ICartStore calls specifically, where the int userId parameter is
    /// meaningless anyway once the actual implementation turns out to be
    /// SessionCartStore (see Program.cs's ICartStore registration): it never
    /// reads the value, only SqlCartStore does, and only ever for a real
    /// signed-in customer.
    /// </summary>
    public static int CurrentUserIdOrZero(this PageModel page) =>
        page.CurrentUserIdOrNull() ?? 0;

    /// <summary>
    /// The signed-in user's display name, straight from the same claim
    /// SignInAsync below sets - cheaper than a round trip through
    /// IUserAccountStore.FindById just to label an IAuditLogStore entry with
    /// who did it.
    /// </summary>
    public static string CurrentDisplayName(this PageModel page) =>
        page.User.FindFirstValue(ClaimTypes.Name) ?? "?";

    /// <summary>
    /// Issues the auth cookie for <paramref name="user"/>. Used after a
    /// password-confirmed registration, an email-confirmed login, and a
    /// profile update (to refresh the name/email claims immediately rather
    /// than waiting for the next login).
    /// </summary>
    public static async Task SignInAsync(this PageModel page, ApplicationUser user)
    {
        // A real sign-in starts the session's clock now. Re-issuing the cookie
        // for the account that is already signed in (after a profile update)
        // keeps the original sign-in time instead - otherwise saving your
        // profile once a day would get around the maximum session length.
        var sameUser = page.User.Identity?.IsAuthenticated == true
            && page.User.FindFirstValue(ClaimTypes.NameIdentifier) == user.UserId.ToString();
        var signedInAt = (sameUser ? AuthCookiePrincipal.SignedInAt(page.User) : null) ?? DateTimeOffset.UtcNow;

        await page.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, AuthCookiePrincipal.Build(user, signedInAt));
    }

    /// <summary>
    /// Empties the server-side session (a guest cart, a guest's receipt pass)
    /// when a login ends, so nothing of the visit is left for whoever uses
    /// the browser next. Through the feature rather than HttpContext.Session,
    /// which throws when a request has no session at all.
    /// </summary>
    public static void ClearServerSession(this PageModel page) =>
        page.HttpContext.Features.Get<ISessionFeature>()?.Session?.Clear();

    /// <summary>
    /// Folds a just-signed-in user's guest cart into their account's real
    /// one, so browsing as a guest and then logging in partway through
    /// doesn't lose what was already added. Call right after SignInAsync,
    /// in the two places a request actually goes from anonymous to signed
    /// in mid-request (ConfirmLogin, VerifyTotp) - by then this request's
    /// own ICartStore has already been resolved against the still-anonymous
    /// HttpContext.User from before SignInAsync ran (see Program.cs), so it
    /// IS the guest's SessionCartStore; accountCart is SqlCartStore,
    /// injected separately and directly for exactly this reason.
    /// A no-op (not even a Clear()) when the guest cart was already empty -
    /// the common case, most people logging in weren't just shopping anonymously first.
    /// </summary>
    public static void MergeGuestCartIntoAccount(ICartStore guestCart, SqlCartStore accountCart, int userId)
    {
        var guestLines = guestCart.GetLines(0);
        if (guestLines.Count == 0) return;

        var existingProductIds = accountCart.GetLines(userId).Select(l => l.ProductId).ToHashSet();
        foreach (var line in guestLines)
        {
            // AddOrIncrement blindly sums quantities, which is right for an
            // accessory (5 already in the account's cart + 3 from the guest
            // cart really is 8) but would be wrong for an animal - a guinea
            // pig's "quantity" must stay 1 even in the rare case where the
            // same one somehow ended up in both carts (the two carts were
            // never meant to be shopped in at once, but nothing stops it).
            if (line.IsAnimal && existingProductIds.Contains(line.ProductId)) continue;
            accountCart.AddOrIncrement(userId, line.ProductId, line.Quantity);
        }
        guestCart.Clear(0);
    }

    /// <summary>
    /// A guest who creates an account (or logs in) right after checking out
    /// gets that order moved into the account, so it shows up - with its
    /// status - in their order history. Two things both have to hold: this
    /// browser session is the one that placed the order (the same
    /// "GuestOrderId" stamp Cart/Confirmation trusts for showing a guest
    /// their receipt), and the order was placed with the account's own email
    /// address (checked in IOrderStore.ClaimGuestOrder). Either alone isn't
    /// enough - someone else signing up on a shared computer shouldn't
    /// inherit the previous person's order, address and all.
    /// </summary>
    public static void ClaimGuestOrderIntoAccount(this PageModel page, IOrderStore orders, ApplicationUser user)
    {
        if (user.Role != UserRole.Customer) return;
        // No session at all on a PageModel constructed directly in a unit test.
        if (page.HttpContext.Features.Get<ISessionFeature>() is null) return;

        var guestOrderId = page.HttpContext.Session.GetInt32("GuestOrderId");
        if (guestOrderId is null) return;

        if (orders.ClaimGuestOrder(guestOrderId.Value, user.UserId, user.Email))
            page.HttpContext.Session.Remove("GuestOrderId");
    }

    // Deliberately not PageModel.Url.IsLocalUrl: that needs an IUrlHelper wired up
    // through the full request pipeline, which a PageModel constructed directly in
    // a unit test doesn't have (Url is null there), so it throws where this doesn't.
    // Same local-path shape Url.IsLocalUrl checks: exactly one leading slash - not
    // "//host/evil" or "/\host/evil", either of which a browser can treat as
    // protocol-relative and follow off-site.
    public static bool IsSafeLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
}
