using System.Globalization;
using System.Text;

namespace Csir.Spme.Infrastructure.Communications;

internal sealed class AppraisalPdfDocument(byte[] logo)
{
    public const float PageWidth = 612f;
    public const float PageHeight = 792f;

    private readonly List<AppraisalPdfPage> pages = [];

    public AppraisalPdfPage AddPage()
    {
        var page = new AppraisalPdfPage();
        pages.Add(page);
        return page;
    }

    public byte[] Build(string templateChecksum)
    {
        const int firstPageObjectNumber = 7;
        var pageReferences = string.Join(' ', pages.Select((_, index) =>
            $"{firstPageObjectNumber + index * 2} 0 R"));
        var objects = new List<byte[]>
        {
            Bytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Bytes($"<< /Type /Pages /Kids [{pageReferences}] /Count {pages.Count} >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>"),
            Join(
                Bytes($"<< /Type /XObject /Subtype /Image /Width 602 /Height 602 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {logo.Length} >>\nstream\n"),
                logo,
                Bytes("\nendstream"))
        };

        foreach (var (page, index) in pages.Select((value, index) => (value, index)))
        {
            var pageObjectNumber = firstPageObjectNumber + index * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            var content = Bytes(page.Content);
            objects.Add(Bytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] " +
                $"/Resources << /ProcSet [/PDF /Text /ImageC] /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> " +
                $"/XObject << /Logo 6 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));
            objects.Add(Join(
                Bytes($"<< /Length {content.Length} >>\nstream\n"),
                content,
                Bytes("\nendstream")));
        }

        var infoObjectNumber = objects.Count + 1;
        objects.Add(Bytes(
            "<< /Title (PERFORMANCE APPRAISAL FORMS) " +
            "/Subject (CSIR Staff Performance Planning, Review and Appraisal Form) " +
            "/Author (Council for Scientific and Industrial Research) " +
            $"/Keywords (source-template-sha256:{AppraisalPdfPage.Escape(templateChecksum)}) " +
            "/Creator (CSIR SPME API V2) >>"));

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n");
            output.Write(objects[index]);
            Write(output, "\nendobj\n");
        }

        var crossReferenceOffset = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            Write(output, $"{offset:D10} 00000 n \n");
        Write(output,
            $"trailer << /Size {objects.Count + 1} /Root 1 0 R /Info {infoObjectNumber} 0 R >>\n" +
            $"startxref\n{crossReferenceOffset}\n%%EOF");
        return output.ToArray();
    }

    private static byte[] Bytes(string value) => Encoding.Latin1.GetBytes(value);

    private static byte[] Join(params byte[][] values)
    {
        using var stream = new MemoryStream();
        foreach (var value in values)
            stream.Write(value);
        return stream.ToArray();
    }

    private static void Write(Stream stream, string value) => stream.Write(Bytes(value));
}

internal sealed class AppraisalPdfPage
{
    private readonly StringBuilder content = new();

    public string Content => content.ToString();

    public void Text(
        string? value,
        float x,
        float top,
        float fontSize = 8f,
        bool bold = false,
        bool italic = false,
        PdfTextAlignment alignment = PdfTextAlignment.Left,
        float availableWidth = 0f,
        PdfColor? color = null)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
            return;

