using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests;

/// <summary>Builds the bare minimum PageContext a PageModel needs to see a signed-in User in tests.</summary>
internal static class TestAuth
{
    public static PageContext ContextFor(int userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new PageContext
        {
            HttpContext = httpContext
        };
    }

    /// <summary>An unauthenticated PageContext - for guest-checkout tests (Cart/Payment, Cart/Confirmation no longer [Authorize]).</summary>
    public static PageContext Anonymous()
    {
        // No authenticationType passed to ClaimsIdentity - that's what makes
        // User.Identity.IsAuthenticated false, the same as a real anonymous
        // request, rather than merely having no claims.
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        // A bare DefaultHttpContext has no session wired up at all - unlike
        // a real request, which gets one from the UseSession() middleware in
        // Program.cs (an ordinary unit test never runs through that
        // pipeline). Guest checkout reads/writes HttpContext.Session
        // directly (the "GuestOrderId" receipt pass - see Payment.OnPostAsync
        // and ConfirmationModel.OnGet), so it would throw without this.
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature(new FakeSession()));

        return new PageContext
        {
            HttpContext = httpContext
        };
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }
}
