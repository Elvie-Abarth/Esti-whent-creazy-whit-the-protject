namespace MarsvinWebExample.Models;

/// <summary>
/// One specific guinea pig. Stock is always exactly 1, so this type has a
/// status instead of a quantity.
/// </summary>
public sealed class Animal : Product
{
    public required string Breed { get; init; }
    public required Sex Sex { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string Colour { get; init; }

    /// <summary>Hex colours used to draw the illustration.</summary>
    public required string CoatPrimary { get; init; }
    public required string CoatSecondary { get; init; }

    public AnimalStatus Status { get; set; } = AnimalStatus.Available;

    /// <summary>Guinea pigs are social. Bonded animals are sold together.</summary>
    public int? BondedWithId { get; init; }

    public string Personality { get; init; } = "";

    public string? DescriptionEn { get; init; }
    public string? PersonalityEn { get; init; }
    public string? BreedEn { get; init; }
    public string? ColourEn { get; init; }

    /// <summary>Real photo, once the shop has one. Falls back to the drawn illustration when null.</summary>
    public string? PhotoUrl { get; init; }

    public int AgeInWeeks() =>
        (DateOnly.FromDateTime(DateTime.Today).DayNumber - DateOfBirth.DayNumber) / 7;

    /// <summary>BR3 - not sold before 4 weeks old.</summary>
    public bool IsOldEnoughToSell() => AgeInWeeks() >= 4;

    public override bool CanBeAddedToCart(int quantity) =>
        quantity == 1 && Status == AnimalStatus.Available && IsOldEnoughToSell();

    public string SexName => Sex == Sex.Boar ? "Han" : "Hun";
    public string SexNameEn => Sex == Sex.Boar ? "Male" : "Female";

    public string AgeText
    {
        get
        {
            var weeks = AgeInWeeks();
            if (weeks < 16) return $"{weeks} uger";
            var months = weeks / 4;
            return months < 24 ? $"{months} måneder" : $"{months / 12} år";
        }
    }

    public string AgeTextEn
    {
        get
        {
            var weeks = AgeInWeeks();
            if (weeks < 16) return $"{weeks} weeks";
            var months = weeks / 4;
            return months < 24 ? $"{months} months" : $"{months / 12} years";
        }
    }

    public string StatusText => Status switch
    {
        AnimalStatus.Available => "Ledig",
        AnimalStatus.Reserved  => "Reserveret",
        AnimalStatus.Sold      => "Solgt",
        _                      => "Ikke til salg"
    };

    public string StatusTextEn => Status switch
    {
        AnimalStatus.Available => "Available",
        AnimalStatus.Reserved  => "Reserved",
        AnimalStatus.Sold      => "Sold",
        _                      => "Not for sale"
    };

    public string StatusClass => Status switch
    {
        AnimalStatus.Available => "status status--ledig",
        AnimalStatus.Reserved  => "status status--reserveret",
        _                      => "status status--solgt"
    };
}
