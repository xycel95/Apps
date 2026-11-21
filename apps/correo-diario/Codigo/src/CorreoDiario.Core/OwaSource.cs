using System.Globalization;
using System.Text.Json;
namespace CorreoDiario.Core;

public sealed class OwaSource(IOutlookTransport session) : IMailSource
{
    private readonly Dictionary<string, MailFolder> foldersById = new(StringComparer.Ordinal);
    private static Dictionary<string, object> D(params (string Key, object Value)[] fields) => fields.ToDictionary(x => x.Key, x => x.Value);
    private static object Distinguished(string name) => D(("__type", "DistinguishedFolderId:#Exchange"), ("Id", name));
    private static object FolderId(string id) => D(("__type", "FolderId:#Exchange"), ("Id", id));
    private static object ItemId(string id, string? revision = null)
    {
        var d = D(("__type", "ItemId:#Exchange"), ("Id", id));
        if (!string.IsNullOrEmpty(revision)) d["ChangeKey"] = revision;
        return d;
    }
    private static object Shape(string type, string baseShape, params string[] fields) => D(("__type", type + ":#Exchange"),
        ("BaseShape", baseShape), ("AdditionalProperties", fields.Select(f => D(("__type", "PropertyUri:#Exchange"), ("FieldURI", f))).ToArray()));
    private static object Paging(int offset) => D(("__type", "IndexedPageView:#Exchange"), ("BasePoint", "Beginning"), ("Offset", offset), ("MaxEntriesReturned", 100));

