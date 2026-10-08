using System.Net;
using System.Text.Json;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests.Pages;

/// <summary>
/// The public "around the shop" pages over real HTTP: contact form, FAQ,
/// brands, donations, company checkout and the support chat endpoint.
/// </summary>
[Collection("WebApp collection")]
public class EndToEndSitePagesTests(MarsvinWebAppFactory factory)
{
    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    private static SqlInboxStore Inbox => new(MarsvinWebAppFactory.ConnectionString);

    private static async Task<string> Html(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Theory]
    [InlineData("/Faq", "Ofte stillede spørgsmål")]
    [InlineData("/Maerker", "JR Farm")]
    [InlineData("/Stoet", "Støt marsvin i nød")]
    [InlineData("/Kontakt", "Skriv til os")]
    public async Task PublicPages_AreReachableWithoutLoggingIn(string path, string expectedText)
    {
        var response = await MakeClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedText, await Html(response));
    }

    [Theory]
    [InlineData("/Admin/Messages/Index")]
    [InlineData("/Admin/Donations/Index")]
    public async Task StaffInboxPages_SendAnonymousVisitorsToLogin(string path)
    {
        var response = await MakeClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ContactForm_StoresTheMessageForStaff()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var marker = $"Hej, jeg har et spørgsmål {Guid.NewGuid():N}";
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Kontakt");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Kontakt", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = "Kunde Kundesen",
            ["Input.Email"] = "kunde@example.com",
            ["Input.Topic"] = "Accessory",
            ["Input.Message"] = marker
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var stored = Assert.Single(Inbox.GetContactMessages(200), m => m.Message == marker);
        Assert.Equal(ContactTopic.Accessory, stored.Topic);
        Assert.Equal("kunde@example.com", stored.Email);
    }

    [Theory]
    [InlineData("Kunde", "not-an-email", "En besked der er lang nok.", "E-mailadressen ser forkert ud.")]
    [InlineData("Kunde", "kunde@example.com", "kort", "Beskeden skal være mellem 10 og 2000 tegn.")]
    [InlineData("", "kunde@example.com", "En besked der er lang nok.", "Udfyld dit navn.")]
    public async Task ContactForm_WithInvalidInput_ShowsTheErrorAndStoresNothing(string name, string email, string message, string expectedError)
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var before = Inbox.GetContactMessages(200).Count;
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Kontakt");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Kontakt", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = name,
            ["Input.Email"] = email,
            ["Input.Topic"] = "Other",
            ["Input.Message"] = message
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, await Html(response));
        Assert.Equal(before, Inbox.GetContactMessages(200).Count);
    }

    [Fact]
    public async Task ContactForm_WithoutAntiforgeryToken_IsRejected()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await HttpTestHelpers.GetWithToken(client, jar, "/Kontakt");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Kontakt", new()
        {
            ["Input.Name"] = "Kunde",
            ["Input.Email"] = "kunde@example.com",
            ["Input.Message"] = "En besked der er lang nok."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MoneyDonation_CanBeAnonymous_AndKeepsOnlyTheAmount()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Stoet");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Stoet", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Kind"] = "Money",
            ["Input.AmountKr"] = "7341",
            // Posted anyway (a tampered form) - a money donation has no item description.
            ["Input.ItemDescription"] = "should not be stored"
        });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var stored = Inbox.GetDonations(200).First(d => d.AmountKr == 7341);
        Assert.Equal(DonationKind.Money, stored.Kind);
        Assert.Null(stored.ItemDescription);
        Assert.Null(stored.DonorName);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("10001")]
    [InlineData("")]
    public async Task MoneyDonation_OutsideTheAllowedRange_IsRejected(string amount)
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Stoet");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Stoet", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Kind"] = "Money",
            ["Input.AmountKr"] = amount
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Vælg et beløb mellem", await Html(response));
    }

    [Fact]
    public async Task ProductDonation_NeedsADescriptionAndContactDetails()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Stoet");

        var missing = await HttpTestHelpers.PostForm(client, jar, "/Stoet", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Kind"] = "Products"
        });
        var html = await Html(missing);
        Assert.Contains("Skriv hvad du gerne vil donere.", html);
        Assert.Contains("Udfyld din e-mail", html);

        var marker = $"2 poser hø {Guid.NewGuid():N}";
        var ok = await HttpTestHelpers.PostForm(client, jar, "/Stoet", new()
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Kind"] = "Products",
            ["Input.ItemDescription"] = marker,
            ["Input.DonorName"] = "Giver Giversen",
            ["Input.DonorEmail"] = "giver@example.com"
        });
        Assert.Equal(HttpStatusCode.Found, ok.StatusCode);
        var stored = Assert.Single(Inbox.GetDonations(200), d => d.ItemDescription == marker);
        Assert.Null(stored.AmountKr);
    }

    [Fact]
    public async Task AccessoriesPage_CanBeFilteredByBrand_AndIgnoresABrandThatDoesNotExist()
    {
        var client = MakeClient();

        var filtered = await Html(await client.GetAsync("/Tilbehor?maerke=" + Uri.EscapeDataString("Savic")));
        Assert.Contains("Bur 120 x 60 cm", filtered);
        Assert.DoesNotContain("Timothy-hø, 2 kg", filtered);

        // Not echoed back anywhere, and not treated as a filter.
        var unknown = await Html(await client.GetAsync("/Tilbehor?maerke=" + Uri.EscapeDataString("<script>alert(1)</script>")));
        Assert.Contains("Timothy-hø, 2 kg", unknown);
        Assert.DoesNotContain("alert(1)", unknown);
    }

    [Fact]
    public async Task CompanyCheckout_PutsTheCompanyAndVatOnTheReceipt()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var response = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Indkøber",
            ["Input.GuestEmail"] = $"firma-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "MobilePay",
            ["Input.BuyerType"] = "Company",
            ["Input.CompanyName"] = "Testskolen ApS",
            ["Input.CompanyCvr"] = "12 34 56 74"
        });
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var receipt = await Html(await HttpTestHelpers.Get(client, jar, response.Headers.Location!.ToString()));
        Assert.Contains("Testskolen ApS", receipt);
        Assert.Contains("12345674", receipt);          // stored as digits only
        Assert.Contains("Heraf moms (25 %)", receipt);
    }

    [Theory]
    [InlineData("12345678", "Testskolen ApS", "CVR-nummeret skal være 8 cifre")]   // wrong check digit
    [InlineData("1234", "Testskolen ApS", "CVR-nummeret skal være 8 cifre")]
    [InlineData("12345674", "", "Udfyld virksomhedens navn.")]
    public async Task CompanyCheckout_WithABadCvrOrNoName_IsRejected(string cvr, string companyName, string expectedError)
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var payToken = await AddToCartAndOpenPayment(client, jar);

        var response = await HttpTestHelpers.PostForm(client, jar, "/Cart/Payment", new()
        {
            ["__RequestVerificationToken"] = payToken,
            ["Input.GuestName"] = "Indkøber",
            ["Input.GuestEmail"] = $"firma-{Guid.NewGuid():N}@example.com",
            ["Input.DeliveryMethod"] = "Pickup",
            ["Input.PaymentMethod"] = "MobilePay",
            ["Input.BuyerType"] = "Company",
            ["Input.CompanyName"] = companyName,
            ["Input.CompanyCvr"] = cvr
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, await Html(response));
    }

    // ---- Support chat: a keyword lookup inside the app, no outside service ----

    private static async Task<(HttpStatusCode Status, string Answer)> Ask(
        HttpClient client, CookieJar jar, string token, string message, string lang = "da")
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["message"] = message,
            ["lang"] = lang
        };
        var response = await HttpTestHelpers.PostForm(client, jar, "/Chat", fields);
        if (response.StatusCode != HttpStatusCode.OK) return (response.StatusCode, "");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, json.RootElement.GetProperty("answer").GetString()!);
    }

    [Fact]
    public async Task Chat_AnswersFromTheShopsOwnPages_InTheVisitorsLanguage()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, pageHtml, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Faq");
        Assert.Contains("id=\"chat\"", pageHtml); // the bubble is on public pages

        var (_, danish) = await Ask(client, jar, token, "Hvad er jeres åbningstider?");
        Assert.Contains("14-18", danish);

        var (_, english) = await Ask(client, jar, token, "What are your opening hours?", lang: "en");
        Assert.Contains("Thursday", english);

        // From the FAQ, not just the four pages.
        var (_, returns) = await Ask(client, jar, token, "Kan jeg returnere tilbehør?");
        Assert.Contains("14 dage", returns);
    }

    [Fact]
    public async Task Chat_SaysItDoesNotKnow_RatherThanGuessing()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Faq");

        var (_, answer) = await Ask(client, jar, token, "Zxqvy plomfritt wubbadub?");

        Assert.StartsWith("Det ved jeg ikke", answer);
    }

    [Fact]
    public async Task Chat_RejectsAnOverlongQuestion_AndUnderstandsEverydayWording()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Faq");

        var (_, tooLong) = await Ask(client, jar, token, new string('a', ChatModelMaxLength + 1));
        Assert.Contains("højst", tooLong);

        // Not the page's own word ("åbningstider") - found through the synonym list.
        var (_, hours) = await Ask(client, jar, token, "Hvornår har I åbent?");
        Assert.Contains("14-18", hours);

        var (_, greeting) = await Ask(client, jar, token, "Hej!");
        Assert.StartsWith("Wheek", greeting);
    }

    private const int ChatModelMaxLength = MarsvinWebExample.Pages.ChatModel.MaxQuestionLength;

    [Fact]
    public async Task Chat_WithoutAntiforgeryToken_IsRejected()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await HttpTestHelpers.GetWithToken(client, jar, "/Faq");

        var response = await HttpTestHelpers.PostForm(client, jar, "/Chat", new() { ["message"] = "Hej" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

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
}
