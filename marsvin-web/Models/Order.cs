namespace MarsvinWebExample.Models;

public sealed class OrderItem
{
    public required int ProductId { get; init; }
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }

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
