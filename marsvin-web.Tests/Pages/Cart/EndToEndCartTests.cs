using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests.Pages.Cart;

/// <summary>
/// Guest checkout: an anonymous visitor can add to cart, pay, and see their
/// own receipt, with no account at all - their cart lives in session
/// (SessionCartStore) rather than dbo.CartItems. These replace two older
/// tests that asserted the opposite (anonymous add-to-cart redirected to
/// login and silently lost the POST body) from back when guest checkout
/// wasn't supported at all.
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
    public async Task AnonymousVisitor_PostingAddToCart_ActuallyAddsIt()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        // No login at any point - establishes the session (and its
        // antiforgery cookie/token) the same way a real first visit would.
        var tilbehorPage = await HttpTestHelpers.Get(client, jar, "/Tilbehor");
        var token = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());

        var addResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = token,
            ["productId"] = "104",
            ["quantity"] = "1"
        });
        Assert.Equal(HttpStatusCode.Found, addResponse.StatusCode);
        Assert.DoesNotContain("/Account/Login", addResponse.Headers.Location!.ToString());

        var cartPage = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        var cartHtml = await cartPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("kurv er tom", cartHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestCheckout_WithNameAndEmail_CompletesAndShowsOwnReceipt()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        var tilbehorPage = await HttpTestHelpers.Get(client, jar, "/Tilbehor");
        var addToken = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());
        await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = addToken,
            ["productId"] = "104",
            ["quantity"] = "1"
        });

        var paymentPage = await HttpTestHelpers.Get(client, jar, "/Cart/Payment");
        var paymentHtml = await paymentPage.Content.ReadAsStringAsync();
        Assert.Contains("Input.GuestName", paymentHtml); // the guest-details fields actually rendered
        var payToken = CookieJar.ExtractAntiforgeryToken(paymentHtml);

        var checkoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = $"guest-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "Card",
            ["Input.CardHolder"] = "Guest Buyer",
            ["Input.CardNumber"] = "4242 4242 4242 4242",
            ["Input.Expiry"] = "12/29",
            ["Input.Cvc"] = "123"
        });
        Assert.Equal(HttpStatusCode.Found, checkoutResponse.StatusCode);
        Assert.Contains("/Cart/Confirmation/", checkoutResponse.Headers.Location!.ToString());

        // The guest can see the receipt for the order they just placed...
        var receiptUrl = checkoutResponse.Headers.Location!.ToString();
        var receiptResponse = await HttpTestHelpers.Get(client, jar, receiptUrl);
        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
        var receiptHtml = await receiptResponse.Content.ReadAsStringAsync();
        Assert.Contains("Tak for din bestilling", receiptHtml);

        // ...but the cart that order came from is now empty (cleared on checkout).
        var cartAfter = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        Assert.Contains("kurv er tom", await cartAfter.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestCheckout_AnotherAnonymousSessionCannotViewTheReceiptByGuessingTheOrderId()
    {
        // IDOR check for the guest path specifically: FindForUser's ownership
        // check doesn't exist for a guest (no account to own anything with) -
        // ConfirmationModel falls back to a one-time id stamped into the
        // buyer's own session instead (see Payment.OnPostAsync). A second,
        // unrelated anonymous visitor must not be able to read it just by
        // knowing or guessing the orderId.
        var buyerClient = MakeClient();
        var buyerJar = new CookieJar();
        var tilbehorPage = await HttpTestHelpers.Get(buyerClient, buyerJar, "/Tilbehor");
        var addToken = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());
        await HttpTestHelpers.PostForm(buyerClient, buyerJar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = addToken,
            ["productId"] = "104",
            ["quantity"] = "1"
        });
        var paymentPage = await HttpTestHelpers.Get(buyerClient, buyerJar, "/Cart/Payment");
        var payToken = CookieJar.ExtractAntiforgeryToken(await paymentPage.Content.ReadAsStringAsync());
        var checkoutResponse = await HttpTestHelpers.PostForm(buyerClient, buyerJar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Real Buyer",
            ["Input.GuestEmail"] = $"realbuyer-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "Card",
            ["Input.CardHolder"] = "Real Buyer",
            ["Input.CardNumber"] = "4242 4242 4242 4242",
            ["Input.Expiry"] = "12/29",
            ["Input.Cvc"] = "123"
        });
        var receiptUrl = checkoutResponse.Headers.Location!.ToString();

        // A different anonymous visitor, same server, no session overlap at all.
        var snooperClient = MakeClient();
        var snooperJar = new CookieJar();
        var snooperResponse = await HttpTestHelpers.Get(snooperClient, snooperJar, receiptUrl);

        Assert.Equal(HttpStatusCode.NotFound, snooperResponse.StatusCode);
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

    [Fact]
    public async Task ShoppingAsAGuestThenRegistering_CarriesTheCartIntoTheNewAccount()
    {
        // The real end-to-end path for ConfirmLoginModel's merge logic -
        // same browser session throughout (one CookieJar), going from
        // SessionCartStore (anonymous) to SqlCartStore (signed in) exactly
        // the way Program.cs's ICartStore registration would for a real
        // visitor, not a hand-constructed FakeCartStore like the
        // PaymentModelTests/ConfirmLoginModelTests unit tests use.
        var client = MakeClient();
        var jar = new CookieJar();

        var tilbehorPage = await HttpTestHelpers.Get(client, jar, "/Tilbehor");
        var addToken = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());
        await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = addToken,
            ["productId"] = "104",
            ["quantity"] = "1"
        });

        var email = $"mergecart-{Guid.NewGuid():N}@example.com";
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Merge Cart Test",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);

        var cartAfterLogin = await HttpTestHelpers.Get(client, jar, "/Cart/Index");
        var html = await cartAfterLogin.Content.ReadAsStringAsync();
        Assert.DoesNotContain("kurv er tom", html, StringComparison.OrdinalIgnoreCase);
    }
}
