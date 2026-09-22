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
}
