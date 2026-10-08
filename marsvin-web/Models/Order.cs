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

// A record rather than a class for `with`: SqlOrderStore reads every order's
// header first and attaches its items afterwards.
public sealed record Order
{
    public int OrderId { get; init; }

    /// <summary>Where the order is on its way to the buyer - moved along by staff from Admin/Orders.</summary>
    public OrderStatus Status { get; init; } = OrderStatus.Placed;

    /// <summary>Null once the buyer's account has been deleted, or always for a guest checkout - the order itself is kept either way.</summary>
    public int? UserId { get; init; }

    public required decimal TotalPrice { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyList<OrderItem> Items { get; init; }

    public DeliveryMethod DeliveryMethod { get; init; } = DeliveryMethod.Pickup;

    /// <summary>Only set when <see cref="DeliveryMethod"/> is Shipping.</summary>
    public string? ShippingAddress { get; init; }

    /// <summary>Only set when <see cref="DeliveryMethod"/> is Shipping.</summary>
    public ShippingCarrier? ShippingCarrier { get; init; }

    /// <summary>Already included in <see cref="TotalPrice"/>. 0 for a pickup order.</summary>
    public decimal ShippingCost { get; init; }

    /// <summary>
    /// Weight and parcel count of what's actually shipped (never the animals) -
    /// snapshots from the moment of purchase, like ShippingCost itself, so a
    /// later change to a product's weight or to the rates doesn't rewrite an
    /// old order. Null for a pickup order, or one placed before these existed.
    /// </summary>
    public int? ShippingWeightGrams { get; init; }

    public int? ParcelCount { get; init; }

    public decimal ItemsTotal => TotalPrice - ShippingCost;

    /// <summary>
    /// Set (together with <see cref="CompanyCvr"/>) when the order was placed
    /// on behalf of a company - shown on the receipt with the VAT amount, the
    /// way a company needs it for its bookkeeping. Null for a private purchase.
    /// </summary>
    public string? CompanyName { get; init; }

    public string? CompanyCvr { get; init; }

    /// <summary>Danish prices include 25% VAT, so the VAT is a fifth of the total.</summary>
    public decimal VatAmount => Math.Round(TotalPrice * 0.2m, 2);

    /// <summary>The carrier's track-and-trace number, typed in by staff when the parcel is handed over. Null until then.</summary>
    public string? TrackingNumber { get; init; }

    /// <summary>Given at checkout so the shop or the carrier can reach the buyer about this order. Null if none was given.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>The buyer confirmed being 16 or older - asked only when the order contains a guinea pig.</summary>
    public bool AgeConfirmed { get; init; }

    /// <summary>Estimated delivery dates, counted from the day the order was placed. Null for a pickup order.</summary>
    public (DateOnly Earliest, DateOnly Latest)? ExpectedDelivery =>
        DeliveryMethod == DeliveryMethod.Shipping && ShippingCarrier is ShippingCarrier carrier
            ? ShippingCalculator.DeliveryWindow(carrier, DateOnly.FromDateTime(CreatedAt))
            : null;

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Card;

    /// <summary>
    /// Only populated by GetAllOrders (the admin order list) - null for
    /// FindForUser/GetOrdersForUser, where the caller already knows who they
    /// are. Reflects UserId: null once the buyer's account has been deleted,
    /// the same as UserId itself (and always null for a guest order - see
    /// GuestName/GuestEmail instead).
    /// </summary>
    public string? BuyerDisplayName { get; init; }

    public string? BuyerEmail { get; init; }

    /// <summary>
    /// Only set for a guest checkout (UserId is null and this app never had
    /// an account to delete) - collected directly on the payment form
    /// instead of looked up from Users, since there's no account to look up.
    /// </summary>
    public string? GuestName { get; init; }

    public string? GuestEmail { get; init; }
}
