using System.Security.Claims;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await page.HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
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
