using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Account;

[Collection("SqlCatalog collection")]
public class ForgotPasswordModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private (ForgotPasswordModel Model, RecordingEmailSender Email) MakeModel()
    {
        var email = new RecordingEmailSender();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        var model = new ForgotPasswordModel(_users, _pendingLogins, email)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
        return (model, email);
    }

    [Fact]
    public async Task OnPostAsync_KnownEmail_SendsResetLinkAndRedirectsToCheckEmail()
    {
        var address = $"forgot-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(address, "hash", "Forgot Test", UserRole.Customer);
        var (model, email) = MakeModel();
        model.Input = new ForgotPasswordModel.InputModel { Email = address };

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("CheckEmail", redirect.PageName);
        Assert.Single(email.Sent);
        Assert.Equal(address, email.Sent[0].ToEmail);
        Assert.Contains("/Account/ResetPassword?token=", email.Sent[0].Body);
    }

    [Fact]
    public async Task OnPostAsync_UnknownEmail_RedirectsTheSameWayWithoutSendingAnything()
    {
        // Anti-enumeration: the response must not reveal whether the address
        // has an account - otherwise this form could be used to test which
        // emails are registered.
        var (model, email) = MakeModel();
        model.Input = new ForgotPasswordModel.InputModel { Email = $"nobody-{Guid.NewGuid():N}@example.com" };

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("CheckEmail", redirect.PageName);
        Assert.Empty(email.Sent);
    }
}
