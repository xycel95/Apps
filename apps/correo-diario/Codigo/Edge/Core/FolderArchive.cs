using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig.Writer;

namespace CorreoDiario;

public sealed record FolderResult(string Directory, int Listed, int Saved, int Reused, int Review, int Pending,
    int IncompleteFolders, bool Cancelled, int ThreadErrors)
{
    public bool Complete => !Cancelled && Pending == 0 && Review == 0 && IncompleteFolders == 0 && ThreadErrors == 0;
}
public static class FolderArchive
{
    sealed record Artifact(string Name, string Hash);
    sealed record Receipt(string Revision, string Folder, string Stamp, DateTimeOffset? Date, string Subject,
        string MailKey, int Pages, List<Artifact> Files, string[] Notes);
    sealed record Entry(CloudMessage Message, string Key, string State, Receipt? Saved, string? Reason);
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static string Hash(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    static void Store<T>(string path, T value)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, value, Json);
                output.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static string UniqueDirectory(string parent, string stem)
    {
        var name = stem;
        for (int n = 2; Directory.Exists(Path.Combine(parent, name)) || File.Exists(Path.Combine(parent, name)); n++)
            name = stem + " (" + n + ")";
        return name;
    }
    static bool LocalName(string s) => !string.IsNullOrWhiteSpace(s) && s is not ("." or "..")
        && !Path.IsPathRooted(s) && s.IndexOfAny(['/', '\\', ':']) < 0 && Path.GetFileName(s) == s;

