using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public sealed record FaqEntry(string Topic, string TopicEn, string Question, string QuestionEn, string Answer, string AnswerEn);

/// <summary>
/// The shop's frequently asked questions - one list, used twice: rendered as
/// the /Faq page, and handed to the support chat as everything it knows
/// about the shop (see ClaudeChatAssistant / FaqChatAssistant). Keeping it
/// in one place is what stops the chat from answering differently from the
/// page. Numbers that exist elsewhere in code (the free-shipping threshold)
/// are read from there rather than typed in again.
/// </summary>
public static class FaqData
{
    private static readonly string FreeShipping = ShippingCalculator.FreeShippingThreshold.ToString("0");

    public static IReadOnlyList<FaqEntry> Entries { get; } =
    [
        new("Bestilling", "Ordering",
            "Skal jeg have en konto for at købe?",
            "Do I need an account to buy?",
            "Nej. Du kan købe som gæst med navn og e-mail. Med en konto får du en ordrehistorik, hvor du kan følge hver ordres status og annullere en ordre selv.",
            "No. You can check out as a guest with a name and email. With an account you get an order history where you can follow each order's status and cancel an order yourself."),
        new("Bestilling", "Ordering",
            "Er varerne i min kurv reserveret?",
            "Are the items in my cart reserved?",
            "Nej. En vare er først din, når købet er gennemført. Det gælder især marsvinene - der findes kun ét af hvert.",
            "No. An item is only yours once you've checked out. That goes especially for the guinea pigs - there's only one of each."),
        new("Bestilling", "Ordering",
            "Kan jeg købe som virksomhed?",
            "Can I buy as a company?",
            "Ja. Vælg \"Virksomhed\" i kassen og angiv firmanavn og CVR-nummer. De kommer med på kvitteringen sammen med momsbeløbet.",
            "Yes. Choose \"Company\" at checkout and enter the company name and CVR number. Both appear on the receipt along with the VAT amount."),
        new("Bestilling", "Ordering",
            "Kan jeg fortryde en ordre?",
            "Can I cancel an order?",
            "Ja, gratis, så længe ordrens status er \"modtaget\" - på kvitteringssiden eller under Min konto. Er vi begyndt at pakke den, så kontakt butikken; vi kan annullere, indtil den er sendt.",
            "Yes, free, for as long as the order's status is \"received\" - on the receipt page or under My account. Once we've started packing it, contact the shop; we can cancel until it has been sent."),

        new("Levering", "Delivery",
            "Hvad koster fragt?",
            "What does shipping cost?",
            $"Afhentning i butikken er gratis. Fragt koster fra 39 kr. afhængigt af vægt og fragtselskab, og er gratis, når det tilbehør du får sendt koster {FreeShipping} kr. eller mere. Den præcise pris vises i kassen, før du betaler.",
            $"Pickup in store is free. Shipping starts at 39 kr. depending on weight and carrier, and is free when the accessories you have shipped come to {FreeShipping} kr. or more. The exact price is shown at checkout before you pay."),
        new("Levering", "Delivery",
            "Hvor lang tid tager levering?",
            "How long does delivery take?",
            "1-4 hverdage afhængigt af fragtselskab: PostNord 1-2, GLS 1-3 og DAO 2-4 hverdage. Det er et skøn, og den forventede dato vises i kassen og på kvitteringen.",
            "1-4 weekdays depending on carrier: PostNord 1-2, GLS 1-3 and DAO 2-4 weekdays. It's an estimate, and the expected date is shown at checkout and on the receipt."),
        new("Levering", "Delivery",
            "Leverer I til døren eller til et udleveringssted?",
            "Do you deliver to the door or to a pick-up point?",
            "Begge dele. PostNord leverer til døren; GLS og DAO leverer til et udleveringssted nær din adresse, og fragtselskabet giver besked, når pakken kan hentes.",
            "Both. PostNord delivers to the door; GLS and DAO deliver to a pick-up point near your address, and the carrier messages you when the parcel is ready."),
        new("Levering", "Delivery",
            "Kan I sende et marsvin?",
            "Can you ship a guinea pig?",
            "Nej. Marsvin afhentes altid i butikken, så vi kan nå at snakke om buret med dig først. Tilbehør i samme ordre kan stadig sendes.",
            "No. Guinea pigs are always picked up in store, so we can talk through the cage with you first. Accessories in the same order can still be shipped."),
        new("Levering", "Delivery",
            "Hvordan følger jeg min ordre?",
            "How do I follow my order?",
            "Du får en e-mail, hver gang ordren skifter status: under behandling, afsendt eller klar til afhentning, leveret. Med en konto ser du også status og track & trace-nummer under Min konto.",
            "You get an email every time the order's status changes: being prepared, sent or ready for pickup, delivered. With an account you also see the status and track & trace number under My account."),

        new("Retur", "Returns",
            "Kan jeg returnere tilbehør?",
            "Can I return accessories?",
            "Ja, inden for 14 dage fra du modtager varen, ubrugt og i original emballage. Aflevér den gratis i butikken, eller send den retur for egen regning. Pengene tilbageføres senest 14 dage efter, at vi har fået varen.",
            "Yes, within 14 days of receiving the item, unused and in its original packaging. Hand it in at the shop for free, or send it back at your own cost. The money is returned within 14 days of us getting it back."),
        new("Retur", "Returns",
            "Hvad hvis det ikke går med marsvinet?",
            "What if it doesn't work out with the guinea pig?",
            "Kom tilbage til butikken med det inden for 14 dage og få hele beløbet retur - og kontakt os gerne også derefter. Vi tager hellere et dyr tilbage, end at det bor et sted, hvor det ikke trives.",
            "Bring it back to the shop within 14 days for a full refund - and talk to us after that too. We'd rather take an animal back than have it somewhere it isn't thriving."),

        new("Marsvin", "Guinea pigs",
            "Kan jeg købe ét marsvin alene?",
            "Can I buy a single guinea pig?",
            "Kun hvis det flytter ind hos et marsvin, du allerede har. Marsvin er flokdyr og mistrives alene, så de sælges som par eller til en eksisterende flok.",
            "Only if it's joining a guinea pig you already have. Guinea pigs are herd animals and don't thrive alone, so they're sold as a pair or to an existing herd."),
        new("Marsvin", "Guinea pigs",
            "Hvor gammel skal jeg være for at købe et marsvin?",
            "How old do I have to be to buy a guinea pig?",
            "Du skal være fyldt 16 år. Er du yngre, så kom forbi butikken sammen med en forælder.",
            "You have to be 16 or older. If you're younger, come to the shop with a parent."),
        new("Marsvin", "Guinea pigs",
            "Hvor stort et bur skal to marsvin have?",
            "How big a cage do two guinea pigs need?",
            "Mindst 120 x 60 cm til to marsvin - mindre bure findes, men de er for små. Se Pasningsguiden for resten: foder, hø, C-vitamin og daglig pasning.",
            "At least 120 x 60 cm for two guinea pigs - smaller cages exist, but they're too small. See the care guide for the rest: food, hay, vitamin C and daily care."),

        new("Butikken", "The shop",
            "Hvornår har I åbent, og hvor ligger butikken?",
            "When are you open, and where is the shop?",
            "Havnegade 12, 6700 Esbjerg. Åbent torsdag og fredag 14-18 og lørdag 10-14.",
            "Havnegade 12, 6700 Esbjerg. Open Thursday and Friday 14-18 and Saturday 10-14."),
        new("Butikken", "The shop",
            "Hvordan betaler jeg?",
            "How do I pay?",
            "Med kort eller MobilePay i kassen. Betalingen er en demo: der trækkes ingen penge, og ingen kortoplysninger gemmes.",
            "By card or MobilePay at checkout. The payment is a demo: no money is taken and no card details are stored."),
        new("Butikken", "The shop",
            "Kan jeg donere til marsvin i nød?",
            "Can I donate to guinea pigs in need?",
            "Ja. På siden Støt kan du give et beløb eller tilbyde tilbehør, foder eller hø, som går til de marsvin, butikken tager retur og finder nye hjem til.",
            "Yes. On the Support page you can give an amount or offer accessories, food or hay, which go to the guinea pigs the shop takes back and rehomes."),
        new("Butikken", "The shop",
            "Er det her en rigtig butik?",
            "Is this a real shop?",
            "Nej. Marsvin er en eksempelbutik bygget som skoleprojekt. Adresse, telefonnummer og betaling er pladsholdere - der sælges ikke rigtige dyr.",
            "No. Marsvin is an example shop built as a school project. The address, phone number and payment are placeholders - no real animals are sold.")
    ];
}