    public async Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct)
    {
        var result = new List<MailFolder>();
        foreach (var name in new[] { "inbox", "sentitems" })
        {
            var json = await session.ActionAsync("GetFolder", D(("__type", "GetFolderRequest:#Exchange"),
                ("FolderShape", Shape("FolderResponseShape", "Default")), ("FolderIds", new[] { Distinguished(name) })), ct);
            var folders = Results(json, "Folders");
            if (folders.Count != 1) throw new InvalidDataException("Outlook no ha devuelto una carpeta identificable.");
            var f = folders[0];
            var id = Text(Child(f, "FolderId"), "Id");
            if (string.IsNullOrEmpty(id)) throw new InvalidDataException("Falta el identificador de la carpeta.");
            var folder = new MailFolder(id, Text(f, "DisplayName", name == "inbox" ? "Bandeja de entrada" : "Enviados"), name == "sentitems");
            foldersById[id] = folder;
            result.Add(folder);
        }
        // List other user mail folders. A failure leaves the two verified system folders available.
        try
        {
            for (var offset = 0; offset < 100000; offset += 100)
            {
                var json = await session.ActionAsync("FindFolder", D(("__type", "FindFolderRequest:#Exchange"),
                    ("FolderShape", Shape("FolderResponseShape", "Default", "folder:FolderClass")), ("Traversal", "Deep"),
                    ("Paging", Paging(offset)), ("ParentFolderIds", new[] { Distinguished("msgfolderroot") })), ct);
                var root = Root(json);
                var items = Array(root, "Folders");
                foreach (var f in items)
                {
                    var folderClass = Text(f, "FolderClass");
                    if (folderClass.Length > 0 && folderClass != "IPF.Note") continue;
                    var id = Text(Child(f, "FolderId"), "Id");
                    if (id.Length == 0 || foldersById.ContainsKey(id)) continue;
                    var folder = new MailFolder(id, Text(f, "DisplayName", "Carpeta"));
                    foldersById[id] = folder;
                    result.Add(folder);
                }
                if (LastPage(root, offset, items.Count)) break;
                if (items.Count == 0) throw new InvalidDataException("La lista de carpetas está incompleta.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            OtherFoldersNotice = "Solo se han podido cargar Entrada y Enviados. Las demás carpetas no están disponibles en esta conexión.";
        }
        return result;
    }
    public string? OtherFoldersNotice { get; private set; }

    public async Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var startLocal = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone).ToString("O", CultureInfo.InvariantCulture);
        var end = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), zone).ToString("O", CultureInfo.InvariantCulture);
        var result = new Dictionary<string, MailInfo>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            var field = folder.Sent ? "item:DateTimeSent" : "item:DateTimeReceived";
            object Comparison(string name, string value) => D(("__type", name + ":#Exchange"),
                ("Item", D(("__type", "PropertyUri:#Exchange"), ("FieldURI", field))),
                ("FieldURIOrConstant", D(("Item", D(("__type", "ConstantValue:#Exchange"), ("Value", value))))));
            var restriction = D(("__type", "And:#Exchange"),
                ("Items", new[] { Comparison("IsGreaterThanOrEqualTo", start), Comparison("IsLessThan", end) }));
            var offset = 0;
            for (var page = 0; page < 10000; page++)
            {
                ct.ThrowIfCancellationRequested();
                var json = await session.ActionAsync("FindItem", D(("__type", "FindItemRequest:#Exchange"),
                    ("ItemShape", Shape("ItemResponseShape", "Default", "item:DateTimeReceived", "item:DateTimeSent", "message:From", "message:ToRecipients")),
                    ("Traversal", "Shallow"), ("ViewFilter", "All"), ("Paging", Paging(offset)),
                    ("Restriction", restriction), ("ParentFolderIds", new[] { FolderId(folder.Id) })), ct);
                var root = Root(json);
                var items = Array(root, "Items");
                foreach (var item in items)
                {
                    if (item.TryGetProperty("IsDraft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
                    var mail = Parse(item, folder);
                    var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(mail.Timestamp, zone).DateTime);
                    if (day == date) result[mail.Id] = mail;
                }
                if (LastPage(root, offset, items.Count)) break;
                if (items.Count == 0 || page == 9999) throw new InvalidDataException("Outlook ha devuelto una lista incompleta. No se exporta una selección parcial.");
                offset = root.TryGetProperty("IndexedPagingOffset", out var next) && next.TryGetInt32(out var n) && n > offset ? n : offset + items.Count;
            }
        }
        return result.Values.OrderBy(m => m.Timestamp).ToList();
    }

    public async Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct)
    {
        var shape = (Dictionary<string, object>)Shape("ItemResponseShape", "AllProperties", "item:Body", "item:Attachments", "item:ParentFolderId");
        shape["BodyType"] = "HTML";
        shape["IncludeMimeContent"] = false;
        var json = await session.ActionAsync("GetItem", D(("__type", "GetItemRequest:#Exchange"),
            ("ItemShape", shape), ("ItemIds", new[] { ItemId(item.Id) })), ct);
        var mails = Results(json, "Items");
        if (mails.Count != 1) throw new InvalidDataException("Outlook no ha devuelto el correo completo.");
        var mail = Parse(mails[0], foldersById.GetValueOrDefault(item.FolderId) ?? new(item.FolderId, "", item.Sent));
        if (!mails[0].TryGetProperty("Body", out var body) || body.ValueKind is not JsonValueKind.Object || !body.TryGetProperty("Value", out _))
            throw new InvalidDataException("El contenido de este correo no está disponible para exportar. Puede estar protegido o cifrado.");
        if (!mails[0].TryGetProperty("HasAttachments", out var hasAttachments) || hasAttachments.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Outlook no permite comprobar la lista completa de adjuntos de este correo.");
        if (hasAttachments.GetBoolean() && mail.Attachments.Count == 0)
            throw new InvalidDataException("Outlook indica que hay adjuntos, pero no ha devuelto su lista. Se conserva el correo.");
        var itemClass = Text(mails[0], "ItemClass");
        if (itemClass.Contains("SMIME", StringComparison.OrdinalIgnoreCase) || itemClass.Contains("rpmsg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Este correo está cifrado o protegido. No se considera verificado ni se borra automáticamente.");
        return mail;
    }

    public async Task<byte[]> AttachmentAsync(MailInfo mail, AttachmentInfo attachment, CancellationToken ct)
    {
        var json = await session.ActionAsync("GetAttachment", D(("__type", "GetAttachmentRequest:#Exchange"),
            ("AttachmentShape", D(("IncludeMimeContent", true))),
            ("AttachmentIds", new[] { D(("__type", "AttachmentId:#Exchange"), ("Id", attachment.Id)) })), ct);
        var values = Results(json, "Attachments");
        if (values.Count != 1) throw new InvalidDataException("No se ha podido descargar un adjunto completo.");
        var value = values[0];
        if (Text(Child(value, "AttachmentId"), "Id") is string id && id.Length > 0 && id != attachment.Id)
            throw new InvalidDataException("Outlook ha devuelto un adjunto distinto del solicitado.");
        var content = Text(value, "Content");
        if (content.Length == 0 && value.TryGetProperty("Item", out var nested)) content = Text(Child(nested, "MimeContent"), "Value");
        if (content.Length == 0 && attachment.Size != 0)
            throw new InvalidDataException($"El adjunto «{attachment.Name}» es un enlace, un elemento protegido o no contiene un archivo descargable. No se borra el correo.");
        return Convert.FromBase64String(content);
    }

    public async Task MoveToDeletedAsync(MailInfo item, CancellationToken ct)
    {
        var json = await session.ActionAsync("MoveItem", D(("__type", "MoveItemRequest:#Exchange"),
            ("ToFolderId", D(("__type", "TargetFolderId:#Exchange"), ("BaseFolderId", Distinguished("deleteditems")))),
            ("ItemIds", new[] { ItemId(item.Id, item.Revision) }), ("ReturnNewItemIds", true)), ct);
        var values = Results(json, "Items");
        if (values.Count != 1 || Text(Child(values[0], "ItemId"), "Id").Length == 0)
            throw new InvalidOperationException("Outlook no ha confirmado el movimiento a Eliminados. Comprueba el buzón antes de repetirlo; no se reintenta automáticamente.");
    }

    private static MailInfo Parse(JsonElement item, MailFolder folder)
    {
        var id = Text(Child(item, "ItemId"), "Id");
        if (id.Length == 0) throw new InvalidDataException("Hay un correo sin identificador.");
        var rawDate = Text(item, folder.Sent ? "DateTimeSent" : "DateTimeReceived");
        if (!DateTimeOffset.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            throw new InvalidDataException("Outlook no ha proporcionado la fecha completa de un correo.");
        var attachments = new List<AttachmentInfo>();
        foreach (var a in Array(item, "Attachments"))
        {
            var type = Text(a, "__type");
            if (type.Contains("ReferenceAttachment", StringComparison.Ordinal))
                throw new InvalidDataException("Un correo contiene un adjunto enlazado a la nube. Esta versión no puede verificar ese archivo y conservará el correo.");
            var aid = Text(Child(a, "AttachmentId"), "Id");
            if (aid.Length == 0) throw new InvalidDataException("Un adjunto no tiene identificador descargable.");
            long? size = a.TryGetProperty("Size", out var s) && s.TryGetInt64(out var v) ? v : null;
            var name = Text(a, "Name", "adjunto");
            if (type.Contains("ItemAttachment", StringComparison.Ordinal)) { name += ".eml"; size = null; }
            attachments.Add(new(aid, name, size, Text(a, "ContentType", "application/octet-stream"),
                a.TryGetProperty("IsInline", out var inline) && inline.ValueKind == JsonValueKind.True, Text(a, "ContentId")));
        }
        return new(id, Text(Child(item, "ItemId"), "ChangeKey"), Text(Child(item, "ParentFolderId"), "Id", folder.Id),
            Text(item, "Subject", "Sin asunto"), Address(Child(item, "From")), Recipients(item, "ToRecipients"), Recipients(item, "CcRecipients"),
            date, folder.Sent, Text(Child(item, "Body"), "Value"), attachments,
            DateTimeOffset.TryParse(Text(item, "DateTimeSent"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sentAt) ? sentAt : null,
            Recipients(item, "BccRecipients"));
    }

    private static string Address(JsonElement e) => Text(Child(e, "Mailbox"), "EmailAddress", Text(e, "EmailAddress", Text(Child(e, "Mailbox"), "Name", Text(e, "Name"))));
    private static string Recipients(JsonElement e, string key) => string.Join("; ", Array(e, key).Select(Address));
    private static JsonElement Child(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var child) ? child : default;
    private static string Text(JsonElement e, string key, string fallback = "") => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static List<JsonElement> Array(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.Clone()).ToList() : [];
    private static List<JsonElement> Responses(JsonElement r) => Array(Child(Child(r, "Body"), "ResponseMessages"), "Items");
    private static List<JsonElement> Results(JsonElement r, string key)
    {
        var body = Child(r, "Body");
        var direct = Array(body, key);
        if (direct.Count > 0) return direct;
        return Responses(r).SelectMany(x => Array(x, key)).ToList();
    }
    private static JsonElement Root(JsonElement r)
    {
        var body = Child(r, "Body");
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("RootFolder", out var direct)) return direct;
        var roots = Responses(r).Where(x => x.TryGetProperty("RootFolder", out _)).ToList();
        if (roots.Count != 1) throw new InvalidDataException("No se ha podido comprobar la paginación de Outlook.");
        return roots[0].GetProperty("RootFolder");
    }
    private static bool LastPage(JsonElement root, int offset, int count)
    {
        if (root.TryGetProperty("IncludesLastItemInRange", out var last) && last.ValueKind is JsonValueKind.True or JsonValueKind.False) return last.GetBoolean();
        if (root.TryGetProperty("TotalItemsInView", out var total) && total.TryGetInt32(out var n)) return offset + count >= n;
        throw new InvalidDataException("Outlook no permite comprobar que se han obtenido todos los elementos.");
    }
}
