using System.Text.Json;
using CorreoDiario.Core;

internal static class PendingSelectionChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Pendientes: importa un registro anterior y asocia por ID aunque cambie el orden", () =>
        {
            using var env = new TestRun();
            var ids = PendingSelection.FromReceipt(Receipt());
            var selected = PendingSelection.Match(env.Source.Items.AsEnumerable().Reverse().ToArray(), ids);
            Require(ids.SequenceEqual(new[] { "m1" }) && selected.SetEquals(new[] { "m1" }));
            return Task.CompletedTask;
        });
        foreach (var invalid in new[] { "sin verificar", "movimiento confirmado", "movimiento incierto", "sin pendientes", "ID repetido", "ID ausente", "estructura incorrecta" })
        {
            yield return ("Pendientes: rechaza registro " + invalid, () =>
            {
                Reject(() => PendingSelection.FromReceipt(Receipt(invalid)));
                return Task.CompletedTask;
            });
        }
        yield return ("Pendientes: no aplica coincidencias parciales de otro listado o cuenta", () =>
        {
            using var env = new TestRun();
            Reject(() => PendingSelection.Match(env.Source.Items, new[] { "m1", "no-existe" }));
            Reject(() => PendingSelection.Match(env.Source.Items, new[] { "M1" }));
            return Task.CompletedTask;
        });
        yield return ("Pendientes: no acepta selección vacía, repetida o sin identidad", () =>
        {
            using var env = new TestRun();
            foreach (var ids in new[] { Array.Empty<string>(), new[] { "m1", "m1" }, new[] { "" } })
                Reject(() => PendingSelection.Match(env.Source.Items, ids));
            return Task.CompletedTask;
        });
        yield return ("Identificadores: copia explícita exacta con caracteres invisibles y sin cuerpo ni sesión", () =>
        {
            const string raw = "cid:%3Clogo%40example.invalid%3E\u200B";
            var attachment = new AttachmentInfo("ID_PRIVADO_NO_COPIAR", "NOMBRE_PRIVADO_NO_COPIAR.png", 1, "image/png", true, "<otro@example.invalid>");
            try { InlineImages.Resolve([raw], [attachment], new Dictionary<string, byte[]> { [attachment.Id] = [1] }); }
            catch (InvalidDataException ex)
            {
                var outer = new Exception("Aviso controlado", ex);
                outer.Data["Authorization"] = "SESION_NO_COPIAR";
                outer.Data["Html"] = "CUERPO_NO_COPIAR";
                outer.Data[ErrorReport.ExportContextDataKey] = new ExportErrorContext(8, 7, 2, 0, 1, "VERIFICANDO", false, 1, [1]);
                var report = ErrorReport.BuildImageIdentifiers(outer, "prueba")!;
                using var parsed = JsonDocument.Parse(report[report.IndexOf('{')..]);
                var details = parsed.RootElement.GetProperty("ImageIdentifiers");
                Require(details.GetProperty("References")[0].GetProperty("Reference").GetString() == raw);
                Require(details.GetProperty("Attachments")[0].GetProperty("ContentId").GetString() == attachment.ContentId);
                Require(report.Contains("\\u200B") && !report.Contains("NO_COPIAR") && parsed.RootElement.GetProperty("FailedMessageNumber").GetInt32() == 1);
                var ordinary = ErrorReport.Build(outer, "prueba", "Windows");
                Require(!ordinary.Contains("example.invalid") && !JsonSerializer.Serialize(ex.Data[InlineImages.FailureDataKey]).Contains("example.invalid"));
                return Task.CompletedTask;
            }
            throw new Exception("Debe conservar la evidencia de una referencia sin correspondencia.");
        });
        yield return ("Identificadores: no ofrece datos inexistentes en errores de otro tipo", () =>
        {
            Require(ErrorReport.BuildImageIdentifiers(new Exception("Error de prueba"), "prueba") is null);
            return Task.CompletedTask;
        });
    }

    private static string Receipt(string invalid = "") => invalid == "estructura incorrecta" ? "[]" : JsonSerializer.Serialize(new
    {
        Version = "0.4.7-beta", SavedCopiesVerified = invalid != "sin verificar",
        MovedToDeleted = invalid == "movimiento confirmado" ? new[] { "m2" } : Array.Empty<string>(),
        MoveInProgress = invalid == "movimiento incierto" ? "m2" : null,
        PendingMessages = invalid switch
        {
            "sin pendientes" => Array.Empty<object>(),
            "ID repetido" => new object[] { new { Id = "m1" }, new { Id = "m1" } },
            "ID ausente" => new object[] { new { Number = 1 } },
            _ => new object[] { new { Id = "m1", Number = 1 } }
        }
    });
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("No debe cambiar la selección con un registro o listado incoherente.");
    }
    private static void Require(bool ok) { if (!ok) throw new Exception("Selección de pendientes o informe explícito incorrectos."); }
}
