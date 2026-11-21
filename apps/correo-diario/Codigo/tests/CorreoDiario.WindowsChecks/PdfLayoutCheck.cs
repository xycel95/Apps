using System.Text;
using System.Text.RegularExpressions;
using CorreoDiario.Core;
using CorreoDiario.Windows;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

internal static class PdfLayoutCheck
{
    public static async Task RunAsync(PdfRenderer renderer)
    {
        var longWord = string.Concat(Enumerable.Repeat("documentacion", 22));
        var wrapped = Mail("Direcciones y palabras largas", $"<p>Inicio {longWord} final.</p>") with
        {
            To = longWord + "@ejemplo.invalid"
        };
        var bytes = await renderer.RenderAsync(wrapped, new Dictionary<string, byte[]>(), CancellationToken.None);
        var expected = Header(wrapped) + "Inicio " + longWord + " final.";
        PdfVerifier.Validate(bytes, expected);
        var actual = Text(bytes);
        var counts = Words(actual).GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        if (Words(expected).GroupBy(s => s).All(g => counts.GetValueOrDefault(g.Key) >= g.Count()))
            throw new Exception("La regresión debe reproducir el falso rechazo del cotejo de palabras anterior.");
        Reject(bytes, expected + " CONTENIDOOMITIDO");
        Console.WriteLine("PASS PDF Windows: falso rechazo anterior reproducido; palabras y direcciones partidas conservadas y cotejadas por completo.");

        var body = new StringBuilder("<div style='width:1600px;min-width:1600px;height:30px;max-height:30px;overflow:hidden'>");
        body.Append("<table width='1500' style='min-width:1500px;white-space:nowrap'><thead><tr><th>Referencia</th><th>Descripción</th><th>Estado</th></tr></thead><tbody>");
        var plain = new StringBuilder("Referencia Descripción Estado ");
        for (var i = 0; i < 90; i++)
        {
            var reference = "REF" + i.ToString("000");
            body.Append($"<tr><td width='700' nowrap>{reference}</td><td style='height:12px;overflow:hidden'>Solicitud número {i}: revisión de documentación completa.</td><td>VERIFICADO</td></tr>");
            plain.Append($"{reference} Solicitud número {i}: revisión de documentación completa. VERIFICADO ");
        }
        body.Append("</tbody></table><p>ULTIMOREGISTROCOMPLETO</p></div>");
        plain.Append("ULTIMOREGISTROCOMPLETO");
        var table = Mail("Tabla extensa de formato fijo", body.ToString());
        bytes = await renderer.RenderAsync(table, new Dictionary<string, byte[]>(), CancellationToken.None);
        PdfVerifier.Validate(bytes, Header(table) + plain);
        using (var pdf = PdfDocument.Open(bytes))
            if (pdf.NumberOfPages < 2) throw new Exception("La tabla debe ocupar varias páginas completas.");
        Reject(bytes, Header(table) + plain + " FILAAUSENTE");
        Console.WriteLine("PASS PDF Windows: tabla ancha, nowrap y contenedor con altura/overflow limitados; 90 filas y texto final presentes en varias páginas.");

        var unicode = Mail("Información y firmas", "<p>José Muñoz: información recibida, cafe&#x301;, ﬁrma y nº １２３４５.</p><ol><li>Primera solicitud</li><li>Segunda revisión</li></ol><pre>Línea uno\nLínea dos</pre>");
        bytes = await renderer.RenderAsync(unicode, new Dictionary<string, byte[]>(), CancellationToken.None);
        PdfVerifier.Validate(bytes, Header(unicode) + "José Muñoz: información recibida, cafe\u0301, ﬁrma y nº １２３４５. Primera solicitud Segunda revisión Línea uno Línea dos");
        Console.WriteLine("PASS PDF Windows: tildes, caracteres combinados, ligaduras, cifras Unicode, listas y texto preformateado.");

        const string image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j9mUAAAAASUVORK5CYII=";
        var illustrated = Mail("Imágenes y saneado", "<script>document.body.innerHTML='BORRADO'</script><style>body{display:none}</style><p onclick='alert(1)'>Imagen adjunta</p><img src='cid:logo'><img src='https://example.invalid/remota.png' alt='Imagen remota'><iframe src='https://example.invalid/marco'></iframe>") with
        {
            Attachments = [new("imagen1", "logo.png", null, "image/png", true, "logo")]
        };
        bytes = await renderer.RenderAsync(illustrated, new Dictionary<string, byte[]> { ["imagen1"] = Convert.FromBase64String(image) }, CancellationToken.None);
        PdfVerifier.Validate(bytes, Header(illustrated) + "Imagen adjunta Imagen externa Imagen remota Las imágenes externas se han dejado indicadas en el texto. Las imágenes adjuntas están incluidas.");
        using (var pdf = PdfDocument.Open(bytes))
            if (!pdf.GetPages().Any(p => p.NumberOfImages > 0)) throw new Exception("Falta la imagen CID en el PDF.");
        try { await renderer.RenderAsync(illustrated, new Dictionary<string, byte[]>(), CancellationToken.None); }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS PDF Windows: imagen CID incluida, recursos activos retirados y adjunto ausente rechazado.");
            return;
        }
        throw new Exception("Debía rechazar una imagen adjunta ausente.");
    }

    private static MailInfo Mail(string subject, string html) => new("pdf-layout", "v1", "inbox", subject,
        "remitente@ejemplo.invalid", "destino@ejemplo.invalid", "", new DateTimeOffset(2026, 1, 21, 10, 0, 0, TimeSpan.Zero), false, html, []);

    internal static string Header(MailInfo mail)
    {
        var local = TimeZoneInfo.ConvertTime(mail.SentAt ?? mail.Timestamp, TimeZoneInfo.Local);
        var text = $"{mail.Subject} De: {mail.From} Enviado: {local:dd/MM/yyyy HH:mm:ss} Para: {mail.To} ";
        if (mail.Cc.Length > 0) text += "CC: " + mail.Cc + " ";
        if (mail.Bcc.Length > 0) text += "CCO: " + mail.Bcc + " ";
        if (mail.Attachments.Count > 0) text += "Adjuntos: " + string.Join("; ", mail.Attachments.Select(a => a.Name)) + " ";
        return text;
    }

    private static string Text(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join("\n", pdf.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
    }
    private static string[] Words(string value) => Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
        .Select(m => m.Value).ToArray();
    private static void Reject(byte[] bytes, string expected)
    {
        try { PdfVerifier.Validate(bytes, expected); }
        catch (InvalidDataException) { return; }
        throw new Exception("Debía rechazar contenido omitido en el PDF real.");
    }
}
