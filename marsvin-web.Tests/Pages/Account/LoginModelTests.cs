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

[Collection("SqlCatalog collection")]
public class LoginModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private (LoginModel Model, RecordingAuthenticationService Auth) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var model = new LoginModel(_users) { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth);
    }

    private string NewCustomerWithPassword(string password, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, password), caller, UserRole.Customer);
        return email;
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_SignsInAndRedirectsHome()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, auth) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.NotNull(auth.SignedInAs);
        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task OnPostAsync_WrongPassword_DoesNotSignInAndShowsError()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, auth) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "WrongPassword!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAsync_UnknownEmail_FailsWithoutThrowing()
    {
        var (model, auth) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = $"nobody-{Guid.NewGuid():N}@example.com", Password = "Whatever123!" };

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_FiveWrongAttempts_LocksOutEvenWithCorrectPasswordAfterward()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");

        for (var i = 0; i < 5; i++)
        {
            var (attempt, _) = MakeModel();
            attempt.Input = new LoginModel.InputModel { Email = email, Password = "WrongPassword!" };
            await attempt.OnPostAsync(returnUrl: null);
        }

        var (finalTry, auth) = MakeModel();
        finalTry.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };
        var result = await finalTry.OnPostAsync(returnUrl: null);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ReturnUrl_RedirectsThereInsteadOfHome()
    {
        var email = NewCustomerWithPassword("CorrectPass123!");
        var (model, _) = MakeModel();
        model.Input = new LoginModel.InputModel { Email = email, Password = "CorrectPass123!" };

        var result = await model.OnPostAsync(returnUrl: "/Marsvin");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Marsvin", redirect.Url);
    }
}
