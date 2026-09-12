using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Models;

public class StockProductTests
{
    private static StockProduct MakeProduct(int stock) => new()
    {
        ProductId = 101,
        Name = "Hø",
        Description = "Test",
        Sku = "SKU-1",
        Category = AccessoryCategory.Hay,
        StockQuantity = stock,
        Price = 50
    };

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(-1, 1, false)]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    [InlineData(5, 0, false)]
    public void CanBeAddedToCart_RespectsStockAndQuantityBounds(int stock, int quantity, bool expected)
    {
        var product = MakeProduct(stock);

        Assert.Equal(expected, product.CanBeAddedToCart(quantity));
    }

    [Theory]
    [InlineData(AccessoryCategory.Hay, "Hø", "Hay")]
    [InlineData(AccessoryCategory.Food, "Foder", "Food")]
    [InlineData(AccessoryCategory.Cage, "Bure", "Cages")]
    [InlineData(AccessoryCategory.House, "Huse", "Houses")]
    [InlineData(AccessoryCategory.Toy, "Legetøj", "Toys")]
    [InlineData(AccessoryCategory.Bedding, "Strøelse", "Bedding")]
    public void CategoryName_MatchesCategory(AccessoryCategory category, string expectedDa, string expectedEn)
    {
        var typed = new StockProduct
        {
            ProductId = 1, Name = "x", Description = "x", Sku = "x",
            Category = category, StockQuantity = 1, Price = 1
        };

        Assert.Equal(expectedDa, typed.CategoryName);
        Assert.Equal(expectedEn, typed.CategoryNameEn);
    }
}
