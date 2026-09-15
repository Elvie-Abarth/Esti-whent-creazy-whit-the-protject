using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

// Mirrors the content of Pages/Foderliste.cshtml for FoodListPdfDocument.
// Keep this in sync by hand when that page's lists change - see the note on
// GuideListItem/GuideListSection for why it isn't the other way around.
public static class FoodListData
{
    public const string TitleDa = "Marsvins foderliste";
    public const string TitleEn = "Guinea pig food list";

    public static readonly IReadOnlyList<(string Da, string En)> IntroParagraphs =
    [
        ("Hvad der er sikkert hver dag, hvad der er fint i små mængder, og hvad du aldrig skal give.",
         "What's safe every day, what's fine in small amounts, and what to never give."),
        ("Skift mellem et par forskellige ting fra dagslisten i stedet for at give de samme to grøntsager hver dag — variation er det, der reelt giver dem det fulde spekter af næringsstoffer, ikke én bestemt \"supergrøntsag\".",
         "Rotate a few different items from the daily list rather than feeding the same two vegetables every day — variety is what actually gets them the full spread of nutrients, not any single \"superfood\" vegetable."),
        ("Dækker grøntsager, frugt, og — siden marsvineejere ofte plukker grønt direkte fra haven — de ukrudtsplanter, blomster og hækplanter, der oftest dukker op, enten som et godt fund eller som en reel risiko.",
         "Covers vegetables, fruit, and — since guinea pig owners often pick greens straight from the garden — the weeds, flowers, and hedge plants that come up most often as either a good find or a real risk."),
    ];

