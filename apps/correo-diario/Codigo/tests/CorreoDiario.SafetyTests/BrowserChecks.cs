using System.Text.Json;
using CorreoDiario.Core;

internal static class BrowserChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Navegador: descriptor local y saltos de línea Windows", () =>
        {
            var endpoint = BrowserEndpoint.Parse("53911\r\n/devtools/browser/1234-abcd\r\n");
            Require(endpoint.AbsoluteUri == "ws://127.0.0.1:53911/devtools/browser/1234-abcd", "Conexión del equipo");
            return Task.CompletedTask;
        });
        yield return ("Navegador: rechaza rutas, puertos y destinos manipulados", () =>
        {
            foreach (var bad in new[] { "0\n/devtools/browser/a", "65536\n/devtools/browser/a", "9222\nhttps://otro.test/a", "9222\n/devtools/browser/a?token=x", "9222\n/devtools/browser/a\nextra" })
            {
                try { BrowserEndpoint.Parse(bad); } catch (InvalidDataException) { continue; }
                throw new Exception("Debía rechazar el descriptor");
            }
            Require(!BrowserEndpoint.IsLocal(new("ws://otro.test:9222/devtools/browser/a")), "Sin servidor externo");
            return Task.CompletedTask;
        });
        yield return ("Navegador: solo muestra pestañas de correo HTTPS", async () =>
        {
            var protocol = new FakeProtocol();
            protocol.Targets = [new("mail", "Correo - Outlook", "https://outlook.cloud.microsoft/mail/"),
                new("work", "Outlook del centro", "https://correo.ejemplo.es/owa/"),
                new("other", "Otra web", "https://ejemplo.es/"), new("http", "Outlook", "http://correo.ejemplo.es/owa/")];
            var tabs = await BrowserTabSession.FindAsync(protocol, default);
            Require(tabs.Select(t => t.Id).SequenceEqual(["mail", "work"]), "Selección de pestañas");
        });
        yield return ("Navegador: no conecta a una pestaña no válida", async () =>
        {
            var protocol = new FakeProtocol();
            await Fails(() => BrowserTabSession.AttachAsync(protocol, new("x", "Outlook", "http://correo.ejemplo.es/"), default));
            Require(protocol.Calls.Count == 0, "Sin petición al navegador");
        });
        yield return ("Navegador: cambio de origen bloquea la operación", async () =>
        {
            var protocol = new FakeProtocol();
            var session = await BrowserTabSession.AttachAsync(protocol, protocol.Selected, default);
            protocol.CurrentUrl = "https://otro.ejemplo.es/";
            await Fails(() => session.EvaluateAsync("Promise.resolve(true)", default));
            Require(protocol.Evaluations == 0, "No evalúa en el sitio nuevo");
        });
        yield return ("Navegador: cambio de pestaña bloquea la operación", async () =>
        {
            var protocol = new FakeProtocol();
            var session = await BrowserTabSession.AttachAsync(protocol, protocol.Selected, default);
            protocol.CurrentId = "otra";
            await Fails(() => session.EvaluateAsync("Promise.resolve(true)", default));
            Require(protocol.Evaluations == 0, "Identificador fijado");
        });
        yield return ("Navegador: evalúa solo en la sesión de la pestaña elegida", async () =>
        {
            var protocol = new FakeProtocol();
            var session = await BrowserTabSession.AttachAsync(protocol, protocol.Selected, default);
            await session.EvaluateAsync("Promise.resolve(true)", default);
            Require(protocol.LastSession == "chosen-session", "Sesión fijada");
            Require(protocol.LastScript.Contains("location.origin!==\"https://outlook.cloud.microsoft\""), "Segunda comprobación dentro de la página");
        });
        yield return ("Navegador: una excepción de página no expone el protocolo", async () =>
        {
            var protocol = new FakeProtocol { PageException = true };
            var session = await BrowserTabSession.AttachAsync(protocol, protocol.Selected, default);
            var failure = await Fails(() => session.EvaluateAsync("Promise.resolve(true)", default));
            Require(!failure.Message.Contains("dato-interno"), "Aviso sin contenido interno");
        });
        yield return ("Navegador: bloquea operaciones de credenciales", async () =>
        {
            using var protocol = new BrowserSocket();
            await Fails(() => protocol.RequestAsync("Network.getAllCookies", new { }, null, default));
        });
        yield return ("Navegador: no reintenta un movimiento sin respuesta", async () =>
        {
            var page = new FakePage { Failure = new TimeoutException("sin respuesta") };
            try { await new OwaPageTransport(page).ActionAsync("MoveItem", new { }, default); }
            catch (TimeoutException) { Require(page.Calls == 1, "Sin reintento"); return; }
            throw new Exception("Debía detenerse");
        });
        yield return ("Navegador: conserva la autenticación en la página", async () =>
        {
            var page = new FakePage();
            await new OwaPageTransport(page).ActionAsync("GetFolder", new { }, default);
            Require(page.Script.Contains("'same-origin'"), "Sesión del navegador");
            Require(!page.Script.Contains("Authorization", StringComparison.OrdinalIgnoreCase), "Sin extracción de bearer");
            Require(page.Script.Contains("candidates.includes(selected)"), "Rutas de servicio limitadas");
        });
        yield return ("Navegador: rechaza errores del servidor antes de continuar", async () =>
        {
            var page = new FakePage { Response = JsonSerializer.SerializeToElement(new { ok = true, data = new { Body = new { ResponseCode = "ErrorAccessDenied" } } }) };
            await Fails(() => new OwaPageTransport(page).ActionAsync("GetItem", new { }, default));
            Require(page.Calls == 1, "Una petición");
        });
    }
    private static void Require(bool value, string why) { if (!value) throw new Exception(why); }
    private static async Task<Exception> Fails<T>(Func<Task<T>> run)
    {
        try { await run(); } catch (InvalidOperationException ex) { return ex; } catch (InvalidDataException ex) { return ex; }
        throw new Exception("Debía detenerse");
    }
    private sealed class FakePage : IOutlookPage
    {
        public int Calls;
        public string Script = "";
        public Exception? Failure;
        public JsonElement Response = JsonSerializer.SerializeToElement(new { ok = true, data = new { Body = new { ResponseCode = "NoError" } } });
        public Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct)
        {
            Calls++; Script = expression;
            return Failure is null ? Task.FromResult(Response) : Task.FromException<JsonElement>(Failure);
        }
    }
    private sealed class FakeProtocol : IBrowserProtocol
    {
        public BrowserTab Selected = new("mail", "Correo - Outlook", "https://outlook.cloud.microsoft/mail/");
        public BrowserTab[] Targets = [];
        public string CurrentUrl = "https://outlook.cloud.microsoft/mail/";
        public string CurrentId = "mail";
        public int Evaluations;
        public bool PageException;
        public string LastSession = "", LastScript = "";
        public List<string> Calls = [];
        public Task<JsonElement> RequestAsync(string method, object? parameters, string? sessionId, CancellationToken ct)
        {
            Calls.Add(method);
            object response;
            switch (method)
            {
                case "Target.getTargets": response = new { targetInfos = Targets.Select(t => new { type = "page", targetId = t.Id, url = t.Url, title = t.Title }) }; break;
                case "Target.attachToTarget": response = new { sessionId = "chosen-session" }; break;
                case "Target.getTargetInfo": response = new { targetInfo = new { type = "page", targetId = CurrentId, url = CurrentUrl } }; break;
                case "Runtime.evaluate":
                    Evaluations++; LastSession = sessionId ?? "";
                    LastScript = JsonSerializer.SerializeToElement(parameters).GetProperty("expression").GetString()!;
                    response = PageException ? new { exceptionDetails = new { text = "dato-interno" } } : new { result = new { value = true } };
                    break;
                default: throw new Exception("Petición inesperada");
            }
            return Task.FromResult(JsonSerializer.SerializeToElement(response));
        }
    }
}
