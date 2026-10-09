using System.Globalization;
using System.Text.RegularExpressions;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

// The things Pip works out rather than looks up: whether the shop is open
// right now, what a parcel of a given weight costs to send, how much floor
// five guinea pigs need, which animals are for sale, a straight yes or no
// on a food - plus the handful of things it says about itself, and the two
// safety stops (personal details, and anything that sounds like a sick
// animal). Each returns null when the question isn't its kind, and the
// ordinary lookup in ChatAssistants.cs takes over.
//
// Same rule as the lookup: every number and name here comes from the shop's
// own public data (the catalog, the shipping calculator, the food list).
public sealed partial class KeywordChatAssistant
{
    private static readonly Regex EmailPattern = new(@"\S+@\S+\.\S+", RegexOptions.Compiled);
    // Eight or more digits in a row (spaces and dashes allowed between them):
    // a phone, card or CPR number - nothing Pip is ever asked for.
    private static readonly Regex LongNumberPattern = new(@"(?:\d[\s\-]?){8,}", RegexOptions.Compiled);
    private static readonly Regex WeightPattern = new(@"(\d+(?:[.,]\d+)?)\s*(kg|kilo|gram|g)\b", RegexOptions.Compiled);
    private static readonly Regex CountPattern = new(@"\b(\d{1,2})\b", RegexOptions.Compiled);

    private static readonly string[] IllnessCues =
    [
        "spiser ikke", "vil ikke spise", "holdt op med at spise", "drikker ikke", "bloeder", "blod", "diarre", "tynd mave",
        "vejrtraekning", "traekke vejret", "traekker vejret", "hiver efter vejret", "sloev", "kramper", "halter", "saar", "knude", "doeende",
        "haevet", "skorpe", "haartab", "taber haar", "mider", "nyser", "snot", "syg", "sygt", "smerte", "ondt",
        "not eating", "won't eat", "wont eat", "stopped eating", "not drinking", "bleeding", "blood", "diarrhea", "diarrhoea",
        "breathing", "wheezing", "lethargic", "seizure", "limping", "wound", "lump", "dying", "swollen", "sneezing",
        "hair loss", "mites", "lice", "crusty", "runny", "sick", "ill", "pain", "hurt", "injured"
    ];

    private static readonly string[] IdentityCues =
    [
        "hvem er du", "hvad er du", "hvad hedder du", "dit navn", "er du en robot", "er du et menneske", "er du en ai", "er du ai",
        "er du rigtig", "er du et rigtigt", "chatgpt", "kunstig intelligens",
        "who are you", "what are you", "your name", "are you a robot", "are you a bot", "are you human", "are you a human",
        "are you an ai", "are you ai", "are you real", "artificial intelligence"
    ];

    private static readonly string[] AbilityCues =
        ["hvad kan du", "hvad kan jeg spoerge", "hjaelp", "what can you", "what can i ask", "help"];

    private static readonly string[] ByeCues = ["farvel", "hej hej", "vi ses", "bye", "goodbye", "see you"];
    private static readonly string[] HowAreYouCues = ["hvordan gaar det", "hvordan har du det", "how are you", "how's it going"];

    private static readonly string[] OpenCues = ["aaben", "aabent", "aabner", "lukket", "lukker", "open", "opens", "closed", "closes", "close"];
    private static readonly string[] ShippingCues = ["fragt", "sende", "sendes", "sender", "forsendelse", "levering", "porto", "pakke", "ship", "shipping", "send", "delivery", "postage", "parcel"];
    private static readonly string[] CageCues = ["bur", "buret", "plads", "gulvplads", "stort", "stor", "cage", "space", "room", "big", "large", "size"];
    private static readonly string[] SaleCues = ["til salg", "ledige", "ledig", "kan jeg koebe", "kan man koebe", "for sale", "available", "can i buy", "hvilke marsvin", "which guinea pigs"];
    private static readonly string[] AgeCues = ["gammel", "alder", "foedt", "old", "age", "born"];
    private static readonly string[] SexCues = ["koen", "han eller hun", "dreng eller pige", "sex", "gender", "male or female", "boy or girl"];
    private static readonly string[] FoodCues = ["spise", "spiser", "aede", "maa de", "maa marsvin", "maa mit", "taaler", "eat", "eats", "feed", "can they have", "can he have", "can she have", "safe for", "giftig", "giftigt", "poisonous", "toxic"];

