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
    public async Task OnPostAsync_EmailAlreadyRegistered_LooksIdenticalToASignup_AndNeverTellsTheBrowser()
    {
        // User-enumeration guard: a taken email must NOT come back as a form
        // error (that would let anyone probe which addresses have an account).
        // The response is the same "check your email" redirect as a real
        // signup; the notice goes to the address itself instead.
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

        // Same outcome as OnPostAsync_NewEmail_... above - indistinguishable.
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("CheckEmail", redirect.PageName);
        Assert.Null(auth.SignedInAs);
        Assert.True(model.ModelState.IsValid);
        // The account is untouched: same display name and hash, no duplicate.
        var stored = _users.FindByEmail(email)!;
        Assert.Equal("Existing", stored.DisplayName);
        Assert.Equal("existing-hash", stored.PasswordHash);
        // The notice went to the owner's address, is NOT a confirmation link
        // (no account was created), and never echoes the submitter's name.
        var sent = Assert.Single(emailSender.Sent);
        Assert.Equal(email, sent.ToEmail);
        Assert.DoesNotContain("/Account/ConfirmLogin?token=", sent.Body);
        Assert.DoesNotContain("Impersonator", sent.Body);
        Assert.Contains("Existing", sent.Body);
    }

    [Fact]
    public async Task OnPostAsync_EmailAlreadyRegistered_InADifferentCase_StillDoesNotCreateASecondAccount()
    {
        // The lookup is normalised, so a capitalised retype can't slip past
        // the "already registered" check and open a duplicate for one inbox.
        var email = $"case-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "existing-hash", "Existing", UserRole.Customer);
        var (model, _, emailSender) = MakeModel();
        model.Input = new RegisterModel.InputModel
        {
            Email = email.ToUpperInvariant(),
            DisplayName = "Impersonator",
            Password = "SomePass123!",
            ConfirmPassword = "SomePass123!"
        };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Existing", _users.FindByEmail(email)!.DisplayName);
        Assert.DoesNotContain("/Account/ConfirmLogin?token=", Assert.Single(emailSender.Sent).Body);
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

    [Fact]
    public async Task OnPostAsync_WeakPassword_IsRejected_NoAccount_NoEmail_NoSignIn()
    {
        var email = $"weak-{Guid.NewGuid():N}@example.com";
        var (model, auth, emailSender) = MakeModel();
        model.Input = new RegisterModel.InputModel
        {
            Email = email,
            DisplayName = "New Customer",
            Password = "weakpass",            // under 12, no upper/digit/symbol
            ConfirmPassword = "weakpass"
        };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("Input.Password"));
        Assert.Null(_users.FindByEmail(email));
        Assert.Null(auth.SignedInAs);
        Assert.Empty(emailSender.Sent);
    }
}
