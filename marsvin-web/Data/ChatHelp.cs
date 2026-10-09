namespace MarsvinWebExample.Data;

/// <summary>
/// What the chat's "i" button shows: the kinds of things Pip can be asked,
/// each with example questions that can be clicked. Kept here rather than
/// in the markup so a test can ask Pip every one of them, in both
/// languages - an example Pip can't answer would be worse than no example.
/// </summary>
public static class ChatHelp
{
    public sealed record Example(string Da, string En);

    public sealed record Topic(string Da, string En, IReadOnlyList<Example> Examples);

    public static IReadOnlyList<Topic> Topics { get; } =
    [
        new("Marsvin til salg", "Guinea pigs for sale",
        [
            new("Hvilke marsvin er til salg?", "Which guinea pigs are for sale?"),
            new("Har I nogen hunner?", "Do you have any females?"),
            new("Hvad koster et marsvin?", "What does a guinea pig cost?")
        ]),
        new("Foder", "Food",
        [
            new("Hvad spiser marsvin?", "What do guinea pigs eat?"),
            new("Må de spise agurk?", "Can they eat cucumber?"),
            new("Er avocado giftigt?", "Is avocado poisonous?")
        ]),
        new("Pasning", "Care",
        [
            new("Hvor stort et bur til 3 marsvin?", "How big a cage for 3 guinea pigs?"),
            new("Hvor tit skal jeg gøre rent?", "How often do I clean the cage?"),
            new("Hvilken strøelse er bedst?", "What bedding is best?")
        ]),
        new("Tilbehør", "Accessories",
        [
            new("Hvilke bure har I?", "Which cages do you have?"),
            new("Hvad koster en negleklipper?", "How much are the nail clippers?"),
            new("Hvilke mærker har I?", "What brands do you have?")
        ]),
        new("Levering og retur", "Delivery and returns",
        [
            new("Hvad koster det at sende 2 kg?", "What does it cost to send 2 kg?"),
            new("Kan jeg returnere et bur?", "Can I return a cage?"),
            new("Hvornår er fragten gratis?", "When is shipping free?")
        ]),
        new("Butikken", "The shop",
        [
            new("Har I åbent nu?", "Are you open now?"),
            new("Hvor ligger butikken?", "Where is the shop?"),
            new("Kan jeg betale med MobilePay?", "Can I pay with MobilePay?")
        ])
    ];

    /// <summary>Small things that make Pip easier to use, shown under the examples.</summary>
    public static IReadOnlyList<Example> Tips { get; } =
    [
        new("Du kan spørge videre: \"Fortæl om Cotton\" og bagefter \"Hvad koster hun?\"",
            "You can ask a follow-up: \"Tell me about Cotton\" and then \"What does she cost?\""),
        new("Flere spørgsmål i samme besked er fint.", "Several questions in one message is fine."),
        new("Stavefejl gør ikke noget, og du kan skrive på dansk eller engelsk.",
            "Spelling slips don't matter, and you can write in Danish or English.")
    ];

    /// <summary>What Pip won't do - said up front, so nobody has to find out by asking.</summary>
    public static IReadOnlyList<Example> Limits { get; } =
    [
        new("Se ordrer, konti eller noget om personer - brug Min konto eller kontaktsiden.",
            "See orders, accounts or anything about people - use My account or the contact page."),
        new("Vurdere et sygt marsvin - kontakt en dyrlæge.", "Judge a sick guinea pig - contact a vet."),
        new("Svare på, hvordan hjemmesiden er bygget eller beskyttet.", "Answer how the website is built or protected.")
    ];
}
