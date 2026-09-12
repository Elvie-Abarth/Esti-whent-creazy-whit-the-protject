using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Cart;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Cart;

[Collection("SqlCatalog collection")]
public class ConfirmationModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    [Fact]
    public void OnGet_OwnOrder_ReturnsPageWithOrder()
    {
        var owner = NewCustomerId();
        _cart.AddOrIncrement(owner, 104, 1);
        var order = _orders.Checkout(owner).Order!;
        var model = new ConfirmationModel(_orders) { PageContext = TestAuth.ContextFor(owner, "Customer") };

        var result = model.OnGet(order.OrderId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(order.OrderId, model.Order.OrderId);
    }

    [Fact]
    public void OnGet_SomeoneElsesOrder_ReturnsNotFound()
    {
        var owner = NewCustomerId();
        var stranger = NewCustomerId();
        _cart.AddOrIncrement(owner, 104, 1);
        var order = _orders.Checkout(owner).Order!;
        var model = new ConfirmationModel(_orders) { PageContext = TestAuth.ContextFor(stranger, "Customer") };

        var result = model.OnGet(order.OrderId);

        Assert.IsType<NotFoundResult>(result);
    }
}
