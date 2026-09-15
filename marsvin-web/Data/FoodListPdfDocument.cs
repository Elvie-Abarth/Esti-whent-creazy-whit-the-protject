using MarsvinWebExample.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarsvinWebExample.Data;

// The downloadable PDF for /Foderliste - a real, server-generated document
// (selectable text, not a screenshot of the page) so "save as PDF" doesn't
// depend on what the visitor's browser print dialog happens to offer. Colours
// mirror the .food-callout--safe/--moderate/--never tiers in site.css.
public sealed class FoodListPdfDocument(bool english) : IDocument
{
    private const string Safe = "#74964A";
    private const string SafeTint = "#EFF4E6";
    private const string Moderate = "#C1791A";
    private const string ModerateTint = "#FBF1DE";
    private const string Never = "#7A1F3D";
    private const string NeverTint = "#FBEAEE";
    private const string Ink = "#2C4327";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Text(english ? FoodListData.TitleEn : FoodListData.TitleDa)
                    .Bold().FontSize(20).FontColor(Ink);
                col.Item().PaddingTop(2).Text(english
                    ? "Marsvin - example shop built for a school project, not a real business"
                    : "Marsvin - eksempelbutik bygget til et skoleprojekt, ikke en rigtig forretning")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingTop(12).Column(col =>
            {
                foreach (var (da, en) in FoodListData.IntroParagraphs)
                    col.Item().PaddingBottom(6).Text(english ? en : da).FontSize(9.5f);

                foreach (var section in FoodListData.FoodSections)
                    RenderSection(col, section);

                col.Item().PaddingTop(16).Text(english ? FoodListData.GardenHeading.En : FoodListData.GardenHeading.Da)
                    .Bold().FontSize(15).FontColor(Ink);
                col.Item().PaddingTop(2).PaddingBottom(4)
                    .Text(english ? FoodListData.GardenIntro.En : FoodListData.GardenIntro.Da).FontSize(9.5f);

                foreach (var section in FoodListData.GardenSections)
                    RenderSection(col, section);
            });

            page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1)).Text(x =>
            {
                x.Span(english ? "Marsvin food list - page " : "Marsvins foderliste - side ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }

    private void RenderSection(ColumnDescriptor column, FoodListSection section)
    {
        var (accent, tint) = section.Tier switch
        {
            "safe" => (Safe, SafeTint),
            "moderate" => (Moderate, ModerateTint),
            _ => (Never, NeverTint),
        };

        column.Item().PaddingTop(12).Background(tint).BorderLeft(3).BorderColor(accent).Padding(10).Column(inner =>
        {
            inner.Item().Text(english ? section.TitleEn : section.TitleDa).Bold().FontSize(13).FontColor(Ink);

            var intro = english ? section.IntroEn : section.IntroDa;
            if (!string.IsNullOrEmpty(intro))
                inner.Item().PaddingTop(3).Text(intro).FontSize(9).FontColor(Colors.Grey.Darken2);

            inner.Item().PaddingTop(8).Element(c => RenderItemGrid(c, section.Items));
        });
    }

    // Two columns, filled left-to-right row by row - the same order the
    // site's CSS grid (grid-auto-flow: row, the default) lays these out in.
    private void RenderItemGrid(IContainer container, IReadOnlyList<FoodListItem> items)
    {
        container.Column(col =>
        {
            for (var i = 0; i < items.Count; i += 2)
            {
                col.Item().PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem().Element(c => RenderItem(c, items[i]));
                    if (i + 1 < items.Count)
                        row.RelativeItem().PaddingLeft(10).Element(c => RenderItem(c, items[i + 1]));
                    else
                        row.RelativeItem();
                });
            }
        });
    }

    private void RenderItem(IContainer container, FoodListItem item)
    {
        container.Column(col =>
        {
            col.Item().Text(english ? item.En : item.Da).Bold().FontSize(9.5f);
            var note = english ? item.NoteEn : item.NoteDa;
            if (!string.IsNullOrEmpty(note))
                col.Item().PaddingTop(1).Text(note).FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }
}
