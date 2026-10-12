using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages;
using MarsvinWebExample.Tests.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarsvinWebExample.Tests.Pages;

/// <summary>The thank-you email a donation sends - who gets one, and what it does and doesn't repeat.</summary>
[Collection("SqlCatalog collection")]
public class StoetModelTests(SqlCatalogFixture fixture)
{
    // languageCookie is what lang-toggle.js leaves in the browser ("da"/"en").
    private (StoetModel Model, RecordingEmailSender Email) MakeModel(IEmailSender? sender = null, string? languageCookie = null)
    {
        var email = new RecordingEmailSender();
        var httpContext = new DefaultHttpContext();
        if (languageCookie is not null)
            httpContext.Request.Headers.Cookie = $"{SiteLanguage.CookieName}={languageCookie}";
        var model = new StoetModel(
            new SqlInboxStore(fixture.ConnectionString), new AlwaysPassRecaptchaVerifier(),
            sender ?? email, NullLogger<StoetModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
        return (model, email);
    }

    [Fact]
    public async Task MoneyDonation_WithAnEmail_SendsAThankYouWithTheAmount()
    {
        var (model, email) = MakeModel();
        model.Input = new StoetModel.InputModel
        {
            Kind = DonationKind.Money, AmountKr = 75, DonorName = "Giver Giversen", DonorEmail = "giver@example.com"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("giver@example.com", sent.ToEmail);
        Assert.Equal("Tak for din donation til Marsvin", sent.Subject);
        Assert.Contains("Hej Giver Giversen,", sent.Body);
        Assert.Contains("Tusind tak", sent.Body);
        Assert.Contains("75 kr.", sent.Body);
        Assert.Contains("ikke trukket noget", sent.Body);
    }

    [Fact]
    public async Task ADonationMadeWithThePageInEnglish_GetsTheThankYouInEnglish()
    {
        var (model, email) = MakeModel(languageCookie: "en");
        model.Input = new StoetModel.InputModel
        {
            Kind = DonationKind.Money, AmountKr = 75, DonorName = "Giver Giversen", DonorEmail = "giver@example.com"
        };

        await model.OnPostAsync();

        var sent = Assert.Single(email.Sent);
        Assert.True(Assert.Single(email.SentInEnglish));     // the footer follows the text
        Assert.Equal("Thank you for your donation to Marsvin", sent.Subject);
        Assert.Contains("Hello Giver Giversen,", sent.Body);
        Assert.Contains("donation of 75 kr.", sent.Body);
        Assert.DoesNotContain("Tusind tak", sent.Body);
    }

    [Fact]
    public async Task AnUnknownLanguageValue_FallsBackToDanish()
    {
        var (model, email) = MakeModel(languageCookie: "xx");
        model.Input = new StoetModel.InputModel
        {
            Kind = DonationKind.Money, AmountKr = 75, DonorEmail = "giver@example.com"
        };

        await model.OnPostAsync();

        Assert.Equal("Tak for din donation til Marsvin", Assert.Single(email.Sent).Subject);
    }

    [Fact]
    public async Task MoneyDonation_WithoutAnEmail_SendsNothing()
    {
        var (model, email) = MakeModel();
        model.Input = new StoetModel.InputModel { Kind = DonationKind.Money, AmountKr = 75 };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task ProductDonation_SendsAThankYou_WithoutRepeatingTheDonorsOwnText()
    {
        var (model, email) = MakeModel();
        model.Input = new StoetModel.InputModel
        {
            Kind = DonationKind.Products, ItemDescription = "Besøg min-side.example nu",
            DonorName = "Giver\r\nGiversen", DonorEmail = "giver@example.com", Message = "En hilsen med reklame"
        };

        await model.OnPostAsync();

        var sent = Assert.Single(email.Sent);
        Assert.Contains("Hej Giver Giversen,", sent.Body);     // the name can't start a line of its own
        Assert.Contains("Havnegade 12", sent.Body);
        Assert.DoesNotContain("min-side.example", sent.Body);
        Assert.DoesNotContain("reklame", sent.Body);
    }

    [Fact]
    public async Task ADonation_IsStillAccepted_WhenTheEmailCannotBeSent()
    {
        var marker = $"hø {Guid.NewGuid():N}";
        var (model, _) = MakeModel(new FailingEmailSender());
        model.Input = new StoetModel.InputModel
        {
            Kind = DonationKind.Products, ItemDescription = marker,
            DonorName = "Giver Giversen", DonorEmail = "giver@example.com"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Single(new SqlInboxStore(fixture.ConnectionString).GetDonations(200), d => d.ItemDescription == marker);
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, bool english = false) =>
            throw new InvalidOperationException("SMTP is down");
    }
}
