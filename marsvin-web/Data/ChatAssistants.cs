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

    /// <summary>
    /// The answer plus a few questions to offer as one-click buttons when Pip
    /// didn't know. <paramref name="previous"/> is the visitor's own question
    /// just before this one (sent along by the page, never stored), so "what
    /// does she cost?" can be understood after "tell me about Cotton".
    /// </summary>
    ChatReply Reply(string question, bool english, string? previous = null);
}

public sealed record ChatReply(string Answer, IReadOnlyList<string> Suggestions);

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

    public static string NoPersonalDetails(bool english) => english
        ? "Please don't type personal details here - no email address, phone, card or ID number. I can't use them, and I don't keep the conversation. Ask again without them, or use the contact page if it's about your own order."
        : "Skriv ikke personlige oplysninger her - ingen e-mailadresse, telefon-, kort- eller personnummer. Jeg kan ikke bruge dem, og jeg gemmer ikke samtalen. Spørg igen uden dem, eller brug kontaktsiden, hvis det handler om din egen ordre.";

    public static string SeeAVet(bool english) => english
        ? "I can't judge that - I'm not a vet, and I can't see your guinea pig. Contact a vet, ideally one who knows guinea pigs. If a guinea pig stops eating or drinking for a day, struggles to breathe or is bleeding, it's an emergency and can't wait."
        : "Det kan jeg ikke vurdere - jeg er ikke dyrlæge, og jeg kan ikke se dit marsvin. Kontakt en dyrlæge, gerne en der kender marsvin. Holder et marsvin op med at spise eller drikke i et døgn, har det svært ved at trække vejret, eller bløder det, er det akut og kan ikke vente.";

    public static string WhoIAm(bool english) => english
        ? "I'm Pip, the shop's chatbot - a small program that looks things up in the shop's own pages. I'm not a person and not an AI service, so I only know what the shop itself has written: the guinea pigs, the accessories, care and feeding, delivery, returns and opening hours."
        : "Jeg er Pip, butikkens chatbot - et lille program, der slår op i butikkens egne sider. Jeg er hverken et menneske eller en AI-tjeneste, så jeg ved kun det, butikken selv har skrevet: marsvinene, tilbehøret, pasning og foder, levering, retur og åbningstider.";

    public static string WhatICanDo(bool english) => english
        ? "Ask me things like:\n- Which guinea pigs are for sale?\n- Can they eat cucumber?\n- How big a cage for 3 guinea pigs?\n- What does it cost to send 2 kg?\n- Are you open now?\n- Can I return a cage?\nI can't see orders or accounts - for that, use My account or the contact page."
        : "Spørg mig for eksempel:\n- Hvilke marsvin er til salg?\n- Må de spise agurk?\n- Hvor stort et bur til 3 marsvin?\n- Hvad koster det at sende 2 kg?\n- Har I åbent nu?\n- Kan jeg returnere et bur?\nJeg kan ikke se ordrer eller konti - brug Min konto eller kontaktsiden til det.";

    public static string Bye(bool english) => english ? "Bye for now - wheek!" : "Farvel for nu - wheek!";

    public static string HowIAm(bool english) => english
        ? "Very well, thanks - there's hay in the rack. What can I help you with?"
        : "Rigtig godt, tak - der er hø i hækken. Hvad kan jeg hjælpe med?";

    public static string UnknownFood(bool english) => english
        ? "That one isn't on our food list, so I can't say it's safe. When in doubt, leave it out - hay, pellets and the vegetables on the list are the safe choices. The full list is on the Food list page."
        : "Den står ikke på vores foderliste, så jeg kan ikke sige, at den er sikker. Er du i tvivl, så lad være - hø, pillefoder og grøntsagerne på listen er de sikre valg. Hele listen står på siden Foderliste.";

    public static string NotAboutPeople(bool english) => english
        ? "I can't see orders, accounts or anything about people - not yours and not anyone else's. Your own orders are under My account, or write to us on the contact page."
        : "Jeg kan ikke se ordrer, konti eller noget om personer - hverken dine eller andres. Dine egne ordrer finder du under Min konto, eller skriv til os på kontaktsiden.";

    public static string NotAboutTheWebsite(bool english) => english
        ? "That's not something I answer - I only know about the shop, the animals and the products, not how the website is built or protected. If you can't log in, use \"Forgot password\" on the login page."
        : "Det svarer jeg ikke på - jeg ved kun noget om butikken, dyrene og varerne, ikke om hvordan hjemmesiden er bygget eller beskyttet. Kan du ikke logge ind, så brug \"Glemt adgangskode\" på log ind-siden.";
}

