using System.Text.RegularExpressions;

namespace MarsvinWebExample.Data;

/// <summary>
/// Answers one question in the support chat bubble ("Pip"). Deliberately not
/// an AI service: nothing is sent anywhere and nothing costs anything to run.
/// It looks the question's words up in <see cref="ShopKnowledge"/> and
/// replies with the best-matching facts, so it can only ever say things that
/// are already on the shop's own public pages.
/// </summary>
public interface IChatAssistant
{
    /// <param name="english">Which language the visitor's page is showing - the answer follows it.</param>
    string Answer(string question, bool english);
}

public static class ChatReplies
{
    public static string DontKnow(bool english) => english
        ? "I don't know that. I can tell you about our guinea pigs, the accessories, caring for and feeding guinea pigs, delivery, returns and the shop. For anything else, write to us on the contact page."
        : "Det ved jeg ikke. Jeg kan fortælle om vores marsvin, tilbehøret, pasning og foder, levering, retur og butikken. Til alt andet: skriv til os på kontaktsiden.";

    public static string Greeting(bool english) => english
        ? "Wheek, hello! Ask me about our guinea pigs, the accessories, what guinea pigs can eat, delivery, returns or the shop's opening hours."
        : "Wheek, hej! Spørg mig om vores marsvin, tilbehøret, hvad marsvin må spise, levering, retur eller butikkens åbningstider.";

    public static string Thanks(bool english) => english
        ? "You're welcome! Just ask if there's anything else."
        : "Selv tak! Spørg endelig, hvis der er mere.";

    public static string NotAboutPeople(bool english) => english
        ? "I can't see orders, accounts or anything about people - not yours and not anyone else's. Your own orders are under My account, or write to us on the contact page."
        : "Jeg kan ikke se ordrer, konti eller noget om personer - hverken dine eller andres. Dine egne ordrer finder du under Min konto, eller skriv til os på kontaktsiden.";

    public static string NotAboutTheWebsite(bool english) => english
        ? "That's not something I answer - I only know about the shop, the animals and the products, not how the website is built or protected. If you can't log in, use \"Forgot password\" on the login page."
        : "Det svarer jeg ikke på - jeg ved kun noget om butikken, dyrene og varerne, ikke om hvordan hjemmesiden er bygget eller beskyttet. Kan du ikke logge ind, så brug \"Glemt adgangskode\" på log ind-siden.";
}

public sealed class KeywordChatAssistant(ICatalog catalog) : IChatAssistant
{
    private static readonly Regex WordPattern = new(@"[\p{L}\p{Nd}]{2,}", RegexOptions.Compiled);

