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
        ? "I don't know that - I can only answer from what's on our front page, the guinea pig and accessories pages, the footer and the FAQ. For anything else, write to us on the contact page."
        : "Det ved jeg ikke - jeg kan kun svare ud fra forsiden, marsvin- og tilbehørssiderne, bunden af siden og Ofte stillede spørgsmål. Til alt andet: skriv til os på kontaktsiden.";

    public static string Greeting(bool english) => english
        ? "Wheek, hello! Ask me about our guinea pigs, the accessories, delivery, returns or the shop's opening hours."
        : "Wheek, hej! Spørg mig om vores marsvin, tilbehøret, levering, retur eller butikkens åbningstider.";

    public static string Thanks(bool english) => english
        ? "You're welcome! Just ask if there's anything else."
        : "Selv tak! Spørg endelig, hvis der er mere.";
}

public sealed class KeywordChatAssistant(ICatalog catalog) : IChatAssistant
{
    private static readonly Regex WordPattern = new(@"[\p{L}\p{Nd}]{2,}", RegexOptions.Compiled);

    // Words too common to tell one fact from another.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "hvad", "hvor", "hvordan", "hvornår", "hvilke", "hvilken", "hvem", "hvorfor", "fortæl", "noget", "nogen", "jeres",
        "har", "kan", "jeg", "der", "det", "den", "med", "til", "er", "en", "et", "på", "af", "om", "vi", "du", "at", "og",
        "for", "som", "eller", "ikke", "min", "mit", "mine", "jer", "man", "skal", "vil", "koster", "pris", "mig",
        "what", "where", "when", "which", "how", "who", "why", "this", "that", "there", "some", "any",
        "the", "and", "you", "your", "have", "does", "can", "are", "with", "is", "it", "do", "to", "of", "in", "on", "me", "my",
        "much", "many", "cost", "costs", "price", "about", "tell", "kr"
    };

    // People rarely use the page's own word. Each group lists ways of asking
    // about one thing; any word in a group also searches for the group's
    // last entry, which is a word the facts themselves use.
    private static readonly string[][] Synonyms =
    [
        ["åben", "åbent", "åbner", "lukker", "lukket", "open", "opens", "close", "closed", "hours", "åbningstider"],
        ["adresse", "ligger", "finder", "address", "located", "location", "find", "Havnegade"],
        ["sende", "sender", "sendes", "forsendelse", "porto", "ship", "shipping", "postage", "delivery", "deliver", "levering", "fragt"],
        ["returnere", "bytte", "refundering", "refund", "exchange", "return", "returns", "retur"],
        ["annullere", "afbestille", "fortryde", "cancel", "annullér", "fortryd"],
        ["betale", "betaling", "kort", "mobilepay", "pay", "payment", "card", "betaler"],
        ["firma", "virksomhed", "skole", "institution", "company", "business", "school", "cvr"],
        ["donere", "donation", "støtte", "donate", "støt"],
        ["alder", "gammel", "old", "age", "fyldt"],
        ["spise", "spiser", "mad", "foder", "eat", "eats", "food", "feed", "hø"],
        ["alene", "enkelt", "single", "alone", "one", "flokdyr"],
        ["bur", "plads", "størrelse", "cage", "size", "space", "room", "120"],
        ["syg", "dyrlæge", "sick", "ill", "vet", "dyrlæge"],
        ["vitamin", "vitaminer", "vitamins", "C-vitamin"],
        ["konto", "login", "account", "gæst", "guest", "konto"],
        ["spore", "status", "track", "tracking", "følger"],
    ];

    private static readonly HashSet<string> Greetings = new(StringComparer.OrdinalIgnoreCase)
        { "hej", "hejsa", "goddag", "davs", "hallo", "hello", "hi", "hey" };

    private static readonly HashSet<string> ThankYous = new(StringComparer.OrdinalIgnoreCase)
        { "tak", "takker", "thanks", "thank" };

    public string Answer(string question, bool english)
    {
        var allWords = WordPattern.Matches(question).Select(m => m.Value).ToList();
        var words = allWords.Where(w => !StopWords.Contains(w)).ToList();

        // "Hej" / "tak" and nothing else worth looking up.
        var rest = words.Where(w => !Greetings.Contains(w) && !ThankYous.Contains(w)).ToList();
        if (rest.Count == 0)
        {
            if (allWords.Any(ThankYous.Contains)) return ChatReplies.Thanks(english);
            if (allWords.Any(Greetings.Contains)) return ChatReplies.Greeting(english);
            return ChatReplies.DontKnow(english);
        }

        var terms = rest
            .Concat(rest.SelectMany(w => Synonyms.Where(g => g.Contains(w, StringComparer.OrdinalIgnoreCase)).Select(g => g[^1])))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var best = ShopKnowledge.Build(catalog)
            // Matched against both languages - a Danish product name typed on
            // the English page (or the other way round) should still be found.
            .Select(f => (Fact: f, Score: terms.Count(t => ContainsWord(f.Da, t) || ContainsWord(f.En, t))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(2)
            .ToList();
        if (best.Count == 0) return ChatReplies.DontKnow(english);

        // Only facts as good as the best one - one clear hit shouldn't drag a weak one along.
        return string.Join("\n\n", best.Where(x => x.Score == best[0].Score).Select(x => english ? x.Fact.ReplyEn : x.Fact.ReplyDa));
    }

    // A term has to start a word in the fact: "bur" finds "Bur 120 x 60" and
    // "burkammerat", but not the "bur" inside "Esbjerg"-style accidents.
    private static bool ContainsWord(string text, string term)
    {
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (index == 0 || !char.IsLetterOrDigit(text[index - 1])) return true;
            index += term.Length;
        }
        return false;
    }
}
