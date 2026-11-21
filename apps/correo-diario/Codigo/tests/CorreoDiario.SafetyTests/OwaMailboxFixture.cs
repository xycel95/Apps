using System.Text.Json;
using CorreoDiario.Core;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

// Synthetic mailbox, independent of the request builder. Never loads user data.
internal sealed class OwaMailboxFixture
{
    public int Searches, FullReads, AttachmentReads, Moves;
    private static Dictionary<string, object> D(params (string Key, object Value)[] fields) => fields.ToDictionary(x => x.Key, x => x.Value);
    private static object Reply(string name, object value) => new { Body = new { ResponseMessages = new { Items = new[] { D(("ResponseCode", "NoError"), (name, value)) } } } };
    public object Handle(string action, JsonElement request)
    {
        if (action == "GetFolder")
        {
            var id = request.GetProperty("FolderIds")[0].GetProperty("Id").GetString();
            return Reply("Folders", new[] { new { FolderId = new { Id = "fixture-" + id }, DisplayName = id } });
        }
        if (action == "FindFolder") return Reply("RootFolder", new { IncludesLastItemInRange = true,
            Folders = new[] { new { FolderId = new { Id = "fixture-custom" }, DisplayName = "Carpeta de prueba", FolderClass = "IPF.Note" } } });
        if (action == "FindItem")
        {
            var range = OwaSearchContract.Read(request);
            var folder = request.GetProperty("ParentFolderIds")[0].GetProperty("Id").GetString();
            if (folder is not ("fixture-inbox" or "fixture-sentitems" or "fixture-custom")) throw new InvalidDataException("Carpeta de prueba desconocida.");
            var expected = folder == "fixture-sentitems" ? "item:DateTimeSent" : "item:DateTimeReceived";
            if (range.Field != expected) throw new InvalidDataException("Campo de fecha incorrecto.");
            Interlocked.Increment(ref Searches);
            var offset = request.GetProperty("Paging").GetProperty("Offset").GetInt32();
            var field = range.Field.Split(':')[1];
            var matches = Messages().Where(m => ((Dictionary<string, object>)m["ParentFolderId"])["Id"].ToString() == folder)
                .Where(m => DateTimeOffset.Parse((string)m[field]) >= range.Start && DateTimeOffset.Parse((string)m[field]) < range.End).ToList();
            var page = matches.Skip(offset).Take(1).Select(Summary).ToArray();
            return Reply("RootFolder", new { IncludesLastItemInRange = offset + page.Length >= matches.Count,
                IndexedPagingOffset = offset + page.Length, Items = page });
        }
        if (action == "GetItem")
        {
            Interlocked.Increment(ref FullReads);
            var id = request.GetProperty("ItemIds")[0].GetProperty("Id").GetString();
            return Reply("Items", new[] { Messages().Single(m => ((Dictionary<string, object>)m["ItemId"])["Id"].ToString() == id) });
        }
        if (action == "GetAttachment")
        {
            var id = OwaAttachmentContract.Read(request);
            Interlocked.Increment(ref AttachmentReads);
            if (id is null || !id.StartsWith("attachment-fixture-")) throw new InvalidDataException("Adjunto de prueba desconocido.");
            return Reply("Attachments", new[] { new { AttachmentId = new { Id = id }, Content = "b3JpZ2luYWw=" } });
        }
        if (action == "MoveItem") { Interlocked.Increment(ref Moves); throw new InvalidOperationException("La prueba con borrado desmarcado no puede mover correos."); }
        throw new InvalidDataException("Operación de prueba desconocida.");
    }
    private static Dictionary<string, object> Summary(Dictionary<string, object> full)
    {
        var summary = full.Where(p => p.Key is not ("Body" or "Attachments" or "ToRecipients")).ToDictionary(p => p.Key, p => p.Value);
        summary["DisplayTo"] = "Destino de prueba";
        summary["From"] = D(("Mailbox", D(("Name", "Remitente de prueba"))));
        return summary;
    }
    private static IEnumerable<Dictionary<string, object>> Messages()
    {
        foreach (var (id, folder, received, sent) in new[] {
            ("fixture-in-1", "fixture-inbox", "2026-11-20T08:15:00Z", "2026-11-20T08:10:00Z"),
            ("fixture-in-2", "fixture-inbox", "2026-11-20T10:15:00Z", "2026-11-20T10:10:00Z"),
            ("fixture-sent-1", "fixture-sentitems", "2026-11-19T08:15:00Z", "2026-11-20T09:00:00Z"),
            ("fixture-custom-1", "fixture-custom", "2026-11-20T11:00:00Z", "2026-11-19T10:55:00Z"),
            ("fixture-last-day", "fixture-inbox", "2026-11-21T08:15:00Z", "2026-11-21T08:10:00Z"),
            ("fixture-other-day", "fixture-inbox", "2026-11-22T08:15:00Z", "2026-11-22T08:10:00Z") })
        {
            yield return D(("ItemId", D(("Id", id), ("ChangeKey", "fixture-rev"))), ("ParentFolderId", D(("Id", folder))),
                ("Subject", "Prueba de correo"), ("DateTimeReceived", received), ("DateTimeSent", sent), ("ItemClass", "IPM.Note"),
                ("From", D(("Mailbox", D(("EmailAddress", "remitente@example.invalid"))))),
                ("ToRecipients", new[] { D(("EmailAddress", "destino@example.invalid")) }),
                ("Body", D(("Value", "<p>Contenido completo</p>"))), ("HasAttachments", true),
                ("Attachments", new[] { D(("__type", "FileAttachment:#Exchange"), ("AttachmentId", D(("Id", "attachment-" + id))),
                    ("Name", "adjunto.txt"), ("Size", 8), ("ContentType", "text/plain"), ("IsInline", false)) }));
        }
    }
    public sealed class Renderer : IPdfRenderer
    {
        public Task<byte[]> RenderAsync(MailInfo mail, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
        {
            var builder = new PdfDocumentBuilder(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(595, 842).AddText("Contenido completo", 12, new PdfPoint(40, 760), font);
            return Task.FromResult(builder.Build());
        }
    }
}
