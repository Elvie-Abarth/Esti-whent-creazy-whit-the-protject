using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>One thing the support chat knows, in both languages.</summary>
public sealed record ShopFact(string Da, string En)
{
    /// <summary>
    /// What to say when this fact is given as an answer on its own - for an FAQ
    /// entry, the answer without the question in front. Defaults to the fact itself.
    /// </summary>
    public string ReplyDa { get; init; } = Da;

    public string ReplyEn { get; init; } = En;
}

/// <summary>
/// Everything the support chat is allowed to know - and it is exactly what a
/// visitor can already read in five public places: the front page, the
/// accessories page, the guinea pig pages, the footer and the FAQ. Built fresh from
/// the catalog for every question, so the chat never quotes a price or a
/// "for sale" that the pages themselves no longer show.
///
/// That limit is the chat's main safety property, not just a scope choice:
/// nothing here is private (no orders, no accounts, no staff data, and the
/// customer-facing "in stock / low / out" rather than the real stock count),
/// so no question, however cleverly worded, can make the chat reveal
/// something that wasn't already public.
/// </summary>
public static class ShopKnowledge
{
    public static IReadOnlyList<ShopFact> Build(ICatalog catalog)
    {
        var facts = new List<ShopFact>
        {
            // ---- Footer ----
            new("Butikken hedder Marsvin og ligger på Havnegade 12, 6700 Esbjerg.",
                "The shop is called Marsvin and is at Havnegade 12, 6700 Esbjerg."),
            new("Åbningstider: torsdag og fredag 14-18, lørdag 10-14.",
                "Opening hours: Thursday and Friday 14-18, Saturday 10-14."),
            new("Marsvin hentes i butikken, så vi kan nå at snakke om buret først.",
                "Guinea pigs are picked up in store, so we can talk through the cage first."),
            new("Marsvin er en eksempelbutik bygget som inspiration til et Zealand-skoleprojekt. Det er ikke en rigtig forretning.",
                "Marsvin is an example shop built as inspiration for a Zealand school project. It is not a real business."),
            new("I bunden af siden er der links til Om os, Kontakt, Ofte stillede spørgsmål, Betaling & levering, Støt og Privatlivspolitik.",
                "The footer links to About us, Contact, Frequently asked questions, Payment & delivery, Support and the Privacy policy."),

            // ---- Front page ----
            new("Vi har marsvin og alt, hvad de skal bruge. Hvert dyr står med navn, alder og køn, så du ved præcis, hvem du henter.",
                "We have guinea pigs and everything they need. Every animal is listed with name, age and sex, so you know exactly who you're picking up."),
            new("Vi sælger primært marsvin parvis. Et enkelt marsvin sælges kun, hvis du bekræfter, at det flytter ind hos et marsvin eller en flok, du allerede har. Dyr, der bor sammen, flytter også sammen.",
                "We mainly sell guinea pigs in pairs. A single guinea pig is only sold once you confirm it's joining a guinea pig or herd you already have. Animals that live together move in together too."),
            new("Har du plads til to? Et par skal have mindst 120 x 60 cm. Marsvin lever i grupper og bliver stille og passive alene.",
                "Do you have room for two? A pair needs at least 120 x 60 cm. Guinea pigs live in groups and become quiet and passive when alone."),
            new("Hø er ikke tilbehør, det er hovedmåltidet, og det skal være der hele døgnet.",
                "Hay isn't an accessory, it's the main meal, and it needs to be there around the clock."),
            new("C-vitamin-reglen: marsvin kan ikke selv danne C-vitamin. Uden et dagligt tilskud bliver de syge.",
                "The vitamin C rule: guinea pigs can't produce vitamin C themselves. Without a daily supplement they get sick."),
            new("Kan du komme til en dyrlæge? Et marsvin, der holder op med at spise i et døgn, er et akut tilfælde, ikke noget der kan vente til mandag.",
                "Can you get to a vet? A guinea pig that stops eating for a day is an emergency, not something that can wait until Monday."),
            new("Buret skal fra første dag have tre ting: hø, foder og selve buret. Resten kan vente til weekenden efter.",
                "From day one the cage needs three things: hay, food and the cage itself. The rest can wait until the following weekend.")
        };

        // ---- Guinea pig pages ----
        var animals = catalog.Animals;
        var available = catalog.AvailableAnimals().Count();
        facts.Add(new(
            $"Lige nu er {available} marsvin til salg ud af {animals.Count} i alt på siden.",
            $"Right now {available} guinea pigs are for sale, out of {animals.Count} listed on the site."));

        foreach (var animal in animals)
        {
            var partner = animal.BondedWithId is int partnerId ? animals.FirstOrDefault(a => a.ProductId == partnerId) : null;
            facts.Add(new(
                $"Marsvinet {animal.Name}: {animal.Breed}, {animal.SexName.ToLowerInvariant()}, {animal.AgeText}, farve {animal.Colour}. " +
                $"Status: {animal.StatusText}. Pris {animal.Price:0} kr. {animal.Personality} " +
                (partner is not null
                    ? $"Bor sammen med {partner.Name} og sælges kun sammen med {partner.Name}."
                    : "Sælges enkeltvis, men kun til et hjem, der allerede har marsvin."),
                $"The guinea pig {animal.Name}: {animal.BreedEn ?? animal.Breed}, {animal.SexNameEn.ToLowerInvariant()}, {animal.AgeTextEn}, colour {animal.ColourEn ?? animal.Colour}. " +
                $"Status: {animal.StatusTextEn}. Price {animal.Price:0} kr. {animal.PersonalityEn ?? animal.Personality} " +
                (partner is not null
                    ? $"Lives with {partner.Name} and is only sold together with {partner.Name}."
                    : "Sold singly, but only to a home that already has guinea pigs.")));
        }

        // ---- Accessories page ----
        facts.Add(new(
            "Tilbehør findes i kategorierne hø, foder, bure, huse, legetøj, strøelse og pleje. Alt er testet på vores egne dyr.",
            "Accessories come in the categories hay, food, cages, houses, toys, bedding and care. Everything is tested on our own animals."));

        foreach (var item in catalog.Accessories)
        {
            var brandDa = item.Brand is null ? "" : $" fra {item.Brand}";
            var brandEn = item.Brand is null ? "" : $" by {item.Brand}";
            facts.Add(new(
                $"Varen {item.Name}{brandDa} (kategori {item.CategoryName}): {item.Description} Pris {item.Price:0} kr. {item.StockLevelText}.",
                $"The product {item.NameEn ?? item.Name}{brandEn} (category {item.CategoryNameEn}): {item.DescriptionEn ?? item.Description} Price {item.Price:0} kr. {item.StockLevelTextEn}."));
        }

        // ---- FAQ page ----
        // The same entries the /Faq page shows, so the chat and the page can't disagree.
        foreach (var entry in FaqData.Entries)
        {
            facts.Add(new($"{entry.Question} {entry.Answer}", $"{entry.QuestionEn} {entry.AnswerEn}")
            {
                ReplyDa = entry.Answer,
                ReplyEn = entry.AnswerEn
            });
        }

        return facts;
    }
}
