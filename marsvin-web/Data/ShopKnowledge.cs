using System.Net;
using System.Text.RegularExpressions;
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
/// Everything the support chat knows: all of the shop's own public content -
/// the front page, the guinea pigs, the accessories and brands, the care
/// guide and food list, delivery and returns, donations, contact details and
/// the FAQ. Built fresh from the catalog for every question, so the chat
/// never quotes a price or a "for sale" that the pages themselves no longer show.
///
/// What is deliberately NOT here is the chat's main safety property:
/// - nothing about people: no orders, no accounts, no customers, no staff
///   (and the customer-facing "in stock / low / out", not the real stock count);
/// - nothing about how the website is built or protected: no word of logins,
///   roles, the database or any security measure.
/// The chat can only repeat what is in this list, so no question, however
/// cleverly worded, can make it reveal either. (KeywordChatAssistant also
/// turns such questions away outright instead of answering with a near miss.)
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

        // ---- About us / contact ----
        facts.Add(new(
            "Om os: vi videreformidler primært marsvin i bundne par og sælger det hø, de bure, huse og den strøelse, der hører til. Hvert dyr er opdrættet efter samme standard som i vores pasningsguide.",
            "About us: we mainly rehome guinea pigs in bonded pairs, and sell the hay, cages, houses and bedding that go with them. Every animal is raised on the same standard as in our care guide."));
        facts.Add(new(
            "Kontakt: skriv til os via formularen på kontaktsiden, send en e-mail til kontakt@marsvin.dk, eller ring på 70 12 34 56 torsdag til lørdag i åbningstiden. Vi svarer inden for et par hverdage.",
            "Contact: write to us through the form on the contact page, email kontakt@marsvin.dk, or call 70 12 34 56 Thursday to Saturday during opening hours. We reply within a couple of weekdays."));

        // ---- Brands ----
        var brands = catalog.Accessories.Select(a => a.Brand).OfType<string>().Distinct().Order().ToList();
        if (brands.Count > 0)
        {
            facts.Add(new(
                $"Mærker vi fører: {string.Join(", ", brands)}. På siden Mærker kan du se alt fra hvert mærke.",
                $"Brands we carry: {string.Join(", ", brands)}. The Brands page shows everything from each brand."));
        }

        // ---- Delivery (the same numbers the checkout uses) ----
        foreach (var carrier in Enum.GetValues<ShippingCarrier>())
        {
            var (minDays, maxDays) = ShippingCalculator.BusinessDays(carrier);
            decimal Price(int grams) => ShippingCalculator.Cost(carrier, grams);
            facts.Add(new(
                $"Fragt med {carrier.DisplayName()}: leveres til {(carrier.DeliversToParcelShop() ? "et udleveringssted nær din adresse" : "døren")}, {minDays}-{maxDays} hverdage. " +
                $"Pris pr. pakke: {Price(1_000):0} kr. op til 1 kg, {Price(5_000):0} kr. op til 5 kg, {Price(10_000):0} kr. op til 10 kg, {Price(20_000):0} kr. op til 20 kg.",
                $"Shipping with {carrier.DisplayName(english: true)}: delivered to {(carrier.DeliversToParcelShop() ? "a pick-up point near your address" : "your door")}, {minDays}-{maxDays} weekdays. " +
                $"Price per parcel: {Price(1_000):0} kr. up to 1 kg, {Price(5_000):0} kr. up to 5 kg, {Price(10_000):0} kr. up to 10 kg, {Price(20_000):0} kr. up to 20 kg."));
        }
        facts.Add(new(
            "En pakke rummer højst 20 kg. En tungere ordre deles i flere pakker, som hver prissættes efter sin egen vægt. I kassen ser du samlet vægt, antal pakker og forventet leveringsdato, før du betaler.",
            "A parcel holds at most 20 kg. A heavier order is split into several parcels, each priced on its own weight. At checkout you see the total weight, number of parcels and expected delivery date before you pay."));

        // ---- Care guide, quick guide and food list ----
        facts.AddRange(CareGuideText.Facts);

        foreach (var section in QuickGuideData.Sections)
        {
            facts.Add(new(
                $"Pasning - {section.TitleDa.ToLowerInvariant()}: {string.Join("; ", section.Items.Select(i => i.NoteDa is null ? i.Da : $"{i.Da} ({i.NoteDa})"))}.",
                $"Care - {section.TitleEn.ToLowerInvariant()}: {string.Join("; ", section.Items.Select(i => i.NoteEn is null ? i.En : $"{i.En} ({i.NoteEn})"))}."));
        }

        // One fact per food or plant, so "må marsvin spise agurk?" lands on
        // cucumber itself rather than on a long list.
        foreach (var section in FoodListData.FoodSections.Concat(FoodListData.GardenSections))
        {
            foreach (var item in section.Items)
            {
                facts.Add(new(
                    $"Foderliste - {item.Da}: {section.TitleDa}.{(item.NoteDa is null ? "" : $" {item.NoteDa}.")}",
                    $"Food list - {item.En}: {section.TitleEn}.{(item.NoteEn is null ? "" : $" {item.NoteEn}.")}"));
            }
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

/// <summary>
/// The care guide, read straight from its own page (Pages/Pasningsguide.cshtml,
/// copied next to the app at build time - see the .csproj) rather than typed
/// in a second time: every paragraph there is already written as Danish text
/// with its English twin in a data-en attribute, which is exactly a
/// <see cref="ShopFact"/>. Read once, at first use. If the file isn't there,
/// the chat simply knows less - it never fails over it.
/// </summary>
public static class CareGuideText
{
    private static readonly Regex Paragraph = new(
        """<(p|li|dd|dt)\b[^>]*\sdata-en="([^"]*)"[^>]*>(.*?)</\1>""",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Lazy<IReadOnlyList<ShopFact>> Loaded = new(Load);

    public static IReadOnlyList<ShopFact> Facts => Loaded.Value;

    private static IReadOnlyList<ShopFact> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Pages", "Pasningsguide.cshtml");
        if (!File.Exists(path)) return [];

        var facts = new List<ShopFact>();
        foreach (Match match in Paragraph.Matches(File.ReadAllText(path)))
        {
            var english = match.Groups[2].Value;
            var danish = match.Groups[3].Value;
            // Plain prose only: anything with markup or Razor code in it isn't a sentence to quote.
            if (danish.Contains('<') || danish.Contains('@') || english.Contains('@')) continue;

            danish = Regex.Replace(WebUtility.HtmlDecode(danish), @"\s+", " ").Trim();
            english = WebUtility.HtmlDecode(english).Trim();
            // Short strings are headings and button labels, not knowledge.
            if (danish.Length < 60) continue;

            facts.Add(new ShopFact(danish, english));
        }
        return facts;
    }
}
