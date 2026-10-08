namespace MarsvinWebExample.Models;

public enum ContactTopic
{
    Order,
    GuineaPig,
    Accessory,
    Company,
    Other
}

public static class ContactTopicExtensions
{
    public static string DisplayName(this ContactTopic topic, bool english = false) => topic switch
    {
        ContactTopic.Order => english ? "An order" : "En ordre",
        ContactTopic.GuineaPig => english ? "A guinea pig" : "Et marsvin",
        ContactTopic.Accessory => english ? "Accessories" : "Tilbehør",
        ContactTopic.Company => english ? "Company purchase" : "Køb som virksomhed",
        _ => english ? "Something else" : "Noget andet"
    };
}

/// <summary>One message sent through the contact form - read by staff on Admin/Messages.</summary>
public sealed class ContactMessage
{
    public int ContactMessageId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required ContactTopic Topic { get; init; }

    /// <summary>Optional - the order the message is about, as typed by the sender (not verified to be theirs).</summary>
    public int? OrderId { get; init; }

    public required string Message { get; init; }
    public DateTime CreatedAt { get; init; }
}

public enum DonationKind
{
    Money,
    Products
}

/// <summary>
/// A donation towards the guinea pigs the shop takes back and rehomes -
/// either an amount (a demo, like the checkout: nothing is charged) or an
/// offer of food, hay or equipment to hand in. Read by staff on Admin/Donations.
/// </summary>
public sealed class Donation
{
    public int DonationId { get; init; }
    public required DonationKind Kind { get; init; }

    /// <summary>Only set for a Money donation.</summary>
    public decimal? AmountKr { get; init; }

    /// <summary>Only set for a Products donation - what's being offered, in the donor's own words.</summary>
    public string? ItemDescription { get; init; }

    /// <summary>Optional for money (anonymous is fine); required for products, so the shop can arrange the hand-over.</summary>
    public string? DonorName { get; init; }

    public string? DonorEmail { get; init; }
    public string? Message { get; init; }
    public DateTime CreatedAt { get; init; }
}
