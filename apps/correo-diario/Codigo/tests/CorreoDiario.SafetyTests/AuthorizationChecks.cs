using System.Text.Json;
using CorreoDiario.Core;

internal static class AuthorizationChecks
{
    private const string Origin = "https://outlook.cloud.microsoft";
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Autorización: exige una solicitud correcta de la pestaña y marco elegidos", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, session: "otra"); Publish(events, id: "otra-frame", frame: "iframe");
            Require(await capture.GetAsync(false, default) is null, "Sin datos de otras sesiones o marcos");
            Publish(events, id: "correcta");
            Require(await capture.GetAsync(false, default) is not null, "Solicitud autorizada disponible");
        });
        yield return ("Autorización: acepta cabeceras extra fuera de orden", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            events.Emit("Network.requestWillBeSentExtraInfo", "s", new { requestId = "a", headers = new { Authorization = "Bearer fixture-session" } });
            events.Emit("Network.responseReceived", "s", new { requestId = "a", response = new { status = 200 } });
            events.Emit("Network.requestWillBeSent", "s", Request("a", "main", Origin + "/owa/service.svc", new { }));
            Require(await capture.GetAsync(false, default) is not null, "Orden de eventos independiente");
        });
        yield return ("Autorización: no reutiliza solicitudes rechazadas", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, status: 401);
            Require(await capture.GetAsync(false, default) is null, "401 no constituye una autorización");
        });
        yield return ("Autorización: rechaza dominios, rutas y esquemas ajenos", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            foreach (var endpoint in new[] { "https://otro.test/owa/service.svc", "https://outlook.office.com.otro.test/owa/service.svc", Origin + "/graph/v1.0/me", "http://outlook.cloud.microsoft/owa/service.svc", "https://user:secret@outlook.cloud.microsoft/owa/service.svc" })
                Publish(events, id: endpoint, endpoint: endpoint);
            Require(await capture.GetAsync(false, default) is null, "No captura autorización de otras API");
        });
        yield return ("Autorización: conserva el destino de servicio usado por Outlook", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, endpoint: "https://outlook.office.com/owa/0/service.svc?action=GetFolder");
            var context = await capture.GetAsync(false, default);
            Require(context!.Endpoint.AbsoluteUri == "https://outlook.office.com/owa/0/service.svc", "Ruta y servidor observados");
        });
        yield return ("Autorización: no expone credenciales en diagnósticos o serialización", async () =>
        {
            var events = new Events(); using var capture = Capture(events); Publish(events);
            var context = await capture.GetAsync(false, default);
            Require(JsonSerializer.Serialize(context) == "{}", "Sin secretos serializados");
            Require(!context!.ToString().Contains("fixture-session"), "Sin secretos en ToString");
        });
        yield return ("Autorización: solo reenvía cabeceras permitidas", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, headers: new Dictionary<string, string> { ["Authorization"] = "Bearer fixture-session", ["Cookie"] = "not-for-export", ["Host"] = "not-for-export", ["X-AnchorMailbox"] = "fixture@example.test" });
            var page = new AuthorizedPage(capture);
            await new OwaPageTransport(page).ActionAsync("GetFolder", new { }, default);
            Require(page.Script.Contains("Bearer fixture-session"), "Autorización de la pestaña");
            Require(!page.Script.Contains("not-for-export"), "Cookie y Host no se copian");
        });
        yield return ("Autorización: una lectura 401 se repite una vez con autorización observada", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            var page = new AuthorizedPage(capture) { FailFirst = true, OnWait = () => Publish(events) };
            await new OwaPageTransport(page).ActionAsync("GetFolder", new { }, default);
            Require(page.Calls == 2 && page.WaitCalls == 1, "Una sola recuperación de lectura");
        });
        yield return ("Autorización: un movimiento 401 no se reintenta", async () =>
        {
            var events = new Events(); using var capture = Capture(events); Publish(events);
            var page = new AuthorizedPage(capture) { AlwaysUnauthorized = true };
            try { await new OwaPageTransport(page).ActionAsync("MoveItem", new { }, default); }
            catch (InvalidOperationException) { Require(page.Calls == 1 && page.WaitCalls == 0, "Movimiento sin reintentos"); return; }
            throw new Exception("Debía detenerse");
        });
        yield return ("Autorización: descarta contextos invalidados y al desconectar", async () =>
        {
            var events = new Events(); var capture = Capture(events); Publish(events);
            capture.Invalidate((await capture.GetAsync(false, default))!);
            Require(await capture.GetAsync(false, default) is null, "Contexto invalidado");
            Publish(events, id: "nuevo"); Require(await capture.GetAsync(false, default) is not null, "Solicitud nueva válida");
            capture.Dispose(); Publish(events, id: "tras-cerrar");
            Require(await capture.GetAsync(false, default) is null, "Sin autorización tras cerrar");
        });
        yield return ("Autorización: la espera responde a Cancelar", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await capture.GetAsync(true, cancel.Token); }
            catch (OperationCanceledException) { return; }
            throw new Exception("Debía cancelarse");
        });
        yield return ("Autorización: admite la cabecera MSA observada sin recuperar contraseñas", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, headers: new { Authorization = "MSAuth1.0 usertoken=fixture-session" });
            Require(await capture.GetAsync(false, default) is not null, "Cabecera de la sesión MSA observada");
        });
        yield return ("Autorización: no reutiliza Basic o negociación del sistema", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, headers: new { Authorization = "Basic fixture" });
            Publish(events, id: "otro", headers: new { Authorization = "Negotiate fixture" });
            Require(await capture.GetAsync(false, default) is null, "Solo esquemas de sesión previstos");
        });
        yield return ("Autorización: respeta la versión de petición observada", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, version: "V2018_01_08");
            var page = new AuthorizedPage(capture);
            await new OwaPageTransport(page).ActionAsync("GetFolder", new { }, default);
            Require(page.Script.Contains("V2018_01_08"), "Versión del servidor de Outlook");
        });
        yield return ("Autorización: rechaza inyección de líneas en cabeceras", async () =>
        {
            var events = new Events(); using var capture = Capture(events);
            Publish(events, headers: new { Authorization = "Bearer fixture\r\nInjected: value" });
            Require(await capture.GetAsync(false, default) is null, "Cabecera inválida");
        });
    }

    private static OutlookAuthorizationCapture Capture(Events events) => new(events, "s", "main", Origin);
    private static object Request(string id, string frame, string url, object headers, string version = "Exchange2016") => new
    {
        requestId = id, frameId = frame, request = new { method = "POST", url, headers,
            postData = JsonSerializer.Serialize(new { Header = new { RequestServerVersion = version } }) }
    };
    private static void Publish(Events events, string id = "a", string session = "s", string frame = "main", string endpoint = Origin + "/owa/service.svc", int status = 200, object? headers = null, string version = "Exchange2016")
    {
        events.Emit("Network.requestWillBeSent", session, Request(id, frame, endpoint, headers ?? new { Authorization = "Bearer fixture-session" }, version));
        events.Emit("Network.responseReceived", session, new { requestId = id, response = new { status } });
    }
    private static void Require(bool value, string why) { if (!value) throw new Exception(why); }
    private sealed class Events : IBrowserEvents
    {
        public event Action<string, string?, JsonElement>? EventReceived;
        public void Emit(string name, string session, object data) => EventReceived?.Invoke(name, session, JsonSerializer.SerializeToElement(data));
    }
    private sealed class AuthorizedPage(OutlookAuthorizationCapture capture) : IOutlookPage, IOutlookAuthorizationSource
    {
        public int Calls, WaitCalls;
        public bool FailFirst, AlwaysUnauthorized;
        public Action? OnWait;
        public string Script = "";
        public async Task<OutlookAuthorization?> GetAuthorizationAsync(bool waitForFreshRequest, CancellationToken ct)
        { if (waitForFreshRequest) { WaitCalls++; OnWait?.Invoke(); } return await capture.GetAsync(waitForFreshRequest, ct); }
        public void InvalidateAuthorization(OutlookAuthorization authorization) => capture.Invalidate(authorization);
        public Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct)
        {
            Script = expression; Calls++;
            object reply = AlwaysUnauthorized || FailFirst && Calls == 1 ? new { ok = false, status = 401, error = "unauthorized" } : new { ok = true, data = new { Body = new { ResponseCode = "NoError" } } };
            return Task.FromResult(JsonSerializer.SerializeToElement(reply));
        }
    }
}
