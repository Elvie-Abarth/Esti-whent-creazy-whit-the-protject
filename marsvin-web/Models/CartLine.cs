namespace MarsvinWebExample.Models;

/// <summary>One line in a shopping cart, resolved against the current product data.</summary>
public sealed class CartLine
{
    public required int ProductId { get; init; }
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required bool IsAnimal { get; init; }

    public decimal LineTotal => UnitPrice * Quantity;
}