    private static readonly HashSet<string> MaleWords = ["hanner", "hanmarsvin", "drenge", "dreng", "male", "males", "boar", "boars", "boy", "boys"];
    private static readonly HashSet<string> FemaleWords = ["hunner", "hunmarsvin", "piger", "pige", "female", "females", "sow", "sows", "girl", "girls"];

    private static readonly Dictionary<string, int> NumberWords = new()
    {
        ["et"] = 1, ["en"] = 1, ["to"] = 2, ["tre"] = 3, ["fire"] = 4, ["fem"] = 5, ["seks"] = 6, ["syv"] = 7, ["otte"] = 8,
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8
    };

    // Thursday and Friday 14-18, Saturday 10-14 - the same hours as the footer.
    private static readonly Dictionary<DayOfWeek, (int Opens, int Closes)> OpeningHours = new()
    {
        [DayOfWeek.Thursday] = (14, 18),
        [DayOfWeek.Friday] = (14, 18),
        [DayOfWeek.Saturday] = (10, 14)
    };

    private static readonly (DayOfWeek Day, string[] Words)[] DayWords =
    [
        (DayOfWeek.Monday, ["mandag", "monday"]), (DayOfWeek.Tuesday, ["tirsdag", "tuesday"]),
        (DayOfWeek.Wednesday, ["onsdag", "wednesday"]), (DayOfWeek.Thursday, ["torsdag", "thursday"]),
        (DayOfWeek.Friday, ["fredag", "friday"]), (DayOfWeek.Saturday, ["loerdag", "saturday"]),
        (DayOfWeek.Sunday, ["soendag", "sunday"])
    ];

    private static bool Has(string folded, string[] cues) => cues.Any(c => ContainsWord(folded, c));

    // The whole phrase and nothing glued on: "what are you" must not be
    // found inside "what are your opening hours".
    private static bool HasExactly(string folded, string[] cues) => cues.Any(cue =>
    {
        var index = 0;
        while ((index = folded.IndexOf(cue, index, StringComparison.Ordinal)) >= 0)
        {
            var end = index + cue.Length;
            if ((index == 0 || !char.IsLetterOrDigit(folded[index - 1])) && (end == folded.Length || !char.IsLetterOrDigit(folded[end])))
                return true;
            index = end;
        }
        return false;
    });

    /// <summary>The safety stops that come before anything else is even looked at.</summary>
    private static string? SafetyStop(string question, string folded, bool english)
    {
        if (EmailPattern.IsMatch(question) || LongNumberPattern.IsMatch(question))
            return ChatReplies.NoPersonalDetails(english);
        return null;
    }

    private string? Skill(string folded, Intent intent, bool english)
    {
        var words = WordPattern.Matches(folded).Select(m => m.Value).ToList();

        if (HasExactly(folded, IdentityCues)) return ChatReplies.WhoIAm(english);
        if (HasExactly(folded, HowAreYouCues)) return ChatReplies.HowIAm(english);
        if (HasExactly(folded, AbilityCues) && words.Count <= 5) return ChatReplies.WhatICanDo(english);
        if (HasExactly(folded, ByeCues) && words.Count <= 4) return ChatReplies.Bye(english);

        // Before any advice: Pip must never sound like it can judge a sick animal.
        if (Has(folded, IllnessCues)) return ChatReplies.SeeAVet(english);

        return OpenNow(folded, english)
            ?? ShippingForWeight(folded, english)
            ?? CageFor(folded, words, english)
            ?? AboutOneAnimal(folded, words, english)
            ?? AnimalsForSale(folded, words, intent, english)
            ?? FoodVerdict(folded, words, english);
    }

    // ---- "Are you open now / on Sunday / tomorrow?" ----

