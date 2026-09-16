using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Pages.Account;

[Collection("SqlCatalog collection")]
public class RegisterModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private (RegisterModel Model, RecordingAuthenticationService Auth, RecordingEmailSender Email) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");

        var email = new RecordingEmailSender();
        var model = new RegisterModel(_users, _pendingLogins, email, new AlwaysPassRecaptchaVerifier())
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
        return (model, auth, email);
    }

    [Fact]
    public async Task OnPostAsync_NewEmail_CreatesCustomerAccountButDoesNotSignInYet()
    {
        // Registering no longer signs the visitor in immediately - it never
        // actually proved they own the address. A confirmation link goes out
        // instead, the same as Login, and ConfirmLoginModel finishes the
        // sign-in once it's opened (see ConfirmLoginModelTests).
        var email = $"new-{Guid.NewGuid():N}@example.com";
        var (model, auth, emailSender) = MakeModel();
        model.Input = new RegisterModel.InputModel
        {
            Email = email,
            DisplayName = "New Customer",
            Password = "SomePass123!",
            ConfirmPassword = "SomePass123!"
        };

        var result = await model.OnPostAsync(returnUrl: null);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("CheckEmail", redirect.PageName);
        Assert.Null(auth.SignedInAs);
        var created = _users.FindByEmail(email);
        Assert.NotNull(created);
        Assert.Equal(UserRole.Customer, created!.Role);
        Assert.Single(emailSender.Sent);
        Assert.Equal(email, emailSender.Sent[0].ToEmail);
        Assert.Contains("/Account/ConfirmLogin?token=", emailSender.Sent[0].Body);
    }

    [Fact]
    public async Task OnPostAsync_EmailAlreadyRegistered_FailsWithoutSigningInOrDuplicating()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "existing-hash", "Existing", UserRole.Customer);
        var (model, auth, emailSender) = MakeModel();
        model.Input = new RegisterModel.InputModel
        {
            Email = email,
            DisplayName = "Impersonator",
            Password = "SomePass123!",
            ConfirmPassword = "SomePass123!"
        };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal("Existing", _users.FindByEmail(email)!.DisplayName);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task OnPostAsync_NeverAssignsAnyRoleOtherThanCustomer()
    {
        // Register has no role field at all - this documents that self-signup
        // can only ever create Customer accounts, regardless of what a
        // malicious client might try to smuggle into the POST body.
        var email = $"self-signup-{Guid.NewGuid():N}@example.com";
        var (model, _, _) = MakeModel();
        model.Input = new RegisterModel.InputModel
        {
            Email = email,
            DisplayName = "Whoever",
            Password = "SomePass123!",
            ConfirmPassword = "SomePass123!"
        };

        await model.OnPostAsync(returnUrl: null);

        Assert.Equal(UserRole.Customer, _users.FindByEmail(email)!.Role);
    }
}
