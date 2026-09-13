using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Cart;
using MarsvinWebExample.Tests.Data;

namespace MarsvinWebExample.Tests.Pages.Cart;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    private IndexModel MakeModel(int userId) => new(_cart, _catalog, _orders)
    {
        PageContext = TestAuth.ContextFor(userId, "Customer")
    };

    [Fact]
    public void OnPostAdd_AvailableAccessory_AddsLineToCart()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 104, quantity: 2);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(104, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void OnPostAdd_MoreThanAvailableStock_DoesNotAddAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        var stock = _catalog.Accessories.Single(p => p.ProductId == 109).StockQuantity;

        model.OnPostAdd(productId: 109, quantity: stock + 1);

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostAdd_UnavailableAnimal_DoesNotAddAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 5, quantity: 1); // Freja - seeded Reserved

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostAdd_AvailableAnimal_AddsWithQuantityOne()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 1, quantity: 1); // Pelle - seeded Available

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.True(line.IsAnimal);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public void OnPostAdd_UnknownProductId_SetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 999999, quantity: 1);

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostUpdateQuantity_WithinStock_UpdatesTheLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 1);

        model.OnPostUpdateQuantity(productId: 104, quantity: 3);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public void OnPostUpdateQuantity_MoreThanAvailableStock_LeavesQuantityUnchangedAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 109, quantity: 1);
        var stock = _catalog.Accessories.Single(p => p.ProductId == 109).StockQuantity;

        model.OnPostUpdateQuantity(productId: 109, quantity: stock + 1);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(1, line.Quantity);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostUpdateQuantity_ZeroOrLess_RemovesTheLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 2);

        model.OnPostUpdateQuantity(productId: 104, quantity: 0);

        Assert.Empty(_cart.GetLines(userId));
    }

    [Fact]
    public void OnPostUpdateQuantity_Animal_LeavesQuantityAtOneAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 1, quantity: 1); // Pelle - seeded Available

        model.OnPostUpdateQuantity(productId: 1, quantity: 2);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(1, line.Quantity);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostRemove_RemovesOnlyThatLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 1);
        model.OnPostAdd(productId: 109, quantity: 1);

        model.OnPostRemove(productId: 104);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(109, line.ProductId);
    }
}
