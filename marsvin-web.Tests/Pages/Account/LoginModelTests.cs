using System.Text.RegularExpressions;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Pages.Account;

/// <summary>Records SignInAsync calls without needing the real auth middleware pipeline.</summary>
internal sealed class RecordingAuthenticationService : IAuthenticationService
{
    public System.Security.Claims.ClaimsPrincipal? SignedInAs { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        SignedInAs = principal;
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;
}

/// <summary>Records SendAsync calls instead of actually sending anything - shared across every test that triggers email.</summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string toEmail, string subject, string body)
    {
        Sent.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Always passes, regardless of the token - these tests aren't exercising
/// reCAPTCHA itself (GoogleRecaptchaVerifierTests covers that), and every
/// PageModel here is constructed directly rather than through DI, so the
/// real Recaptcha:SecretKey-driven skip in GoogleRecaptchaVerifier never
/// comes into play the way it does for the full HTTP end-to-end tests.
/// </summary>
internal sealed class AlwaysPassRecaptchaVerifier : IRecaptchaVerifier
{
    public Task<bool> VerifyAsync(string? token) => Task.FromResult(true);
}

[Collection("SqlCatalog collection")]
public class LoginModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private (LoginModel Model, RecordingAuthenticationService Auth, RecordingEmailSender Email) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var email = new RecordingEmailSender();
        var model = new LoginModel(_users, _pendingLogins, email, new AlwaysPassRecaptchaVerifier())
            { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth, email);
    }

    private string NewCustomerWithPassword(string password, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, password), caller, UserRole.Customer);
        return email;
    }

    private static string ExtractToken(string emailBody)
    {
        var match = Regex.Match(emailBody, "token=([0-9A-Fa-f]+)");
        Assert.True(match.Success, "confirmation email did not contain a token in the expected format");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_SendsConfirmationEmailAndDoesNotSignInYet()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, auth, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        var sent = Assert.Single(sentEmail.Sent);
        Assert.Equal(email, sent.ToEmail);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("CheckEmail", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_WrongPassword_DoesNotSignInAndShowsError()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, auth, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "WrongPassword!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.Empty(sentEmail.Sent);
        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_UnknownEmail_FailsWithoutThrowing()
    {
        var (model, auth, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = $"nobody-{Guid.NewGuid():N}@example.com", Password = "Whatever123!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.Empty(sentEmail.Sent);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_FiveWrongAttempts_LocksOutEvenWithCorrectPasswordAfterward()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");

        for (var i = 0; i < 5; i++)
        {
            var (attempt, _, _) = MakeModel();
            attempt.Input = new LoginModel.InputModel { Email = email, Password = "WrongPassword!" };
            await attempt.OnPostAsync(returnUrl: null);
        }

        var (finalTry, auth, sentEmail) = MakeModel();
        finalTry.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };
        var result = await finalTry.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.Empty(sentEmail.Sent);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ReturnUrl_IsPreservedForAfterConfirmation()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, _, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        await model.OnPostAsync(returnUrl: "/Marsvin");

        var token = ExtractToken(sentEmail.Sent[0].Body);
        var ticket = _pendingLogins.Consume(token);
        Assert.NotNull(ticket);
        Assert.Equal("/Marsvin", ticket!.ReturnUrl);
    }

    [Fact]
    public async Task OnPostAsync_UnsafeReturnUrl_IsDroppedRatherThanStored()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, _, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        await model.OnPostAsync(returnUrl: "//evil.example.com");

        var token = ExtractToken(sentEmail.Sent[0].Body);
        var ticket = _pendingLogins.Consume(token);
        Assert.NotNull(ticket);
        Assert.Null(ticket!.ReturnUrl);
    }
}
