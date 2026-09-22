using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Cart;
using MarsvinWebExample.Tests.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Cart;

[Collection("SqlCatalog collection")]
public class PaymentModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private readonly RecordingEmailSender _email = new();

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    private PaymentModel MakeModel(int userId) => new(_cart, _orders, _users, _email)
    {
        PageContext = TestAuth.ContextFor(userId, "Customer")
    };

    private static PaymentModel.PaymentInputModel ValidInput() => new()
    {
        CardHolder = "Test Testesen",
        CardNumber = "4242 4242 4242 4242",
        Expiry = "12/29",
        Cvc = "123"
    };

    [Fact]
    public void OnGet_EmptyCart_RedirectsToIndexRatherThanShowingAPaymentForm()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        var result = model.OnGet();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
    }

    [Fact]
    public void OnGet_NonEmptyCart_ShowsTheCartTotal()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 2);
        var model = MakeModel(userId);

        var result = model.OnGet();

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, model.Lines.Single().Quantity);
    }

    [Fact]
    public async Task OnPost_ValidDemoCardAndAvailableStock_CompletesCheckoutAndRedirectsToConfirmation()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Confirmation", redirect.PageName);
        Assert.Empty(_cart.GetLines(userId)); // checkout cleared the cart
    }

    [Fact]
    public async Task OnPost_MissingCardFields_DoesNotCheckOutAndReturnsPage()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        model.Input = new PaymentModel.PaymentInputModel(); // all fields blank, PaymentMethod defaults to Card

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.True(model.ModelState.ContainsKey("Input.CardNumber"));
        Assert.Single(_cart.GetLines(userId)); // still in the cart - nothing was charged or checked out
    }

    [Fact]
    public async Task OnPost_MobilePay_SkipsCardValidationAndChecksOut()
    {
        // Card fields aren't required (or even read) once MobilePay is the
        // chosen method - the demo form hides them entirely for this reason.
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        model.Input = new PaymentModel.PaymentInputModel { PaymentMethod = PaymentMethod.MobilePay };

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Confirmation", redirect.PageName);
        var order = _orders.GetOrdersForUser(userId).Single();
        Assert.Equal(PaymentMethod.MobilePay, order.PaymentMethod);
    }

    [Fact]
    public async Task OnPost_ItemNoLongerInStock_LeavesErrorForTheCartPageAndRedirectsThere()
    {
        var userId = NewCustomerId();
        var stock = new SqlCatalog(fixture.ConnectionString).Accessories.Single(p => p.ProductId == 109).StockQuantity;
        _cart.AddOrIncrement(userId, 109, stock + 1000); // stale cart line, more than is actually left
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task OnPost_CartEmptiedInAnotherTabMeanwhile_RedirectsToIndexWithoutError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
    }

    [Fact]
    public void OnGet_AccessoriesOnlyCart_CanShipIsTrue()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);

        model.OnGet();

        Assert.True(model.CanShip);
    }

    [Fact]
    public void OnGet_CartIsOnlyAnAnimal_CanShipIsFalse()
    {
        var userId = NewCustomerId();
        var animal = NewThrowawayAnimal();
        try
        {
            _cart.AddOrIncrement(userId, animal.ProductId, 1);
            var model = MakeModel(userId);

            model.OnGet();

            Assert.False(model.CanShip);
            Assert.True(model.HasAnimal);
        }
        finally
        {
            _cart.RemoveLine(userId, animal.ProductId);
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void OnGet_MixedCartWithAnimalAndAccessory_CanShipIsTrue()
    {
        // An animal in the cart no longer blocks shipping outright - it just
        // means the shippable half is the accessories, and the animal itself
        // always needs a separate in-store pickup (HasAnimal drives that
        // wording on the page).
        var userId = NewCustomerId();
        var animal = NewThrowawayAnimal();
        try
        {
            _cart.AddOrIncrement(userId, animal.ProductId, 1);
            _cart.AddOrIncrement(userId, 104, 1);
            var model = MakeModel(userId);

            model.OnGet();

            Assert.True(model.CanShip);
            Assert.True(model.HasAnimal);
        }
        finally
        {
            _cart.RemoveLine(userId, animal.ProductId);
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public async Task OnPost_ShippingWithAnAddress_ChecksOutAsShippingToThatAddress()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        var input = ValidInput();
        input.DeliveryMethod = DeliveryMethod.Shipping;
        input.ShippingAddress = "Testvej 1, 6700 Esbjerg";
        input.ShippingCarrier = ShippingCarrier.Gls;
        model.Input = input;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var order = _orders.GetOrdersForUser(userId).Single();
        Assert.Equal(DeliveryMethod.Shipping, order.DeliveryMethod);
        Assert.Equal("Testvej 1, 6700 Esbjerg", order.ShippingAddress);
        Assert.Equal(ShippingCarrier.Gls, order.ShippingCarrier);
    }

    [Fact]
    public async Task OnPost_ShippingWithoutAnAddress_ShowsFieldErrorAndDoesNotCheckOut()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        var input = ValidInput();
        input.DeliveryMethod = DeliveryMethod.Shipping;
        model.Input = input;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.True(model.ModelState.ContainsKey("Input.ShippingAddress"));
        Assert.Single(_cart.GetLines(userId));
    }

    [Fact]
    public async Task OnPost_ShippingWithCartThatIsOnlyAnAnimal_IsRejectedEvenThoughTheFormClaimsShipping()
    {
        // Defense in depth: the UI never offers Shipping when there's nothing
        // shippable in the cart, but nothing stops a tampered POST from
        // claiming it anyway.
        var userId = NewCustomerId();
        var animal = NewThrowawayAnimal();
        try
        {
            _cart.AddOrIncrement(userId, animal.ProductId, 1);
            var model = MakeModel(userId);
            var input = ValidInput();
            input.DeliveryMethod = DeliveryMethod.Shipping;
            input.ShippingAddress = "Testvej 1";
            model.Input = input;

            var result = await model.OnPostAsync();

            Assert.False(model.ModelState.IsValid);
            Assert.Single(_cart.GetLines(userId));
        }
        finally
        {
            _cart.RemoveLine(userId, animal.ProductId);
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public async Task OnPost_ShippingWithMixedCart_ChecksOutAndStillSellsTheAnimal()
    {
        var userId = NewCustomerId();
        var animal = NewThrowawayAnimal();
        try
        {
            _cart.AddOrIncrement(userId, animal.ProductId, 1);
            _cart.AddOrIncrement(userId, 104, 1);
            var model = MakeModel(userId);
            var input = ValidInput();
            input.DeliveryMethod = DeliveryMethod.Shipping;
            input.ShippingAddress = "Testvej 1, 6700 Esbjerg";
            model.Input = input;

            var result = await model.OnPostAsync();

            Assert.IsType<RedirectToPageResult>(result);
            var order = _orders.GetOrdersForUser(userId).Single();
            Assert.Equal(DeliveryMethod.Shipping, order.DeliveryMethod);
            Assert.Contains(order.Items, i => i.ProductId == animal.ProductId && i.IsAnimal);
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    private Animal NewThrowawayAnimal([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var animal = new Animal
        {
            ProductId = 0,
            Name = $"{caller}-{Guid.NewGuid():N}",
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
        return _catalog.Animals.Single(a => a.Name == animal.Name);
    }
}
