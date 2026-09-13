using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlCartStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    [Fact]
    public void GetLines_EmptyCart_ReturnsNoLines()
    {
        var userId = NewCustomerId();

        Assert.Empty(_cart.GetLines(userId));
    }

    [Fact]
    public void AddOrIncrement_NewLine_ResolvesNameAndPriceFromCatalog()
    {
        var userId = NewCustomerId();

        _cart.AddOrIncrement(userId, 101, 2); // Timothy-hø, 2 kg - 89 kr

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(101, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(89m, line.UnitPrice);
        Assert.False(line.IsAnimal);
        Assert.Equal(178m, line.LineTotal);
    }

    [Fact]
    public void AddOrIncrement_ExistingLine_AddsToQuantityRatherThanDuplicating()
    {
        var userId = NewCustomerId();

        _cart.AddOrIncrement(userId, 101, 1);
        _cart.AddOrIncrement(userId, 101, 3);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(4, line.Quantity);
    }

    [Fact]
    public void AddOrIncrement_Animal_IsFlaggedAsAnimal()
    {
        var userId = NewCustomerId();

        _cart.AddOrIncrement(userId, 1, 1); // Pelle

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.True(line.IsAnimal);
        Assert.Equal("Pelle", line.ProductName);
    }

    [Fact]
    public void SetQuantity_ExistingLine_ReplacesRatherThanAdds()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 101, 1);

        _cart.SetQuantity(userId, 101, 5);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(5, line.Quantity);
    }

    [Fact]
    public void SetQuantity_ZeroOrLess_RemovesTheLine()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 101, 2);

        _cart.SetQuantity(userId, 101, 0);

        Assert.Empty(_cart.GetLines(userId));
    }

    [Fact]
    public void RemoveLine_DeletesOnlyThatProduct()
    {
        var userId = NewCustomerId();
        _cart.AddOrIncrement(userId, 101, 1);
        _cart.AddOrIncrement(userId, 102, 1);

        _cart.RemoveLine(userId, 101);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(102, line.ProductId);
    }

    [Fact]
    public void Clear_RemovesEveryLineForThatUserOnly()
    {
        var userA = NewCustomerId();
        var userB = NewCustomerId();
        _cart.AddOrIncrement(userA, 101, 1);
        _cart.AddOrIncrement(userB, 102, 1);

        _cart.Clear(userA);

        Assert.Empty(_cart.GetLines(userA));
        Assert.Single(_cart.GetLines(userB));
    }
}
