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
        },

        // Real photos of the author's own guinea pigs (see wwwroot/img/animals),
        // added alongside the fictional demo animals above rather than
        // replacing them.
        new Animal
        {
            ProductId = 7, Name = "Shelly", Breed = "Amerikaner", Sex = Sex.Sow,
            DateOfBirth = new DateOnly(2022, 1, 1), Colour = "Sort med hvid", Price = 450,
            CoatPrimary = "#2B2B2B", CoatSecondary = "#F5F0E6", PhotoUrl = "/img/animals/shelly.jpg",
            Personality = "Venlig og snakkesalig - fløjter gerne når der er noget godt i skålen.",
            Description = "Amerikaner-hun med et markant hvidt stribe ned ad ansigtet. Fødselsår kendt, præcis dato usikker.",
            BreedEn = "American", ColourEn = "Black with white",
            PersonalityEn = "Friendly and vocal - happy to whistle when there's something good in the bowl.",
            DescriptionEn = "American sow with a striking white blaze down her face. Birth year known, exact date uncertain."
        },
        new Animal
        {
            ProductId = 8, Name = "Bo", Breed = "Peruviansk", Sex = Sex.Boar,
            DateOfBirth = new DateOnly(2019, 1, 1), Colour = "Sølvbrun og creme, langhåret", Price = 475,
            CoatPrimary = "#8B6F47", CoatSecondary = "#E8DFC8", PhotoUrl = "/img/animals/bo.jpg",
            Personality = "Meget kærlig fyr, også kendt som Børge og Einstein.",
            Description = "Langhåret peruviansk han. Kun fødselsår kendt.",
            BreedEn = "Peruvian", ColourEn = "Silver-brown and cream, long-haired",
            PersonalityEn = "A super sweet guy, also known as Børge and Einstein.",
            DescriptionEn = "Long-haired Peruvian boar. Only birth year known."
        },
        new Animal
        {
            ProductId = 9, Name = "Eefje", Breed = "Enfarvet", Sex = Sex.Sow,
            DateOfBirth = new DateOnly(2022, 9, 1), Colour = "Chokoladebrun", Price = 400,
            CoatPrimary = "#3D2418", CoatSecondary = "#5C3A28", PhotoUrl = "/img/animals/eefje.jpg",
            Personality = "Sød, men bange for det meste. Fløjter usikkert når hun virkelig gerne vil have en godbid - og kan godt være lidt af en bølle.",
            Description = "Glathåret hun i en dyb, ensfarvet chokoladebrun.",
            BreedEn = "Self", ColourEn = "Chocolate brown",
            PersonalityEn = "Sweet but scared of everything. Squeaks unsurely when really wanting a treat - and can be a bit of a bully.",
            DescriptionEn = "Smooth-coated sow in a deep, solid chocolate brown."
        },
        new Animal
        {
            ProductId = 10, Name = "Cookie", Breed = "Abyssinier", Sex = Sex.Boar,
            DateOfBirth = new DateOnly(2024, 10, 27), Colour = "Sort, rødbrun og grå", Price = 450,
            CoatPrimary = "#201C1A", CoatSecondary = "#C97A2E", PhotoUrl = "/img/animals/cookie.jpg",
            Personality = "Elsker at blive kælet med.",
            Description = "Abyssinier-han med rosetmønster i sort, rødbrun og grå. Hedder Eigil på stamtavlen.",
            BreedEn = "Abyssinian", ColourEn = "Black, rust and grey",
            PersonalityEn = "Loves cuddles.",
            DescriptionEn = "Abyssinian boar with a rosette pattern in black, rust and grey. Registered as Eigil on his pedigree."
        },
        new Animal
        {
            ProductId = 11, Name = "Tot", Breed = "Abyssinier", Sex = Sex.Sow,
            DateOfBirth = new DateOnly(2022, 4, 14), Colour = "Sort-hvid roan", Price = 425,
            CoatPrimary = "#23211F", CoatSecondary = "#E8E2D8", PhotoUrl = "/img/animals/tot.png",
            BondedWithId = 12,
            Personality = "Lillesøster til Lente - en snakkesalig, venlig sjæl der elsker at blive kælet med.",
            Description = "Abyssinier-hun med sort-hvidt roan-mønster. Søster-par med Lente, født samme dag.",
            BreedEn = "Abyssinian", ColourEn = "Black-and-white roan",
            PersonalityEn = "Little sister of Lente - a vocal, friendly sweetheart who absolutely loves cuddles.",
            DescriptionEn = "Abyssinian sow with a black-and-white roan coat. Sister pair with Lente, born the same day."
        },
        new Animal
        {
            ProductId = 12, Name = "Lente", Breed = "Abyssinier", Sex = Sex.Sow,
            DateOfBirth = new DateOnly(2022, 4, 14), Colour = "Trefarvet - sort, hvid og rødbrun", Price = 425,
            CoatPrimary = "#1E1B19", CoatSecondary = "#C1702E", PhotoUrl = "/img/animals/lente.png",
            BondedWithId = 11,
            Personality = "Bulldozeren - storesøster til Tot.",
            Description = "Abyssinier-hun, trefarvet med markant rødbrun rosette. Tåler ikke persille.",
            BreedEn = "Abyssinian", ColourEn = "Tri-colour - black, white and ginger",
            PersonalityEn = "The bulldozer - big sister of Tot.",
            DescriptionEn = "Abyssinian sow, tri-colour with a striking ginger rosette. Can't tolerate parsley."
        }
    ];

    public IReadOnlyList<StockProduct> Accessories { get; } =
    [
        // Photo: "Rusty The Smiling Guinea Pig" by Christina Telep (500px),
        // CC BY 3.0, via Wikimedia Commons - a real photo, not a stock
        // studio shot of the bag itself, but it's this product in use.
        new StockProduct { ProductId = 101, Name = "Timothy-hø, 2 kg", Sku = "HAY-TIM-2",
            Category = AccessoryCategory.Hay, StockQuantity = 48, Price = 89, Unit = "pose",
            Description = "Grundfoderet. Skal være tilgængeligt hele døgnet - marsvin spiser hø nærmest konstant.",
            NameEn = "Timothy hay, 2 kg",
            DescriptionEn = "The staple feed. Should be available around the clock - guinea pigs eat hay almost constantly.",
            PhotoUrl = "/img/products/guinea-pig-with-hay.jpg" },
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
            DescriptionEn = "Washable alternative to loose bedding. Lasts for years." },
        new StockProduct { ProductId = 113, Name = "Alfalfa-hø til unger og diegivende, 1 kg", Sku = "HAY-ALF-1",
            Category = AccessoryCategory.Hay, StockQuantity = 22, Price = 79, Unit = "pose",
            Description = "Højere protein- og kalkindhold end timothy. Kun til unger, diegivende eller syge dyr - for kalorietungt til voksne raske marsvin i det daglige.",
            NameEn = "Alfalfa hay for pups and nursing sows, 1 kg",
            DescriptionEn = "Higher protein and calcium than timothy. Only for pups, nursing sows, or sick animals - too calorie-dense for healthy adults day to day." },
        new StockProduct { ProductId = 114, Name = "Kornfri pillefoder, 1 kg", Sku = "FOD-KOR-1",
            Category = AccessoryCategory.Food, StockQuantity = 27, Price = 135, Unit = "pose",
            Description = "Til marsvin med sart mave. Ingen korn eller tilsat sukker.",
            NameEn = "Grain-free pellet feed, 1 kg",
            DescriptionEn = "For guinea pigs with a sensitive stomach. No grain or added sugar." },
        new StockProduct { ProductId = 115, Name = "Tørret grøntsagsmix, 200 g", Sku = "FOD-GRO-200",
            Category = AccessoryCategory.Food, StockQuantity = 34, Price = 55, Unit = "pose",
            Description = "Peberfrugt, gulerod og squash. Godbid, ikke en erstatning for frisk grønt.",
            NameEn = "Dried vegetable mix, 200 g",
            DescriptionEn = "Bell pepper, carrot, and squash. A treat, not a substitute for fresh vegetables." },
        new StockProduct { ProductId = 116, Name = "Modulbur, 3 etager", Sku = "BUR-MOD-3",
            Category = AccessoryCategory.Cage, StockQuantity = 5, Price = 1299, Unit = "stk",
            Description = "Til flere marsvin, eller mere gulvplads uden mere gulvareal. Rammerne skrues sammen uden værktøj.",
            NameEn = "Modular cage, 3 tiers",
            DescriptionEn = "For several guinea pigs, or more floor space without more floor area. Frames assemble without tools." },
        new StockProduct { ProductId = 117, Name = "Rejsebur til transport", Sku = "BUR-REJ",
            Category = AccessoryCategory.Cage, StockQuantity = 14, Price = 175, Unit = "stk",
            Description = "Til dyrlægebesøg og korte ture. For lille til at bo i - ikke en erstatning for et rigtigt bur.",
            NameEn = "Travel carrier",
            DescriptionEn = "For vet visits and short trips. Too small to live in - not a substitute for a real cage." },
        new StockProduct { ProductId = 118, Name = "Iglo i kokosnød", Sku = "HUS-KOK",
            Category = AccessoryCategory.House, StockQuantity = 17, Price = 89, Unit = "stk",
            Description = "Naturmateriale, kan gnaves på undervejs. Skiftes ud når den er gnavet igennem.",
            NameEn = "Coconut igloo",
            DescriptionEn = "Natural material, safe to chew on along the way. Replace once it's been gnawed through." },
        new StockProduct { ProductId = 119, Name = "Snack-bold med hø", Sku = "LEG-BOL",
            Category = AccessoryCategory.Toy, StockQuantity = 29, Price = 39, Unit = "stk",
            Description = "Fyldes med hø eller grøntsager. Kombinerer foder og beskæftigelse.",
            NameEn = "Hay snack ball",
            DescriptionEn = "Fill with hay or vegetables. Combines feeding and enrichment." },
        new StockProduct { ProductId = 120, Name = "Snuse-måtte til foder", Sku = "LEG-SNU",
            Category = AccessoryCategory.Toy, StockQuantity = 18, Price = 129, Unit = "stk",
            Description = "Skjuler tørrede urter og grøntsager i stoflommer. Stimulerer den naturlige søgeadfærd. Ikke et løbehjul - marsvins ryg tåler ikke det, i modsætning til hamstres.",
            NameEn = "Foraging snuffle mat",
            DescriptionEn = "Hides dried herbs and vegetables in fabric pockets, stimulating natural foraging behaviour. Not a running wheel - unlike hamsters, a guinea pig's spine isn't built for one." },
        new StockProduct { ProductId = 121, Name = "Papirstrøelse, støvfri, 15 l", Sku = "STR-PAP-15",
            Category = AccessoryCategory.Bedding, StockQuantity = 25, Price = 139, Unit = "pose",
            Description = "Alternativ til hamp for dyr med planteallergi. Meget absorberende.",
            NameEn = "Dust-free paper bedding, 15 l",
            DescriptionEn = "Alternative to hemp for animals with a plant allergy. Highly absorbent." },
        // Photo: "Muizenkooiwaterfles.JPG", public domain, via Wikimedia Commons.
        new StockProduct { ProductId = 122, Name = "Vandflaske, 600 ml", Sku = "PLJ-VAN-600",
            Category = AccessoryCategory.Care, StockQuantity = 33, Price = 49, Unit = "stk",
            Description = "Monteres udenpå buret. Skift vandet dagligt, selvom flasken ser fyldt ud.",
            NameEn = "Water bottle, 600 ml",
            DescriptionEn = "Mounts on the outside of the cage. Change the water daily, even if the bottle looks full.",
            PhotoUrl = "/img/products/water-bottle.jpg" },
        new StockProduct { ProductId = 123, Name = "Høhæk i naturtræ", Sku = "PLJ-HOE-HAEK",
            Category = AccessoryCategory.Care, StockQuantity = 20, Price = 99, Unit = "stk",
            Description = "Holder høet samlet og tørt frem for spredt ud over buret.",
            NameEn = "Wooden hay rack",
            DescriptionEn = "Keeps hay together and dry instead of scattered across the cage." },
        new StockProduct { ProductId = 124, Name = "Negleklipper til smådyr", Sku = "PLJ-NEG",
            Category = AccessoryCategory.Care, StockQuantity = 24, Price = 65, Unit = "stk",
            Description = "Klip hver 4.-6. uge. For lange negle gør det svært at gå ordentligt.",
            NameEn = "Small-animal nail clippers",
            DescriptionEn = "Trim every 4-6 weeks. Overgrown nails make walking properly difficult." },
        new StockProduct { ProductId = 125, Name = "Pelsbørste, blød", Sku = "PLJ-BOR",
            Category = AccessoryCategory.Care, StockQuantity = 28, Price = 55, Unit = "stk",
            Description = "Særligt vigtig til langhårede racer som peruaner - børstes flere gange om ugen.",
            NameEn = "Soft grooming brush",
            DescriptionEn = "Especially important for long-haired breeds like Peruvians - brush several times a week." },
        new StockProduct { ProductId = 126, Name = "C-vitamin dråber, 50 ml", Sku = "PLJ-CVIT-50",
            Category = AccessoryCategory.Care, StockQuantity = 30, Price = 89, Unit = "stk",
            Description = "Supplement til pillefoderet - særligt nyttigt hvis et dyr spiser mindre end normalt.",
            NameEn = "Vitamin C drops, 50 ml",
            DescriptionEn = "A supplement to pellet feed - especially useful if an animal is eating less than usual." }
    ];

    public Animal? FindAnimal(int id) => Animals.FirstOrDefault(a => a.ProductId == id);

    public Product? FindProduct(int id) =>
        (Product?)Animals.FirstOrDefault(a => a.ProductId == id) ??
        Accessories.FirstOrDefault(a => a.ProductId == id);

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
