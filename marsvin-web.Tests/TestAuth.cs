using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
}
