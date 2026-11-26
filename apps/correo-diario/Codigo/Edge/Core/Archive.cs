using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace CorreoDiario;

public sealed record ArchiveResult(
    string Folder,
    int Messages,
    int Pdfs,
    int Combined,
    int Warnings
);

internal sealed record SavedAttachment(
    [property: System.Text.Json.Serialization.JsonPropertyName("nombre")] string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("archivo")] string File,
    [property: System.Text.Json.Serialization.JsonPropertyName("sha256")] string Hash,
    [property: System.Text.Json.Serialization.JsonPropertyName("incrustado")] bool Inline);

public static class Archive
{
    static readonly UTF8Encoding Utf8 = new(false);

    static string ArtifactName(string original, int max)
    {
        var ext = Path.GetExtension(original);
        if (ext.Length > 12)
            ext = "";
        return MailReader.SafeName(ext.Length > 0 ? original[..^ext.Length] : original, max)
            + (ext.Length == 0 ? "" : "." + MailReader.SafeName(ext[1..], 11));
    }

    static string AttachmentPath(string folder, string original, HashSet<string> occupied)
    {
        var name = ArtifactName(original, 86);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var chosen = name;
        int copy = 2;
        while (!occupied.Add(chosen) || File.Exists(Path.Combine(folder, chosen)))
            chosen = stem + " (" + (copy++) + ")" + ext;
        return Path.Combine(folder, chosen);
    }

    internal static async Task<int> WriteFiles(Mail mail, Content content, string folder, MailPrinter printer,
        List<string> notes, List<SavedAttachment> files, CancellationToken ct)
    {
        notes.AddRange(content.Notices);
        var occupied = new HashSet<string>(["Correo.pdf"], StringComparer.OrdinalIgnoreCase);
        foreach (var part in content.Parts)
        {
            ct.ThrowIfCancellationRequested();
            var path = AttachmentPath(folder, part.Name, occupied);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(part.Bytes);
            var hash = Convert.ToHexStringLower(SHA256.HashData(part.Bytes));
            if (MailReader.FileHash(path) != hash)
                throw new IOException("Error de comprobación del adjunto " + part.Name);
            files.Add(new(part.Name, Path.GetFileName(path), hash, part.Inline));
        }
        var html = BodyHtml.Create(mail, content, notes);
        var pdf = Path.Combine(folder, "Correo.pdf");
        notes.AddRange(await printer.PrintAsync(html, pdf, mail.Key, ct));
        return MailPrinter.VerifyPdf(pdf, mail.Key);
    }

