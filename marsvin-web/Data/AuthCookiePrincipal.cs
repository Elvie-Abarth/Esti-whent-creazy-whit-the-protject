using System.Security.Claims;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MarsvinWebExample.Data;

/// <summary>
/// What goes into the auth cookie, and the per-request check that it's
/// still true. The cookie is a snapshot taken at sign-in - on its own it
/// would keep saying "Admin" (or "exists at all") for its whole 8-hour
/// sliding lifetime, no matter what /Admin/Users did to the account since.
/// </summary>
public static class AuthCookiePrincipal
{
    public static ClaimsPrincipal Build(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Wired up as the cookie handler's OnValidatePrincipal in Program.cs, so
    /// it runs on every request carrying an auth cookie, before any
    /// [Authorize] check sees the principal. A deleted or deactivated account
    /// is signed out on the spot; a changed role (or name/email) replaces the
    /// stale claims, so a demoted Admin loses /Admin/* on their very next
    /// request rather than whenever the cookie happens to expire.
    /// </summary>
    public static async Task RevalidateAsync(CookieValidatePrincipalContext context)
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountStore>();
        var user = int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? users.FindById(userId)
            : null;

        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var principal = context.Principal!;
        if (principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString() &&
            principal.FindFirstValue(ClaimTypes.Name) == user.DisplayName &&
            principal.FindFirstValue(ClaimTypes.Email) == user.Email)
        {
            return;
        }

        context.ReplacePrincipal(Build(user));
        // Re-issues the cookie with the fresh claims, instead of re-doing
        // this same replacement on every request until it expires.
        context.ShouldRenew = true;
    }
}
