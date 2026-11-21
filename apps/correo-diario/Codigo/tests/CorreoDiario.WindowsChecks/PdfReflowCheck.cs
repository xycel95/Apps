using System.Text;
using System.Text.Json;
using CorreoDiario.Core;
using CorreoDiario.Windows;
using UglyToad.PdfPig;

internal static class PdfReflowCheck
{
    public static async Task RunAsync(PdfRenderer renderer)
    {
        // The 0.4.3 comparison still fails when a wrapped word occurs alongside
        // generated list markers: innerText omits the numbers printed in PDF.
        var word = string.Concat(Enumerable.Repeat("documentacion", 22));
        var html = new StringBuilder($"<ol type='I' start='4'><li>{word}</li><li value='9'>Novena cláusula 123456</li></ol><ol reversed type='a' start='3'><li>Tercera solicitud</li><li>Segunda solicitud</li></ol><ol style='list-style-type:none'><li>Sin marca</li></ol>");
        var text = new StringBuilder($"IV. {word} IX. Novena cláusula 123456 c. Tercera solicitud b. Segunda solicitud Sin marca ");
        for (var i = 0; i < 60; i++)
        {
            html.Append($"<p>Registro {i}: José Muñoz entrega la documentación completa para su revisión.</p>");
            text.Append($"Registro {i}: José Muñoz entrega la documentación completa para su revisión. ");
        }
        const string link = "https://ejemplo.invalid/consulta?id=123456";
        html.Append("<p>FINALDELACOPIA</p><p><a href='" + link + "'>Enlace de consulta</a></p><img src='cid:logo'>");
        text.Append("FINALDELACOPIA Enlace de consulta");
        var item = new MailInfo("reflow", "v1", "inbox", "Composición alternativa verificada", "origen@ejemplo.invalid", "destino@ejemplo.invalid", "",
            new DateTimeOffset(2026, 1, 21, 10, 0, 0, TimeSpan.Zero), false, html.ToString(), [new("imagen", "logo.png", null, "image/png", true, "logo")]);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j9mUAAAAASUVORK5CYII=");
        var directory = Path.Combine(Path.GetTempPath(), "cddi-reflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = new Source(item, png);
            var result = await new ExportEngine(source, renderer).RunAsync([item], new(directory), null, CancellationToken.None);
            using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(result.ReceiptPath));
            var verification = receipt.RootElement.GetProperty("Messages")[0].GetProperty("Pdf").Deserialize<PdfRenderVerification>()!;
            if (verification.Mode != "simplified" || verification.FormattedFailure is not { CompleteWords: false, ExactSequence: false } || !verification.Text.ExactSequence)
                throw new Exception("Debe reproducir el rechazo de 0.4.3 y volver a componer/cotejar toda la secuencia.");
            if (result.SimplifiedPdfs != 1 || result.Moved != 0 || source.Reads != 2 || source.Downloads != 2 || source.Moves != 0 || Directory.GetDirectories(directory).Length != 0)
                throw new Exception("Debe guardar y verificar el PDF/adjunto en el destino directo, sin movimientos.");
            var bytes = await File.ReadAllBytesAsync(Directory.GetFiles(directory, "*_CORREO.pdf").Single());
            var expected = PdfRenderer.SimplifiedNotice + "\n" + PdfLayoutCheck.Header(item) + text + "\nEnlaces del correo\n" + link + "\nImágenes del correo\nImagen 1";
            PdfVerifier.ValidateText(bytes, expected, exactSequence: true);
            using (var pdf = PdfDocument.Open(bytes))
            {
                if (pdf.NumberOfPages < 2 || !pdf.GetPages().Any(p => p.NumberOfImages > 0))
                    throw new Exception("Faltan páginas o la imagen del correo en la composición alternativa.");
            }
            try { PdfVerifier.ValidateText(bytes, expected.Replace("123456", "123457"), exactSequence: true); }
            catch (InvalidDataException ex) when (ex.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck)
            {
                Console.WriteLine("PASS PDF alternativo Windows: rechazo de 0.4.3 reproducido; texto completo, numeración romana/alfabética, 60 registros, varias páginas e imagen cotejados; cifra cambiada rechazada.");
                Console.WriteLine("PASS Exportación alternativa Windows: registro con modo y diagnóstico, adjunto leído dos veces, destino directo y cero movimientos.");
                return;
            }
            throw new Exception("El cotejo alternativo debía rechazar la cifra cambiada.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Source(MailInfo item, byte[] png) : IMailSource
    {
        public int Reads, Downloads, Moves;
        public Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MailFolder>>([new("inbox", "Entrada")]);
        public Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, MailDateRange dates, TimeZoneInfo zone, CancellationToken ct) => Task.FromResult<IReadOnlyList<MailInfo>>([item]);
        public Task<MailInfo> ReadAsync(MailInfo mail, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Reads++; return Task.FromResult(item); }
        public Task<byte[]> AttachmentAsync(MailInfo mail, AttachmentInfo attachment, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Downloads++; return Task.FromResult(png.ToArray()); }
        public Task MoveToDeletedAsync(MailInfo mail, CancellationToken ct) { Moves++; throw new Exception("No se autorizan movimientos en esta prueba."); }
    }
}
