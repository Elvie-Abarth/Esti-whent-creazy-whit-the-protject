namespace MarsvinWebExample.Models;

public sealed class OrderItem
{
    public required int ProductId { get; init; }
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }

    /// <summary>
    /// A snapshot, same as ProductName/UnitPrice - whether this line was a
    /// guinea pig at the moment of purchase. Drives the "still needs pickup"
    /// half of a shipped order's confirmation, and survives the animal's own
    /// product listing later being edited or removed.
    /// </summary>
    public bool IsAnimal { get; init; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed class Order
{
    public int OrderId { get; init; }

    /// <summary>Null once the buyer's account has been deleted - the order itself is kept as a historical record.</summary>
    public int? UserId { get; init; }

    public required decimal TotalPrice { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyList<OrderItem> Items { get; init; }

    public DeliveryMethod DeliveryMethod { get; init; } = DeliveryMethod.Pickup;

    /// <summary>Only set when <see cref="DeliveryMethod"/> is Shipping.</summary>
    public string? ShippingAddress { get; init; }
}
