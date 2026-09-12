namespace MarsvinWebExample.Models;

/// <summary>
/// Base type for everything the shop sells. Guinea pigs and hay are both
/// products, but they behave differently - see the two subclasses.
/// </summary>
public abstract class Product
{
    public int ProductId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }

    public abstract bool CanBeAddedToCart(int quantity);
}