    public static async Task<ArchiveResult> WriteAsync(
        IEnumerable<Mail> items,
        string dest,
        MailPrinter printer,
        IEnumerable<string> initialNotes,
        IProgress<string>? progress,
        CancellationToken ct,
        Action<string, Exception>? reportError = null
    )
    {
        var threads = Threads.Find(items);
        var collisions = threads
            .SelectMany(g => g.Messages)
            .GroupBy(m => m.FolderStem, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (threads.Count == 0)
            throw new InvalidOperationException("Añade al menos un correo completo.");
        var folder = Path.Combine(
            dest,
            "CorreoDiario_"
                + DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")
                + "_"
                + Guid.NewGuid().ToString("N")[..8]
        );
        Directory.CreateDirectory(folder);
        var log = new List<string>(initialNotes);
        var report = new StringBuilder(
            "CORREODIARIO · EXPORTACIÓN\r\nFechas del correo en hora de Madrid.\r\n\r\n"
        );
        var ledger = new List<object>();
        var messages = 0;
        var pdfs = 0;
        var joined = 0;
        var total = threads.Sum(t => t.Messages.Count);
        var index = 0;
        try
        {
            foreach (var group in threads)
            {
                var first = group.Messages[0];
                var groupPdfs = new List<string>();
                var groupPages = 0;
                report.AppendLine(first.Subject).AppendLine(group.OriginText);
                if (group.Missing.Count > 0)
                    report.AppendLine(
                        "Referencias sin recuperar: " + string.Join(", ", group.Missing)
                    );
                if (
                    group.Missing.Count > 0
                    || group.OriginText.Contains("no confirmado")
                    || group.OriginText.Contains("revisión")
                )
                    log.Add(first.Subject + " · " + group.OriginText);
                foreach (var m in group.Messages)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"Exportando {++index}/{total} · {m.Subject}");
                    var preferred = collisions.Contains(m.FolderStem) ? m.BaseName : m.FolderStem;
                    var dirName = preferred;
                    for (int suffix = 2; Directory.Exists(Path.Combine(folder, dirName)); suffix++)
                        dirName = preferred + " (" + suffix + ")";
                    var mailFolder = Path.Combine(folder, dirName);
                    Directory.CreateDirectory(mailFolder);
                    var notes = new List<string>();
                    var files = new List<SavedAttachment>();
                    string? pdf = null;
                    bool cancelled = false;
                    try
                    {
                        var c = MailReader.ReadContent(m);
                        messages++;
                        groupPages += await WriteFiles(m, c, mailFolder, printer, notes, files, ct);
                        var targetPdf = Path.Combine(mailFolder, "Correo.pdf");
                        pdf = Path.GetFileName(targetPdf);
                        pdfs++;
                        groupPdfs.Add(targetPdf);
                    }
                    catch (OperationCanceledException)
                    {
                        notes.Add("Exportación interrumpida durante este mensaje.");
                        cancelled = true;
                    }
                    catch (Exception e)
                    {
                        reportError?.Invoke("Guardar mensaje, PDF y adjuntos", e);
                        notes.Add("Exportación parcial: " + e.Message);
                    }
                    if (m.Id.Length == 0)
                        notes.Add("El mensaje no declara Message-ID.");
                    if (m.Madrid == null)
                        notes.Add(
                            "El mensaje no declara una fecha de envío ni recepción utilizable."
                        );
                    if (m.From.Length == 0)
                        notes.Add("No consta el remitente en las cabeceras.");
                    foreach (var note in notes.Distinct())
                        log.Add(dirName + " · " + note);
                    report
                        .Append("  ")
                        .Append(m.DisplayDate)
                        .Append("  ")
                        .Append(m.From)
                        .Append("  ")
                        .AppendLine(dirName);
                    ledger.Add(
                        new
                        {
                            carpeta = dirName,
                            sha256 = m.Hash,
                            pdf,
                            asunto = m.Subject,
                            remitente = m.From,
                            enviadoPor = m.Sender,
                            responderA = m.ReplyTo,
                            retorno = m.ReturnPath,
                            para = m.To,
                            cc = m.Cc,
                            identificador = m.Id,
                            enRespuestaA = m.Parents,
                            referencias = m.References,
                            indiceOutlook = m.ThreadIndex,
                            fecha = m.Date,
                            procedenciaFecha = m.DateSource,
                            recepcion = m.Received,
                            dominio = m.Origin.Site,
                            carpetaOVista = m.Origin.Folder,
                            metodo = m.Origin.Method,
                            adjuntos = files,
                            observaciones = notes.Distinct().ToArray(),
                        }
                    );
                    if (cancelled)
                        throw new OperationCanceledException(ct);
                }
                if (group.Messages.Count > 1 && groupPdfs.Count == group.Messages.Count)
                {
                    var combined = Path.Combine(
                        Directory.CreateDirectory(Path.Combine(folder, "Hilos")).FullName,
                        first.BaseName + ".pdf"
                    );
                    var temp = combined + ".parcial";
                    try
                    {
                        using (var output = new FileStream(temp, FileMode.CreateNew))
                            PdfMerger.Merge(output, groupPdfs.ToArray());
                        if (MailPrinter.VerifyPdf(temp) != groupPages)
                            throw new InvalidDataException(
                                "El número de páginas del hilo no coincide."
                            );
                        File.Move(temp, combined);
                        report.AppendLine(
                            "  PDF del hilo: " + Path.GetRelativePath(folder, combined)
                        );
                        joined++;
                    }
                    catch (Exception e)
                    {
                        reportError?.Invoke("Combinar PDF del hilo", e);
                        log.Add(first.Subject + " · PDF del hilo pendiente: " + e.Message);
                    }
                    finally
                    {
                        if (File.Exists(temp))
                            File.Delete(temp);
                    }
                }
                report.AppendLine();
            }
        }
        catch (OperationCanceledException)
        {
            log.Add(
                "Exportación cancelada. El resumen recoge los archivos cerrados hasta ese momento."
            );
            throw;
        }
        finally
        {
            if (log.Count > 0)
            {
                report.AppendLine("AVISOS");
                foreach (var line in log.Distinct())
                    report.AppendLine("• " + line);
            }
            report
                .AppendLine()
                .AppendLine(
                    $"Mensajes leídos: {messages}. PDF individuales: {pdfs}. PDF de hilo: {joined}."
                );
            File.WriteAllText(Path.Combine(folder, "Resumen.txt"), report.ToString(), Utf8);
            File.WriteAllText(
                Path.Combine(folder, "Origen.json"),
                JsonSerializer.Serialize(
                    new { mensajes = ledger, avisos = log.Distinct().ToArray() },
                    new JsonSerializerOptions { WriteIndented = true }
                ),
                Utf8
            );
        }
        return new(folder, messages, pdfs, joined, log.Distinct().Count());
    }
}
