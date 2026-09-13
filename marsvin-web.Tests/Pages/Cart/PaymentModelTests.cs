using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Cart;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Cart;

[Collection("SqlCatalog collection")]
public class PaymentModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    private PaymentModel MakeModel(int userId) => new(_cart, _orders)
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
    public void OnPost_ValidDemoCardAndAvailableStock_CompletesCheckoutAndRedirectsToConfirmation()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = model.OnPost();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Confirmation", redirect.PageName);
        Assert.Empty(_cart.GetLines(userId)); // checkout cleared the cart
    }

    [Fact]
    public void OnPost_MissingCardFields_DoesNotCheckOutAndReturnsPage()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 104, 1);
        var model = MakeModel(userId);
        model.Input = new PaymentModel.PaymentInputModel(); // all fields blank
        model.ModelState.AddModelError("Input.CardNumber", "Udfyld kortnummeret.");

        var result = model.OnPost();

        Assert.IsType<PageResult>(result);
        Assert.Single(_cart.GetLines(userId)); // still in the cart - nothing was charged or checked out
    }

    [Fact]
    public void OnPost_ItemNoLongerInStock_LeavesErrorForTheCartPageAndRedirectsThere()
    {
        var userId = NewCustomerId();
        var stock = new SqlCatalog(fixture.ConnectionString).Accessories.Single(p => p.ProductId == 109).StockQuantity;
        _cart.AddOrIncrement(userId, 109, stock + 1000); // stale cart line, more than is actually left
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = model.OnPost();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPost_CartEmptiedInAnotherTabMeanwhile_RedirectsToIndexWithoutError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.Input = ValidInput();

        var result = model.OnPost();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);
    }
}
