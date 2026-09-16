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

    // One tracker shared across every MakeModel() call *within a single
    // test* (xUnit gives each test method its own fresh instance of this
    // whole class, so this never leaks state between tests) - matching how
    // the real app shares one Singleton LoginLockoutTracker across every
    // request, which the "five wrong attempts across five separate model
    // instances still locks out" test below depends on.
    private readonly LoginLockoutTracker _lockout = new();

    private (LoginModel Model, RecordingAuthenticationService Auth, RecordingEmailSender Email) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var email = new RecordingEmailSender();
        var model = new LoginModel(_users, _pendingLogins, email, new AlwaysPassRecaptchaVerifier(), _lockout)
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
    public async Task OnPostAsync_TotpEnrolledAccount_RedirectsToVerifyTotpInsteadOfEmailing()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var user = _users.FindByEmail(email)!;
        _users.SetTotpSecret(user.UserId, Totp.GenerateSecret());
        _users.SetTotpEnabled(user.UserId, true);
        var (model, auth, sentEmail) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.Empty(sentEmail.Sent); // no email link step for a TOTP-enrolled account
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("VerifyTotp", redirect.PageName);
        Assert.NotNull(redirect.RouteValues?["token"]);
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

    /// <summary>
    /// Reaches into the tracker's private per-email delay via reflection to
    /// simulate the progressive delay having already elapsed - the real
    /// tracker deliberately refuses a same-instant retry (LoginModel checks
    /// IsLockedOut before ever touching credentials), so without this a test
    /// driving three attempts back-to-back would only ever register the
    /// first one and would need real multi-second Thread.Sleep calls to
    /// observe the rest.
    /// </summary>
    private static void ExpireLockoutDelay(LoginLockoutTracker tracker, string email)
    {
        var entries = (System.Collections.IDictionary)typeof(LoginLockoutTracker)
            .GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(tracker)!;
        var entry = entries[email]!;
        entry.GetType().GetField("DelayedUntil")!.SetValue(entry, DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public async Task OnPostAsync_ThirdWrongAttempt_EmailsTheAccountOwner()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        RecordingEmailSender? thirdAttemptEmail = null;

        for (var i = 0; i < 3; i++)
        {
            var (attempt, _, sent) = MakeModel();
            attempt.Input = new LoginModel.InputModel { Email = email, Password = "WrongPassword!" };
            await attempt.OnPostAsync(returnUrl: null);
            ExpireLockoutDelay(_lockout, email.ToLowerInvariant());
            if (i == 2) thirdAttemptEmail = sent;
        }

        var notification = Assert.Single(thirdAttemptEmail!.Sent);
        Assert.Equal(email, notification.ToEmail);
        Assert.Contains("Mistænkelig aktivitet", notification.Subject);
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
