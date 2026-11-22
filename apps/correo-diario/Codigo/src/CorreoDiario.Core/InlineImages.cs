namespace CorreoDiario.Core;

public static class InlineImages
{
    public const string FailureDataKey = "CorreoDiario.InlineImageFailure";
    public sealed record Resolved(int Index, string AttachmentId, string MimeType, byte[] Bytes);

    // Content-ID is header data, not a URL: never percent-decode this side.
    public static string ContentId(string value)
    {
        var id = value.Trim();
        if (id.StartsWith('<') && id.EndsWith('>')) id = id[1..^1].Trim();
        return id;
    }

    public static string ReferenceId(string value)
    {
        var url = value.Trim();
        if (!url.StartsWith("cid:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Referencia de imagen no válida.");
        // RFC 2392: decode once, then compare with the bracket-free header ID.
        // '+' stays '+'. It is not form-url-encoded data.
        return ContentId(Uri.UnescapeDataString(url[4..]));
    }

    public static IReadOnlyList<Resolved> Resolve(IReadOnlyList<string> references, IReadOnlyList<AttachmentInfo> attachments,
        IReadOnlyDictionary<string, byte[]> contents)
    {
        var resolved = new List<Resolved>();
        var issues = new List<InlineImageIssue>();
        for (var i = 0; i < references.Count; i++)
        {
            string id;
            try { id = ReferenceId(references[i]); }
            catch (Exception ex) when (ex is InvalidDataException or UriFormatException)
            { issues.Add(new(i + 1, "invalid-reference", references[i].Length, 0, 0)); continue; }
            var candidates = attachments.Where(a => id.Length > 0 && ContentId(a.ContentId) == id).ToArray();
            if (candidates.Length == 0)
                candidates = attachments.Where(a => id.Length > 0 && string.Equals(ContentId(a.ContentId), id, StringComparison.OrdinalIgnoreCase)).ToArray();
            string? reason = null;
            if (candidates.Length == 0) reason = "content-id-not-found";
            else if (candidates.Any(a => !contents.ContainsKey(a.Id))) reason = "attachment-bytes-missing";
            else if (candidates.Skip(1).Any(a => !string.Equals(a.MimeType, candidates[0].MimeType, StringComparison.OrdinalIgnoreCase) ||
                !contents[a.Id].AsSpan().SequenceEqual(contents[candidates[0].Id]))) reason = "ambiguous-content-id";
            if (reason is not null) { issues.Add(new(i + 1, reason, references[i].Length, id.Length, candidates.Length)); continue; }
            var item = candidates[0];
            var mime = item.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? item.MimeType : "application/octet-stream";
            resolved.Add(new(i, item.Id, mime, contents[item.Id]));
        }
        if (issues.Count > 0)
        {
            var failure = new InlineImageFailure(references.Count, attachments.Count, attachments.Count(a => ContentId(a.ContentId).Length > 0), issues);
            var error = new InvalidDataException($"No se han podido relacionar {issues.Count} imágenes incrustadas con sus adjuntos. El correo queda pendiente.");
            error.Data[FailureDataKey] = failure;
            throw error;
        }
        return resolved;
    }

    public static AttachmentInfo ForRendering(AttachmentInfo original, AttachmentDownload download)
    {
        var before = ContentId(original.ContentId); var complete = ContentId(download.ContentId);
        if (before.Length > 0 && complete.Length > 0 && !string.Equals(before, complete, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La identidad de contenido del adjunto no coincide con la del correo. No se inicia el borrado.");
        return original with
        {
            ContentId = before.Length > 0 ? original.ContentId : download.ContentId,
            MimeType = (original.MimeType.Length == 0 || original.MimeType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)) && download.ContentType.Length > 0
                ? download.ContentType : original.MimeType
        };
    }
    public static string MetadataIdentity(AttachmentDownload download) => System.Text.Json.JsonSerializer.Serialize(new[] { ContentId(download.ContentId), download.ContentType.ToLowerInvariant() });
}
