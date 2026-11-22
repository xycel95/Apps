using System.Text.Json;
using CorreoDiario.Core;

internal static class InlineImageChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        foreach (var sample in new[]
        {
            ("normal", "cid:logo@example.invalid", "<logo@example.invalid>"),
            ("corchetes codificados y espacios", "  CID:%3Clogo%40example.invalid%3E  ", " <logo@example.invalid> "),
            ("signo más", "cid:logo%2Buno", "logo+uno"),
            ("porcentaje decodificado solo una vez", "cid:logo%2540uno", "logo%40uno"),
            ("variante de mayúsculas única", "cid:LOGO", "logo")
        })
        {
            yield return ("Imagen CID: " + sample.Item1, () =>
            {
                var attachment = new AttachmentInfo("a", "logo.png", 1, "image/png", true, sample.Item3);
                var result = InlineImages.Resolve([sample.Item2], [attachment], new Dictionary<string, byte[]> { ["a"] = [7] });
                Require(result.Count == 1 && result[0].AttachmentId == "a" && result[0].Bytes.SequenceEqual(new byte[] { 7 }));
                return Task.CompletedTask;
            });
        }
        yield return ("Imagen CID: identificador ambiguo con bytes distintos se rechaza", () =>
        {
            Reject([new("a", "uno.png", 1, "image/png", true, "logo"), new("b", "dos.png", 1, "image/png", true, "logo")],
                new Dictionary<string, byte[]> { ["a"] = [1], ["b"] = [2] }, "ambiguous-content-id");
            return Task.CompletedTask;
        });
        yield return ("Imagen CID: duplicados idénticos no cambian el contenido", () =>
        {
            var found = InlineImages.Resolve(["cid:logo"], [new("a", "uno.png", 1, "image/png", true, "logo"), new("b", "dos.png", 1, "image/png", true, "logo")],
                new Dictionary<string, byte[]> { ["a"] = [1], ["b"] = [1] });
            Require(found.Count == 1 && found[0].Bytes.SequenceEqual(new byte[] { 1 })); return Task.CompletedTask;
        });
        yield return ("Imagen CID: no adivina el adjunto por su nombre", () =>
        {
            Reject([new("a", "logo", 1, "image/png", true, "")], new Dictionary<string, byte[]> { ["a"] = [1] }, "content-id-not-found");
            return Task.CompletedTask;
        });
        yield return ("Imagen CID: exige los bytes del adjunto referenciado", () =>
        {
            Reject([new("a", "logo.png", 1, "image/png", true, "logo")], new Dictionary<string, byte[]>(), "attachment-bytes-missing");
            return Task.CompletedTask;
        });
        yield return ("Imagen: completa CID/MIME con la descarga sin modificar el snapshot", () =>
        {
            var original = new AttachmentInfo("a", "logo.png", 1, "application/octet-stream", true, "");
            var rendering = InlineImages.ForRendering(original, new([1], "<logo>", "image/png"));
            Require(original.ContentId == "" && rendering.ContentId == "<logo>" && rendering.MimeType == "image/png");
            return Task.CompletedTask;
        });
        yield return ("Imagen: un CID declarado contradictorio se rechaza", () =>
        {
            try { InlineImages.ForRendering(new("a", "logo.png", 1, "image/png", true, "original"), new([1], "otro", "image/png")); }
            catch (InvalidDataException) { return Task.CompletedTask; }
            throw new Exception("No debe sustituir una identidad declarada distinta.");
        });
        yield return ("Outlook: conserva ContentId y ContentType de GetAttachment", async () =>
        {
            var mail = new MailInfo("m", "r", "inbox", "Prueba", "a@example.invalid", "b@example.invalid", "", DateTimeOffset.UtcNow, false, "", []);
            var result = await new OwaSource(new MetadataTransport()).DownloadAttachmentAsync(mail, new("a", "logo.png", 4, "application/octet-stream", false, ""), default);
            Require(result.ContentId == "<logo>" && result.ContentType == "image/png" && result.Bytes.SequenceEqual(new byte[] { 0, 1, 2, 255 }));
        });
        yield return ("Exportación: usa los metadatos completos y verifica sin mutar el correo", async () =>
        {
            using var env = new TestRun(); var source = new DetailedSource(env.Source); SetReferences(env.Source);
            var result = await new ExportEngine(source, new CidRenderer()).RunAsync(env.Source.Items, new(env.Directory), null, default);
            Require(result.Messages == 2 && result.Moved == 0 && env.Source.Reads == 4 && env.Source.Downloads == 4 && env.Source.Items.All(m => m.Attachments[0].ContentId == ""));
        });
        yield return ("Integridad: metadatos de imagen cambiados con los mismos bytes impiden mover", async () =>
        {
            using var env = new TestRun(); SetReferences(env.Source);
            try { await new ExportEngine(new DetailedSource(env.Source) { ChangeMetadata = true }, new CidRenderer()).RunAsync(env.Source.Items, new(env.Directory, true), null, default); }
            catch (ExportException) { Require(env.Source.Moved.Count == 0); return; }
            throw new Exception("No debe mover si cambia la identidad del contenido.");
        });
        foreach (var deletion in new[] { false, true })
        {
            yield return ($"Lote con imagen pendiente: copia los demás y no mueve (casilla {deletion})", async () =>
            {
                using var env = new TestRun();
                try { await new ExportEngine(env.Source, new CidRenderer { FailFirst = true }).RunAsync(env.Source.Items, new(env.Directory, deletion), null, default); }
                catch (ExportException ex)
                {
                    using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(ex.ReceiptPath)); var r = receipt.RootElement;
                    Require(env.Source.Moved.Count == 0 && r.GetProperty("Messages").GetArrayLength() == 1 && r.GetProperty("PendingMessages").GetArrayLength() == 1);
                    Require(r.GetProperty("FailedMessageNumber").GetInt32() == 1 && r.GetProperty("SavedCopiesVerified").GetBoolean() && env.Source.Reads == 3 && env.Source.Downloads == 3);
                    Require(ex.Data[ErrorReport.PendingSelectionDataKey] is string[] ids && ids.SequenceEqual(new[] { "m1" }) &&
                        PendingSelection.FromReceipt(r.GetRawText()).SequenceEqual(ids) && ErrorReport.BuildImageIdentifiers(ex, "prueba") is not null);
                    Require(Directory.GetFiles(env.Directory, "*_CORREO.pdf").Length == 1 && ErrorReport.Build(ex, "prueba", "prueba").Contains("ImageFailure")); return;
                }
                throw new Exception("El lote debe terminar con resultado incompleto explícito.");
            });
        }
        yield return ("Lote pendiente: sigue rechazando un cambio de origen durante la verificación", async () =>
        {
            using var env = new TestRun(); env.Source.ChangeAfterExport = true;
            try { await new ExportEngine(env.Source, new CidRenderer { FailFirst = true }).RunAsync(env.Source.Items, new(env.Directory, true), null, default); }
            catch (ExportException ex)
            {
                using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(ex.ReceiptPath));
                Require(env.Source.Moved.Count == 0 && !receipt.RootElement.GetProperty("SavedCopiesVerified").GetBoolean() && ex.Data[ErrorReport.PendingSelectionDataKey] is null); return;
            }
            throw new Exception("Debe comprobar las copias guardadas aunque haya un correo pendiente.");
        });
        yield return ("Copiar imagen fallida: solo incluye recuentos y motivos, sin CID ni cuerpo", () =>
        {
            try { InlineImages.Resolve(["cid:privado@example.invalid"], [], new Dictionary<string, byte[]>()); }
            catch (InvalidDataException ex)
            {
                var report = ErrorReport.Build(ex, "prueba", "Windows");
                Require(report.Contains("ImageFailure") && report.Contains("content-id-not-found") && !report.Contains("privado@example.invalid")); return Task.CompletedTask;
            }
            throw new Exception("Debe diagnosticar la imagen ausente.");
        });
        yield return ("Lote con todas las imágenes pendientes: no inventa PDF ni inicia movimientos", async () =>
        {
            using var env = new TestRun();
            try { await new ExportEngine(env.Source, new CidRenderer { FailAll = true }).RunAsync(env.Source.Items, new(env.Directory, true), null, default); }
            catch (ExportException ex)
            {
                using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(ex.ReceiptPath));
                Require(receipt.RootElement.GetProperty("PendingMessages").GetArrayLength() == 2 && env.Source.Moved.Count == 0 && Directory.GetFiles(env.Directory, "*.pdf").Length == 0); return;
            }
            throw new Exception("No puede completar un lote sin imágenes necesarias.");
        });
    }
    private static void SetReferences(FakeSource source)
    {
        for (var i = 0; i < source.Items.Count; i++) source.Items[i] = source.Items[i] with { Html = "cid:cid-" + source.Items[i].Attachments[0].Id };
    }
    private static void Reject(IReadOnlyList<AttachmentInfo> attachments, IReadOnlyDictionary<string, byte[]> bytes, string reason)
    {
        try { InlineImages.Resolve(["cid:logo"], attachments, bytes); }
        catch (InvalidDataException ex) when (ex.Data[InlineImages.FailureDataKey] is InlineImageFailure failure)
        { Require(failure.Issues[0].Reason == reason); return; }
        throw new Exception("Debe rechazar el enlace no verificable.");
    }
    private static void Require(bool ok) { if (!ok) throw new Exception("Comprobación de imágenes o lote incumplida."); }
    private sealed class CidRenderer : IPdfRenderer
    {
        public bool FailFirst, FailAll;
        public Task<byte[]> RenderAsync(MailInfo mail, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
        {
            if (FailAll || FailFirst && mail.Id == "m1") InlineImages.Resolve(["cid:ausente"], mail.Attachments, attachments);
            else if (mail.Html.StartsWith("cid:")) InlineImages.Resolve([mail.Html], mail.Attachments, attachments);
            return Task.FromResult(FakeRenderer.ValidPdf("Contenido completo"));
        }
    }
    private sealed class DetailedSource(FakeSource source) : IAttachmentDownloadSource
    {
        public bool ChangeMetadata;
        private readonly Dictionary<string, int> calls = new();
        public Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct) => source.FoldersAsync(ct);
        public Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, MailDateRange dates, TimeZoneInfo zone, CancellationToken ct) => source.ListAsync(folders, dates, zone, ct);
        public Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct) => source.ReadAsync(item, ct);
        public Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo attachment, CancellationToken ct) => source.AttachmentAsync(item, attachment, ct);
        public Task MoveToDeletedAsync(MailInfo item, CancellationToken ct) => source.MoveToDeletedAsync(item, ct);
        public async Task<AttachmentDownload> DownloadAttachmentAsync(MailInfo mail, AttachmentInfo attachment, CancellationToken ct)
        {
            calls[attachment.Id] = calls.GetValueOrDefault(attachment.Id) + 1;
            return new(await source.AttachmentAsync(mail, attachment, ct), (ChangeMetadata && calls[attachment.Id] > 1 ? "changed-" : "") + "cid-" + attachment.Id, "image/png");
        }
    }
    private sealed class MetadataTransport : IOutlookTransport
    {
        public Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
        {
            if (action != "GetAttachment" || OwaAttachmentContract.Read(JsonSerializer.SerializeToElement(body)) != "a") throw new Exception("Contrato de descarga inválido.");
            return Task.FromResult(JsonSerializer.SerializeToElement(new { Body = new { Attachments = new[] { new { AttachmentId = new { Id = "a" }, Content = "AAEC/w==", ContentId = "<logo>", ContentType = "image/png" } } } }));
        }
    }
}