    public static readonly IReadOnlyList<GuideListSection> FoodSections =
    [
        new("Sikkert, hver dag", "Safe, every day", "safe",
        [
            new("Hø", "Hay", "Ubegrænset, altid tilgængeligt", "Unlimited, always available"),
            new("Marsvine-specifikt pillefoder", "Guinea-pig-specific pellets", "En afmålt daglig portion", "A measured daily portion"),
            new("Frisk vand", "Fresh water", "Tjekket dagligt, ikke bare fyldt op", "Checked daily, not just topped up"),
            new("Peberfrugt", "Bell pepper", "Højt C-vitaminindhold, især rød og gul", "High in vitamin C, especially red and yellow"),
            new("Agurk", "Cucumber"),
            new("Romainesalat, rucola, feldsalat", "Romaine, rocket, lamb's lettuce"),
            new("Tomat", "Tomato", "Kun kødet - ikke stilk eller blade", "Flesh only - not the stem or leaves"),
            new("Broccoli", "Broccoli", "Små mængder - kan give luft i maven", "Small amounts - can cause gas"),
            new("Selleri", "Celery", "Fjern de seje tråde først - ellers kvælningsfare", "Cut the stringy fibres first - a choking hazard otherwise"),
            new("Squash / courgette", "Courgette / squash"),
            new("Grønne bønner", "Green beans"),
            new("Friske mælkebøtteblade", "Fresh dandelion greens", "Fra et sted der ikke er sprøjtet, ikke fra vejkanten", "From an unsprayed patch, not the roadside"),
            new("Basilikum", "Basil"),
            new("Koriander", "Coriander"),
            new("Gulerodstoppe (det grønne)", "Carrot tops (the leafy greens)", "Ofte mere nærende end selve roden", "Often more nutritious than the root itself"),
            new("Fennikel, knold og top", "Fennel, bulb and fronds", "En favorit hos de fleste marsvin", "A favourite for most guinea pigs"),
            new("Pak choi", "Bok choy / pak choi"),
            new("Radicchio og julesalat", "Radicchio and endive"),
            new("Brøndkarse", "Watercress", "Skarp smag - nogle marsvin bryder sig ikke om den", "Peppery — some guinea pigs turn their nose up at it"),
            new("Dild", "Dill"),
            new("Oregano og timian", "Oregano and thyme"),
            new("Mynte", "Mint", "Kraftig smag - lidt rækker langt", "Strong flavour — a little goes a long way"),
            new("Radisetoppe (det grønne)", "Radish tops (the leafy greens)"),
        ]),
        new("Fint i små mængder, et par gange om ugen", "Fine in small amounts, a couple of times a week", "moderate",
        [
            new("Gulerod", "Carrot", "Højt sukkerindhold", "High in sugar"),
            new("Æble", "Apple", "Uden kerner - de indeholder spor af cyanid", "No seeds - they contain trace cyanide"),
            new("Jordbær, banan, blåbær, hindbær", "Strawberry, banana, blueberry, raspberry", "Højt sukkerindhold - godbid-størrelse", "High in sugar - treat-sized portions"),
            new("Tørret mælkebøtte", "Dried dandelion"),
            new("Majs", "Corn", "Små mængder - højt stivelsesindhold", "Small amounts - high in starch"),
            new("Ærter", "Peas"),
            new("Rødbede", "Beetroot", "Kan farve urinen rød - harmløst, men forveksl det ikke med blod", "Can tint their urine red - harmless, but don't mistake it for blood"),
            new("Græskar", "Pumpkin"),
            new("Kål / hvidkål", "Cabbage / white cabbage", "Kan give luft i maven", "Can cause gas"),
            new("Spinat og grønkål", "Spinach and kale", "Højt i calcium/oxalsyre - et par gange om ugen, ikke dagligt", "High in calcium/oxalic acid - a couple of times a week, not daily"),
            new("Persille", "Parsley", "Højt i oxalsyre trods C-vitaminet - samme grund som spinat", "High in oxalic acid despite the vitamin C - the same reason as spinach"),
            new("Citrusfrugt (fx appelsin)", "Citrus (e.g. orange)", "Kan irritere munden i større mængder", "Can irritate the mouth in larger amounts"),
            new("Pære", "Pear", "Uden kerner, samme grund som æble", "No seeds, same reason as apple"),
            new("Melon (vandmelon, cantaloupe)", "Melon (watermelon, cantaloupe)", "Højt sukker- og vandindhold", "High in sugar and water"),
            new("Vindruer", "Grapes", "Halveret, uden kerner", "Halved, seedless"),
            new("Kiwi", "Kiwi", "Rig på C-vitamin, men sukker- og syreholdig", "High in vitamin C, but sugary and acidic"),
            new("Mango og ananas", "Mango and pineapple", "Højt sukker- og syreindhold", "High in sugar and acidity"),
            new("Blomkål og rosenkål", "Cauliflower and Brussels sprouts", "Kan give luft i maven", "Can cause gas"),
            new("Bladbeder", "Swiss chard", "Højt i oxalsyre, samme grund som spinat", "High in oxalic acid, the same reason as spinach"),
            new("Radise", "Radish", "Skarp smag - små mængder", "Sharp flavour — small amounts"),
            new("Kløver", "Clover", "Kan give luft i maven eller oppustethed i store mængder", "Can cause gas or bloat in large amounts"),
            new("Iceberg-salat", "Iceberg lettuce", "Ikke giftig, men mest vand og næsten ingen næringsværdi - for meget kan give diarré", "Not toxic, but mostly water and almost no nutrition - too much can cause diarrhoea"),
        ]),
        new("Aldrig", "Never", "never",
        [
            new("Avocado", "Avocado", "Giftig for marsvin", "Toxic to guinea pigs"),
            new("Løg, hvidløg, porre", "Onion, garlic, leek", "Hele løg-familien - skader røde blodlegemer", "The whole allium family - damages red blood cells"),
            new("Chokolade", "Chocolate", "Giftig", "Toxic"),
            new("Mejeriprodukter", "Dairy products", "Voksne marsvin er laktoseintolerante", "Adult guinea pigs are lactose intolerant"),
            new("Rabarber", "Rhubarb", "Giftig - højt indhold af oxalsyre", "Toxic - high in oxalic acid"),
            new("Nødder og frø", "Nuts and seeds", "Kvælningsfare, for meget fedt", "Choking hazard, too much fat"),
            new("Grønne kartofler / kartoffeltop", "Green potato / potato sprouts", "Indeholder solanin - giftigt", "Contains solanine - toxic"),
            new("Brød, pasta, kød og andet menneskemad", "Bread, pasta, meat and other human food", "Ikke bygget til et marsvins fordøjelse", "Not built for a guinea pig's gut"),
            new("Svampe", "Mushrooms", "Kan være giftige og er svære at kende sikkert fra hinanden", "Can be toxic and are hard to tell apart safely"),
            new("Tørrede eller rå bønner (fx kidneybønner)", "Dried or raw beans (e.g. kidney beans)", "Giftige rå - ikke det samme som de friske grønne bønner ovenfor", "Toxic uncooked - not the same as fresh green beans above"),
        ]),
    ];

