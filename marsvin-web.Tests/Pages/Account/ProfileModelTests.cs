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
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
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
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)
                ],
                authenticationType: "Test"))
        };

        var model = new ProfileModel(_users, _orders, _cart, _catalog) { PageContext = new PageContext { HttpContext = httpContext } };
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

    [Fact]
    public void OnGet_CustomerWithOrders_PopulatesOrderHistory()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 1);
        _orders.Checkout(user.UserId);
        var (model, _) = MakeModel(user.UserId);

        model.OnGet();

        Assert.Single(model.Orders);
    }

    [Fact]
    public void OnGet_StaffAccount_NeverPopulatesOrderHistory()
    {
        var user = NewCustomer();
        var (model, _) = MakeModel(user.UserId, role: "Admin");

        model.OnGet();

        Assert.Empty(model.Orders);
    }

    [Fact]
    public void OnPostReorder_AvailableAccessory_AddsItToTheCart()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 2);
        var order = _orders.Checkout(user.UserId).Order!;
        var (model, _) = MakeModel(user.UserId);

        model.OnPostReorder(order.OrderId);

        var line = Assert.Single(_cart.GetLines(user.UserId));
        Assert.Equal(104, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.NotNull(model.ToastMessage);
    }

    [Fact]
    public void OnPostReorder_AnimalInThatOrder_IsAlwaysSkipped()
    {
        // A throwaway animal, not one of the seeded ones (Pelle etc.) - this
        // test's checkout actually marks it Sold, and reusing a seeded animal
        // would permanently break every other test that assumes it's Available.
        var user = NewCustomer();
        var animal = new Animal
        {
            ProductId = 0,
            Name = $"ReorderTest-{Guid.NewGuid():N}",
            Description = "test",
            Breed = "test",
            Sex = Sex.Boar,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-70)),
            Colour = "test",
            CoatPrimary = "#000000",
            CoatSecondary = "#ffffff",
            Status = AnimalStatus.Available
        };
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);
        try
        {
            _cart.AddOrIncrement(user.UserId, created.ProductId, 1);
            var order = _orders.Checkout(user.UserId).Order!;
            var (model, _) = MakeModel(user.UserId);

            model.OnPostReorder(order.OrderId);

            Assert.Empty(_cart.GetLines(user.UserId));
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void OnPostReorder_OrderBelongingToAnotherUser_IsNotFoundNotLeaked()
    {
        var owner = NewCustomer();
        var stranger = NewCustomer();
        _cart.AddOrIncrement(owner.UserId, 104, 1);
        var order = _orders.Checkout(owner.UserId).Order!;
        var (model, _) = MakeModel(stranger.UserId);

        model.OnPostReorder(order.OrderId);

        Assert.Empty(_cart.GetLines(stranger.UserId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostReorder_NonCustomerAccount_IsForbidden()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        var (model, _) = MakeModel(user.UserId, role: "Admin");

        var result = model.OnPostReorder(order.OrderId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void OnPostReorder_NowOutOfStock_SkipsWithoutThrowing()
    {
        var user = NewCustomer();
        const int productId = 109;
        var stock = _catalog.Accessories.Single(p => p.ProductId == productId).StockQuantity;
        _cart.AddOrIncrement(user.UserId, productId, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        // Deplete the remaining stock so the reorder can no longer be fulfilled.
        _catalog.UpdateStockQuantity(productId, 0);
        var (model, _) = MakeModel(user.UserId);

        model.OnPostReorder(order.OrderId);

        Assert.Empty(_cart.GetLines(user.UserId));
        Assert.NotNull(model.ErrorMessage);

        _catalog.UpdateStockQuantity(productId, stock - 1); // restore for other tests in this shared fixture
    }
}