    private string? OpenNow(string folded, bool english)
    {
        if (!Has(folded, OpenCues)) return null;

        var now = ShopTime();
        var named = DayWords.Where(d => d.Words.Any(w => ContainsWord(folded, w))).Select(d => (DayOfWeek?)d.Day).FirstOrDefault();
        DayOfWeek? day = named
            ?? (ContainsWord(folded, "i morgen") || ContainsWord(folded, "tomorrow") ? now.AddDays(1).DayOfWeek
            : ContainsWord(folded, "i dag") || ContainsWord(folded, "today") || ContainsWord(folded, "idag") ? now.DayOfWeek
            : null);
        var asksNow = ContainsWord(folded, "nu") || ContainsWord(folded, "now") || ContainsWord(folded, "lige nu");
        if (day is null && !asksNow) return null;

        var all = english ? "Opening hours: Thursday and Friday 14-18, Saturday 10-14."
                          : "Åbningstider: torsdag og fredag 14-18, lørdag 10-14.";

        if (asksNow && named is null)
        {
            if (OpeningHours.TryGetValue(now.DayOfWeek, out var today) && now.Hour >= today.Opens && now.Hour < today.Closes)
                return english ? $"Yes, we're open right now - until {today.Closes}:00. {all}"
                               : $"Ja, vi har åbent lige nu - til kl. {today.Closes}. {all}";

            // The next time the door opens: later today, or the first open day after it.
            for (var ahead = 0; ahead <= 7; ahead++)
            {
                var date = now.AddDays(ahead);
                if (!OpeningHours.TryGetValue(date.DayOfWeek, out var hours)) continue;
                if (ahead == 0 && now.Hour >= hours.Opens) continue;
                var when = ahead == 0 ? (english ? "today" : "i dag") : DayName(date.DayOfWeek, english);
                return english ? $"No, we're closed right now. We open again {when} at {hours.Opens}:00. {all}"
                               : $"Nej, lige nu har vi lukket. Vi åbner igen {when} kl. {hours.Opens}. {all}";
            }
        }

        var asked = day ?? now.DayOfWeek;
        var name = DayName(asked, english);
        return OpeningHours.TryGetValue(asked, out var open)
            ? (english ? $"Yes - on {Capital(name)} we're open {open.Opens}-{open.Closes}. {all}"
                       : $"Ja - {name} har vi åbent {open.Opens}-{open.Closes}. {all}")
            : (english ? $"No, we're closed on {Capital(name)}. {all}"
                       : $"Nej, {name} har vi lukket. {all}");
    }

    private DateTime ShopTime()
    {
        var utc = (clock ?? TimeProvider.System).GetUtcNow();
        try
        {
            return TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")).DateTime;
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.LocalDateTime;
        }
    }

    private static string DayName(DayOfWeek day, bool english) =>
        (english ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("da-DK")).DateTimeFormat.GetDayName(day).ToLowerInvariant();

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    // ---- "What does it cost to send 3 kg?" ----

    private static string? ShippingForWeight(string folded, bool english)
    {
        var match = WeightPattern.Match(folded);
        if (!match.Success || !Has(folded, ShippingCues)) return null;

        var amount = double.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var grams = (int)Math.Round(match.Groups[2].Value is "kg" or "kilo" ? amount * 1000 : amount);
        if (grams is <= 0 or > 200_000) return null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var parcels = ShippingCalculator.ParcelCount(grams);
        var prices = string.Join(", ", Enum.GetValues<ShippingCarrier>().Select(c =>
            $"{c.DisplayName(english)} {ShippingCalculator.Quote(c, grams, today).Cost:0} kr"));
        var weight = ShippingCalculator.FormatWeight(grams, english);

        return english
            ? $"Sending {weight} ({(parcels == 1 ? "1 parcel" : $"{parcels} parcels")}) costs: {prices}. " +
              $"Shipping is free when the accessories come to {ShippingCalculator.FreeShippingThreshold:0} kr. or more, and pickup in the shop is always free."
            : $"At sende {weight} ({(parcels == 1 ? "1 pakke" : $"{parcels} pakker")}) koster: {prices}. " +
              $"Fragten er gratis, når tilbehøret koster {ShippingCalculator.FreeShippingThreshold:0} kr. eller mere, og afhentning i butikken er altid gratis.";
    }

    // ---- "How big a cage for four guinea pigs?" ----

