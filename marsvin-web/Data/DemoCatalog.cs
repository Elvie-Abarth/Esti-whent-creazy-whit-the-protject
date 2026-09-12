using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Hard-coded demo data so the example runs with no database.
/// In the real project this is replaced by ADO.NET repositories
/// reading from SQL Server with parameterised SqlCommand.
/// </summary>
public sealed class DemoCatalog : ICatalog
{
    private static DateOnly WeeksAgo(int weeks) =>
        DateOnly.FromDateTime(DateTime.Today.AddDays(-7 * weeks));

    public IReadOnlyList<Animal> Animals { get; } =
    [
        new Animal
        {
            ProductId = 1, Name = "Pelle", Breed = "Abyssinier", Sex = Sex.Boar,
            DateOfBirth = WeeksAgo(11), Colour = "Rød med hvid", Price = 450,
            CoatPrimary = "#B5643C", CoatSecondary = "#F3EAD8", BondedWithId = 2,
            Personality = "Den nysgerrige af de to. Står altid først ved tremmerne når høet kommer.",
            Description = "Abyssinier med otte tydelige rosetter. Vant til at blive håndteret.",
            BreedEn = "Abyssinian", ColourEn = "Red with white",
            PersonalityEn = "The curious one of the two. Always first at the bars when the hay arrives.",
            DescriptionEn = "Abyssinian with eight distinct rosettes. Used to being handled."
        },
        new Animal
        {
            ProductId = 2, Name = "Basse", Breed = "Abyssinier", Sex = Sex.Boar,
            DateOfBirth = WeeksAgo(11), Colour = "Sort med hvid", Price = 450,
            CoatPrimary = "#3A3733", CoatSecondary = "#F3EAD8", BondedWithId = 1,
            Personality = "Mere forsigtig. Lader Pelle gå først, men popcorner mest.",
            Description = "Kuldbror til Pelle. De to har været sammen siden fødslen.",
            BreedEn = "Abyssinian", ColourEn = "Black with white",
            PersonalityEn = "More cautious. Lets Pelle go first, but popcorns the most.",
            DescriptionEn = "Litter brother of Pelle. The two have been together since birth."
        },
        new Animal
        {
            ProductId = 3, Name = "Nougat", Breed = "Glathåret", Sex = Sex.Sow,
            DateOfBirth = WeeksAgo(19), Colour = "Beige", Price = 400,
            CoatPrimary = "#C99A63", CoatSecondary = "#EFD9B4", BondedWithId = 4,
            Personality = "Rolig og tålmodig. God til børn der skal lære at holde et marsvin.",
            Description = "Glathåret hun, kort pels der næsten passer sig selv.",
            BreedEn = "Smooth-haired", ColourEn = "Beige",
            PersonalityEn = "Calm and patient. Good for children learning to hold a guinea pig.",
            DescriptionEn = "Smooth-haired sow with a short coat that's nearly self-maintaining."
        },
        new Animal
        {
            ProductId = 4, Name = "Smilla", Breed = "Glathåret", Sex = Sex.Sow,
            DateOfBirth = WeeksAgo(19), Colour = "Trefarvet", Price = 400,
            CoatPrimary = "#8A5A3B", CoatSecondary = "#E8DCC0", BondedWithId = 3,
            Personality = "Højlydt. Fløjter hver gang køleskabet åbnes.",
            Description = "Trefarvet hun med et markant hvidt bælte over ryggen.",
            BreedEn = "Smooth-haired", ColourEn = "Tri-colour",
            PersonalityEn = "Loud. Whistles every time the fridge opens.",
            DescriptionEn = "Tri-colour sow with a striking white band across her back."
        },
        new Animal
        {
            ProductId = 5, Name = "Freja", Breed = "Peruviansk", Sex = Sex.Sow,
            DateOfBirth = WeeksAgo(31), Colour = "Gylden", Price = 500,
            CoatPrimary = "#D8A24A", CoatSecondary = "#F6E7C4",
            Status = AnimalStatus.Reserved,
            Personality = "Tager det roligt og lader sig børste uden protest.",
            Description = "Langhåret peruviansk hun. Kræver børstning et par gange om ugen.",
            BreedEn = "Peruvian", ColourEn = "Golden",
            PersonalityEn = "Takes it easy and lets herself be brushed without protest.",
            DescriptionEn = "Long-haired Peruvian sow. Needs brushing a couple of times a week."
        },
        new Animal
        {
            ProductId = 6, Name = "Storm", Breed = "Rex", Sex = Sex.Boar,
            DateOfBirth = WeeksAgo(3), Colour = "Grå", Price = 425,
            CoatPrimary = "#7E8A8C", CoatSecondary = "#DCE2E0",
            Status = AnimalStatus.NotForSale,
            Personality = "Stadig hos sin mor. Kan ses, men ikke hentes endnu.",
            Description = "Rex-han med kort krøllet pels. Klar til nyt hjem om en uge.",
            BreedEn = "Rex", ColourEn = "Grey",
            PersonalityEn = "Still with his mother. Can be seen, but not taken home yet.",
            DescriptionEn = "Rex boar with a short curly coat. Ready for a new home in a week."
        }
    ];

