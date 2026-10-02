using System.Security.Claims;
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
public class ConfirmLoginModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);
    private readonly SqlCartStore _accountCart = new(fixture.ConnectionString);

    // A fresh one per call, not a shared field - a real request only ever
    // has one guest cart (its own session's), but two MakeModel() calls in
    // the same test (see the single-use-token test below) must not somehow
    // share one, the way two different browsers never would.
    private (ConfirmLoginModel Model, RecordingAuthenticationService Auth, FakeCartStore GuestCart) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var guestCart = new FakeCartStore();
        var model = new ConfirmLoginModel(_users, _pendingLogins, guestCart, _accountCart)
        {
            PageContext = new PageContext { HttpContext = httpContext }
        };
        return (model, auth, guestCart);
    }

    private ApplicationUser NewCustomer([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, "whatever"), caller, UserRole.Customer);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_SignsInAndRedirectsHome()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (model, auth, _) = MakeModel();

        var result = await model.OnGetAsync(token);

        Assert.NotNull(auth.SignedInAs);
        Assert.Equal(user.UserId.ToString(), auth.SignedInAs!.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_RecordsActivity()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (model, _, _) = MakeModel();

        await model.OnGetAsync(token);

        var refreshed = _users.FindById(user.UserId)!;
        Assert.True(refreshed.LastActiveAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_WithReturnUrl_RedirectsThereInstead()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: "/Marsvin", TimeSpan.FromMinutes(15));
        var (model, _, _) = MakeModel();

        var result = await model.OnGetAsync(token);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Marsvin", redirect.Url);
    }

    [Fact]
    public async Task OnGetAsync_TokenIsSingleUse_SecondAttemptFails()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (first, _, _) = MakeModel();
        await first.OnGetAsync(token);

        var (second, auth2, _) = MakeModel();
        var result = await second.OnGetAsync(token);

        Assert.Null(auth2.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ExpiredToken_DoesNotSignIn()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        var (model, auth, _) = MakeModel();

        var result = await model.OnGetAsync(token);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_UnknownToken_DoesNotSignIn()
    {
        var (model, auth, _) = MakeModel();

        var result = await model.OnGetAsync("not-a-real-token");

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_NullToken_DoesNotSignIn()
    {
        var (model, auth, _) = MakeModel();

        var result = await model.OnGetAsync(null);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_MergesWhateverWasInTheGuestCartIntoTheAccount()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (model, _, guestCart) = MakeModel();
        guestCart.Add(new CartLine
        {
            ProductId = 101, ProductName = "Timothy-hø, 2 kg", UnitPrice = 89m, Quantity = 2, IsAnimal = false
        });

        await model.OnGetAsync(token);

        var line = Assert.Single(_accountCart.GetLines(user.UserId));
        Assert.Equal(101, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Empty(guestCart.GetLines(0)); // the guest cart itself is cleared once merged
    }

    [Fact]
    public async Task OnGetAsync_DeactivatedAccount_DoesNotSignIn()
    {
        var user = NewCustomer();
        _users.SetActive(user.UserId, false);
        try
        {
            var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
            var (model, auth, _) = MakeModel();

            var result = await model.OnGetAsync(token);

            Assert.Null(auth.SignedInAs);
            Assert.IsType<PageResult>(result);
        }
        finally
        {
            _users.SetActive(user.UserId, true);
        }
    }
}
