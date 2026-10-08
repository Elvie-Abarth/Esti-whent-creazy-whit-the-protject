namespace MarsvinWebExample.Models;

public enum Sex
{
    /// <summary>Han</summary>
    Boar,
    /// <summary>Hun</summary>
    Sow
}

public enum AnimalStatus
{
    Available,
    Reserved,
    Sold,
    NotForSale
}

public enum AccessoryCategory
{
    Hay,
    Food,
    Cage,
    House,
    Toy,
    Bedding,
    Care
}

public enum TimeOffStatus
{
    Pending,
    Approved,
    Denied
}

public enum DeliveryMethod
{
    Pickup,
    Shipping
}

/// <summary>Only meaningful when <see cref="DeliveryMethod"/> is Shipping.</summary>
public enum ShippingCarrier
{
    PostNord,
    Gls,
    DaoPakkeshop
}

public enum PaymentMethod
{
    Card,
    MobilePay
}

/// <summary>Stored as a number in dbo.Orders.Status - only ever add to the end.</summary>
public enum OrderStatus
{
    Placed,
    Processing,
    /// <summary>Handed to the carrier - or, for a pickup order, ready to be collected.</summary>
    Sent,
    /// <summary>Delivered - or, for a pickup order, collected.</summary>
    Completed,
    /// <summary>Called off before it was sent; its items are back in stock. Final - never changed again.</summary>
    Cancelled
}

public static class OrderStatusExtensions
{
    // The last two steps mean something different for an order that's being
    // collected in store than for one that's being shipped.
    public static string DisplayName(this OrderStatus status, DeliveryMethod deliveryMethod, bool english = false)
    {
        var shipping = deliveryMethod == DeliveryMethod.Shipping;
        return status switch
        {
            OrderStatus.Placed => english ? "Order placed" : "Ordre modtaget",
            OrderStatus.Processing => english ? "Being prepared" : "Under behandling",
            OrderStatus.Sent => shipping
                ? (english ? "Sent" : "Afsendt")
                : (english ? "Ready for pickup" : "Klar til afhentning"),
            OrderStatus.Completed => shipping
                ? (english ? "Delivered" : "Leveret")
                : (english ? "Picked up" : "Afhentet"),
            OrderStatus.Cancelled => english ? "Cancelled" : "Annulleret",
            _ => status.ToString()
        };
    }
}

// PostNord/GLS are the same name in Danish and English; only "Pakkeshop"
// (parcel shop) needs translating, so one method covers both languages
// rather than duplicating a switch per call site (Confirmation, Profile's
// and Admin's order history, and the order confirmation email).
public static class ShippingCarrierExtensions
{
    public static string DisplayName(this ShippingCarrier carrier, bool english = false) => carrier switch
    {
        ShippingCarrier.PostNord => "PostNord",
        ShippingCarrier.Gls => "GLS",
        ShippingCarrier.DaoPakkeshop => english ? "DAO parcel shop" : "DAO Pakkeshop",
        _ => carrier.ToString()
    };

    /// <summary>PostNord brings the parcel to the door; GLS and DAO leave it at a parcel shop for the buyer to collect.</summary>
    public static bool DeliversToParcelShop(this ShippingCarrier carrier) => carrier != ShippingCarrier.PostNord;

    /// <summary>
    /// Where the parcel ends up, for the given address. Which parcel shop
    /// exactly is the carrier's call (the one nearest the address), not
    /// something this demo can look up without their API.
    /// </summary>
    public static string DestinationText(this ShippingCarrier carrier, string? address, bool english = false)
    {
        if (!carrier.DeliversToParcelShop())
            return english ? $"your door at {address}" : $"din dør på {address}";

        // DAO's DisplayName already says "parcel shop"; GLS's doesn't.
        var shop = carrier == ShippingCarrier.Gls
            ? (english ? "GLS parcel shop" : "GLS Pakkeshop")
            : carrier.DisplayName(english);
        return english
            ? $"the {shop} nearest {address} - the carrier messages you when it's ready to collect"
            : $"{shop} nærmest {address} - fragtselskabet giver besked, når pakken kan hentes";
    }
}