        var width = Measure(normalized, fontSize, bold);
        var drawX = alignment switch
        {
            PdfTextAlignment.Center when availableWidth > 0 => x + Math.Max(0, (availableWidth - width) / 2f),
            PdfTextAlignment.Right when availableWidth > 0 => x + Math.Max(0, availableWidth - width),
            _ => x
        };
        var baseline = AppraisalPdfDocument.PageHeight - top - fontSize;
        var font = bold ? "/F2" : italic ? "/F3" : "/F1";
        var drawColor = color ?? PdfColor.Black;
        content.Append(CultureInfo.InvariantCulture,
            $"q {drawColor.Red:0.###} {drawColor.Green:0.###} {drawColor.Blue:0.###} rg " +
            $"BT {font} {fontSize:0.##} Tf 1 0 0 1 {drawX:0.##} {baseline:0.##} Tm ({Escape(normalized)}) Tj ET Q\n");
    }

    public float WrappedText(
        string? value,
        PdfRectangle rectangle,
        float fontSize = 8f,
        bool bold = false,
        bool italic = false,
        PdfTextAlignment alignment = PdfTextAlignment.Left,
        PdfColor? color = null,
        float minimumFontSize = 4f,
        float lineHeightMultiplier = 1.2f)
    {
        var normalized = NormalizeMultiline(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return 0f;

        var selectedSize = fontSize;
        IReadOnlyList<string> lines;
        float lineHeight;
        while (true)
        {
            lines = Wrap(normalized, rectangle.Width, selectedSize, bold);
            lineHeight = selectedSize * lineHeightMultiplier;
            if (lines.Count * lineHeight <= rectangle.Height || selectedSize <= minimumFontSize)
                break;
            selectedSize = Math.Max(minimumFontSize, selectedSize - 0.25f);
        }

        var maximumLines = Math.Max(1, (int)Math.Floor(rectangle.Height / lineHeight));
        if (lines.Count > maximumLines)
        {
            var fitted = lines.Take(maximumLines).ToList();
            fitted[^1] = Ellipsize(fitted[^1], rectangle.Width, selectedSize, bold);
            lines = fitted;
        }

        for (var index = 0; index < lines.Count; index++)
            Text(lines[index], rectangle.X, rectangle.Top + index * lineHeight, selectedSize, bold, italic,
                alignment, rectangle.Width, color);
        return lines.Count * lineHeight;
    }

    public void Line(float x1, float top1, float x2, float top2, float width = 0.5f, PdfColor? color = null)
    {
        var drawColor = color ?? PdfColor.Black;
        var y1 = AppraisalPdfDocument.PageHeight - top1;
        var y2 = AppraisalPdfDocument.PageHeight - top2;
        content.Append(CultureInfo.InvariantCulture,
            $"q {drawColor.Red:0.###} {drawColor.Green:0.###} {drawColor.Blue:0.###} RG " +
            $"{width:0.##} w {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S Q\n");
    }

    public void Rectangle(PdfRectangle rectangle, float width = 0.5f, PdfColor? stroke = null, PdfColor? fill = null)
    {
        var y = AppraisalPdfDocument.PageHeight - rectangle.Top - rectangle.Height;
        var strokeColor = stroke ?? PdfColor.Black;
        content.Append("q ");
        if (fill.HasValue)
            content.Append(CultureInfo.InvariantCulture,
                $"{fill.Value.Red:0.###} {fill.Value.Green:0.###} {fill.Value.Blue:0.###} rg ");
        content.Append(CultureInfo.InvariantCulture,
            $"{strokeColor.Red:0.###} {strokeColor.Green:0.###} {strokeColor.Blue:0.###} RG " +
            $"{width:0.##} w {rectangle.X:0.##} {y:0.##} {rectangle.Width:0.##} {rectangle.Height:0.##} re ");
        content.Append(fill.HasValue ? "B Q\n" : "S Q\n");
    }

    public void Image(float x, float top, float width, float height)
    {
        var y = AppraisalPdfDocument.PageHeight - top - height;
        content.Append(CultureInfo.InvariantCulture,
            $"q {width:0.##} 0 0 {height:0.##} {x:0.##} {y:0.##} cm /Logo Do Q\n");
    }

    public static string Escape(string value) => Normalize(value)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    public static float Measure(string? value, float fontSize, bool bold = false)
    {
        var normalized = Normalize(value);
        var units = normalized.Sum(character => character switch
        {
            'i' or 'l' or 'I' or '.' or ',' or ':' or ';' or '!' or '|' => 0.25f,
            'm' or 'w' or 'M' or 'W' or '@' => 0.85f,
            ' ' => 0.28f,
            _ => 0.52f
        });
        return units * fontSize * (bold ? 1.04f : 1f);
    }

    private static IReadOnlyList<string> Wrap(string value, float width, float fontSize, bool bold)
    {
        var lines = new List<string>();
        foreach (var paragraph in value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (Measure(candidate, fontSize, bold) <= width)
                {
                    current.Clear();
                    current.Append(candidate);
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (Measure(word, fontSize, bold) <= width)
                {
                    current.Append(word);
                    continue;
                }

                var fragment = new StringBuilder();
                foreach (var character in word)
                {
                    if (Measure(fragment + character.ToString(), fontSize, bold) > width && fragment.Length > 0)
                    {
                        lines.Add(fragment.ToString());
                        fragment.Clear();
                    }
                    fragment.Append(character);
                }
                current.Append(fragment);
            }

            if (current.Length > 0)
                lines.Add(current.ToString());
        }
        return lines;
    }

    private static string Ellipsize(string value, float width, float fontSize, bool bold)
    {
        var result = value.TrimEnd();
        while (result.Length > 1 && Measure(result + "...", fontSize, bold) > width)
            result = result[..^1].TrimEnd();
        return result + "...";
    }

    private static string NormalizeMultiline(string? value) => Normalize(value, preserveLineBreaks: true);

    private static string Normalize(string? value, bool preserveLineBreaks = false)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2026', '.')
            .Replace('\u00A0', ' ')
            .Replace('\u2022', '-')
            .Replace('\t', ' ');
        if (!preserveLineBreaks)
            normalized = normalized.Replace('\r', ' ').Replace('\n', ' ');
        return new string(normalized.Select(character => character <= byte.MaxValue ? character : '?').ToArray());
    }
}

internal enum PdfTextAlignment
{
    Left,
    Center,
    Right
}

internal readonly record struct PdfRectangle(float X, float Top, float Width, float Height)
{
    public PdfRectangle Inset(float horizontal, float vertical) =>
        new(X + horizontal, Top + vertical, Math.Max(0, Width - horizontal * 2), Math.Max(0, Height - vertical * 2));
}

internal readonly record struct PdfColor(float Red, float Green, float Blue)
{
    public static readonly PdfColor Black = new(0f, 0f, 0f);
    public static readonly PdfColor Navy = new(0.02f, 0.16f, 0.36f);
    public static readonly PdfColor Pink = new(0.82f, 0f, 0.34f);
    public static readonly PdfColor Muted = new(0.34f, 0.36f, 0.4f);
    public static readonly PdfColor HeaderFill = new(0.96f, 0.97f, 0.98f);
}
