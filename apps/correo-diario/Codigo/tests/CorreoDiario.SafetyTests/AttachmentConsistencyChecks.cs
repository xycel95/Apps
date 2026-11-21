using System.Text.Json;
using CorreoDiario.Core;

internal static class AttachmentConsistencyChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        foreach (var reported in new long[] { 344, 0 })
        {
            yield return ($"Integridad: tamaño de almacén {reported} y contenido de 8 bytes comprobados por separado", async () =>
            {
                using var run = new TestRun(); SetStoreSizes(run.Source, reported);
                var result = await run.Run(false);
                if (result.Attachments != 2 || result.Moved != 0 || run.Source.Downloads != 4) throw new Exception("Deben verificarse las dos lecturas sin movimientos");
                using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(result.Directory, "Comprobacion.json")));
                foreach (var message in receipt.RootElement.GetProperty("Messages").EnumerateArray())
                {
                    var proof = message.GetProperty("Attachments")[0];
                    if (proof.GetProperty("ReportedSize").GetInt64() != reported || proof.GetProperty("ContentLength").GetInt64() != 8 ||
                        !proof.GetProperty("SourceReadbackVerified").GetBoolean() || proof.GetProperty("ReportedSizeMeaning").GetString() != "OutlookStoreBytes")
                        throw new Exception("El recibo debe distinguir tamaño de almacén y bytes guardados");
                }
            });
        }
        foreach (var truncate in new[] { false, true })
        {
            yield return (truncate ? "Integridad: una segunda lectura truncada impide todos los movimientos" : "Integridad: cambiar bytes sin cambiar longitud impide todos los movimientos", async () =>
            {
                using var run = new TestRun(); SetStoreSizes(run.Source, 344);
                run.Source.ChangeAttachmentAfterExport = !truncate;
                run.Source.TruncateAttachmentAfterExport = truncate;
                try { await run.Run(true); }
                catch (ExportException ex)
                {
                    if (ex.Moved != 0 || run.Source.MoveCalls != 0 || Directory.GetFiles(ex.Directory, "*_CORREO.pdf").Length != 2)
                        throw new Exception("Debe conservar copias y no comenzar ningún movimiento");
                    return;
                }
                throw new Exception("Debe detectar el cambio de contenido");
            });
        }
        yield return ("Integridad: archivo vacío explícito con propiedades de almacén se conserva", async () =>
        {
            using var run = new TestRun(); SetStoreSizes(run.Source, 344); run.Source.EmptyAttachments = true;
            var result = await run.Run(false);
            var files = Directory.GetFiles(result.Directory, "*_ADJ01_*");
            if (files.Length != 2 || files.Any(p => new FileInfo(p).Length != 0) || run.Source.Downloads != 4) throw new Exception("Debe comprobar ambos archivos vacíos");
        });
        yield return ("Integridad: no confunde falta de Content con un archivo vacío", async () =>
        {
            var source = new OwaSource(new MissingContentTransport());
            var mail = new MailInfo("m", "r", "inbox", "Prueba", "a@example.invalid", "b@example.invalid", "", DateTimeOffset.UtcNow, false, "", []);
            try { await source.AttachmentAsync(mail, new("a", "vacio.txt", 0, "text/plain", false, "", AttachmentSizeMeaning.OutlookStoreBytes), default); }
            catch (InvalidDataException) { return; }
            throw new Exception("Debe detenerse si el servidor no devuelve Content");
        });
    }
    private static void SetStoreSizes(FakeSource source, long reported)
    {
        for (var i = 0; i < source.Items.Count; i++) source.Items[i] = source.Items[i] with
        { Attachments = source.Items[i].Attachments.Select(a => a with { Size = reported, SizeMeaning = AttachmentSizeMeaning.OutlookStoreBytes }).ToArray() };
    }
    private sealed class MissingContentTransport : IOutlookTransport
    {
        public Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct) => Task.FromResult(JsonSerializer.SerializeToElement(
            new { Body = new { Attachments = new[] { new { AttachmentId = new { Id = "a" } } } } }));
    }
}
