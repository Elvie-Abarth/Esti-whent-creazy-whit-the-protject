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

[Collection("SqlCatalog collection")]
public class ProfileModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private const string OriginalPassword = "OriginalPass123!";

    // Same RecordingAuthenticationService as LoginModelTests (same namespace) -
    // lets OnPostAsync's HttpContext.SignInAsync succeed without the real auth
    // middleware pipeline, and lets the test see what it signed in as.
    private (ProfileModel Model, RecordingAuthenticationService Auth) MakeModel(int userId, string role = "Customer")
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "Test"))
        };

        var model = new ProfileModel(_users) { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth);
    }

    private ApplicationUser NewCustomer([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, OriginalPassword), caller, UserRole.Customer);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void OnGet_LoadsCurrentDisplayNameAndEmail()
    {
        var user = NewCustomer();
        var (model, _) = MakeModel(user.UserId);

        model.OnGet();

        Assert.Equal(user.DisplayName, model.Input.DisplayName);
        Assert.Equal(user.Email, model.Input.Email);
    }

    [Fact]
    public async Task OnPostAsync_WrongCurrentPassword_DoesNotUpdateAndShowsError()
    {
        var user = NewCustomer();
        var (model, auth) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = "New Name",
            Email = user.Email,
            CurrentPassword = "WrongPassword!"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.Equal(user.DisplayName, _users.FindById(user.UserId)!.DisplayName);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_UpdatesNameAndEmailAndSignsInAgain()
    {
        var user = NewCustomer();
        var (model, auth) = MakeModel(user.UserId);
        var newEmail = $"updated-{Guid.NewGuid():N}@example.com";
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = "Updated Name",
            Email = newEmail,
            CurrentPassword = OriginalPassword
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var updated = _users.FindById(user.UserId)!;
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal(newEmail, updated.Email);
        Assert.NotNull(auth.SignedInAs);
        Assert.Equal("Updated Name", auth.SignedInAs!.Identity!.Name);
    }

    [Fact]
    public async Task OnPostAsync_EmailAlreadyUsedByAnotherAccount_ShowsErrorAndDoesNotUpdate()
    {
        var user = NewCustomer();
        var other = NewCustomer();
        var (model, auth) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = other.Email,
            CurrentPassword = OriginalPassword
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.Equal(user.Email, _users.FindById(user.UserId)!.Email);
    }

    [Fact]
    public async Task OnPostAsync_NewPasswordProvided_ChangesPasswordHash()
    {
        var user = NewCustomer();
        var (model, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            CurrentPassword = OriginalPassword,
            NewPassword = "BrandNewPass456!",
            ConfirmNewPassword = "BrandNewPass456!"
        };

        await model.OnPostAsync();

        var updated = _users.FindById(user.UserId)!;
        var hasher = new PasswordHasher<ApplicationUser>();
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, updated.PasswordHash, OriginalPassword));
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, updated.PasswordHash, "BrandNewPass456!"));
    }

    [Fact]
    public async Task OnPostAsync_NoNewPassword_LeavesPasswordUnchanged()
    {
        var user = NewCustomer();
        var passwordHashBefore = _users.FindById(user.UserId)!.PasswordHash;
        var (model, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            CurrentPassword = OriginalPassword
        };

        await model.OnPostAsync();

        Assert.Equal(passwordHashBefore, _users.FindById(user.UserId)!.PasswordHash);
    }
}
