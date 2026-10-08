using System.Net;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
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

    // The two below post the card fields *blank*, the way a real browser
    // submits an untouched input - model binding turns that into null, which
    // PaymentModelTests (calling OnPostAsync directly, with Input built by
    // hand) can't reproduce.
    [Fact]
    public async Task GuestCheckout_WithMobilePayAndBlankCardFields_Completes()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var checkoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = $"guest-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "MobilePay",
            ["Input.CardHolder"] = "",
            ["Input.CardNumber"] = "",
            ["Input.Expiry"] = "",
            ["Input.Cvc"] = ""
        });

        Assert.Equal(HttpStatusCode.Found, checkoutResponse.StatusCode);
        Assert.Contains("/Cart/Confirmation/", checkoutResponse.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GuestCheckout_WithCardAndBlankCardFields_RedisplaysTheFormWithErrors()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var checkoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = $"guest-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "Card",
            ["Input.CardHolder"] = "",
            ["Input.CardNumber"] = "",
            ["Input.Expiry"] = "",
            ["Input.Cvc"] = ""
        });

        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
        Assert.Contains("Kortnummeret ser forkert ud.", await checkoutResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddingToCartFromAProductPage_AsksWhetherToGoToTheCartOrKeepShopping()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        var tilbehorPage = await HttpTestHelpers.Get(client, jar, "/Tilbehor");
        var token = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());
        var addResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = token,
            ["productId"] = "104",
            ["quantity"] = "1",
            ["returnUrl"] = "/Tilbehor"
        });
        Assert.Equal("/Tilbehor", addResponse.Headers.Location!.ToString());

        var backOnTheShopPage = await (await HttpTestHelpers.Get(client, jar, "/Tilbehor")).Content.ReadAsStringAsync();
        Assert.Contains("toast--ask", backOnTheShopPage);
        Assert.Contains("Gå til kurven", backOnTheShopPage);
        Assert.Contains("Fortsæt med at handle", backOnTheShopPage);

        // "Keep shopping" just loads the same page again - asked once, not on every page view.
        var afterChoosingToKeepShopping = await (await HttpTestHelpers.Get(client, jar, "/Tilbehor")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("toast--ask", afterChoosingToKeepShopping);
    }

    [Fact]
    public async Task PaymentPage_ShowsPriceDestinationAndDeliveryForEveryCarrier()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await AddToCartAndOpenPayment(client, jar);

        // Decoded: Razor HTML-encodes non-ASCII letters in anything written from C# ("ø" -> "&#xF8;").
        var html = WebUtility.HtmlDecode(
            await (await HttpTestHelpers.Get(client, jar, "/Cart/Payment")).Content.ReadAsStringAsync());

        // Product 104 is 120 g - the cheapest bracket with each carrier.
        Assert.Contains("55 kr.", html); // PostNord
        Assert.Contains("39 kr.", html); // DAO
        Assert.Contains("Samlet vægt", html);
        Assert.Contains("120 g", html);
        Assert.Contains("1 pakke", html);
        Assert.Contains("Forventet levering", html);
        Assert.Contains("Din dør, på adressen ovenfor", html);
        Assert.Contains("Udleveringssted nær dig", html);
        // The order summary above the pay button: what it comes to with each choice.
        Assert.Contains("At betale", html);
        Assert.Contains("100 kr.", html); // 45 kr. + 55 kr. PostNord
        Assert.Contains("84 kr.", html);  // 45 kr. + 39 kr. DAO
    }

    [Fact]
    public async Task GuestCheckout_WithShipping_ChargesTheShippingAndShowsItOnTheReceipt()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var checkoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = $"guest-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Shipping",
            ["Input.ShippingAddress"] = "Testvej 1, 4000 Roskilde",
            ["Input.ShippingCarrier"] = "DaoPakkeshop",
            ["Input.PaymentMethod"] = "MobilePay"
        });
        Assert.Equal(HttpStatusCode.Found, checkoutResponse.StatusCode);

        var receiptHtml = WebUtility.HtmlDecode(
            await (await HttpTestHelpers.Get(client, jar, checkoutResponse.Headers.Location!.ToString()))
                .Content.ReadAsStringAsync());
        // 45 kr. for the item + 39 kr. DAO shipping for 120 g.
        Assert.Contains("fragt: 39 kr.", receiptHtml);
        Assert.Contains("84 kr.", receiptHtml);
        Assert.Contains("DAO Pakkeshop nærmest Testvej 1, 4000 Roskilde", receiptHtml);
        Assert.Contains("Forventet levering", receiptHtml);
        Assert.Contains("Samlet vægt: 120 g, sendes som 1 pakke", receiptHtml);
        Assert.Contains("Status: Ordre modtaget", receiptHtml);
        // Still a guest - offered an account to keep the order under.
        Assert.Contains("Vil du følge denne ordre?", receiptHtml);
    }

    [Fact]
    public async Task GuestWhoRegistersAfterCheckout_WithTheSameEmail_GetsTheOrderInTheirHistoryWithItsStatus()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = $"guest-then-account-{Guid.NewGuid():N}@example.com";
        var orderId = await CheckOutAsGuest(client, jar, email);

        await Register(client, jar, email);

        var profileHtml = await (await HttpTestHelpers.Get(client, jar, "/Account/Profile")).Content.ReadAsStringAsync();
        Assert.Contains($"Ordre #{orderId}", profileHtml);
        Assert.Contains("Status: Ordre modtaget", profileHtml);

        // Staff move it along (Admin/Orders) - the customer sees the new status.
        Assert.True(new SqlOrderStore(MarsvinWebAppFactory.ConnectionString).UpdateStatus(orderId, OrderStatus.Sent));
        profileHtml = await (await HttpTestHelpers.Get(client, jar, "/Account/Profile")).Content.ReadAsStringAsync();
        Assert.Contains("Status: Klar til afhentning", profileHtml);
    }

    [Fact]
    public async Task GuestOrder_IsNotHandedToAnAccountWithADifferentEmail()
    {
        // Same browser session, but not the address the order was placed
        // with - e.g. the next person at a shared computer signing up.
        var client = MakeClient();
        var jar = new CookieJar();
        var orderId = await CheckOutAsGuest(client, jar, $"guest-{Guid.NewGuid():N}@example.com");

        await Register(client, jar, $"someone-else-{Guid.NewGuid():N}@example.com");

        var profileHtml = await (await HttpTestHelpers.Get(client, jar, "/Account/Profile")).Content.ReadAsStringAsync();
        Assert.DoesNotContain($"Ordre #{orderId}", profileHtml);
        Assert.Null(new SqlOrderStore(MarsvinWebAppFactory.ConnectionString).FindById(orderId)!.UserId);
    }

    private static async Task<int> CheckOutAsGuest(HttpClient client, CookieJar jar, string email)
    {
        var payToken = await AddToCartAndOpenPayment(client, jar);
        var checkoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = email,
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "MobilePay"
        });
        return int.Parse(checkoutResponse.Headers.Location!.ToString().Split('/').Last());
    }

    private static async Task Register(HttpClient client, CookieJar jar, string email)
    {
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Guest Buyer",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
    }

    /// <summary>Puts one accessory in a guest cart and returns the antiforgery token from the payment page.</summary>
    private static async Task<string> AddToCartAndOpenPayment(HttpClient client, CookieJar jar)
    {
        var tilbehorPage = await HttpTestHelpers.Get(client, jar, "/Tilbehor");
        var addToken = CookieJar.ExtractAntiforgeryToken(await tilbehorPage.Content.ReadAsStringAsync());
        await HttpTestHelpers.PostForm(client, jar, "/Cart/Index?handler=Add", new()
        {
            ["__RequestVerificationToken"] = addToken,
            ["productId"] = "104",
            ["quantity"] = "1"
        });

        var paymentPage = await HttpTestHelpers.Get(client, jar, "/Cart/Payment");
        return CookieJar.ExtractAntiforgeryToken(await paymentPage.Content.ReadAsStringAsync());
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