    private static string? CageFor(string folded, List<string> words, bool english)
    {
        if (!Has(folded, CageCues) || !words.Any(EverywhereWords.Contains)) return null;
        // "120 x 60" in the question is a cage size, not a number of animals.
        if (folded.Contains(" x ")) return null;

        var count = CountPattern.Matches(folded).Select(m => int.Parse(m.Value)).Cast<int?>().FirstOrDefault()
            ?? words.Where(w => NumberWords.ContainsKey(w) && w is not ("en" or "et")).Select(w => (int?)NumberWords[w]).FirstOrDefault();
        if (count is null or < 1 or > 20) return null;

        if (count == 1)
            return english ? "A guinea pig shouldn't live alone - they are herd animals. Plan for two, which need at least 120 x 60 cm of floor."
                           : "Et marsvin skal ikke bo alene - de er flokdyr. Regn med to, som skal have mindst 120 x 60 cm gulv.";
        if (count == 2)
            return english ? "Two guinea pigs need at least 120 x 60 cm of floor (about 0.7 m²). Floor is what counts - extra levels don't."
                           : "To marsvin skal have mindst 120 x 60 cm gulv (ca. 0,7 m²). Det er gulvet, der tæller - ekstra etager gør ikke.";

        // 120 x 60 cm for the first two, about half a square metre for each one after that.
        var area = 0.72 + 0.5 * (count.Value - 2);
        var length = (int)(Math.Ceiling(area / 0.7 * 100 / 10) * 10);
        return english
            ? $"For {count} guinea pigs, plan for at least about {area.ToString("0.0", CultureInfo.InvariantCulture)} m² of floor - for example {length} x 70 cm. " +
              "Two need 120 x 60 cm, and each extra guinea pig about 0.5 m² more. Floor is what counts, not extra levels."
            : $"Til {count} marsvin skal du regne med mindst ca. {area.ToString("0.0", CultureInfo.GetCultureInfo("da-DK"))} m² gulv - for eksempel {length} x 70 cm. " +
              "To skal have 120 x 60 cm, og hvert ekstra marsvin ca. 0,5 m² mere. Det er gulvet, der tæller, ikke ekstra etager.";
    }

    // ---- "How old is Cotton?" / "Is Bo for sale?" ----

    private string? AboutOneAnimal(string folded, List<string> words, bool english)
    {
        var animal = catalog.Animals.FirstOrDefault(a => words.Contains(Fold(a.Name)));
        if (animal is null) return null;

        if (Has(folded, AgeCues))
            return english ? $"{animal.Name} is {animal.AgeTextEn} old." : $"{animal.Name} er {animal.AgeText} gammel.";

        if (Has(folded, SexCues) || words.Any(MaleWords.Contains) || words.Any(FemaleWords.Contains))
            return english ? $"{animal.Name} is a {animal.SexNameEn.ToLowerInvariant()}." : $"{animal.Name} er en {animal.SexName.ToLowerInvariant()}.";

        if (Has(folded, SaleCues) || ContainsWord(folded, "reserveret") || ContainsWord(folded, "reserved") || ContainsWord(folded, "solgt") || ContainsWord(folded, "sold"))
        {
            var partner = animal.BondedWithId is int id ? catalog.Animals.FirstOrDefault(a => a.ProductId == id) : null;
            var together = partner is null ? "" : english ? $" Only sold together with {partner.Name}." : $" Sælges kun sammen med {partner.Name}.";
            return english
                ? $"{animal.Name}: {animal.StatusTextEn.ToLowerInvariant()}. Price {animal.Price:0} kr.{together} Guinea pigs are picked up in the shop."
                : $"{animal.Name}: {animal.StatusText.ToLowerInvariant()}. Pris {animal.Price:0} kr.{together} Marsvin hentes i butikken.";
        }

        return null;
    }

    // ---- "Which guinea pigs are for sale?" / "Do you have any females?" ----

