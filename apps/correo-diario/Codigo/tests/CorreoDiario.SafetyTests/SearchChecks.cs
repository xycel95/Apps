using System.Runtime.Serialization;
using System.Text.Json;
using CorreoDiario.Core;

internal static class SearchChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Búsqueda: el contrato rechaza con 400 el filtro antiguo sin Item", () =>
        {
            using var old = JsonDocument.Parse("""{"Restriction":{"__type":"And:#Exchange","Items":[]}}""");
            Reject(old.RootElement); return Task.CompletedTask;
        });
        yield return ("Búsqueda: el contrato rechaza el nombre ConstantValue", () =>
        {
            using var old = JsonDocument.Parse("""
                {"Restriction":{"Item":{"__type":"And:#Exchange","Items":[
                  {"__type":"IsGreaterThanOrEqualTo:#Exchange","Item":{"__type":"PropertyUri:#Exchange","FieldURI":"item:DateTimeReceived"},"FieldURIOrConstant":{"Item":{"__type":"ConstantValue:#Exchange","Value":"2026-11-19T23:00:00Z"}}},
                  {"__type":"IsLessThan:#Exchange","Item":{"__type":"PropertyUri:#Exchange","FieldURI":"item:DateTimeReceived"},"FieldURIOrConstant":{"Item":{"__type":"ConstantValue:#Exchange","Value":"2026-11-20T23:00:00Z"}}}
                ]}}}
                """);
            Reject(old.RootElement); return Task.CompletedTask;
        });
        yield return ("Búsqueda: el contrato acepta la petición generada sin leer otra carpeta", async () =>
        {
            var transport = new SearchTransport();
            var result = await new OwaSource(transport).ListAsync([new("custom", "Carpeta")], new(2026, 11, 20), Madrid, default);
            Require(result.Count == 0 && transport.Folders.SequenceEqual(["custom"]), "Carpeta solicitada y resultado vacío completo");
        });
        foreach (var (date, hours) in new[] { (new DateOnly(2026, 11, 20), 24), (new DateOnly(2026, 3, 29), 23), (new DateOnly(2026, 10, 25), 25) })
        {
            yield return ($"Búsqueda: día de {hours} horas, inicio incluido y fin excluido", async () =>
            {
                var transport = new SearchTransport { ItemsAtBoundaries = true };
                var items = await new OwaSource(transport).ListAsync([new("inbox", "Entrada")], date, Madrid, default);
                Require(transport.Ranges.Single().End - transport.Ranges.Single().Start == TimeSpan.FromHours(hours), "Límites UTC del día local");
                Require(items.Select(m => m.Id).SequenceEqual(["inbox-start", "inbox-last"]), "No incluye correos de otros días");
            });
        }
        yield return ("Búsqueda: Enviados usa fecha de envío y otras carpetas fecha de recepción", async () =>
        {
            var transport = new SearchTransport();
            await new OwaSource(transport).ListAsync([new("sent", "Enviados", true), new("custom", "Carpeta")], new(2026, 11, 20), Madrid, default);
            Require(transport.Folders.SequenceEqual(["sent", "custom"]), "Todas las carpetas seleccionadas");
            Require(transport.Ranges.Select(r => r.Field).SequenceEqual(["item:DateTimeSent", "item:DateTimeReceived"]), "Campo de fecha correspondiente");
        });
        yield return ("Búsqueda: un 400 en la segunda carpeta impide un resultado parcial", async () =>
        {
            var transport = new SearchTransport { FailSecond = true, ItemsAtBoundaries = true };
            try { await new OwaSource(transport).ListAsync([new("inbox", "Entrada"), new("custom", "Carpeta")], new(2026, 11, 20), Madrid, default); }
            catch (InvalidOperationException) { Require(transport.Folders.Count == 2, "Falló la segunda carpeta"); return; }
            throw new Exception("No debe mostrar una selección parcial");
        });
        yield return ("Búsqueda: usa el resumen de destinatarios que admite FindItem", async () =>
        {
            var transport = new SearchTransport { ItemsAtBoundaries = true };
            var result = await new OwaSource(transport).ListAsync([new("sent", "Enviados", true)], new(2026, 11, 20), Madrid, default);
            Require(result.All(m => m.To == "Destino de prueba"), "Resumen visible sin pedir listas completas de destinatarios");
        });
    }
    private static TimeZoneInfo Madrid => TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static void Reject(JsonElement value)
    {
        try { OwaSearchContract.Read(value); } catch (SerializationException) { return; }
        throw new Exception("El contrato debe rechazar la petición antigua");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class SearchTransport : IOutlookTransport
    {
        public List<string> Folders { get; } = [];
        public List<OwaSearchContract.Range> Ranges { get; } = [];
        public bool ItemsAtBoundaries, FailSecond;
        public Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
        {
            Require(action == "FindItem", "Solo lectura de búsqueda");
            var request = JsonSerializer.SerializeToElement(body);
            var fields = request.GetProperty("ItemShape").GetProperty("AdditionalProperties").EnumerateArray().Select(p => p.GetProperty("FieldURI").GetString()).ToArray();
            Require(!fields.Contains("message:ToRecipients") && fields.Contains("item:DisplayTo"), "FindItem devuelve resúmenes; GetItem obtiene los destinatarios completos");
            var range = OwaSearchContract.Read(request); Ranges.Add(range);
            var folder = request.GetProperty("ParentFolderIds")[0].GetProperty("Id").GetString()!; Folders.Add(folder);
            if (FailSecond && Folders.Count == 2) throw new InvalidOperationException("Outlook ha rechazado la búsqueda de correos (400).");
            object Message(string id, DateTimeOffset timestamp) => new { ItemId = new { Id = folder + "-" + id, ChangeKey = "fixture-rev" },
                ParentFolderId = new { Id = folder }, Subject = "Prueba", DisplayTo = "Destino de prueba", DateTimeReceived = timestamp.ToString("O"), DateTimeSent = timestamp.ToString("O") };
            var items = ItemsAtBoundaries ? new[] { Message("before", range.Start.AddTicks(-1)), Message("start", range.Start), Message("last", range.End.AddTicks(-1)), Message("after", range.End) } : [];
            return Task.FromResult(JsonSerializer.SerializeToElement(new { Body = new { RootFolder = new { IncludesLastItemInRange = true, Items = items } } }));
        }
    }
}
