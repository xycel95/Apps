using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CorreoDiario;

public sealed record CloudFolder(string Id, string Path)
{
    public override string ToString() => Path;
}
public sealed record CloudMessage(string Id, string FolderId, string Folder, string Subject, string Revision,
    string Conversation, DateTimeOffset? Date, string InternetId, bool HasAttachments);
public sealed record CloudAttachment(string Id, string Name, string Kind, long? Size, bool Inline, string Mime);
public sealed record CloudContent(Mail Mail, Content Content);

public sealed class MailboxException(string reason, string message, int? status = null) : Exception(message)
{
    public string Reason { get; } = reason;
    public int? Status { get; } = status;
}

// The production client has automatic redirection disabled. Authorization is attached to
// each validated Graph request, never to a URL from a message or attachment.
public sealed class GraphMailbox(HttpClient http, Func<CancellationToken, Task<string>> access, string? userObjectId = null)
{
    const string Base = "https://graph.microsoft.com/v1.0/me/";
    static readonly HashSet<HttpStatusCode> Transient =
        [HttpStatusCode.TooManyRequests, HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout];
    static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
            throw Invalid("Identificador de Microsoft 365 no válido.");
        return Uri.EscapeDataString(value);
    }
    static MailboxException Invalid(string detail) => new("respuesta no válida", detail);
    static string Field(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static string Required(JsonElement e, string name)
    {
        var s = Field(e, name);
        return s.Length == 0 ? throw Invalid("Microsoft 365 ha devuelto un registro incompleto.") : s;
    }
    static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    static string MessagePath(string id) => "messages/" + Segment(id);

    Uri SafeUri(string url, string expectedPath)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.IdnHost, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !SameCollection(uri.AbsolutePath, expectedPath))
            throw Invalid("Microsoft 365 devolvió un enlace de paginación inesperado.");
        return uri;
    }

    bool SameCollection(string path, string expected)
    {
        if (path.Equals(expected, StringComparison.Ordinal)) return true;
        // Graph can resolve /me to the authenticated object and use OData key syntax
        // or different casing for collection names in the nextLink.
        static string[] Parts(string value)
        {
            var parts = new List<string>();
            foreach (var segment in value.Trim('/').Split('/'))
            {
                string decoded = Uri.UnescapeDataString(segment);
                var key = Regex.Match(decoded, @"^(users|mailFolders|messages|attachments)\('([^']+)'\)$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (key.Success) { parts.Add(key.Groups[1].Value); parts.Add(key.Groups[2].Value); }
                else parts.Add(decoded);
            }
            return parts.ToArray();
        }
        var actual = Parts(path).ToList();
        if (actual.Count >= 3 && actual[1].Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(userObjectId, out var owner) || !Guid.TryParse(actual[2], out var target) || owner != target) return false;
            actual[1] = "me";
            actual.RemoveAt(2);
        }
        var wanted = Parts(expected);
        if (actual.Count != wanted.Length) return false;
        bool identifier = false;
        for (int i = 0; i < wanted.Length; i++)
        {
            if (!string.Equals(actual[i], wanted[i], identifier ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) return false;
            identifier = !identifier && wanted[i] is "mailFolders" or "messages" or "attachments";
        }
        return true;
    }

    async Task<HttpResponseMessage> Get(Uri uri, string media, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await access(ct).ConfigureAwait(false));
            request.Headers.Accept.ParseAdd(media);
            request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
            HttpResponseMessage response;
            try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(1 << attempt), ct).ConfigureAwait(false);
                continue;
            }
            if (response.IsSuccessStatusCode) return response;
            var status = response.StatusCode;
            var retry = response.Headers.RetryAfter;
            response.Dispose();
            if (Transient.Contains(status) && attempt < 4)
            {
                var delay = retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(1 << attempt);
                if (delay <= TimeSpan.FromMinutes(2))
                {
                    await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, ct).ConfigureAwait(false);
                    continue;
                }
            }
            int code = (int)status;
            var message = code switch
            {
                401 => "Microsoft 365 pide volver a conectar la cuenta.",
                403 => "La organización ha denegado la lectura. Comprueba la autorización de CorreoDiario con Informática.",
                404 or 410 => "El elemento ya no está disponible en la ubicación consultada.",
                429 or 503 => "Microsoft 365 ha limitado temporalmente la lectura. Vuelve a exportar más tarde.",
                _ => $"Microsoft 365 respondió con HTTP {code}.",
            };
            throw new MailboxException("HTTP " + code, message, code);
        }
    }

    async Task<JsonDocument> Json(Uri uri, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        ct = deadline.Token;
        using var response = await Get(uri, "application/json", ct).ConfigureAwait(false);
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    async Task Pages(string query, Action<JsonElement> add, CancellationToken ct)
    {
        string? next = Base + query;
        var path = new Uri(next).AbsolutePath;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (next != null)
        {
            var uri = SafeUri(next, path);
            if (!visited.Add(uri.AbsoluteUri)) throw Invalid("Microsoft 365 repitió una página. El listado está incompleto.");
            using var page = await Json(uri, ct).ConfigureAwait(false);
            var root = page.RootElement;
            if (!root.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
                throw Invalid("Microsoft 365 no devolvió la lista solicitada.");
            foreach (var item in items.EnumerateArray()) { ct.ThrowIfCancellationRequested(); add(item); }
            next = root.TryGetProperty("@odata.nextLink", out var link) ? link.GetString() : null;
        }
    }

    public async Task<List<CloudFolder>> Folders(CancellationToken ct)
    {
        var result = new List<CloudFolder>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<(string Collection, string Prefix)>();
        pending.Enqueue(("mailFolders", ""));
        while (pending.TryDequeue(out var parent))
        {
            await Pages(parent.Collection + "?$select=id,displayName,childFolderCount&$top=100", item =>
            {
                var id = Required(item, "id");
                if (!seen.Add(id)) return;
                string path = parent.Prefix + Required(item, "displayName");
                result.Add(new(id, path));
                if (!item.TryGetProperty("childFolderCount", out var count) || !count.TryGetInt32(out int children))
                    throw Invalid("Microsoft 365 no indicó si la carpeta contiene subcarpetas.");
                if (children > 0) pending.Enqueue(("mailFolders/" + Segment(id) + "/childFolders", path + " / "));
            }, ct).ConfigureAwait(false);
        }
        return result.OrderBy(x => x.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<List<CloudMessage>> Messages(CloudFolder folder, CancellationToken ct)
    {
        var items = new Dictionary<string, CloudMessage>(StringComparer.Ordinal);
        await Pages("mailFolders/" + Segment(folder.Id) + "/messages?$top=100&$select=id,subject,changeKey,parentFolderId,conversationId,receivedDateTime,internetMessageId,hasAttachments", item =>
        {
            var id = Required(item, "id");
            var parent = Required(item, "parentFolderId");
            if (parent != folder.Id) throw Invalid("La carpeta cambió durante la lectura. Vuelve a cargarla.");
            items[id] = new(id, parent, folder.Path, Field(item, "subject"), Required(item, "changeKey"),
                Field(item, "conversationId"), DateTimeOffset.TryParse(Field(item, "receivedDateTime"), out var date) ? date : null,
                MailReader.NormalizeId(Field(item, "internetMessageId")), Bool(item, "hasAttachments"));
        }, ct).ConfigureAwait(false);
        return items.Values.ToList();
    }

    public async Task<CloudContent> Read(CloudMessage message, CancellationToken ct)
    {
        string path = MessagePath(message.Id);
        var uri = new Uri(Base + path + "/$value");
        byte[] bytes;
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            using var response = await Get(SafeUri(uri.AbsoluteUri, uri.AbsolutePath), "*/*", deadline.Token).ConfigureAwait(false);
            bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long expected && bytes.LongLength != expected)
                throw Invalid("La lectura del correo quedó incompleta.");
        }
        var mail = MailReader.Inspect(MailInput.FromMemory(bytes, false), new("Microsoft 365", message.Folder, message.Conversation, "Carpeta autorizada"));
        if (message.InternetId.Length > 0 && mail.Id != message.InternetId)
            throw Invalid("La identidad del mensaje recibido no coincide con la del listado.");
        var content = MailReader.ReadContent(mail);
        var attachments = new List<CloudAttachment>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        await Pages(path + "/attachments?$select=id,name,size,isInline,contentType", item =>
        {
            if (ids.Add(Required(item, "id"))) attachments.Add(new(Required(item, "id"), Field(item, "name"), Field(item, "@odata.type"),
                item.TryGetProperty("size", out var size) && size.TryGetInt64(out var n) ? n : null, Bool(item, "isInline"), Field(item, "contentType")));
        }, ct).ConfigureAwait(false);
        if (message.HasAttachments && attachments.Count == 0)
            throw Invalid("Microsoft 365 anunció adjuntos pero devolvió una lista vacía.");
        await VerifyParts(path, attachments, content, ct).ConfigureAwait(false);
        var check = new Uri(Base + path + "?$select=id,parentFolderId,changeKey");
        using var current = await Json(SafeUri(check.AbsoluteUri, check.AbsolutePath), ct).ConfigureAwait(false);
        if (Required(current.RootElement, "id") != message.Id || Required(current.RootElement, "parentFolderId") != message.FolderId
            || Required(current.RootElement, "changeKey") != message.Revision)
            throw Invalid("El mensaje cambió de carpeta o de versión durante la exportación. Vuelve a exportar la carpeta.");
        return new(mail, content);
    }

    async Task VerifyParts(string path, List<CloudAttachment> expected, Content content, CancellationToken ct)
    {
        var remaining = content.Parts.ToList();
        foreach (var item in expected)
        {
            if (item.Kind == "#microsoft.graph.referenceAttachment")
            {
                content.Notices.Add("Archivo enlazado sin copia de su contenido · " + item.Name);
                continue;
            }
            if (item.Kind is not ("#microsoft.graph.fileAttachment" or "#microsoft.graph.itemAttachment"))
            {
                content.Notices.Add("Tipo de adjunto pendiente de comprobar · " + item.Name);
                continue;
            }
            bool Match(FilePart part) => string.Equals(part.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                || item.Kind == "#microsoft.graph.itemAttachment" && string.Equals(part.Name, item.Name + ".eml", StringComparison.OrdinalIgnoreCase);
            var match = remaining.FindIndex(p => Match(p) && (item.Kind == "#microsoft.graph.itemAttachment" || item.Size == p.Bytes.LongLength));
            if (match >= 0) { remaining.RemoveAt(match); continue; }
            if (item.Kind != "#microsoft.graph.fileAttachment" || string.IsNullOrWhiteSpace(item.Name))
            {
                content.Notices.Add("Adjunto anunciado pendiente de verificar · " + item.Name);
                continue;
            }
            // Some renditions omit a file from MIME. Ask Graph for that exact attachment.
            var raw = new Uri(Base + path + "/attachments/" + Segment(item.Id) + "/$value");
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromMinutes(10));
                using var response = await Get(SafeUri(raw.AbsoluteUri, raw.AbsolutePath), "*/*", deadline.Token).ConfigureAwait(false);
                var data = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
                if (data.LongLength != item.Size || response.Content.Headers.ContentLength is long length && data.LongLength != length)
                {
                    content.Notices.Add("Tamaño de adjunto pendiente de verificar · " + item.Name);
                    continue;
                }
                // Preserve distinct MIME parts if two files happen to share a name.
                if (remaining.Any(Match)) content.Notices.Add("Versiones distintas de un adjunto con el mismo nombre · " + item.Name);
                content.Parts.Add(new(item.Name, item.Mime, "", "", item.Inline, data));
            }
            catch (Exception e) when (!ct.IsCancellationRequested && e is MailboxException or HttpRequestException or IOException or OperationCanceledException)
            {
                var reason = e is MailboxException failure ? failure.Reason : e.GetType().Name;
                content.Notices.Add("Adjunto pendiente de recuperar · " + item.Name + " · " + reason);
            }
        }
    }
}