    static Receipt? VerifiedReceipt(string path, string root, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var saved = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path));
            if (saved == null || saved.Folder == null || !LocalName(saved.Folder) || saved.Files == null
                || saved.Notes == null || saved.Files.Count == 0 || saved.Pages < 1
                || saved.Files.Any(f => f == null) || saved.Files.Count(f => f.Name == "Correo.pdf") != 1) return null;
            var dir = Path.Combine(root, saved.Folder);
            if (!Directory.Exists(dir) || File.GetAttributes(dir).HasFlag(FileAttributes.ReparsePoint)) return null;
            foreach (var file in saved.Files)
            {
                ct.ThrowIfCancellationRequested();
                if (!LocalName(file.Name)) return null;
                var target = Path.Combine(dir, file.Name);
                if (!File.Exists(target) || File.GetAttributes(target).HasFlag(FileAttributes.ReparsePoint)
                    || MailReader.FileHash(target) != file.Hash) return null;
            }
            return saved;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    static async Task<Receipt> SaveMessage(GraphMailbox mailbox, CloudMessage message, string key, string root,
        string state, string receiptPath, Receipt? previous, MailPrinter printer, CancellationToken ct)
    {
        var current = await mailbox.Read(message, ct).ConfigureAwait(false);
        var mail = current.Mail;
        var stage = Path.Combine(state, "trabajo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var notes = new List<string>();
            var attachments = new List<SavedAttachment>();
            int pages = await Archive.WriteFiles(mail, current.Content, stage, printer, notes, attachments, ct).ConfigureAwait(false);
            var files = attachments.Select(a => new Artifact(a.File, a.Hash)).ToList();
            files.Add(new("Correo.pdf", MailReader.FileHash(Path.Combine(stage, "Correo.pdf"))));
            var warnings = notes.Distinct().ToArray();
            if (previous != null && previous.Stamp == mail.Hash && previous.Notes.SequenceEqual(warnings)
                && previous.Files.Where(f => f.Name != "Correo.pdf").SequenceEqual(files.Where(f => f.Name != "Correo.pdf")))
            {
                var reused = previous with { Revision = message.Revision };
                Store(receiptPath, reused);
                return reused;
            }
            ct.ThrowIfCancellationRequested();
            var dir = UniqueDirectory(root, mail.FolderStem + " (" + key[..12] + ")");
            var receipt = new Receipt(message.Revision, dir, mail.Hash, mail.Date ?? message.Date, mail.Subject,
                mail.Key, pages, files, warnings);
            // Recover an interruption between publishing the directory and replacing its receipt.
            var intent = receiptPath + ".pendiente";
            Store(intent, receipt);
            Directory.Move(stage, Path.Combine(root, dir));
            File.Move(intent, receiptPath, true);
            return receipt;
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }

    public static async Task<FolderResult> WriteAsync(GraphMailbox mailbox, string accountKey, IReadOnlyList<CloudFolder> folders,
        string destination, IProgress<string>? progress, CancellationToken ct, Action<string, Exception>? error = null)
    {
        if (folders.Count == 0 || string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Elige al menos una carpeta del buzón y una carpeta de destino.");
        ct.ThrowIfCancellationRequested();
        string root = Directory.CreateDirectory(Path.Combine(destination, "CorreoDiario_365")).FullName;
        string state = Directory.CreateDirectory(Path.Combine(root, "_registro")).FullName;
        string reports = Directory.CreateDirectory(Path.Combine(state, "Lotes")).FullName;
        var run = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + "_" + Guid.NewGuid().ToString("N")[..8];
        var report = Path.Combine(reports, run + ".json");
        using var lease = TakeLock(state);
        var items = new Dictionary<string, CloudMessage>(StringComparer.Ordinal);
        var listedFolders = new HashSet<string>(StringComparer.Ordinal);
        var selected = folders.DistinctBy(f => f.Id).ToArray();
        var errors = new List<string>();
        var results = new List<Entry>();
        int saved = 0, reused = 0, review = 0, threadErrors = 0;
        bool cancelled = false;
        var printer = new MailPrinter();
        try
        {
            foreach (var folder in selected)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report("Leyendo carpeta · " + folder.Path);
                try
                {
                    var messages = await mailbox.Messages(folder, ct).ConfigureAwait(false);
                    foreach (var msg in messages) items.TryAdd(msg.Id, msg);
                    listedFolders.Add(folder.Id);
                }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    errors.Add(folder.Path + " · " + Explain(e));
                    error?.Invoke("Enumerar carpeta de Microsoft 365", e);
                }
            }
            foreach (var message in items.Values)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Exportando {results.Count + 1} de {items.Count} · {message.Subject}");
                var key = Hash(accountKey + "\n" + message.Id);
                var receiptPath = Path.Combine(state, key + ".json");
                try
                {
                    var intent = receiptPath + ".pendiente";
                    if (VerifiedReceipt(intent, root, ct) != null) File.Move(intent, receiptPath, true);
                    var prior = VerifiedReceipt(receiptPath, root, ct);
                    if (prior?.Revision == message.Revision && prior.Notes.Length == 0)
                    {
                        reused++;
                        results.Add(new(message, key, "Ya verificado", prior, null));
                        continue;
                    }
                    var receipt = await SaveMessage(mailbox, message, key, root, state, receiptPath, prior, printer, ct).ConfigureAwait(false);
                    bool valid = receipt.Notes.Length == 0;
                    if (valid) saved++; else review++;
                    results.Add(new(message, key, valid ? "Verificado" : "Revisar", receipt, null));
                }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    results.Add(new(message, key, "Pendiente", null, Explain(e)));
                    error?.Invoke("Exportar mensaje de Microsoft 365", e);
                }
            }
            foreach (var group in results.Where(x => x.Message.Conversation.Length > 0).GroupBy(x => x.Message.Conversation))
            {
                ct.ThrowIfCancellationRequested();
                if (group.Count() < 2 || group.Any(x => x.Saved == null || x.Saved.Notes.Length > 0)) continue;
                try { Combine(group.Select(x => x.Saved!).ToArray(), root, ct); }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    threadErrors++; errors.Add("PDF del hilo · " + Explain(e)); error?.Invoke("Combinar hilo de Microsoft 365", e);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; }
        var summary = new FolderResult(root, items.Count, saved, reused, review, items.Count - saved - reused - review,
            selected.Length - listedFolders.Count, cancelled, threadErrors);
        var done = results.ToDictionary(x => x.Message.Id);
        Store(report, new
        {
            resultado = summary, carpetas = selected.Select(f => new { ruta = f.Path, listadoCompleto = listedFolders.Contains(f.Id) }),
            mensajes = items.Values.Select(m =>
            {
                done.TryGetValue(m.Id, out var entry);
                return new { asunto = m.Subject, carpeta = m.Folder, estado = entry?.State ?? "Pendiente",
                    salida = entry?.Saved?.Folder, detalle = entry?.Reason, avisos = entry?.Saved?.Notes };
            }),
            incidencias = errors,
        });
        File.WriteAllText(Path.Combine(root, "Ultimo-lote.txt"), string.Join(Environment.NewLine, new[]
        {
            summary.Cancelled ? "Exportación cancelada." : summary.Complete ? "Exportación verificada." : "Exportación con incidencias.",
            $"Correos en el listado: {summary.Listed}. Guardados y verificados: {saved}. Ya verificados: {reused}.",
            $"Para revisar: {review}. Pendientes: {summary.Pending}. Carpetas sin enumeración completa: {summary.IncompleteFolders}. Errores de hilo: {threadErrors}.",
            "Detalle del lote: " + Path.GetRelativePath(root, report),
        }.Concat(errors)));
        return summary;
    }

    static FileStream TakeLock(string state)
    {
        try { return new FileStream(Path.Combine(state, "exportacion.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("El destino está ocupado o no admite escritura. Comprueba si hay otra exportación abierta.", e); }
    }

    static void Combine(Receipt[] group, string root, CancellationToken ct)
    {
        var ordered = group.OrderBy(m => m.Date).ThenBy(m => m.Folder, StringComparer.Ordinal).ToArray();
        var inputs = ordered.Select(x => Path.Combine(root, x.Folder, "Correo.pdf")).ToArray();
        var fingerprint = Hash(string.Join("\n", ordered.Select(x => x.Files.Single(f => f.Name == "Correo.pdf").Hash)));
        var dir = Directory.CreateDirectory(Path.Combine(root,"Hilos")).FullName;
        var output = Path.Combine(dir, MailReader.SafeName(ordered[0].Subject, 56) + " (" + fingerprint[..16] + ").pdf");
        bool Valid(string path)
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(path);
            if (pdf.NumberOfPages != ordered.Sum(m => m.Pages)) return false;
            int page = 1;
            foreach (var mail in ordered)
            {
                bool found = false;
                for (int i = 0; i < mail.Pages; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var text = string.Concat(pdf.GetPage(page++).Letters.Select(x => x.Value)).Replace(" ", "");
                    found |= text.Contains(mail.MailKey, StringComparison.OrdinalIgnoreCase);
                }
                if (!found) return false;
            }
            return true;
        }
        // Use a checksum receipt for previously generated combinations to avoid reopening a large PDF for every message.
        var proof = Path.Combine(root, "_registro", "hilo-" + fingerprint + ".sha256");
        if (File.Exists(proof))
        {
            var lines = File.ReadAllLines(proof);
            if (lines.Length == 2 && LocalName(lines[0]))
            {
                var prior = Path.Combine(dir, lines[0]);
                if (File.Exists(prior) && MailReader.FileHash(prior) == lines[1]) return;
            }
        }
        var stem = Path.GetFileNameWithoutExtension(output);
        for (int n = 2; File.Exists(output); n++) output = Path.Combine(dir, stem + " (" + n + ").pdf");
        var temp = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".parcial");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew)) PdfMerger.Merge(stream, inputs);
            ct.ThrowIfCancellationRequested();
            if (!Valid(temp)) throw new InvalidDataException("El PDF del hilo no coincide con los mensajes exportados.");
            File.Move(temp, output);
            File.WriteAllLines(proof, [Path.GetFileName(output), MailReader.FileHash(output)]);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static string Explain(Exception e) => e is OperationCanceledException ? "Microsoft 365 agotó el tiempo de espera. Puedes reanudar el lote."
        : e is MailboxException or IOException or InvalidDataException or FormatException ? e.Message
        : $"No se pudo completar la operación ({e.GetType().Name}).";
}