    public IReadOnlyList<StockProduct> Accessories { get; } =
    [
        new StockProduct { ProductId = 101, Name = "Timothy-hø, 2 kg", Sku = "HAY-TIM-2",
            Category = AccessoryCategory.Hay, StockQuantity = 48, Price = 89, Unit = "pose",
            Description = "Grundfoderet. Skal være tilgængeligt hele døgnet - marsvin spiser hø nærmest konstant.",
            NameEn = "Timothy hay, 2 kg",
            DescriptionEn = "The staple feed. Should be available around the clock - guinea pigs eat hay almost constantly." },
        new StockProduct { ProductId = 102, Name = "Engblanding med kløver, 1 kg", Sku = "HAY-ENG-1",
            Category = AccessoryCategory.Hay, StockQuantity = 26, Price = 65, Unit = "pose",
            Description = "Blandet enghø. Bruges som variation oven på det daglige timothy.",
            NameEn = "Meadow mix with clover, 1 kg",
            DescriptionEn = "Mixed meadow hay. Used as variation on top of the daily timothy." },
        new StockProduct { ProductId = 103, Name = "Pillefoder med C-vitamin, 1,5 kg", Sku = "FOD-PEL-15",
            Category = AccessoryCategory.Food, StockQuantity = 31, Price = 119, Unit = "pose",
            Description = "Marsvin kan ikke selv danne C-vitamin. Et tilskud hver dag er ikke til forhandling.",
            NameEn = "Pellet feed with vitamin C, 1.5 kg",
            DescriptionEn = "Guinea pigs can't produce vitamin C themselves. A daily supplement isn't optional." },
        new StockProduct { ProductId = 104, Name = "Tørret mælkebøtte, 100 g", Sku = "FOD-MAE-100",
            Category = AccessoryCategory.Food, StockQuantity = 60, Price = 45, Unit = "pose",
            Description = "Godbid. En lille håndfuld et par gange om ugen.",
            NameEn = "Dried dandelion, 100 g",
            DescriptionEn = "A treat. A small handful a couple of times a week." },
        new StockProduct { ProductId = 105, Name = "Bur 120 x 60 cm", Sku = "BUR-120",
            Category = AccessoryCategory.Cage, StockQuantity = 7, Price = 899, Unit = "stk",
            Description = "Minimum for to marsvin. Mindre bure findes, men de er for små.",
            NameEn = "Cage 120 x 60 cm",
            DescriptionEn = "Minimum size for two guinea pigs. Smaller cages exist, but they're too small." },
        new StockProduct { ProductId = 106, Name = "Løbegård til gulv, 1,4 m", Sku = "BUR-LOB-14",
            Category = AccessoryCategory.Cage, StockQuantity = 12, Price = 549, Unit = "stk",
            Description = "Til den daglige time uden for buret.",
            NameEn = "Floor playpen, 1.4 m",
            DescriptionEn = "For the daily hour outside the cage." },
        new StockProduct { ProductId = 107, Name = "Hus i ubehandlet træ", Sku = "HUS-TRA",
            Category = AccessoryCategory.House, StockQuantity = 19, Price = 179, Unit = "stk",
            Description = "To udgange, så ingen bliver fanget i hjørnet. Køb ét hus per marsvin.",
            NameEn = "Untreated wood house",
            DescriptionEn = "Two exits, so no one gets cornered. Buy one house per guinea pig." },
        new StockProduct { ProductId = 108, Name = "Hængekøje i fleece", Sku = "HUS-KOJ",
            Category = AccessoryCategory.House, StockQuantity = 23, Price = 149, Unit = "stk",
            Description = "Vaskbar ved 40 grader. Findes i fem farver.",
            NameEn = "Fleece hammock",
            DescriptionEn = "Washable at 40 degrees. Available in five colours." },
        new StockProduct { ProductId = 109, Name = "Gnavepinde af pil, 10 stk", Sku = "LEG-PIL-10",
            Category = AccessoryCategory.Toy, StockQuantity = 44, Price = 59, Unit = "bundt",
            Description = "Tænderne vokser hele livet. Noget hårdt at gnave i holder dem i skak.",
            NameEn = "Willow chew sticks, 10 pcs",
            DescriptionEn = "Teeth grow throughout life. Something hard to chew keeps them in check." },
        new StockProduct { ProductId = 110, Name = "Tunnel i flettet græs", Sku = "LEG-TUN",
            Category = AccessoryCategory.Toy, StockQuantity = 15, Price = 99, Unit = "stk",
            Description = "Kan både løbes igennem og spises. Begge dele sker.",
            NameEn = "Woven grass tunnel",
            DescriptionEn = "Can be run through and eaten. Both happen." },
        new StockProduct { ProductId = 111, Name = "Hampstrøelse, 10 l", Sku = "STR-HAM-10",
            Category = AccessoryCategory.Bedding, StockQuantity = 38, Price = 129, Unit = "pose",
            Description = "Støvfattig. Bedre for luftvejene end spåner.",
            NameEn = "Hemp bedding, 10 l",
            DescriptionEn = "Low-dust. Better for the airways than wood shavings." },
        new StockProduct { ProductId = 112, Name = "Fleecemåtte, 100 x 60 cm", Sku = "STR-FLE-100",
            Category = AccessoryCategory.Bedding, StockQuantity = 21, Price = 199, Unit = "stk",
            Description = "Vaskbart alternativ til løs strøelse. Holder i årevis.",
            NameEn = "Fleece liner, 100 x 60 cm",
            DescriptionEn = "Washable alternative to loose bedding. Lasts for years." }
    ];

    public Animal? FindAnimal(int id) => Animals.FirstOrDefault(a => a.ProductId == id);

    public IEnumerable<Animal> AvailableAnimals() =>
        Animals.Where(a => a.Status == AnimalStatus.Available);

    /// <summary>Groups bonded animals together so they are always shown as a pair.</summary>
    public IEnumerable<IReadOnlyList<Animal>> AnimalGroups()
    {
        var seen = new HashSet<int>();
        foreach (var animal in Animals)
        {
            if (!seen.Add(animal.ProductId)) continue;

            var partner = animal.BondedWithId is int id ? FindAnimal(id) : null;
            if (partner is not null && seen.Add(partner.ProductId))
                yield return [animal, partner];
            else
                yield return [animal];
        }
    }
}
