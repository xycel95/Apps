using System.Text.Json;
using CorreoDiario.Core;

internal static class OwaChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Outlook: paginación y fecha local con cambio de hora", async () =>
        {
            var transport = new FixtureTransport();
            var findCalls = 0;
            transport.Responder = (action, request) =>
            {
                if (action != "FindItem") return FolderResponse(action, request);
                var restriction = request.GetProperty("Restriction");
                var comparisons = restriction.GetProperty("Items");
                var from = comparisons[0].GetProperty("FieldURIOrConstant").GetProperty("Item").GetProperty("Value").GetString();
                var until = comparisons[1].GetProperty("FieldURIOrConstant").GetProperty("Item").GetProperty("Value").GetString();
                Require(DateTimeOffset.Parse(until!) - DateTimeOffset.Parse(from!) == TimeSpan.FromHours(23), "Día de 23 horas en Madrid");
                findCalls++;
                var offset = request.GetProperty("Paging").GetProperty("Offset").GetInt32();
                Require(offset == findCalls - 1, "Offset devuelto por el servidor");
                return JsonSerializer.SerializeToElement(new { Body = new { ResponseMessages = new { Items = new[] { new { ResponseCode = "NoError", RootFolder = new {
                    IncludesLastItemInRange = findCalls == 2, IndexedPagingOffset = findCalls,
                    Items = new[] { Message("m" + findCalls, "2026-03-29T12:00:00Z") }
                } } } } } });
            };
            var source = new OwaSource(transport);
            var folders = await source.FoldersAsync(default);
            var result = await source.ListAsync([folders[0]], new(2026, 3, 29), TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"), default);
            Require(result.Count == 2 && findCalls == 2, "Todas las páginas");
        });
        yield return ("Outlook: paginación desconocida impide una selección parcial", async () =>
        {
            var transport = new FixtureTransport { Responder = (a, r) => a == "FindItem"
                ? JsonSerializer.SerializeToElement(new { Body = new { RootFolder = new { Items = new[] { Message("m", "2026-11-21T12:00:00Z") } } } })
                : FolderResponse(a, r) };
            var source = new OwaSource(transport);
            var folders = await source.FoldersAsync(default);
            await Fails(() => source.ListAsync([folders[0]], new(2026, 11, 21), TimeZoneInfo.Utc, default));
        });
        yield return ("Outlook: no acepta adjuntos anunciados sin lista", async () =>
        {
            var transport = new FixtureTransport { Responder = (_, _) => JsonSerializer.SerializeToElement(new { Body = new { Items = new[] { Message("m", "2026-11-21T12:00:00Z", true) } } }) };
            await Fails(() => new OwaSource(transport).ReadAsync(Snapshot(), default));
        });
        yield return ("Outlook: valida la identidad del adjunto descargado", async () =>
        {
            var transport = new FixtureTransport { Responder = (_, _) => JsonSerializer.SerializeToElement(new { Body = new { Attachments = new[] {
                new { AttachmentId = new { Id = "diferente" }, Content = "b3JpZ2luYWw=" }
            } } }) };
            await Fails(() => new OwaSource(transport).AttachmentAsync(Snapshot(), new("correcto", "a.pdf", 8, "application/pdf", false, ""), default));
        });
        yield return ("Outlook: conserva un adjunto original de cero bytes", async () =>
        {
            var transport = new FixtureTransport { Responder = (_, _) => JsonSerializer.SerializeToElement(new { Body = new { Attachments = new[] {
                new { AttachmentId = new { Id = "a" }, Content = "" }
            } } }) };
            var result = await new OwaSource(transport).AttachmentAsync(Snapshot(), new("a", "vacio.txt", 0, "text/plain", false, ""), default);
            Require(result.Length == 0, "Archivo vacío legítimo");
        });
        yield return ("Outlook: movimiento a Eliminados con versión y sin borrado definitivo", async () =>
        {
            var transport = new FixtureTransport { Responder = (action, request) =>
            {
                Require(action == "MoveItem", "Solo movimiento");
                Require(request.GetProperty("ToFolderId").GetProperty("BaseFolderId").GetProperty("Id").GetString() == "deleteditems", "Destino Eliminados");
                Require(request.GetProperty("ItemIds")[0].GetProperty("ChangeKey").GetString() == "rev1", "Control de versión");
                return JsonSerializer.SerializeToElement(new { Body = new { Items = new[] { new { ItemId = new { Id = "nuevoid" } } } } });
            } };
            await new OwaSource(transport).MoveToDeletedAsync(Snapshot(), default);
            Require(transport.Calls == 1, "Una sola petición");
        });
        yield return ("Outlook: no reintenta un movimiento no confirmado", async () =>
        {
            var transport = new FixtureTransport { Responder = (_, _) => JsonSerializer.SerializeToElement(new { Body = new { Items = System.Array.Empty<object>() } }) };
            await Fails(async () => { await new OwaSource(transport).MoveToDeletedAsync(Snapshot(), default); return true; });
            Require(transport.Calls == 1, "Sin reintentos automáticos");
        });
    }

    private static object Message(string id, string date, bool hasAttachments = false) => new
    {
        ItemId = new { Id = id, ChangeKey = "rev1" }, ParentFolderId = new { Id = "inbox" }, Subject = "Asunto",
        DateTimeReceived = date, DateTimeSent = date, From = new { Mailbox = new { EmailAddress = "ana@ejemplo.es" } },
        Body = new { Value = "<p>Correo completo</p>" }, HasAttachments = hasAttachments
    };
    private static JsonElement FolderResponse(string action, JsonElement request)
    {
        if (action == "FindFolder") return JsonSerializer.SerializeToElement(new { Body = new { RootFolder = new { IncludesLastItemInRange = true, Folders = System.Array.Empty<object>() } } });
        var id = request.GetProperty("FolderIds")[0].GetProperty("Id").GetString();
        return JsonSerializer.SerializeToElement(new { Body = new { Folders = new[] { new { FolderId = new { Id = id }, DisplayName = id } } } });
    }
    private static MailInfo Snapshot() => new("m", "rev1", "inbox", "Asunto", "ana@ejemplo.es", "rafa@ejemplo.es", "", DateTimeOffset.Parse("2026-11-21T12:00:00Z"), false, "", []);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Fails<T>(Func<Task<T>> operation)
    {
        try { await operation(); } catch (InvalidDataException) { return; } catch (InvalidOperationException) { return; }
        throw new Exception("Debía detenerse");
    }
    private sealed class FixtureTransport : IOutlookTransport
    {
        public Func<string, JsonElement, JsonElement> Responder { get; set; } = (_, _) => throw new InvalidOperationException("Respuesta no configurada");
        public int Calls;
        public Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Responder(action, JsonSerializer.SerializeToElement(body)));
        }
    }
}
