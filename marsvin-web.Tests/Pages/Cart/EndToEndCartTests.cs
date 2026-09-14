using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace MarsvinWebExample.Tests.Pages.Cart;

/// <summary>
/// Regression coverage for a real bug: an anonymous visitor clicking
/// "add to cart" got redirected to login, and the add silently vanished -
/// [Authorize]'s redirect-to-login only replays the original request as a
/// GET, so a POST's form body (productId, quantity) never survives the
/// round trip. Every earlier cart test called OnPostAdd directly on an
/// already-"logged in" PageModel, so none of them could have caught this -
/// it only shows up when driving the real HTTP redirect.
/// </summary>
[Collection("WebApp collection")]
public class EndToEndCartTests(MarsvinWebAppFactory factory)
{
    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    [Fact]
    public async Task AnonymousVisitor_PostingAddToCart_IsRedirectedToLoginRatherThanSilentlyDroppingTheItem()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        // No login first - this is the exact click an anonymous visitor makes.
        var response = await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["productId"] = "104",
            ["quantity"] = "1"
        });

        // [Authorize] intercepts before the handler ever runs, so this never
        // reaches OnPostAdd - the item is never silently "added and lost".
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AfterLoggingInFromThatRedirect_TheReturnUrlDoesNotReplayTheAddAsAPost()
    {
        // Documents the actual failure mode, not just the redirect: the
        // ReturnUrl the login page receives is the original request's path
        // and query only. Even a well-behaved login flow that honours it
        // lands on a GET to that URL, which cannot re-trigger OnPostAdd
        // (a POST-only handler) or resupply productId/quantity. This is
        // exactly why the UI now shows a "log in to buy" link instead of a
        // real add-to-cart form for anonymous visitors, rather than trying
        // to make the POST survive the redirect.
        var client = MakeClient();
        var jar = new CookieJar();

        var addAttempt = await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["productId"] = "104",
            ["quantity"] = "1"
        });
        var returnUrl = QueryHelpers.ParseQuery(addAttempt.Headers.Location!.Query)["ReturnUrl"].ToString();

        Assert.Equal("/Cart/Index?handler=Add", returnUrl);

        // Register through that exact return URL, the way a real login would.
        // Registering no longer signs in immediately - it redirects to
        // CheckEmail, and the returnUrl travels along inside the emailed
        // confirmation link instead (the same as Login already does), landing
        // back on returnUrl only once that link is opened.
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register?returnUrl=" + Uri.EscapeDataString(returnUrl));
        var email = $"anon-cart-{Guid.NewGuid():N}@example.com";
        var registerResponse = await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = returnUrl,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Anon Cart",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        Assert.Equal(
            "/Account/CheckEmail?purpose=register&returnUrl=" + Uri.EscapeDataString(returnUrl),
            registerResponse.Headers.Location!.ToString());

        // Opening the confirmation link is what actually signs the session in
        // and lands on returnUrl - a GET, per HTTP, which cannot carry the
        // original POST body, so the cart ends up empty.
        var landingPage = await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
        Assert.Equal(returnUrl, landingPage.Headers.Location!.ToString());

        var cartPage = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        var cartHtml = await cartPage.Content.ReadAsStringAsync();
        Assert.Contains("kurv er tom", cartHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoggedInCustomer_AddToCartWorks()
    {
        // The control case: once actually authenticated first, add-to-cart
        // behaves as expected end to end over real HTTP.
        var client = MakeClient();
        var jar = new CookieJar();
        var email = $"real-buyer-{Guid.NewGuid():N}@example.com";

        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Real Buyer",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);

        var cartPage = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        var addToken = CookieJar.ExtractAntiforgeryToken(await cartPage.Content.ReadAsStringAsync());

        var addResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = addToken,
            ["productId"] = "104",
            ["quantity"] = "1"
        });
        Assert.Equal(HttpStatusCode.Found, addResponse.StatusCode);

        var cartAfter = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        var htmlAfter = await cartAfter.Content.ReadAsStringAsync();
        Assert.DoesNotContain("kurv er tom", htmlAfter, StringComparison.OrdinalIgnoreCase);
    }
}
