using Mems.Application.Receipts;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Mems.Infrastructure.Pdf;

/// <summary>
/// Renders the per-line receipt PDF with PDFsharp (MIT). One A4 page: a details block followed by
/// the receipt image scaled to the remaining space. Image problems degrade to a printed note —
/// an approver's download must never 500 because one photo is corrupt.
/// NOTE: font resolution ("Arial") relies on the Windows platform resolver; hosting this on Linux
/// would need a custom IFontResolver.
/// </summary>
public sealed class ReceiptPdfRenderer : IReceiptPdfRenderer
{
    private const double Margin = 40; // points

    static ReceiptPdfRenderer()
    {
        // The Core build resolves no fonts by itself — this opts into the host's Windows fonts.
        // (It is the documented Windows-host setting; a Linux host would ignore it and need a
        // custom IFontResolver instead.)
        PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public byte[] Render(ReceiptPdfModel model)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A4;

        var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var labelFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var valueFont = new XFont("Arial", 10);
        var noteFont = new XFont("Arial", 10, XFontStyleEx.Italic);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var y = Margin;
            gfx.DrawString("MEMS — Claim line receipt", titleFont, XBrushes.Black, new XPoint(Margin, y));
            y += 26;

            var rows = new (string Label, string Value)[]
            {
                ("Item", $"#{model.ItemNumber}"),
                ("Claim", model.ClaimId.ToString()),
                ("Employee", model.EmployeeId),
                ("Beneficiary", model.Beneficiary),
                ("Category", model.Category),
                ("Expense date", model.ExpenseDate.ToString("dd MMM yyyy")),
                ("Claimed amount", $"Rs {model.ClaimedAmount:N2}"),
                ("Amount at this stage", $"Rs {model.CurrentAmount:N2}"),
                ("Receipt file(s)", model.Images.Count > 1
                    ? $"{model.ReceiptFileName ?? "—"} ({model.Images.Count} images)"
                    : model.ReceiptFileName ?? "—"),
            };
            foreach (var (label, value) in rows)
            {
                gfx.DrawString(label, labelFont, XBrushes.Black, new XPoint(Margin, y));
                gfx.DrawString(value, valueFont, XBrushes.Black, new XPoint(Margin + 130, y));
                y += 16;
            }

            y += 8;
            gfx.DrawLine(XPens.Gray, Margin, y, page.Width.Point - Margin, y);
            y += 20;

            if (model.Images.Count == 0)
            {
                gfx.DrawString("No receipt image on file (submitted before image upload was introduced).",
                    noteFont, XBrushes.Gray, new XPoint(Margin, y));
            }
            else
            {
                // First image shares page 1 with the details block.
                DrawImageScaled(gfx, page, model.Images[0].Content, y, noteFont);
            }
        }

        // Every additional image gets its own page. One XGraphics cannot span pages —
        // create and dispose one per page.
        for (var i = 1; i < model.Images.Count; i++)
        {
            var imagePage = document.AddPage();
            imagePage.Size = PageSize.A4;
            using var imageGfx = XGraphics.FromPdfPage(imagePage);
            imageGfx.DrawString($"Receipt image {i + 1} of {model.Images.Count}",
                labelFont, XBrushes.Black, new XPoint(Margin, Margin));
            DrawImageScaled(imageGfx, imagePage, model.Images[i].Content, Margin + 20, noteFont);
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static void DrawImageScaled(XGraphics gfx, PdfPage page, byte[] imageBytes, double top, XFont noteFont)
    {
        try
        {
            using var image = XImage.FromStream(new MemoryStream(imageBytes));
            var availableWidth = page.Width.Point - 2 * Margin;
            var availableHeight = page.Height.Point - top - Margin;
            var scale = Math.Min(availableWidth / image.PointWidth, availableHeight / image.PointHeight);
            gfx.DrawImage(image, Margin, top, image.PointWidth * scale, image.PointHeight * scale);
        }
        catch (Exception)
        {
            // Magic-byte sniffing passed at upload but the file still failed to decode — print a
            // note rather than failing the whole download.
            gfx.DrawString("Receipt image could not be displayed.", noteFont, XBrushes.Gray, new XPoint(Margin, top));
        }
    }
}
