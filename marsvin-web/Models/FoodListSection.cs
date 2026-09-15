namespace MarsvinWebExample.Models;

// Backs the PDF download for /Foderliste (see Data/FoodListData.cs and
// Data/FoodListPdfDocument.cs). The page itself is hand-written Razor markup,
// not driven by this data - it existed first and has been through several
// rounds of careful review, so this mirrors it rather than replacing it. The
// tradeoff is the two have to be kept in sync by hand when the food list
// changes; that's acceptable for reference content that rarely changes.
public sealed record FoodListItem(string Da, string En, string? NoteDa = null, string? NoteEn = null);

public sealed record FoodListSection(string TitleDa, string TitleEn, string Tier, IReadOnlyList<FoodListItem> Items, string? IntroDa = null, string? IntroEn = null);