    // "ordre 1234", "order #88": someone asking about one specific order.
    private static readonly Regex OrderNumberPattern = new(@"\b(ordre\w*|order\w*|bestilling\w*)\s*(nr\.?|nummer|number|no\.?)?\s*#?\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Words too common to tell one fact from another. (All word lists in this
    // class are written folded - see Fold - so "på" is "paa" here.)
    private static readonly HashSet<string> StopWords =
    [
        "hvad", "hvor", "hvordan", "hvornaar", "hvilke", "hvilken", "hvem", "hvorfor", "fortael", "noget", "nogen", "jeres",
        "har", "kan", "jeg", "der", "det", "den", "med", "til", "er", "en", "et", "paa", "af", "om", "vi", "du", "at", "og",
        "for", "som", "eller", "ikke", "min", "mit", "mine", "jer", "man", "skal", "vil", "koster", "pris", "mig", "maa",
        "godt", "gerne", "have", "faa", "give", "mit", "sit", "sin", "de", "dem", "fra", "naar", "saa", "lige", "bare",
        "vaere", "blive", "pip", "tit", "ofte", "goere", "goer", "betyder", "often", "mean", "means",
        "what", "where", "when", "which", "how", "who", "why", "this", "that", "there", "some", "any",
        "the", "and", "you", "your", "does", "can", "are", "with", "is", "it", "do", "to", "of", "in", "on", "me", "my",
        "much", "many", "cost", "costs", "price", "about", "tell", "kr", "they", "them", "their", "get", "give", "may"
    ];

    // People rarely use the page's own word. Each group lists ways of asking
    // about one thing; any word in a group also searches for the group's
    // last entry, which is a word the facts themselves use.
    private static readonly string[][] Synonyms =
    [
        ["aaben", "aabent", "aabner", "lukker", "lukket", "open", "opens", "close", "closed", "hours", "aabningstider"],
        ["adresse", "ligger", "finder", "address", "located", "location", "find", "havnegade"],
        ["sende", "sender", "sendes", "forsendelse", "porto", "ship", "shipping", "postage", "delivery", "deliver", "levering", "fragt"],
        ["returnere", "bytte", "refundering", "refund", "exchange", "return", "returns", "retur"],
        ["annullere", "afbestille", "fortryde", "cancel", "annuller", "fortryd"],
        ["betale", "betaling", "kort", "mobilepay", "pay", "payment", "card", "betaler"],
        ["firma", "virksomhed", "skole", "institution", "company", "business", "school", "cvr"],
        ["donere", "donation", "stoette", "donate", "stoet"],
        ["alder", "gammel", "old", "age", "fyldt"],
        ["spise", "spiser", "aede", "mad", "eat", "eats", "food", "feed", "feeding", "foder"],
        ["farlig", "farligt", "giftig", "giftigt", "poisonous", "toxic", "dangerous", "harmful", "aldrig"],
        ["alene", "enkelt", "single", "alone", "flokdyr"],
        ["bur", "buret", "plads", "stoerrelse", "cage", "size", "space", "gulvplads"],
        ["syg", "sygt", "sick", "ill", "vet", "akut", "dyrlaege"],
        ["vitamin", "vitaminer", "vitamins", "skoerbug", "scurvy", "c-vitamin"],
        ["konto", "login", "account", "gaest", "guest", "konto"],
        ["spore", "status", "track", "tracking", "foelger"],
        ["rense", "rengoere", "rent", "vaske", "clean", "cleaning", "rengoering"],
        ["popcorner", "popcorn", "hopper", "springer", "jumping", "jumps", "popcorning"],
        ["lyde", "lyd", "piber", "floejter", "sound", "sounds", "noise", "wheeking"],
        ["maerke", "maerker", "brand", "brands", "producent", "maerker"],
        ["ringe", "telefon", "telefonnummer", "mail", "email", "skrive", "phone", "call", "kontakt"],
    ];

    // In nearly every fact, so they say nothing about which one is meant -
    // left out when judging how much of a question a fact covers (unless
    // they're all the question has).
    private static readonly HashSet<string> EverywhereWords = ["marsvin", "marsvinet", "marsvinene", "guinea", "pig", "pigs", "butik", "butikken", "shop"];

    private static readonly HashSet<string> Greetings = ["hej", "hejsa", "goddag", "davs", "hallo", "hello", "hi", "hey"];
    private static readonly HashSet<string> ThankYous = ["tak", "takker", "thanks", "thank"];

    // Questions about how the site is built or protected. Pip has no such
    // knowledge to give (see ShopKnowledge) - these are turned away plainly
    // rather than answered with whatever shop fact happens to share a word.
    private static readonly string[] WebsiteTopics =
    [
        "sikkerhed", "security", "hack", "password", "adgangskode", "kodeord", "admin", "database", "sql", "server",
        "krypter", "encrypt", "token", "firewall", "saarbar", "vulnerab", "exploit", "xss", "csrf",
        "injection", "kildekode", "sourcecode", "source code", "programmer", "framework", "hosting", "backend",
        "2fa", "totp", "captcha", "rate limit", "api", "logfil", "audit"
    ];

    // Questions about people - customers, staff, somebody's order or account.
    private static readonly string[] PeopleTopics =
    [
        "andre kunder", "kundens", "kundeliste", "other customers", "customer list", "medarbejder", "ansatte", "ansat", "employee",
        "staff", "personale", "loen", "salary", "hvem har koebt", "who bought", "andres ordre", "someone else",
        "personnummer", "cpr", "kreditkort", "credit card", "kortnummer", "card number"
    ];

    public string Answer(string question, bool english)
    {
        var folded = Fold(question);

        if (WebsiteTopics.Any(t => ContainsWord(folded, t))) return ChatReplies.NotAboutTheWebsite(english);
        if (OrderNumberPattern.IsMatch(question) || PeopleTopics.Any(t => ContainsWord(folded, t)))
            return ChatReplies.NotAboutPeople(english);

        var allWords = WordPattern.Matches(folded).Select(m => m.Value).ToList();
        var words = allWords.Where(w => !StopWords.Contains(w)).ToList();

        // "Hej" / "tak" and nothing else worth looking up.
        var rest = words.Where(w => !Greetings.Contains(w) && !ThankYous.Contains(w)).ToList();
        if (rest.Count == 0)
        {
            if (allWords.Any(ThankYous.Contains)) return ChatReplies.Thanks(english);
            if (allWords.Any(Greetings.Contains)) return ChatReplies.Greeting(english);
            return ChatReplies.DontKnow(english);
        }

        var facts = ShopKnowledge.Build(catalog).Select(f => (Fact: f, Text: Fold(f.Da) + " \n " + Fold(f.En),
            // Where a fact says what it is about: "Marsvinet Lente: ...", "Varen Timothy-hø ...".
            Opening: Head(Fold(f.Da)) + " \n " + Head(Fold(f.En)))).ToList();

        // Spelling help: a word that matches nothing as typed is swapped for
        // the closest word the shop's own texts (or the synonym list) use.
        var vocabulary = Vocabulary(facts.Select(f => f.Text));
        // Each word of the question becomes a small group of search terms: the
        // word itself (spelling-corrected) plus whatever the synonym list adds.
        var specific = rest.Distinct().Where(w => !EverywhereWords.Contains(w)).ToList();
        var groups = (specific.Count > 0 ? specific : rest.Distinct())
            .Select(w => Correct(w, vocabulary))
            .Select(w => Synonyms.Where(g => g.Contains(w)).Select(g => g[^1])
                .Concat(WithoutEnding(w, vocabulary)).Prepend(w).Distinct().ToList())
            .ToList();
        var terms = groups.SelectMany(g => g).Distinct().ToList();

        // A word found in few facts says more about what's being asked than
        // one found in fifty, so each match counts for more the rarer it is.
        var matches = terms.ToDictionary(t => t, t => facts.Where(f => ContainsWord(f.Text, t)).Select(f => f.Fact).ToHashSet());
        var best = facts
            .Select(f => (f.Fact,
                // A word in a fact's opening counts double: asked about Lente,
                // Lente's own entry should win over her sister's, which only mentions her.
                Score: terms.Sum(t => matches[t].Contains(f.Fact) ? (ContainsWord(f.Opening, t) ? 2.0 : 1.0) / matches[t].Count : 0),
                // How much of the question this fact actually speaks to.
                Coverage: groups.Count(g => g.Any(t => matches[t].Contains(f.Fact))) / (double)groups.Count))
            // One shared word out of several isn't an answer - "the meaning of
            // life" must not get the chew sticks because both say "life".
            .Where(x => x.Score > 0 && (groups.Count == 1 || x.Coverage > 0.5))
            .OrderByDescending(x => x.Score * x.Coverage)
            .Take(2)
            .Select(x => (x.Fact, Score: x.Score * x.Coverage))
            .ToList();
        if (best.Count == 0) return ChatReplies.DontKnow(english);

        // The runner-up only comes along if it's nearly as good a match.
        return string.Join("\n\n", best.Where(x => x.Score >= best[0].Score * 0.8).Select(x => english ? x.Fact.ReplyEn : x.Fact.ReplyDa));
    }

    /// <summary>
    /// Lower-case, with the Danish letters spelled out (å/aa, æ/ae, ø/oe) and
    /// accents dropped - so "åbent", "aabent" and "Åbent" are one word, on a
    /// keyboard with or without those keys.
    /// </summary>
    public static string Fold(string text) => text.ToLowerInvariant()
        .Replace("å", "aa").Replace("æ", "ae").Replace("ø", "oe")
        .Replace("é", "e").Replace("è", "e").Replace("ü", "u").Replace("ö", "oe").Replace("ä", "ae");

    private static Dictionary<string, int> Vocabulary(IEnumerable<string> foldedTexts)
    {
        var vocabulary = new Dictionary<string, int>();
        foreach (var word in foldedTexts.SelectMany(t => WordPattern.Matches(t).Select(m => m.Value))
                     .Concat(Synonyms.SelectMany(g => g)))
        {
            vocabulary[word] = vocabulary.GetValueOrDefault(word) + 1;
        }
        return vocabulary;
    }

    /// <summary>
    /// The word itself if the shop's texts know it (or a longer word starting
    /// with it); otherwise the known word closest to it - one slip allowed in
    /// a word of 4-7 letters, two in a longer one. Shorter words are left
    /// alone: with three letters almost anything is "one letter away".
    /// </summary>
    private static string Correct(string word, Dictionary<string, int> vocabulary)
    {
        if (word.Length < 4 || vocabulary.Keys.Any(v => v.StartsWith(word, StringComparison.Ordinal))) return word;

        var stem = WithoutEnding(word, vocabulary).FirstOrDefault();
        if (stem is not null) return stem;

        var allowed = word.Length >= 8 ? 2 : 1;
        var closest = vocabulary
            .Where(v => v.Key.Length >= 4 && Math.Abs(v.Key.Length - word.Length) <= allowed)
            .Select(v => (Word: v.Key, Distance: EditDistance(word, v.Key, allowed), Uses: v.Value))
            .Where(x => x.Distance <= allowed)
            // Nearest first; between equally near words, the one the texts use most.
            .OrderBy(x => x.Distance).ThenByDescending(x => x.Uses)
            .FirstOrDefault();
        return closest.Word ?? word;
    }

    // A Danish (or English plural) ending on a word the texts have without
    // it: "buret" also searches for "bur", "vitaminer" for "vitamin".
    private static IEnumerable<string> WithoutEnding(string word, Dictionary<string, int> vocabulary)
    {
        foreach (var ending in (string[])["erne", "ene", "ers", "er", "en", "et", "es", "e", "s"])
        {
            if (word.Length - ending.Length >= 3 && word.EndsWith(ending, StringComparison.Ordinal) &&
                vocabulary.ContainsKey(word[..^ending.Length]))
            {
                yield return word[..^ending.Length];
            }
        }
    }

    /// <summary>
    /// How many single-letter slips apart two words are - a letter missing,
    /// extra or wrong, or two neighbours swapped ("levreing"). Gives up early
    /// (returning more than <paramref name="limit"/>) once it can't be within it.
    /// </summary>
    private static int EditDistance(string a, string b, int limit)
    {
        var previous2 = new int[b.Length + 1];
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowBest = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    current[j] = Math.Min(current[j], previous2[j - 2] + 1);
                rowBest = Math.Min(rowBest, current[j]);
            }
            if (rowBest > limit) return limit + 1;
            (previous2, previous, current) = (previous, current, previous2);
        }
        return previous[b.Length];
    }

    private static string Head(string text) => text.Length <= 40 ? text : text[..40];

    // A term has to start a word in the text: "bur" finds "bur 120 x 60" and
    // "burkammerat", but not the letters b-u-r in the middle of another word.
    private static bool ContainsWord(string text, string term)
    {
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(text[index - 1])) return true;
            index += term.Length;
        }
        return false;
    }
}
