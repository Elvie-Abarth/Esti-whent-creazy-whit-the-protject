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
