using System.Globalization;
using System.Security.Claims;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MarsvinWebExample.Data;

/// <summary>
/// What goes into the auth cookie, and the per-request check that it's
/// still true. The cookie is a snapshot taken at sign-in - on its own it
/// would keep saying "Admin" (or "exists at all") for as long as the session
/// is kept alive, no matter what /Admin/Users did to the account since.
/// </summary>
public static class AuthCookiePrincipal
{
    /// <summary>
    /// The account's security stamp at the moment of sign-in. The stamp in
    /// dbo.Users is replaced whenever the password changes (and by "log out
    /// everywhere"), so every cookie still carrying the old one stops working
    /// on its next request - including one a thief is holding.
    /// </summary>
    public const string StampClaim = "marsvin:stamp";

    /// <summary>When this session was actually signed in (Unix seconds) - not when the cookie was last renewed.</summary>
    public const string SignedInAtClaim = "marsvin:signed-in-at";

    /// <summary>
    /// How long a session may sit unused before it ends (the cookie's own
    /// sliding expiry - see Program.cs). Short on purpose: the realistic risk
    /// is a tab left open on a shared computer, and for a staff account that
    /// tab can see every customer's orders and addresses.
    /// </summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The longest a session may live, however active it is. The idle limit
    /// above is sliding - every request renews it - so without this a session
    /// used at least once every half hour would never end. Eight hours is
    /// OWASP's own example for an application used through a working day.
    /// </summary>
    public static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(8);

    // The cookie handler only slides the expiry once half the idle window
    // has passed, which would make "30 minutes" mean anything from 15 to 30.
    // Renewing as soon as the cookie is a minute old keeps the limit honest:
    // 30 minutes from the last request, give or take that minute.
    private static readonly TimeSpan RenewAfter = TimeSpan.FromMinutes(1);

    public static ClaimsPrincipal Build(ApplicationUser user, DateTimeOffset signedInAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(StampClaim, user.SecurityStamp),
            new(SignedInAtClaim, signedInAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>When the session behind <paramref name="principal"/> was signed in, or null if it doesn't say.</summary>
    public static DateTimeOffset? SignedInAt(ClaimsPrincipal? principal) =>
        long.TryParse(principal?.FindFirstValue(SignedInAtClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>
    /// Wired up as the cookie handler's OnValidatePrincipal in Program.cs, so
    /// it runs on every request carrying an auth cookie, before any
    /// [Authorize] check sees the principal. The session is ended on the spot
    /// when the account is gone or deactivated, when its security stamp has
    /// changed since sign-in (the password was changed, or "log out
    /// everywhere" was used), or when it is older than
    /// <see cref="MaxSessionAge"/>. A changed role (or name/email) replaces
    /// the stale claims, so a demoted Admin loses /Admin/* on their very next
    /// request rather than whenever the cookie happens to expire.
    /// </summary>
    public static async Task RevalidateAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var users = services.GetRequiredService<IUserAccountStore>();
        var now = (services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

        var principal = context.Principal;
        var user = int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? users.FindById(userId)
            : null;
        var signedInAt = SignedInAt(principal);

        // A cookie with no stamp or no sign-in time (one issued before these
        // existed) fails both checks too - it simply has to log in again.
        if (user is null || !user.IsActive
            || !string.Equals(principal!.FindFirstValue(StampClaim), user.SecurityStamp, StringComparison.Ordinal)
            || signedInAt is null || now - signedInAt.Value > MaxSessionAge)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (principal.FindFirstValue(ClaimTypes.Role) == user.Role.ToString() &&
            principal.FindFirstValue(ClaimTypes.Name) == user.DisplayName &&
            principal.FindFirstValue(ClaimTypes.Email) == user.Email)
        {
            if (context.Properties.IssuedUtc is { } issued && now - issued > RenewAfter)
                context.ShouldRenew = true;
            return;
        }

        // Fresh claims, same session: the sign-in time is carried over, so
        // an edit to the account never extends how long the session may live.
        context.ReplacePrincipal(Build(user, signedInAt.Value));
        // Re-issues the cookie with the fresh claims, instead of re-doing
        // this same replacement on every request until it expires.
        context.ShouldRenew = true;
    }
}
