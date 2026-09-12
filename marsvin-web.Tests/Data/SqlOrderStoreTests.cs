using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlOrderStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    private int ReadStock(int productId)
    {
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = new SqlCommand(
            "SELECT StockQuantity FROM dbo.StockProducts WHERE ProductId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", productId);
        return (int)command.ExecuteScalar()!;
    }

    [Fact]
    public void Checkout_EmptyCart_Fails()
    {
        var userId = NewCustomerId();

        var result = _orders.Checkout(userId);

        Assert.False(result.Success);
        Assert.Null(result.Order);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Checkout_ValidAccessory_DecrementsStockCreatesOrderAndClearsCart()
    {
        var userId = NewCustomerId();
        const int productId = 104; // Tørret mælkebøtte - ample stock
        var stockBefore = ReadStock(productId);
        _cart.AddOrIncrement(userId, productId, 3);

        var result = _orders.Checkout(userId);

        Assert.True(result.Success);
        Assert.NotNull(result.Order);
        var order = result.Order!;
        var item = Assert.Single(order.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(item.UnitPrice * 3, order.TotalPrice);

        Assert.Equal(stockBefore - 3, ReadStock(productId));
        Assert.Empty(_cart.GetLines(userId));
    }

    [Fact]
    public void Checkout_MoreThanAvailableStock_FailsWithoutChangingStockOrCart()
    {
        var userId = NewCustomerId();
        const int productId = 110; // Tunnel i flettet græs
        var stockBefore = ReadStock(productId);
        _cart.AddOrIncrement(userId, productId, stockBefore + 1000);

        var result = _orders.Checkout(userId);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(stockBefore, ReadStock(productId));
        Assert.Single(_cart.GetLines(userId)); // cart was not cleared - checkout rolled back
    }

    [Fact]
    public void Checkout_AnimalThatIsNotAvailable_FailsAndLeavesStatusUnchanged()
    {
        var userId = NewCustomerId();
        const int reservedAnimalId = 5; // Freja - seeded as Reserved
        _cart.AddOrIncrement(userId, reservedAnimalId, 1);

        var result = _orders.Checkout(userId);

        Assert.False(result.Success);
        Assert.Equal(AnimalStatus.Reserved, _catalog.FindAnimal(reservedAnimalId)!.Status);
        Assert.Single(_cart.GetLines(userId));
    }

    [Fact]
    public void Checkout_AnimalTooYoungToSell_Fails()
    {
        var userId = NewCustomerId();
        var tooYoung = new Animal
        {
            ProductId = 0,
            Name = $"Nyfødt-{Guid.NewGuid():N}",
            Description = "test",
            Breed = "test",
            Sex = Sex.Boar,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today),
            Colour = "test",
            CoatPrimary = "#000000",
            CoatSecondary = "#ffffff",
            Status = AnimalStatus.Available
        };
        _catalog.CreateAnimal(tooYoung);
        var created = _catalog.Animals.Single(a => a.Name == tooYoung.Name);
        try
        {
            _cart.AddOrIncrement(userId, created.ProductId, 1);

            var result = _orders.Checkout(userId);

            Assert.False(result.Success);
            Assert.Contains("gammel", result.ErrorMessage);
        }
        finally
        {
            // Other tests in this shared collection assert exact row counts
            // against the seeded catalog - clean up the animal this test
            // added so it doesn't leak into them.
            _cart.RemoveLine(userId, created.ProductId);
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void Checkout_UnknownProductInCart_Fails()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 999999, 1);

        var result = _orders.Checkout(userId);

        Assert.False(result.Success);
    }

    [Fact]
    public void FindForUser_ReturnsOrderOnlyForTheOwningUser()
    {
        var owner = NewCustomerId();
        var stranger = NewCustomerId();
        _cart.AddOrIncrement(owner, 104, 1);
        var order = _orders.Checkout(owner).Order!;

        Assert.NotNull(_orders.FindForUser(order.OrderId, owner));
        Assert.Null(_orders.FindForUser(order.OrderId, stranger));
    }

    [Fact]
    public void FindForUser_UnknownOrderId_ReturnsNull()
    {
        var userId = NewCustomerId();

        Assert.Null(_orders.FindForUser(999999, userId));
    }
}
