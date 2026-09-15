namespace MarsvinWebExample.Models;

/// <summary>One line in a shopping cart, resolved against the current product data.</summary>
public sealed class CartLine
{
    public required int ProductId { get; init; }
    public required string ProductName { get; init; }

    /// <summary>
    /// Null for an animal (its name is a proper noun, the same in both
    /// languages) or an accessory that's never had one entered - the
    /// English half of a Bilingual message built from this line should
    /// fall back to ProductName in either case, the same way the rest of
    /// the site treats an unset *En field.
    /// </summary>
    public string? ProductNameEn { get; init; }

    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required bool IsAnimal { get; init; }

    public decimal LineTotal => UnitPrice * Quantity;
}
