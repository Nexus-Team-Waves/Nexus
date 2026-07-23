using System.Text;
using Mems.Application.Receipts;
using Mems.Infrastructure.Pdf;

namespace Mems.Tests.Receipts;

/// <summary>Smoke tests against the real PdfSharp renderer — output must always be a PDF.</summary>
public sealed class ReceiptPdfRendererTests
{
    // A real 1×1 PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly ReceiptPdfRenderer _renderer = new();

    [Fact]
    public void Renders_a_pdf_with_a_png_image()
    {
        var bytes = _renderer.Render(Model(Image("receipt.png", TinyPng)));
        AssertIsPdf(bytes);
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public void Renders_one_extra_page_per_additional_image()
    {
        var bytes = _renderer.Render(Model(Image("one.png", TinyPng), Image("two.png", TinyPng), Image("three.png", TinyPng)));
        AssertIsPdf(bytes);
        Assert.Equal(3, PageCount(bytes)); // details+first image, then one page per extra image
    }

    [Fact]
    public void Renders_a_details_only_pdf_for_a_legacy_line_without_an_image()
    {
        var bytes = _renderer.Render(Model());
        AssertIsPdf(bytes);
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public void Corrupt_image_bytes_degrade_to_a_note_instead_of_throwing()
    {
        var corrupt = Encoding.UTF8.GetBytes("not an image at all");
        var bytes = _renderer.Render(Model(Image("bad.png", corrupt), Image("good.png", TinyPng)));
        AssertIsPdf(bytes); // bad image → note on its region; good image still renders
        Assert.Equal(2, PageCount(bytes));
    }

    private static ReceiptPdfImage Image(string name, byte[] content) => new(name, content, "image/png");

    private static ReceiptPdfModel Model(params ReceiptPdfImage[] images) => new(
        Guid.NewGuid(), "DEMO-M-002", 1, "Self", "Opd",
        new DateOnly(2026, 7, 20), 1_500m, 1_500m, "receipt.png", images);

    private static int PageCount(byte[] pdfBytes)
    {
        using var document = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(pdfBytes), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    private static void AssertIsPdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
