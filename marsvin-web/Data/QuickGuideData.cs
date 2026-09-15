using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

// Mirrors the content of Pages/PasningsguideHurtig.cshtml for
// QuickGuidePdfDocument. Keep this in sync by hand when that page's facts
// change - see the note on GuideListItem/GuideListSection for why it isn't
// the other way around.
public static class QuickGuideData
{
    public const string TitleDa = "Marsvins lynguide";
    public const string TitleEn = "Guinea pig quick guide";

    public static readonly (string Da, string En) Intro = (
        "Én side. Alt der er værd at hænge på køleskabet, ingen af forklaringerne. Se den fulde pasningsguide for hvorfor bag hver linje.",
        "One page. Everything worth pinning to the fridge, none of the reading. See the full care guide for the why behind each line.");

    // Tier "none" - these read as plain fact rows on the page itself, not
    // coloured callouts, so QuickGuidePdfDocument renders them that way too.
    public static readonly IReadOnlyList<GuideListSection> Sections =
    [
        new("Hver dag", "Every day", "none",
        [
            new("Hø", "Hay", "Ubegrænset, altid", "Unlimited, always"),
            new("Piller", "Pellets", "Afmålt, marsvine-specifik", "Measured, guinea-pig specific"),
            new("Grøntsager", "Vegetables", "Små mængder, højt C-vitamin", "Small amounts, high in vitamin C"),
            new("Vand", "Water", "Tjekket, ikke bare til stede", "Checked, not just present"),
            new("Punktrengøring", "Spot-clean", "Våd strøelse og efterladenskaber ud", "Wet bedding and droppings out"),
        ]),
        new("Hver uge", "Every week", "none",
        [
            new("Fuld rengøring", "Full clean", "Al strøelse ud, bur rengjort", "All bedding out, cage cleaned"),
            new("Vej dem", "Weigh them", "Faldende tal er ofte første advarsel", "A dropping number is often the first warning sign"),
            new("Vandflaske", "Water bottle", "Skrubbet - alger opbygges indeni", "Scrubbed - algae builds up inside"),
        ]),
        new("Aldrig", "Never", "none",
        [
            new("Alene", "Alone", "Flokdyr - tilbagetrukne, ikke rolige, alene", "Herd animals - withdrawn, not calm, on their own"),
            new("Bur med trådbund", "A wire-bottom cage", "Ømme, sårede poter over tid", "Sore, ulcerated feet over time"),
            new("En dag uden mad, uden handling", "A day without eating, unaddressed", "Ring til dyrlæge, vent ikke", "Call a vet, don't wait"),
            new("Avocado, løg, hvidløg, chokolade", "Avocado, onion, garlic, chocolate", "Se hele foderlisten", "See the full food list"),
        ]),
        new("Læs et marsvin", "Reading a guinea pig", "none",
        [
            new("Popcorner (hopper, snor sig)", "Popcorning (jumping, twisting)", "Ren glæde", "Pure joy"),
            new("Fløjter højlydt", "Wheeking (loud whistle)", "Giv mig mad, nu", "Feed me, now"),
            new("Klaprer med tænderne", "Teeth chattering", "Træk dig - en advarsel", "Back off - a warning"),
            new("Fryser helt stille", "Freezing in place", "Forskrækket eller bange", "Startled or scared"),
        ]),
    ];

    public static readonly (string Da, string En) EmergencyTitle = ("Akut", "Emergency");

    public static readonly (string Da, string En) EmergencyText = (
        "Ikke spist eller drukket i et døgn, besværet vejrtrækning, eller pludseligt vægttab - dyrlæge samme dag, ikke vente og se an.",
        "Not eating or drinking for 24 hours, laboured breathing, or sudden weight loss - same-day vet, not wait-and-see.");
}
