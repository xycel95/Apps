using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CorreoDiario.Core;

public sealed class ExportEngine(IMailSource source, IPdfRenderer renderer)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<ExportResult> RunAsync(IReadOnlyList<MailInfo> selection, ExportOptions options,
        IProgress<ExportProgress>? progress, CancellationToken ct)
    {
        if (selection.Count == 0) throw new InvalidOperationException("No hay correos seleccionados.");
        if (selection.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != selection.Count)
            throw new InvalidOperationException("Hay correos repetidos en la selección. Vuelve a cargar la lista.");
        var destination = Path.GetFullPath(options.Destination);
        if (!Directory.Exists(destination)) throw new DirectoryNotFoundException("La carpeta de destino no existe.");
        var run = Path.Combine(destination, "CorreoDiario_" + DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss") + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(run);
        var receipt = new ExportReceipt { DeletionRequested = options.MoveToDeleted };
        var snapshots = new List<MailInfo>();
        var locked = new List<FileStream>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attachmentChecks = new Dictionary<(string Mail, string Attachment), SavedAttachment>();
        var attachmentCount = 0;
        try
        {
            await SaveReceiptAsync(run, receipt);
            for (var i = 0; i < selection.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new($"Guardando {i + 1}/{selection.Count}: {selection[i].Subject}", i, selection.Count));
                var mail = await source.ReadAsync(selection[i], ct);
                if (mail.Id != selection[i].Id) throw new InvalidDataException("La respuesta no corresponde al correo solicitado.");
                if (mail.Attachments.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != mail.Attachments.Count)
                    throw new InvalidDataException("La lista de adjuntos contiene identificadores repetidos.");
                var allAttachments = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var a in mail.Attachments)
                {
                    ct.ThrowIfCancellationRequested();
                    var bytes = await source.AttachmentAsync(mail, a, ct);
                    if (a.SizeMeaning == AttachmentSizeMeaning.ContentBytes && a.Size is long expected && bytes.LongLength != expected)
                        throw new InvalidDataException($"El tamaño del adjunto «{a.Name}» no coincide. Se conserva el correo.");
                    allAttachments.Add(a.Id, bytes);
                }
                var pdf = await renderer.RenderAsync(mail, allAttachments, ct);
                ValidatePdf(pdf);
                var timestamp = TimeZoneInfo.ConvertTime(mail.Timestamp, TimeZoneInfo.Local);
                var address = mail.Sent ? mail.To : mail.From;
                var prefix = $"{timestamp:yyyy-MM-dd_HH.mm}_{(mail.Sent ? "ENVIADO" : "RECIBIDO")}_{SafeName(address, 75)}";
                var candidate = prefix;
                for (var suffix = 2; !names.Add(candidate); suffix++) candidate = prefix + "_" + suffix.ToString("00");
                prefix = candidate;
                var files = new List<SavedFile>();
                var savedAttachments = new List<SavedAttachment>();
                files.Add(await SaveVerifiedAsync(run, prefix + "_CORREO.pdf", pdf, ct));
                for (var a = 0; a < mail.Attachments.Count; a++)
                {
                    var info = mail.Attachments[a];
                    var name = prefix + $"_ADJ{a + 1:00}_" + SafeName(info.Name, 95);
                    var file = await SaveVerifiedAsync(run, name, allAttachments[info.Id], ct);
                    files.Add(file);
                    var check = new SavedAttachment(info.Id, file.Name, info.Size, info.SizeMeaning, file.Length, file.Sha256);
                    savedAttachments.Add(check);
                    attachmentChecks.Add((mail.Id, info.Id), check);
                    attachmentCount++;
                }
                receipt.Messages.Add(new(mail.Id, mail.Revision, mail.Subject, files) { Attachments = savedAttachments });
                snapshots.Add(mail);
                await SaveReceiptAsync(run, receipt);
            }

            // The deletion phase cannot begin until EVERY message has been exported,
            // every source snapshot is rechecked, and all saved bytes are re-read.
            receipt.Status = "VERIFICANDO";
            await SaveReceiptAsync(run, receipt);
            progress?.Report(new("Comprobando todos los PDF y adjuntos…", selection.Count, selection.Count));
            var verifiedMessages = 0;
            foreach (var mail in snapshots)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new($"Verificando {++verifiedMessages}/{snapshots.Count}: revisando el correo y volviendo a leer sus adjuntos…", selection.Count, selection.Count));
                var current = await source.ReadAsync(mail, ct);
                if (Fingerprint(current) != Fingerprint(mail))
                    throw new InvalidDataException("Un correo ha cambiado durante la exportación. No comienza el borrado.");
                foreach (var attachment in current.Attachments)
                {
                    ct.ThrowIfCancellationRequested();
                    var check = attachmentChecks[(mail.Id, attachment.Id)];
                    var bytes = await source.AttachmentAsync(current, attachment, ct);
                    if (bytes.LongLength != check.ContentLength || Hash(bytes) != check.Sha256)
                        throw new InvalidDataException($"El contenido del adjunto «{attachment.Name}» no coincide en la segunda lectura. Se conserva el correo.");
                    check.SourceReadbackVerified = true;
                }
            }
            foreach (var file in receipt.Messages.SelectMany(m => m.Files))
            {
                ct.ThrowIfCancellationRequested();
                // On Windows, deny writers/deleters until the movement phase ends.
                var stream = new FileStream(Path.Combine(run, file.Name), FileMode.Open, FileAccess.Read, FileShare.Read);
                locked.Add(stream);
                if (stream.Length != file.Length || Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)) != file.Sha256)
                    throw new InvalidDataException($"El archivo «{file.Name}» no supera la comprobación. No comienza el borrado.");
            }
            receipt.Status = "VERIFICADO";
            await SaveReceiptAsync(run, receipt);
            if (options.MoveToDeleted)
            {
                if (snapshots.Any(m => string.IsNullOrEmpty(m.Revision)))
                    throw new InvalidDataException("Outlook no informa de la versión de los correos. La copia queda guardada, pero no se permite el borrado automático.");
                progress?.Report(new("Todo verificado. Moviendo los correos a Eliminados…", selection.Count, selection.Count));
                foreach (var mail in snapshots)
                {
                    ct.ThrowIfCancellationRequested();
                    receipt.MoveInProgress = mail.Id;
                    await SaveReceiptAsync(run, receipt);
                    await source.MoveToDeletedAsync(mail, ct);
                    receipt.MovedToDeleted.Add(mail.Id);
                    receipt.MoveInProgress = null;
                    // Persist each move so an interrupted run reports partial completion accurately.
                    await SaveReceiptAsync(run, receipt);
                }
            }
            receipt.Status = "COMPLETADO";
            await SaveReceiptAsync(run, receipt);
            return new(run, snapshots.Count, attachmentCount, receipt.MovedToDeleted.Count);
        }
        catch (Exception ex)
        {
            receipt.Status = ex is OperationCanceledException ? "CANCELADO" : "ERROR";
            receipt.Error = ex.Message;
            try { await SaveReceiptAsync(run, receipt); } catch (IOException) { }
            var pending = receipt.MoveInProgress is null ? "" : "\nEl último movimiento no ha quedado confirmado. Comprueba el buzón antes de repetirlo.";
            throw new ExportException($"{ex.Message}\n\nArchivos guardados: {run}\nMovimientos confirmados a Eliminados: {receipt.MovedToDeleted.Count}.{pending}", run, receipt.MovedToDeleted.Count, ex);
        }
        finally { foreach (var stream in locked) await stream.DisposeAsync(); }
    }

    public static string Fingerprint(MailInfo item) => Hash(JsonSerializer.SerializeToUtf8Bytes(new
    {
        item.Id, item.Revision, item.FolderId, item.Subject, item.From, item.To, item.Cc, item.Bcc,
        item.Timestamp, item.SentAt, item.Sent, item.Html, Attachments = item.Attachments.OrderBy(a => a.Id, StringComparer.Ordinal)
    }));

    public static string SafeName(string? input, int max = 100)
    {
        var text = Regex.Replace(input ?? "", "[<>:\"/\\\\|?*\\x00-\\x1F]", "_").Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(text)) text = "sin_nombre";
        if (text.Length > max)
        {
            var ext = Path.GetExtension(text);
            if (ext.Length > 15) ext = "";
            text = text[..Math.Max(1, max - ext.Length)] + ext;
        }
        if (Regex.IsMatch(text.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) text = "_" + text;
        return text;
    }

    public static void ValidatePdf(byte[] bytes)
    {
        if (bytes.Length < 150 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidDataException("No se ha generado un PDF válido.");
        var tail = Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 2048), Math.Min(bytes.Length, 2048));
        if (!tail.Contains("%%EOF", StringComparison.Ordinal)) throw new InvalidDataException("El PDF está incompleto.");
        PdfVerifier.Validate(bytes);
    }

    private static async Task<SavedFile> SaveVerifiedAsync(string directory, string name, byte[] bytes, CancellationToken ct)
    {
        var path = Path.Combine(directory, name);
        if (path.Length > 245) throw new PathTooLongException("El destino es demasiado largo. Elige una carpeta más cercana a la raíz.");
        var expected = Hash(bytes);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await output.WriteAsync(bytes, ct);
            await output.FlushAsync(ct);
            output.Flush(true);
        }
        await using var check = File.OpenRead(path);
        if (check.Length != bytes.LongLength || Convert.ToHexString(await SHA256.HashDataAsync(check, ct)) != expected)
            throw new InvalidDataException($"No coincide el contenido guardado de «{name}».");
        return new(name, bytes.LongLength, expected);
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    private static async Task SaveReceiptAsync(string directory, ExportReceipt receipt)
    {
        var path = Path.Combine(directory, "Comprobacion.json");
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(receipt, JsonOptions));
        File.Move(temporary, path, true);
    }
}

public sealed class ExportException(string message, string directory, int moved, Exception inner) : Exception(message, inner)
{
    public string Directory { get; } = directory;
    public int Moved { get; } = moved;
}
