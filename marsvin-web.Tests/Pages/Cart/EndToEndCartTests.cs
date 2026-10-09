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
        Assert.Contains("Kortnummeret skal have 12-19 cifre.", await checkoutResponse.Content.ReadAsStringAsync());
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
            ["Input.Phone"] = "12 34 56 78",
            ["Input.PaymentMethod"] = "MobilePay"
        });
        Assert.Equal(HttpStatusCode.Found, checkoutResponse.StatusCode);

        var receiptHtml = WebUtility.HtmlDecode(
            await (await HttpTestHelpers.Get(client, jar, checkoutResponse.Headers.Location!.ToString()))
                .Content.ReadAsStringAsync());
        // 45 kr. for the item + 39 kr. DAO shipping for 120 g.
        Assert.Contains("<dd>39 kr.</dd>", receiptHtml);
        Assert.Contains("84 kr.", receiptHtml);
        Assert.Contains("Sendes med DAO Pakkeshop", receiptHtml);
        Assert.Contains("Udleveringssted nær Testvej 1, 4000 Roskilde", receiptHtml);
        Assert.Contains("Forventet levering", receiptHtml);
        Assert.Contains("120 g i 1 pakke", receiptHtml);
        Assert.Contains(">Ordre modtaget</li>", receiptHtml);
        Assert.Contains("<dd>12 34 56 78</dd>", receiptHtml);
        Assert.Contains("<dd>Guest Buyer</dd>", receiptHtml);
        // Still a guest - offered an account to keep the order under.
        Assert.Contains("Vil du følge denne ordre?", receiptHtml);
    }

    [Theory]
    [InlineData("", "Angiv et telefonnummer")]          // shipping needs one
    [InlineData("call me maybe", "Telefonnummeret ser forkert ud.")]
    [InlineData("1234", "Telefonnummeret ser forkert ud.")]
    public async Task Shipping_WithoutAUsablePhoneNumber_RedisplaysTheFormWithAnError(string phone, string expectedError)
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var response = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Guest Buyer",
            ["Input.GuestEmail"] = $"guest-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Shipping",
            ["Input.ShippingAddress"] = "Testvej 1, 4000 Roskilde",
            ["Input.ShippingCarrier"] = "PostNord",
            ["Input.Phone"] = phone,
            ["Input.PaymentMethod"] = "MobilePay"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Pickup_WithoutAPhoneNumber_IsFine()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        // CheckOutAsGuest posts a pickup order with no Input.Phone at all.
        var orderId = await CheckOutAsGuest(client, jar, $"guest-{Guid.NewGuid():N}@example.com");

        Assert.Null(new SqlOrderStore(MarsvinWebAppFactory.ConnectionString).FindById(orderId)!.ContactPhone);
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

    private static SqlOrderStore Orders => new(MarsvinWebAppFactory.ConnectionString);

    private static int StockOf(int productId) =>
        new SqlCatalog(MarsvinWebAppFactory.ConnectionString).Accessories.Single(p => p.ProductId == productId).StockQuantity;

    [Fact]
    public async Task CartPage_ShowsDeliveryCostFreeShippingProgressAndThatNothingIsReserved()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await AddToCartAndOpenPayment(client, jar); // one of product 104: 45 kr., 120 g

        var html = WebUtility.HtmlDecode(await (await HttpTestHelpers.Get(client, jar, "/Cart/Index")).Content.ReadAsStringAsync());

        Assert.Contains("fra 39 kr.", html);                 // cheapest carrier for 120 g
        Assert.Contains("for 454 kr. mere", html);           // 499 - 45 to free shipping
        Assert.Contains("ikke reserveret", html);
        Assert.Contains("14 dage på tilbehør", html);
    }

    [Fact]
    public async Task GuestCanCancelTheirOwnOrder_WhileItIsOnlyReceived_AndTheStockComesBackOnce()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var stockBefore = StockOf(104);
        var orderId = await CheckOutAsGuest(client, jar, $"guest-{Guid.NewGuid():N}@example.com");
        Assert.Equal(stockBefore - 1, StockOf(104));

        var (_, receiptHtml, token) = await HttpTestHelpers.GetWithToken(client, jar, $"/Cart/Confirmation/{orderId}");
        Assert.Contains("Annullér denne ordre", receiptHtml);
        var cancelResponse = await HttpTestHelpers.PostForm(client, jar, $"/Cart/Confirmation/{orderId}?handler=Cancel", new()
        {
            ["__RequestVerificationToken"] = token
        });
        Assert.Equal(HttpStatusCode.Found, cancelResponse.StatusCode);

        Assert.Equal(OrderStatus.Cancelled, Orders.FindById(orderId)!.Status);
        Assert.Equal(stockBefore, StockOf(104));

        // A second cancel (double click, replayed request) changes nothing more.
        await HttpTestHelpers.PostForm(client, jar, $"/Cart/Confirmation/{orderId}?handler=Cancel", new()
        {
            ["__RequestVerificationToken"] = token
        });
        Assert.Equal(stockBefore, StockOf(104));

        var afterHtml = await (await HttpTestHelpers.Get(client, jar, $"/Cart/Confirmation/{orderId}")).Content.ReadAsStringAsync();
        Assert.Contains("Denne ordre er annulleret", afterHtml);
        Assert.DoesNotContain("Annullér denne ordre", afterHtml);
    }

    [Fact]
    public async Task AnotherSession_CannotCancelSomeoneElsesGuestOrder()
    {
        var client = MakeClient();
        var ownerJar = new CookieJar();
        var orderId = await CheckOutAsGuest(client, ownerJar, $"guest-{Guid.NewGuid():N}@example.com");

        var strangerJar = new CookieJar();
        var (_, _, strangerToken) = await HttpTestHelpers.GetWithToken(client, strangerJar, "/Account/Register");
        var response = await HttpTestHelpers.PostForm(client, strangerJar, $"/Cart/Confirmation/{orderId}?handler=Cancel", new()
        {
            ["__RequestVerificationToken"] = strangerToken
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrderStatus.Placed, Orders.FindById(orderId)!.Status);
    }

    [Fact]
    public async Task OnceStaffHaveStartedOnIt_TheBuyerCanNoLongerCancel_ButStaffStillCanUntilItIsSent()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var stockBefore = StockOf(104);
        var orderId = await CheckOutAsGuest(client, jar, $"guest-{Guid.NewGuid():N}@example.com");

        Assert.True(Orders.UpdateStatus(orderId, OrderStatus.Processing));
        Assert.False(Orders.Cancel(orderId, OrderStatus.Placed));      // the buyer's limit
        Assert.Equal(OrderStatus.Processing, Orders.FindById(orderId)!.Status);

        Assert.True(Orders.UpdateStatus(orderId, OrderStatus.Sent, "AB123456789DK"));
        Assert.Equal("AB123456789DK", Orders.FindById(orderId)!.TrackingNumber);
        Assert.False(Orders.Cancel(orderId, OrderStatus.Processing));  // staff's limit - it's with the carrier now
        Assert.Equal(stockBefore - 1, StockOf(104));
    }

    [Fact]
    public async Task ACancelledOrder_CanNeverBeMovedToAnotherStatusAgain()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var orderId = await CheckOutAsGuest(client, jar, $"guest-{Guid.NewGuid():N}@example.com");
        Assert.True(Orders.Cancel(orderId, OrderStatus.Processing));

        Assert.False(Orders.UpdateStatus(orderId, OrderStatus.Sent));
        Assert.False(Orders.UpdateStatus(orderId, OrderStatus.Cancelled)); // never through UpdateStatus at all
        Assert.Equal(OrderStatus.Cancelled, Orders.FindById(orderId)!.Status);
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
