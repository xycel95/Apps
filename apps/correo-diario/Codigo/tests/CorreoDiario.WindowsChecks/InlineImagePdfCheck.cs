using System.Text.Json;
using CorreoDiario.Core;
using CorreoDiario.Windows;
using UglyToad.PdfPig;

internal static class InlineImagePdfCheck
{
    public static async Task RunAsync(PdfRenderer renderer)
    {
        var source = new Source();
        var directory = Path.Combine(Path.GetTempPath(), "cddi-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            try { await new ExportEngine(source, renderer).RunAsync(source.Items, new(directory, true), null, CancellationToken.None); }
            catch (ExportException ex)
            {
                using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(ex.ReceiptPath));
                var r = receipt.RootElement;
                if (r.GetProperty("Messages").GetArrayLength() != 2 || r.GetProperty("PendingMessages").GetArrayLength() != 2 ||
                    !r.GetProperty("SavedCopiesVerified").GetBoolean() || source.Reads != 6 || source.Downloads != 6 || source.Moves != 0 ||
                    r.GetProperty("FailedMessageNumber").GetInt32() != 2 || Directory.GetDirectories(directory).Length != 0)
                    throw new Exception("Debe copiar/verificar los dos correos buenos y dejar los dos pendientes sin movimientos.");
                var reasons = r.GetProperty("PendingMessages").EnumerateArray().SelectMany(m => m.GetProperty("ImageFailure").GetProperty("Issues").EnumerateArray().Select(i => i.GetProperty("Reason").GetString())).ToArray();
                if (!reasons.Contains("content-id-not-found") || !reasons.Contains("image-decode-failed"))
                    throw new Exception("Deben distinguirse el CID ausente y los bytes de imagen no decodificables.");
                var files = Directory.GetFiles(directory, "*_CORREO.pdf");
                if (files.Length != 2) throw new Exception("Debe generar dos PDF completos.");
                foreach (var file in files)
                {
                    using var pdf = PdfDocument.Open(await File.ReadAllBytesAsync(file));
                    if (!pdf.GetPages().Any(p => p.NumberOfImages > 0)) throw new Exception("Falta la imagen CID en un PDF guardado.");
                }
                var report = ErrorReport.Build(ex, "prueba", "Windows");
                if (!report.Contains("ImageFailure") || !report.Contains("PendingMessageNumbers") || report.Contains("logo@ejemplo.invalid"))
                    throw new Exception("El informe debe identificar los pendientes sin copiar sus referencias privadas.");
                Console.WriteLine("PASS Imágenes CID Windows: identificador completo de descarga, corchetes codificados y signo más resueltos en PDF con imágenes reales.");
                Console.WriteLine("PASS Lote incompleto Windows: dos correos copiados y verificados, dos pendientes distintos, adjuntos releídos y cero movimientos con borrado marcado.");
                return;
            }
            throw new Exception("No debe completar ni mover un lote con imágenes realmente ausentes o dañadas.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Source : IAttachmentDownloadSource
    {
        private readonly byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j9mUAAAAASUVORK5CYII=");
        public int Reads, Downloads, Moves;
        public IReadOnlyList<MailInfo> Items { get; } = [Mail("1", "cid:%3Clogo%40ejemplo.invalid%3E", ""), Mail("2", "cid:ausente", ""),
            Mail("3", "cid:dos%2Blogo", "<dos+logo>"), Mail("4", "cid:danada", "danada")];
        public Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MailFolder>>([new("inbox", "Entrada")]);
        public Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, MailDateRange dates, TimeZoneInfo zone, CancellationToken ct) => Task.FromResult(Items);
        public Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Reads++; return Task.FromResult(Items.Single(m => m.Id == item.Id)); }
        public async Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo attachment, CancellationToken ct) => (await DownloadAttachmentAsync(item, attachment, ct)).Bytes;
        public Task<AttachmentDownload> DownloadAttachmentAsync(MailInfo mail, AttachmentInfo attachment, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Downloads++;
            var cid = mail.Id switch { "1" => " <logo@ejemplo.invalid> ", "3" => "dos+logo", "4" => "danada", _ => "" };
            return Task.FromResult(new AttachmentDownload(mail.Id == "4" ? "not-an-image"u8.ToArray() : png.ToArray(), cid, "image/png"));
        }
        public Task MoveToDeletedAsync(MailInfo mail, CancellationToken ct) { Moves++; throw new Exception("No debe iniciarse el movimiento de un lote incompleto."); }
        private static MailInfo Mail(string id, string url, string cid) => new(id, "v1", "inbox", "Correo de prueba " + id,
            "origen@ejemplo.invalid", "destino@ejemplo.invalid", "", new DateTimeOffset(2026, 1, 21, 10, 0, 0, TimeSpan.Zero).AddMinutes(int.Parse(id)), false,
            "<p>Contenido completo " + id + "</p><img src='" + url + "'>", [new("a" + id, "logo.png", null, "application/octet-stream", true, cid)]);
    }
}