public sealed partial class KeywordChatAssistant(ICatalog catalog, TimeProvider? clock = null) : IChatAssistant
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
        "gang", "gange", "time", "times", "billigste", "billigst", "dyreste", "cheapest", "cheap", "expensive", "most", "best", "bedst", "bedste", "should", "need", "sell", "saelger", "please",
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
        ["nails", "nail", "claws", "kloer", "klipper", "negle"],
        ["trim", "trimme", "cut", "clip", "klippe", "klip"],
        ["brush", "boerste", "groom", "grooming", "pels", "boerstes"],
        ["bedding", "underlag", "bundlag", "stroeelse"],
        ["hay", "hoe"],
        ["toy", "toys", "lege", "legetoej"],
        ["house", "hideout", "hidey", "skjul", "hule", "hus"],
        ["bottle", "drikke", "drink", "water", "vand"],
    ];

    // What kind of answer the question is after. It decides how an accessory
    // is presented (its price, its advice, or a list of what we carry) and
    // nudges the choice between a product and a page of advice.
    private enum Intent { General, Advice, Price, Sell }

    private static readonly string[] PriceCues =
        ["koster", "pris", "prisen", "billigste", "billigst", "dyreste", "price", "prices", "cost", "costs", "cheapest", "cheap", "expensive", "how much is", "how much are", "how much does", "how much do"];

    private static readonly string[] SellCues =
        ["saelger", "har i", "har du", "foerer i", "udvalg", "paa lager", "sell", "do you have", "have you got", "got any", "in stock", "which", "hvilke", "hvilken"];

    private static readonly string[] AdviceCues =
        ["hvor tit", "hvor ofte", "hvor mange gange", "hvordan", "hvorfor", "skal jeg", "skal man", "boer", "hvornaar skal",
         "how often", "how many", "do i", "how do", "how should", "how to", "why", "should", "when do", "when should", "do i need", "can i", "can they"];

    // "Which cages do you have?" names a whole shelf rather than a product.
    private static readonly (Models.AccessoryCategory Category, string[] Words)[] CategoryWords =
    [
        (Models.AccessoryCategory.Hay,     ["hoe", "hay"]),
        (Models.AccessoryCategory.Food,    ["foder", "pillefoder", "godbidder", "snacks", "food", "pellets", "treats"]),
        (Models.AccessoryCategory.Cage,    ["bur", "bure", "loebegaard", "loebegaarde", "cage", "cages", "run", "runs"]),
        (Models.AccessoryCategory.House,   ["hus", "huse", "huler", "house", "houses", "hideouts"]),
        (Models.AccessoryCategory.Toy,     ["legetoej", "tunnel", "tunneler", "toy", "toys", "tunnels"]),
        (Models.AccessoryCategory.Bedding, ["stroeelse", "bedding"]),
        (Models.AccessoryCategory.Care,    ["plejeprodukter", "skaale", "bowls", "bottles", "drikkeflasker"])
    ];

    private static Intent IntentOf(string folded)
    {
        if (PriceCues.Any(c => ContainsWord(folded, c))) return Intent.Price;
        if (AdviceCues.Any(c => ContainsWord(folded, c))) return Intent.Advice;
        if (SellCues.Any(c => ContainsWord(folded, c))) return Intent.Sell;
        return Intent.General;
    }

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

    public string Answer(string question, bool english) => Reply(question, english).Answer;

    // Words that point back at something said before: "what does *she* cost?"
    private static readonly HashSet<string> Pronouns =
        ["it", "its", "they", "them", "she", "he", "her", "him", "one", "ones", "that", "those", "these",
         "den", "det", "de", "dem", "hun", "han", "hende", "ham", "dens", "hendes", "hans", "disse"];

    private static readonly Regex SentenceEnd = new(@"(?<=[?!.])\s+", RegexOptions.Compiled);

    public ChatReply Reply(string question, bool english, string? previous = null)
    {
        // Two or three questions in one message are answered one by one.
        var parts = SentenceEnd.Split(question.Trim()).Where(p => WordPattern.Matches(p).Count >= 2).Take(3).ToList();
        if (parts.Count > 1)
        {
            var answers = parts.Select(p => One(p, english, previous, [])).OfType<string>().Distinct().ToList();
            if (answers.Count > 0) return new ChatReply(string.Join("\n\n", answers), []);
        }

        var near = new List<ShopFact>();
        if (One(question, english, previous, near) is { } answer) return new ChatReply(answer, []);

        // Didn't know: offer the closest things Pip does know about, as
        // ready-made questions - or a few general ones if nothing came close.
        var suggestions = near.Select(f => english ? f.AskEn : f.AskDa).OfType<string>().Distinct().Take(3).ToList();
        if (suggestions.Count == 0)
        {
            suggestions = english
                ? ["Which guinea pigs are for sale?", "What do guinea pigs eat?", "What does shipping cost?"]
                : ["Hvilke marsvin er til salg?", "Hvad spiser marsvin?", "Hvad koster fragt?"];
        }
        return new ChatReply(ChatReplies.DontKnow(english), suggestions);
    }

    // One question, with one retry: if it can't be answered as it stands, or
    // it only points back ("what does it cost?"), the subject of the
    // visitor's previous question is added and it's tried again.
    private string? One(string question, bool english, string? previous, List<ShopFact> near)
    {
        var words = WordPattern.Matches(Fold(question)).Select(m => m.Value).ToList();
        // A previous question Pip refused is not context for anything: it is
        // dropped, so a follow-up can't be used to come at it sideways.
        if (previous is not null && IsRefused(previous)) previous = null;

        var subject = previous is null ? [] : WordPattern.Matches(Fold(previous)).Select(m => m.Value)
            .Where(w => !StopWords.Contains(w) && !Greetings.Contains(w) && !ThankYous.Contains(w) && !Pronouns.Contains(w))
            .Distinct().Take(4).ToList();
        var pointsBack = subject.Count > 0 && words.Any(Pronouns.Contains)
            && words.Count(w => !StopWords.Contains(w) && !Pronouns.Contains(w)) <= 1;

        if (pointsBack && Find($"{question} {string.Join(' ', subject)}", english, []) is { } followUp) return followUp;

        var direct = Find(question, english, near);
        if (direct is not null || subject.Count == 0 || pointsBack) return direct;

        return Find($"{question} {string.Join(' ', subject)}", english, []);
    }

    private static bool IsRefused(string question)
    {
        var folded = Fold(question);
        return EmailPattern.IsMatch(question) || LongNumberPattern.IsMatch(question) || OrderNumberPattern.IsMatch(question)
            || WebsiteTopics.Any(t => ContainsWord(folded, t)) || PeopleTopics.Any(t => ContainsWord(folded, t));
    }

    /// <summary>The answer, or null when Pip doesn't know. <paramref name="near"/> collects the closest misses.</summary>
    private string? Find(string question, bool english, List<ShopFact> near)
    {
        var folded = Fold(question);
        var intent = IntentOf(folded);

        if (SafetyStop(question, folded, english) is { } stop) return stop;
        if (WebsiteTopics.Any(t => ContainsWord(folded, t))) return ChatReplies.NotAboutTheWebsite(english);
        if (OrderNumberPattern.IsMatch(question) || PeopleTopics.Any(t => ContainsWord(folded, t)))
            return ChatReplies.NotAboutPeople(english);
        if (Skill(folded, intent, english) is { } skilled) return skilled;

        var allWords = WordPattern.Matches(folded).Select(m => m.Value).ToList();
        var words = allWords.Where(w => !StopWords.Contains(w)).ToList();

        // "Hej" / "tak" and nothing else worth looking up.
        var rest = words.Where(w => !Greetings.Contains(w) && !ThankYous.Contains(w) && !Pronouns.Contains(w)).ToList();
        if (rest.Count == 0)
        {
            if (allWords.Any(ThankYous.Contains)) return ChatReplies.Thanks(english);
            if (allWords.Any(Greetings.Contains)) return ChatReplies.Greeting(english);
            return null;
        }

        // "Do you have hay?" / "What does a cage cost?" - a whole shelf and
        // nothing more specific: list it (cheapest first when price is the
        // question) rather than picking one product to describe.
        if (intent is Intent.Sell or Intent.Price)
        {
            var specifics = rest.Where(w => !EverywhereWords.Contains(w)).Distinct().ToList();
            var shelf = CategoryWords.Where(c => specifics.Count > 0 && specifics.All(c.Words.Contains))
                .Select(c => (Models.AccessoryCategory?)c.Category).FirstOrDefault();
            var onShelf = catalog.Accessories.Where(a => a.Category == shelf && a.StockLevel != Models.StockLevel.OutOfStock);
            onShelf = ContainsWord(folded, "dyreste") || ContainsWord(folded, "expensive") ? onShelf.OrderByDescending(a => a.Price)
                : intent == Intent.Price ? onShelf.OrderBy(a => a.Price)
                : onShelf;
            var listed = onShelf.Take(5).ToList();
            if (listed.Count > 1) return ProductList(listed, english);
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
        var topicTerms = groups.Where(g => Synonyms.Any(s => s.Contains(g[0]))).SelectMany(g => g).Distinct().ToList();

        // A word found in few facts says more about what's being asked than
        // one found in fifty, so each match counts for more the rarer it is.
        var matches = terms.ToDictionary(t => t, t => facts.Where(f => ContainsWord(f.Text, t)).Select(f => f.Fact).ToHashSet());

        // "Can they eat pizza?" - a word nothing in the shop's texts knows,
        // in a question about food: say it isn't on the list, rather than
        // answering the rest of the sentence as if the pizza weren't there.
        if (IsFoodQuestion(folded) && groups.Any(g => g.All(t => matches[t].Count == 0)))
            return ChatReplies.UnknownFood(english);
        var scored = facts
            .Select(f => (f.Fact,
                // A word in a fact's opening counts double: asked about Lente,
                // Lente's own entry should win over her sister's, which only mentions her.
                Score: terms.Sum(t => matches[t].Contains(f.Fact) ? (ContainsWord(f.Opening, t) ? 2.0 : 1.0) / matches[t].Count : 0),
                // How much of the question this fact actually speaks to.
                Coverage: groups.Count(g => g.Any(t => matches[t].Contains(f.Fact))) / (double)groups.Count))
            .ToList();
        near.AddRange(scored.Where(x => x.Score > 0 && x.Fact.AskDa is not null)
            .OrderByDescending(x => x.Score * x.Coverage).Take(3).Select(x => x.Fact));

        var best = scored
            // One shared word out of several isn't an answer - "the meaning of
            // life" must not get the chew sticks because both say "life".
            // ...unless the shared word is one of the shop's own topics
            // ("return", "shipping"): "can I return a cage" is about returns
            // even though the returns text never says "cage".
            .Where(x => x.Score > 0 && (groups.Count == 1 || x.Coverage > 0.5 ||
                (x.Coverage >= 0.5 && topicTerms.Any(t => matches[t].Contains(x.Fact)))))
            // "How often..." is after advice, so a page of advice beats a
            // product that happens to share the words; "what does ... cost"
            // and "do you have ..." lean the other way.
            .Select(x => (x.Fact, Score: x.Score * x.Coverage * (x.Fact, intent) switch
            {
                ({ Curated: true }, Intent.Advice or Intent.General) => 1.5,
                ({ Animal: not null }, Intent.Advice) => 0.5,
                ({ Product: not null }, Intent.Advice) => 0.75,
                ({ Product: not null }, Intent.Price or Intent.Sell) => 1.25,
                _ => 1.0
            }))
            .OrderByDescending(x => x.Score)
            .ToList();
        // A food nobody has listed gets a careful "not on the list", never a guess.
        if (best.Count == 0) return IsFoodQuestion(folded) ? ChatReplies.UnknownFood(english) : null;

        if (best[0].Fact.Product is { } product)
        {
            // "Do you have hay?" - several products fit about equally well:
            // name them, rather than picking one and reading it out in full.
            var alike = best.Where(x => x.Fact.Product is not null && x.Score >= best[0].Score * 0.6)
                .Select(x => x.Fact.Product!).Take(5).ToList();
            if (intent == Intent.Sell ? alike.Count > 1 : intent != Intent.Advice && alike.Count > 2)
                return ProductList(alike, english);

            return ProductAnswer(product, intent, english);
        }

        if (intent == Intent.Price && best[0].Fact.Animal is { } animal)
        {
            return english
                ? $"{animal.Name} costs {animal.Price:0} kr. ({animal.StatusTextEn.ToLowerInvariant()}). Guinea pigs are picked up in the shop - they are not shipped."
                : $"{animal.Name} koster {animal.Price:0} kr. ({animal.StatusText.ToLowerInvariant()}). Marsvin hentes i butikken - de sendes ikke.";
        }

        // The runner-up only comes along if it's nearly as good a match.
        return string.Join("\n\n", best.Take(2).Where(x => x.Fact.Product is null && x.Score >= best[0].Score * 0.8)
            .Select(x => english ? x.Fact.ReplyEn : x.Fact.ReplyDa));
    }

    // One accessory, worded for what was asked: the price first for a price
    // question, the advice first for a "how often / how do I" question.
    private static string ProductAnswer(Models.StockProduct item, Intent intent, bool english)
    {
        var name = english ? item.NameEn ?? item.Name : item.Name;
        var text = english ? item.DescriptionEn ?? item.Description : item.Description;
        var stock = (english ? item.StockLevelTextEn : item.StockLevelText).ToLowerInvariant();
        var brand = item.Brand is null ? "" : english ? $" by {item.Brand}" : $" fra {item.Brand}";

        return (intent, english) switch
        {
            (Intent.Advice, true)  => $"{text}\n\nWe sell {name}{brand} for {item.Price:0} kr. ({stock}).",
            (Intent.Advice, false) => $"{text}\n\nVi har {name}{brand} til {item.Price:0} kr. ({stock}).",
            (Intent.Price, true)   => $"{name}{brand} costs {item.Price:0} kr. ({stock}).\n\n{text}",
            (Intent.Price, false)  => $"{name}{brand} koster {item.Price:0} kr. ({stock}).\n\n{text}",
            (_, true)              => $"{name}{brand} - {item.Price:0} kr., {stock}.\n\n{text}",
            (_, false)             => $"{name}{brand} - {item.Price:0} kr., {stock}.\n\n{text}"
        };
    }

    private static string ProductList(IReadOnlyList<Models.StockProduct> items, bool english)
    {
        var lines = items.Select(i => english
            ? $"- {i.NameEn ?? i.Name}, {i.Price:0} kr. ({i.StockLevelTextEn.ToLowerInvariant()})"
            : $"- {i.Name}, {i.Price:0} kr. ({i.StockLevelText.ToLowerInvariant()})");
        return (english ? "We have, among others:\n" : "Vi har blandt andet:\n")
            + string.Join("\n", lines)
            + (english ? "\n\nYou'll find them all under Accessories - ask me about one of them to hear more."
                       : "\n\nDu finder dem alle under Tilbehør - spørg mig om en af dem for at høre mere.");
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
