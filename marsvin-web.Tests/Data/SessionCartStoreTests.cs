using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SessionCartStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private SessionCartStore NewCart() => new(new FakeSession(), _catalog);

    // userId is passed as 0 throughout - SessionCartStore ignores it
    // entirely (there's no user, just "whoever this session belongs to"),
    // the same value PageModelExtensions.CurrentUserIdOrZero actually
    // produces for a real anonymous request.

    [Fact]
    public void GetLines_EmptyCart_ReturnsNoLines()
    {
        var cart = NewCart();

        Assert.Empty(cart.GetLines(0));
    }

    [Fact]
    public void AddOrIncrement_NewLine_ResolvesNameAndPriceFromCatalogLikeSqlCartStoreDoes()
    {
        var cart = NewCart();

        cart.AddOrIncrement(0, 101, 2); // Timothy-hø, 2 kg - 89 kr

        var line = Assert.Single(cart.GetLines(0));
        Assert.Equal(101, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(89m, line.UnitPrice);
        Assert.False(line.IsAnimal);
        Assert.Equal(178m, line.LineTotal);
    }

    [Fact]
    public void AddOrIncrement_ExistingLine_AddsToQuantityRatherThanDuplicating()
    {
        var cart = NewCart();

        cart.AddOrIncrement(0, 101, 1);
        cart.AddOrIncrement(0, 101, 3);

        var line = Assert.Single(cart.GetLines(0));
        Assert.Equal(4, line.Quantity);
    }

    [Fact]
    public void AddOrIncrement_Animal_IsFlaggedAsAnimal()
    {
        var cart = NewCart();

        cart.AddOrIncrement(0, 1, 1); // Pelle

        var line = Assert.Single(cart.GetLines(0));
        Assert.True(line.IsAnimal);
        Assert.Equal("Pelle", line.ProductName);
    }

    [Fact]
    public void SetQuantity_ExistingLine_ReplacesRatherThanAdds()
    {
        var cart = NewCart();
        cart.AddOrIncrement(0, 101, 1);

        cart.SetQuantity(0, 101, 5);

        var line = Assert.Single(cart.GetLines(0));
        Assert.Equal(5, line.Quantity);
    }

    [Fact]
    public void SetQuantity_ZeroOrLess_RemovesTheLine()
    {
        var cart = NewCart();
        cart.AddOrIncrement(0, 101, 2);

        cart.SetQuantity(0, 101, 0);

        Assert.Empty(cart.GetLines(0));
    }

    [Fact]
    public void RemoveLine_DeletesOnlyThatProduct()
    {
        var cart = NewCart();
        cart.AddOrIncrement(0, 101, 1);
        cart.AddOrIncrement(0, 102, 1);

        cart.RemoveLine(0, 101);

        var line = Assert.Single(cart.GetLines(0));
        Assert.Equal(102, line.ProductId);
    }

    [Fact]
    public void Clear_RemovesEveryLine()
    {
        var cart = NewCart();
        cart.AddOrIncrement(0, 101, 1);
        cart.AddOrIncrement(0, 102, 1);

        cart.Clear(0);

        Assert.Empty(cart.GetLines(0));
    }

    [Fact]
    public void GetLines_ProductNoLongerInCatalog_SilentlyDropsThatLineInsteadOfThrowing()
    {
        // A guest's session can outlive the product they added to it (an
        // admin deletes it mid-session) - unlike SqlCartStore, there's no FK
        // to stop a stale entry from existing in the first place, so
        // GetLines has to cope with one gracefully. Checkout re-validates
        // whatever's actually still in the cart at that point regardless.
        var cart = NewCart();
        cart.AddOrIncrement(0, 101, 1);
        cart.AddOrIncrement(0, 999999, 1); // never a real product

        var line = Assert.Single(cart.GetLines(0));
        Assert.Equal(101, line.ProductId);
    }

    [Fact]
    public void TwoSeparateSessionCartStoreInstances_DoNotShareState()
    {
        // Each guest's own FakeSession/ISession instance in production (one
        // per browser) - proven here by two carts backed by two completely
        // separate sessions never seeing each other's lines.
        var cartA = NewCart();
        var cartB = NewCart();

        cartA.AddOrIncrement(0, 101, 1);

        Assert.Single(cartA.GetLines(0));
        Assert.Empty(cartB.GetLines(0));
    }
}
