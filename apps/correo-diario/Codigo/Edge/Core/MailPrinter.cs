using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CorreoDiario;

public sealed class MailPrinter
{
    sealed class Fonts : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(
                "DejaVuSans"
                    + (
                        (isBold, isItalic) switch
                        {
                            (true, true) => "-BoldOblique",
                            (true, false) => "-Bold",
                            (false, true) => "-Oblique",
                            _ => "",
                        }
                    )
            );

        public byte[] GetFont(string faceName)
        {
            using var data = typeof(MailPrinter).Assembly.GetManifestResourceStream(
                "CorreoDiario.Fonts." + faceName + ".ttf"
            ) ?? throw new InvalidDataException("No está disponible la fuente del PDF.");
            using var copy = new MemoryStream();
            data.CopyTo(copy);
            return copy.ToArray();
        }
    }

    static MailPrinter()
    {
        GlobalFontSettings.FontResolver = new Fonts();
    }

    public Task<List<string>> PrintAsync(
        string html,
        string output,
        string key,
        CancellationToken ct
    ) => Task.Run(() => Render(html, output, key, ct), ct);

    static Dictionary<string, int> Tokens(string input) =>
        Regex
            .Matches(input.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(m => m.Value)
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

    static List<string> Render(string html, string output, string key, CancellationToken ct)
    {
        var source = new HtmlDocument();
        source.LoadHtml(html);
        var notes = new List<string>();
        var doc = new Document();
        doc.Info.Title = HtmlEntity.DeEntitize(
            source.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? "Correo"
        );
        doc.Info.Author = "CorreoDiario para Edge";
        var style = doc.Styles["Normal"]!;
        style.Font.Name = "DejaVu Sans";
        style.Font.Size = 10;
        style.Font.Color = Colors.Black;
        style.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromMillimeter(16);
        section.PageSetup.RightMargin = Unit.FromMillimeter(16);
        section.PageSetup.TopMargin = Unit.FromMillimeter(15);
        section.PageSetup.BottomMargin = Unit.FromMillimeter(17);
        var title = section.AddParagraph(doc.Info.Title);
        title.Format.Font.Size = 17;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(22, 52, 76);
        title.Format.SpaceAfter = 9;
        foreach (
            var row in source.DocumentNode.SelectNodes("//dl[@class='meta']/div")
                ?? Enumerable.Empty<HtmlNode>()
        )
        {
            var p = section.AddParagraph();
            p.Format.SpaceAfter = 2;
            p.AddFormattedText(
                HtmlEntity.DeEntitize(row.SelectSingleNode("dt")?.InnerText ?? "") + "  ",
                TextFormat.Bold
            );
            p.AddText(HtmlEntity.DeEntitize(row.SelectSingleNode("dd")?.InnerText ?? ""));
        }
        var gap = section.AddParagraph();
        gap.Format.Font.Size = 3;
        gap.Format.SpaceAfter = 5;
        var body = source.DocumentNode.SelectSingleNode("//div[@class='body']");
        var flow = new Flow(section.Elements, notes, ct);
        if (body != null)
            foreach (var node in body.ChildNodes)
                flow.Add(node);
        var note = source.DocumentNode.SelectSingleNode("//body/p[@class='note']");
        if (note != null || notes.Count > 0)
        {
            var details = section.AddParagraph(
                string.Join(
                    " · ",
                    new[] { HtmlEntity.DeEntitize(note?.InnerText ?? "") }
                        .Concat(notes)
                        .Where(s => s.Length > 0)
                )
            );
            details.Format.Font.Size = 8;
            details.Format.SpaceBefore = 7;
            details.Format.Font.Color = Color.FromRgb(112, 74, 20);
        }
        var footer = section.Footers.Primary.AddParagraph("Original " + key + "     ·     ");
        footer.AddPageField();
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.Format.Alignment = ParagraphAlignment.Right;
        var temp = output + ".parcial";
        try
        {
            ct.ThrowIfCancellationRequested();
            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(temp);
            renderer.PdfDocument.Dispose();
            VerifyPdf(temp, key);
            using (var pdf = PdfDocument.Open(temp))
            {
                var expected = Tokens(
                    body == null
                        ? ""
                        : HtmlEntity.DeEntitize(
                            string.Join(
                                " ",
                                body.DescendantsAndSelf()
                                    .Where(n => n.NodeType == HtmlNodeType.Text)
                                    .Select(n => n.InnerText)
                            )
                        )
                );
                var actual = Tokens(
                    string.Join(
                        " ",
                        pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page))
                    )
                );
                var missing = expected.Sum(t =>
                    Math.Max(0, t.Value - actual.GetValueOrDefault(t.Key))
                );
                if (missing > 0)
                    notes.Add(
                        $"Texto pendiente de cotejar en el PDF: {missing} palabras. El original se conserva completo."
                    );
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, output);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
        return notes;
    }

    public static int VerifyPdf(string path, string? key = null)
    {
        using var pdf = PdfDocument.Open(path);
        if (pdf.NumberOfPages < 1)
            throw new InvalidDataException("PDF sin páginas.");
        bool found = key == null;
        foreach (var page in pdf.GetPages())
        {
            if (page.Width <= 0 || page.Height <= 0)
                throw new InvalidDataException("Página PDF inválida.");
            if (
                key != null
                && string.Concat(page.Letters.Select(x => x.Value))
                    .Replace(" ", "")
                    .Contains(key, StringComparison.OrdinalIgnoreCase)
            )
                found = true;
        }
        if (!found)
            throw new InvalidDataException("El PDF no contiene el identificador del original.");
        return pdf.NumberOfPages;
    }

    sealed class Flow(DocumentElements elements, List<string> notes, CancellationToken ct)
    {
        Paragraph? current;
        bool trailingSpace = true;

        Paragraph P() => current ??= elements.AddParagraph();

        void End()
        {
            current = null;
            trailingSpace = true;
        }

        static readonly HashSet<string> Blocks = new(
            "div p pre blockquote li ul ol h1 h2 h3 h4 h5 h6 dl dd dt table tr".Split(' ')
        );

        public void Add(
            HtmlNode n,
            bool bold = false,
            bool italic = false,
            bool underline = false,
            bool pre = false,
            int indent = 0
        )
        {
            ct.ThrowIfCancellationRequested();
            if (n.NodeType == HtmlNodeType.Text)
            {
                var raw = HtmlEntity.DeEntitize(n.InnerText).Replace("\u00a0", " ");
                var text = pre ? raw : Regex.Replace(raw, @"\s+", " ");
                if (!pre && trailingSpace)
                    text = text.TrimStart(' ');
                if (text.Length == 0)
                    return;
                var p = P();
                p.Format.LeftIndent = Unit.FromPoint(Math.Min(32, indent * 8));
                if (pre)
                {
                    var lines = text.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0)
                            p.AddLineBreak();
                        if (lines[i].Length > 0)
                            p.AddText(lines[i]);
                    }
                }
                else
                {
                    var run = p.AddFormattedText(text);
                    run.Bold = bold;
                    run.Italic = italic;
                    if (underline)
                        run.Underline = Underline.Single;
                }
                trailingSpace = char.IsWhiteSpace(text[^1]);
                return;
            }
            if (n.NodeType != HtmlNodeType.Element)
                return;
            var name = n.Name.ToLowerInvariant();
            if (name == "img")
            {
                Picture(n);
                return;
            }
            if (name == "br")
            {
                P().AddLineBreak();
                trailingSpace = true;
                return;
            }
            if (name == "hr")
            {
                End();
                var l = elements.AddParagraph();
                l.Format.Font.Size = 2;
                l.Format.Borders.Bottom.Width = 0.5;
                return;
            }
            if (name == "table" && TryTable(n))
                return;
            var block = Blocks.Contains(name);
            if (block)
                End();
            if (name is "blockquote" or "li")
                indent++;
            if (name == "li")
            {
                if (n.ParentNode?.Name == "ol")
                {
                    var index =
                        n.ParentNode.ChildNodes.Where(a => a.Name == "li")
                            .TakeWhile(a => a != n)
                            .Count() + 1;
                    P().AddText(index + ". ");
                }
                else
                    P().AddText("• ");
            }
            if (name.Length == 2 && name[0] == 'h' && char.IsDigit(name[1]))
            {
                P().Format.Font.Size = 12;
                bold = true;
            }
            if (name is "td" or "th")
            {
                End();
                if (name == "th")
                    bold = true;
            }
            foreach (var child in n.ChildNodes)
                Add(
                    child,
                    bold || name is "strong" or "b",
                    italic || name is "em" or "i",
                    underline || name == "u",
                    pre || name == "pre",
                    indent
                );
            if (block || name is "td" or "th")
                End();
        }

        bool TryTable(HtmlNode tableNode)
        {
            var rows = tableNode
                .Descendants("tr")
                .Where(r => r.Ancestors("table").FirstOrDefault() == tableNode)
                .ToList();
            var cells = rows.Select(r => r.ChildNodes.Where(n => n.Name is "td" or "th").ToList())
                .ToList();
            int columns = cells.Count == 0 ? 0 : cells.Max(c => c.Count);
            // Layout tables with long prose must flow across pages.
            if (
                columns < 2
                || columns > 6
                || cells.Any(r => r.Count != columns)
                || tableNode.Descendants("table").Any()
                || cells
                    .SelectMany(x => x)
                    .Any(c =>
                        c.InnerText.Length > 250
                        || c.Descendants("img").Any()
                        || c.GetAttributeValue("colspan", "1") != "1"
                        || c.GetAttributeValue("rowspan", "1") != "1"
                    )
            )
                return false;
            End();
            var table = elements.AddTable();
            table.Borders.Width = 0.3;
            table.Borders.Color = Colors.LightGray;
            table.Rows.LeftIndent = 0;
            for (int i = 0; i < columns; i++)
                table.AddColumn(Unit.FromMillimeter(174d / columns));
            foreach (var values in cells)
            {
                var row = table.AddRow();
                for (var i = 0; i < columns; i++)
                {
                    var cell = row.Cells[i];
                    var childFlow = new Flow(cell.Elements, notes, ct);
                    foreach (var n in values[i].ChildNodes)
                        childFlow.Add(n, values[i].Name == "th");
                }
            }
            End();
            return true;
        }

        void Picture(HtmlNode n)
        {
            var src = HtmlEntity.DeEntitize(n.GetAttributeValue("src", ""));
            var comma = src.IndexOf(',');
            if (comma < 0 || !src.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return;
            try
            {
                var bytes = Convert.FromBase64String(src[(comma + 1)..]);
                using var raw = new SKMemoryStream(bytes);
                using var codec = SKCodec.Create(raw);
                if (codec == null || codec.Info.Width < 1 || codec.Info.Height < 1)
                    throw new InvalidDataException();
                var dimensions = codec.Info;
                if ((long)dimensions.Width * dimensions.Height > 50000000)
                    throw new InvalidDataException("Imagen demasiado grande para la composición.");
                using var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidDataException();
                using var raster = SKImage.FromBitmap(bitmap);
                using var encoded = raster.Encode(SKEncodedImageFormat.Png, 95);
                var pic = P().AddImage("base64:" + Convert.ToBase64String(encoded.ToArray()));
                var scale = Math.Min(
                    0.75,
                    Math.Min(490d / dimensions.Width, 620d / dimensions.Height)
                );
                pic.Width = Unit.FromPoint(dimensions.Width * scale);
                pic.Height = Unit.FromPoint(dimensions.Height * scale);
            }
            catch (Exception e)
                when (e is not OperationCanceledException && e is not OutOfMemoryException)
            {
                var alt = HtmlEntity.DeEntitize(n.GetAttributeValue("alt", ""));
                var warning = "Imagen incrustada ilegible" + (alt.Length > 0 ? " · " + alt : "");
                notes.Add(warning + " (" + e.GetType().Name + ": " + e.Message + ")");
                P().AddText("[" + warning + "]");
            }
        }
    }
}
