namespace MarsvinWebExample.Models;

/// <summary>Hay, food, cages, toys - anything with a stock quantity.</summary>
public sealed class StockProduct : Product
{
    public required string Sku { get; init; }
    public required AccessoryCategory Category { get; init; }
    public int StockQuantity { get; set; }
    public string? Unit { get; init; }

    public string? NameEn { get; init; }
    public string? DescriptionEn { get; init; }

    public override bool CanBeAddedToCart(int quantity) =>
        quantity > 0 && quantity <= StockQuantity;

    public string CategoryName => Category switch
    {
        AccessoryCategory.Hay     => "Hø",
        AccessoryCategory.Food    => "Foder",
        AccessoryCategory.Cage    => "Bure",
        AccessoryCategory.House   => "Huse",
        AccessoryCategory.Toy     => "Legetøj",
        AccessoryCategory.Bedding => "Strøelse",
        _ => "Andet"
    };

    public string CategoryNameEn => Category switch
    {
        AccessoryCategory.Hay     => "Hay",
        AccessoryCategory.Food    => "Food",
        AccessoryCategory.Cage    => "Cages",
        AccessoryCategory.House   => "Houses",
        AccessoryCategory.Toy     => "Toys",
        AccessoryCategory.Bedding => "Bedding",
        _ => "Other"
    };
}