    private string? AnimalsForSale(string folded, List<string> words, Intent intent, bool english)
    {
        var males = words.Any(MaleWords.Contains);
        var females = words.Any(FemaleWords.Contains);
        var aboutGuineaPigs = words.Any(EverywhereWords.Contains);
        // Only when nothing more specific was asked: "do you have guinea pig food" is not this.
        var onlyGuineaPigs = aboutGuineaPigs && !words.Any(w => !StopWords.Contains(w) && !EverywhereWords.Contains(w)
            && !MaleWords.Contains(w) && !FemaleWords.Contains(w) && w is not ("salg" or "sale" or "available" or "ledige" or "ledig" or "koebe" or "buy"));

        var asked = (Has(folded, SaleCues) && (aboutGuineaPigs || males || females))
            || ((males || females) && (aboutGuineaPigs || intent == Intent.Sell))
            || (intent is Intent.Sell or Intent.Price && onlyGuineaPigs);
        if (!asked) return null;

        var forSale = catalog.AvailableAnimals()
            .Where(a => males == females || (males ? a.Sex == Sex.Boar : a.Sex == Sex.Sow))
            .ToList();
        if (forSale.Count == 0)
            return english ? "None of those are for sale right now. New animals are added on the guinea pig page as they become ready."
                           : "Ingen af dem er til salg lige nu. Nye dyr kommer på marsvinesiden, når de er klar.";

        var ordered = intent == Intent.Price ? forSale.OrderBy(a => a.Price).ToList() : forSale;
        var lines = ordered.Take(8).Select(a => english
            ? $"- {a.Name}: {(a.BreedEn ?? a.Breed).ToLowerInvariant()}, {a.SexNameEn.ToLowerInvariant()}, {a.AgeTextEn}, {a.Price:0} kr."
            : $"- {a.Name}: {a.Breed.ToLowerInvariant()}, {a.SexName.ToLowerInvariant()}, {a.AgeText}, {a.Price:0} kr.");
        var more = forSale.Count > 8 ? (english ? $"\n... and {forSale.Count - 8} more on the guinea pig page." : $"\n... og {forSale.Count - 8} mere på marsvinesiden.") : "";

        return (english ? $"For sale right now ({forSale.Count}):\n" : $"Til salg lige nu ({forSale.Count}):\n")
            + string.Join("\n", lines) + more
            + (english ? "\n\nBonded pairs are only sold together. Ask me about one of them by name to hear more."
                       : "\n\nBundne par sælges kun sammen. Spørg mig om en af dem ved navn for at høre mere.");
    }

    // ---- "Can they eat cucumber?" - a straight yes or no from the food list ----

    private static bool IsFoodQuestion(string folded) => Has(folded, FoodCues);

    private static string? FoodVerdict(string folded, List<string> words, bool english)
    {
        if (!IsFoodQuestion(folded)) return null;

        var asked = words.Where(w => w.Length >= 3 && !StopWords.Contains(w) && !EverywhereWords.Contains(w)).ToList();
        if (asked.Count == 0) return null;

        var hits = FoodListData.FoodSections.Concat(FoodListData.GardenSections)
            .SelectMany(section => section.Items.Select(item => (section, item,
                names: WordPattern.Matches(Fold(item.Da) + " " + Fold(item.En)).Select(m => m.Value).ToList())))
            .Select(x => (x.section, x.item,
                // An exact word beats a plural or a compound ("tomat" / "tomater" / "tomatplanter").
                rank: x.names.Any(asked.Contains) ? 0
                    : x.names.Any(n => asked.Any(a => a.Length >= 4 && n.Length >= 4 && (n.StartsWith(a, StringComparison.Ordinal) || a.StartsWith(n, StringComparison.Ordinal)))) ? 1
                    : 2,
                size: x.names.Count))
            .Where(x => x.rank < 2)
            .OrderBy(x => x.rank).ThenBy(x => x.size)
            .Take(2)
            .ToList();
        if (hits.Count == 0) return null;

        return string.Join("\n\n", hits.Select(hit =>
        {
            var verdict = (hit.section.Tier, english) switch
            {
                ("safe", true) => "Yes.",
                ("safe", false) => "Ja.",
                ("moderate", true) => "Yes, but only a little.",
                ("moderate", false) => "Ja, men kun lidt.",
                ("never", true) => "No - never.",
                ("never", false) => "Nej - aldrig.",
                _ => ""
            };
            var name = english ? hit.item.En : hit.item.Da;
            var where = (english ? hit.section.TitleEn : hit.section.TitleDa).ToLowerInvariant();
            var note = english ? hit.item.NoteEn : hit.item.NoteDa;
            // "No - never. Chocolate: never." says it twice.
            if (where is "never" or "aldrig") return $"{verdict} {name}{(note is null ? "." : $": {note}.")}";
            return $"{verdict} {name}: {where}.{(note is null ? "" : $" {note}.")}".Trim();
        }));
    }
}
