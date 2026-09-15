using MarsvinWebExample.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarsvinWebExample.Data;

// The downloadable PDF for /PasningsguideHurtig - see FoodListPdfDocument
// for why this exists as a real server-generated document rather than
// relying on the browser's print-to-PDF. Sections render as plain fact rows
// (matching the page's plain <article> blocks, no colour coding there);
// only the closing "Emergency" callout gets an accent, matching the site's
// default .callout border colour (--fleece).
public sealed class QuickGuidePdfDocument(bool english) : IDocument
{
    private const string Ink = "#2C4327";
    private const string Fleece = "#B83C6E";
    private const string FleeceTint = "#FCEAF1";

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
                col.Item().Text(english ? QuickGuideData.TitleEn : QuickGuideData.TitleDa)
                    .Bold().FontSize(20).FontColor(Ink);
                col.Item().PaddingTop(2).Text(english
                    ? "Marsvin - example shop built for a school project, not a real business"
                    : "Marsvin - eksempelbutik bygget til et skoleprojekt, ikke en rigtig forretning")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingTop(12).Column(col =>
            {
                col.Item().PaddingBottom(10).Text(english ? QuickGuideData.Intro.En : QuickGuideData.Intro.Da).FontSize(9.5f);

                foreach (var section in QuickGuideData.Sections)
                    RenderSection(col, section);

                col.Item().PaddingTop(14).Background(FleeceTint).BorderLeft(3).BorderColor(Fleece).Padding(10).Column(inner =>
                {
                    inner.Item().Text(english ? QuickGuideData.EmergencyTitle.En : QuickGuideData.EmergencyTitle.Da)
                        .Bold().FontSize(13).FontColor(Ink);
                    inner.Item().PaddingTop(3)
                        .Text(english ? QuickGuideData.EmergencyText.En : QuickGuideData.EmergencyText.Da).FontSize(9.5f);
                });
            });

            page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1)).Text(x =>
            {
                x.Span(english ? "Marsvin quick guide - page " : "Marsvins lynguide - side ");
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }

    private void RenderSection(ColumnDescriptor column, GuideListSection section)
    {
        column.Item().PaddingTop(12).Text(english ? section.TitleEn : section.TitleDa).Bold().FontSize(13).FontColor(Ink);

        column.Item().PaddingTop(4).Column(inner =>
        {
            foreach (var fact in section.Items)
            {
                inner.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).Row(row =>
                {
                    row.ConstantItem(150).Text(english ? fact.En : fact.Da).FontColor(Colors.Grey.Darken2).FontSize(9.5f);
                    row.RelativeItem().Text(english ? fact.NoteEn ?? "" : fact.NoteDa ?? "").FontSize(9.5f);
                });
            }
        });
    }
}