    public static readonly (string Da, string En) GardenHeading = ("Fra haven", "From the garden");

    public static readonly (string Da, string En) GardenIntro = (
        "Mange marsvineejere plukker grønt til deres marsvin direkte fra haven. Kun fra et sted du ved ikke er sprøjtet, og aldrig fra vejkanten - samme regel som mælkebøtten ovenfor.",
        "Many owners pick greens for their guinea pigs straight from the garden. Only from a patch you know hasn't been sprayed, and never from the roadside - the same rule as the dandelion above.");

    public static readonly IReadOnlyList<GuideListSection> GardenSections =
    [
        new("Sikkert", "Safe", "safe",
        [
            new("Mælkebøtte (blade og blomst)", "Dandelion (leaves and flower)"),
            new("Frisk, usprøjtet græs", "Grass, fresh and unsprayed"),
            new("Vejbred", "Plantain (the weed, not the banana relative)"),
            new("Fuglegræs", "Chickweed"),
            new("Svinemælk", "Sow thistle"),
        ]),
        new("I små mængder", "Fine in small amounts", "moderate",
        [
            new("Røllike", "Yarrow", "Kraftige aromatiske olier - en godbid en gang imellem, ikke en fast del af kosten", "Strong aromatic oils - an occasional treat, not a staple"),
            new("Brændenælde, visnet eller unge blade", "Nettle, wilted or young leaves", "Lad den visne eller hak den først - frisk nælde brænder. Højt calciumindhold, samme grund som kløver", "Wilt or chop it first - fresh nettle stings. High in calcium, the same reason as clover"),
            new("Roseblade og -kronblade", "Rose petals and leaves", "Uden torne og usprøjtet - godbid-størrelse", "Thornless and unsprayed - a treat-sized amount"),
            new("Morgenfrue", "Marigold (Calendula)", "En spiselig blomst - en godbid en gang imellem", "An edible flower - an occasional treat"),
            new("Solsikkeblade og -kronblade", "Sunflower leaves and petals", "Kun bladene og kronbladene - ikke kernerne, nævnt under Aldrig", "The leaves and petals only - not the seeds, listed under Never"),
            new("Hibiscus", "Hibiscus", "En spiselig blomst - en godbid en gang imellem", "An edible flower - an occasional treat"),
        ]),
        new("Giftigt, undgå", "Toxic, avoid", "never",
        [
            new("Fingerbøl", "Foxglove", "Giftig for hjertet", "Toxic to the heart"),
            new("Liljekonval", "Lily of the valley", "Giftig for hjertet", "Toxic to the heart"),
            new("Vedbend", "Ivy"),
            new("Smørblomst, frisk", "Buttercup, fresh", "Irriterer mund og fordøjelse", "Irritates the mouth and gut"),
            new("Rhododendron og azalea", "Rhododendron and azalea"),
            new("Oleander", "Oleander", "Meget giftig", "Highly toxic"),
            new("Taks", "Yew", "Meget giftig", "Highly toxic"),
            new("Skarntyde", "Hemlock", "Meget giftig", "Highly toxic"),
            new("Påskelilje og tulipan (løg og blade)", "Daffodil and tulip (bulbs and leaves)"),
            new("Ørnebregne", "Bracken fern"),
            new("Tomat- og kartoffelplantens blade og stængler", "Tomato and potato plant leaves and stems", "Ikke selve den modne frugt, nævnt ovenfor", "Not the ripe fruit itself, listed above"),
            new("Liguster", "Privet"),
            new("Buksbom", "Boxwood"),
            new("Brandbæger", "Groundsel"),
            new("Hortensia", "Hydrangea"),
        ],
        "Almindelige i danske haver og hække - værd at kunne kende på synet, før du lader noget grønt komme udefra.",
        "Common in Danish gardens and hedges - worth knowing by sight before you let anything green come from outside."),
    ];
}
