using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Account;

[Collection("SqlCatalog collection")]
public class ResetPasswordModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private ResetPasswordModel MakeModel() => new(_users, _pendingLogins);

    private (int UserId, string Token) NewUserWithResetToken([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, "OldPass123!"), caller, UserRole.Customer);
        var userId = _users.FindByEmail(email)!.UserId;
        var token = _pendingLogins.Create(userId, returnUrl: null, TimeSpan.FromMinutes(15));
        return (userId, token);
    }

    [Fact]
    public void OnGet_ValidToken_TokenIsValidIsTrue()
    {
        var (_, token) = NewUserWithResetToken();
        var model = MakeModel();

        model.OnGet(token);

        Assert.True(model.TokenIsValid);
    }

    [Fact]
    public void OnGet_UnknownToken_TokenIsValidIsFalse()
    {
        var model = MakeModel();

        model.OnGet("not-a-real-token");

        Assert.False(model.TokenIsValid);
    }

    [Fact]
    public void OnGet_DoesNotConsumeTheToken_ItIsStillUsableAfterwards()
    {
        // OnGet only peeks (IPendingLoginStore.IsValid) - it must not spend
        // the token, or every visitor who merely opens the reset link before
        // filling in the form would find it already dead by the time they submit.
        var (_, token) = NewUserWithResetToken();
        var model = MakeModel();

        model.OnGet(token);
        model.OnGet(token); // opening the link twice must not matter yet

        Assert.True(model.TokenIsValid);
    }

    [Fact]
    public void OnPost_ValidTokenAndMatchingPasswords_UpdatesPasswordConsumesTokenAndRedirectsToLogin()
    {
        var (userId, token) = NewUserWithResetToken();
        var model = MakeModel();
        model.Token = token;
        model.Input = new ResetPasswordModel.InputModel { NewPassword = "NewPass456!", ConfirmNewPassword = "NewPass456!" };

        var result = model.OnPost();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Login", redirect.PageName);
        var hasher = new PasswordHasher<ApplicationUser>();
        var updated = _users.FindById(userId)!;
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, updated.PasswordHash, "NewPass456!"));

        // Single-use: the same token can't be redeemed a second time.
        var replay = MakeModel();
        replay.Token = token;
        replay.Input = new ResetPasswordModel.InputModel { NewPassword = "AnotherPass789!", ConfirmNewPassword = "AnotherPass789!" };
        replay.OnPost();
        Assert.False(replay.TokenIsValid);
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, _users.FindById(userId)!.PasswordHash, "NewPass456!"));
    }

    [Fact]
    public void OnPost_UnknownToken_DoesNotChangeAnyPassword()
    {
        var (userId, _) = NewUserWithResetToken();
        var originalHash = _users.FindById(userId)!.PasswordHash;
        var model = MakeModel();
        model.Token = "not-a-real-token";
        model.Input = new ResetPasswordModel.InputModel { NewPassword = "NewPass456!", ConfirmNewPassword = "NewPass456!" };

        model.OnPost();

        Assert.False(model.TokenIsValid);
        Assert.Equal(originalHash, _users.FindById(userId)!.PasswordHash);
    }

    [Fact]
    public void OnPost_PasswordsDoNotMatch_ShowsModelErrorAndDoesNotChangePassword()
    {
        var (userId, token) = NewUserWithResetToken();
        var originalHash = _users.FindById(userId)!.PasswordHash;
        var model = MakeModel();
        model.Token = token;
        model.Input = new ResetPasswordModel.InputModel { NewPassword = "NewPass456!", ConfirmNewPassword = "Different789!" };
        model.ModelState.AddModelError("Input.ConfirmNewPassword", "Adgangskoderne er ikke ens.");

        model.OnPost();

        Assert.Equal(originalHash, _users.FindById(userId)!.PasswordHash);
    }
}
