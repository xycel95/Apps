using System.Security.Cryptography;
using CorreoDiario.Core;

internal static class FlatExportChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Destino: todos los documentos quedan en la carpeta elegida sin subcarpetas", async () =>
        {
            using var run = new TestRun();
            var result = await run.Run(false);
            Check(result.Directory == Path.GetFullPath(run.Directory) && Directory.GetDirectories(run.Directory).Length == 0, "Destino directo");
            Check(Directory.GetFiles(run.Directory, "*_CORREO.pdf").Length == 2 && Directory.GetFiles(run.Directory, "*_ADJ01_*").Length == 2, "Todos los documentos juntos");
            Check(Path.GetDirectoryName(result.ReceiptPath) == result.Directory && File.Exists(result.ReceiptPath), "Registro individual en el destino");
        });
        yield return ("Destino: repetir una exportación conserva documentos y registro anteriores", async () =>
        {
            using var run = new TestRun();
            var first = await run.Run(false);
            var originals = Directory.GetFiles(run.Directory).ToDictionary(p => p, Hash);
            var second = await run.Run(false);
            Check(first.ReceiptPath != second.ReceiptPath && first.Directory == second.Directory, "Registro distinto y carpeta común");
            Check(originals.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "No sobrescribe ningún archivo anterior");
            Check(Directory.GetFiles(run.Directory, "*_CORREO.pdf").Length == 4 && Directory.GetFiles(run.Directory, "*_ADJ01_*").Length == 4, "Conserva las dos ejecuciones");
            Check(Directory.GetFiles(run.Directory, "Comprobacion_*.json").Length == 2 && Directory.GetFiles(run.Directory, "*.tmp").Length == 0, "Registros independientes y temporales retirados");
        });
        yield return ("Destino: un adjunto anterior sin PDF reserva su grupo y se conserva", async () =>
        {
            using var run = new TestRun();
            var mail = run.Source.Items[0];
            var prefix = $"{TimeZoneInfo.ConvertTime(mail.Timestamp, TimeZoneInfo.Local):yyyy-MM-dd_HH.mm}_RECIBIDO_{ExportEngine.SafeName(mail.From, 75)}";
            var orphan = Path.Combine(run.Directory, prefix + "_ADJ02_anterior.txt");
            await File.WriteAllTextAsync(orphan, "archivo previo");
            var result = await run.Run(false);
            Check(await File.ReadAllTextAsync(orphan) == "archivo previo", "Adjunto anterior intacto");
            Check(File.Exists(Path.Combine(result.Directory, prefix + "_02_CORREO.pdf")) && File.Exists(Path.Combine(result.Directory, prefix + "_02_ADJ01_solicitud.pdf")), "Nuevo PDF y adjunto comparten sufijo");
        });
        yield return ("Destino: dos exportaciones simultáneas conservan sus archivos sin sobrescrituras", async () =>
        {
            using var run = new TestRun();
            var secondSource = new FakeSource(); secondSource.Items.Clear(); secondSource.Items.AddRange(run.Source.Items);
            var results = await Task.WhenAll(run.Run(false), new ExportEngine(secondSource, new FakeRenderer()).RunAsync(secondSource.Items, new(run.Directory, false), null, default));
            Check(results.All(r => r.Directory == run.Directory && r.Messages == 2 && r.Attachments == 2 && r.Moved == 0), "Ambas exportaciones completas");
            Check(Directory.GetFiles(run.Directory, "*_CORREO.pdf").Length == 4 && Directory.GetFiles(run.Directory, "*_ADJ01_*").Length == 4, "Grupos separados por nombre");
            Check(results.Select(r => r.ReceiptPath).Distinct().Count() == 2 && Directory.GetDirectories(run.Directory).Length == 0, "Dos registros y ninguna subcarpeta");
        });
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
}
