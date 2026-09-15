namespace MarsvinWebExample.Models;

// Backs the PDF downloads for /Foderliste and /PasningsguideHurtig (see
// Data/FoodListData.cs, Data/QuickGuideData.cs and their matching
// *PdfDocument classes). Those pages are hand-written Razor markup, not
// driven by this data - they existed first and have been through several
// rounds of careful review, so this mirrors them rather than replacing them.
// The tradeoff is the two have to be kept in sync by hand when either page's
// content changes; that's acceptable for reference content that rarely
// changes.
public sealed record GuideListItem(string Da, string En, string? NoteDa = null, string? NoteEn = null);

// Tier picks the PDF's colour accent: "safe"/"moderate"/"never" (the same
// three the food list's CSS uses) or "none" for a plain, uncoloured heading.
public sealed record GuideListSection(string TitleDa, string TitleEn, string Tier, IReadOnlyList<GuideListItem> Items, string? IntroDa = null, string? IntroEn = null);
