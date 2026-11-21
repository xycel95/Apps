using System.Text.Json;

namespace CorreoDiario.Core;

public sealed record BrowserTab(string Id, string Title, string Url)
{
    public static bool IsMailCandidate(string type, string url, string title)
    {
        if (type != "page" || !Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "https" || address.UserInfo.Length > 0) return false;
        return address.Host is "outlook.cloud.microsoft" or "outlook.office.com" or "outlook.office365.com" or "outlook.live.com" ||
            title.Contains("Outlook", StringComparison.OrdinalIgnoreCase) ||
            address.AbsolutePath.StartsWith("/owa/", StringComparison.OrdinalIgnoreCase) ||
            address.AbsolutePath.Equals("/owa", StringComparison.OrdinalIgnoreCase);
    }
    public override string ToString() => Title + " · " + new Uri(Url).Host;
}

public interface IOutlookPage
{
    Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct);
}

public sealed class BrowserTabSession : IOutlookPage, IOutlookAuthorizationSource, IDisposable
{
    private readonly IBrowserProtocol protocol;
    private readonly BrowserTab tab;
    private readonly string sessionId;
    private OutlookAuthorizationCapture? authorization;
    public string Origin { get; }

    private BrowserTabSession(IBrowserProtocol protocol, BrowserTab tab, string sessionId)
    {
        this.protocol = protocol; this.tab = tab; this.sessionId = sessionId;
        Origin = new Uri(tab.Url).GetLeftPart(UriPartial.Authority);
    }

    public static async Task<IReadOnlyList<BrowserTab>> FindAsync(IBrowserProtocol protocol, CancellationToken ct)
    {
        var response = await protocol.RequestAsync("Target.getTargets", new { }, null, ct);
        return response.GetProperty("targetInfos").EnumerateArray().Where(t => BrowserTab.IsMailCandidate(
            Text(t, "type"), Text(t, "url"), Text(t, "title")))
            .Select(t => new BrowserTab(Text(t, "targetId"), Text(t, "title"), Text(t, "url"))).ToList();
    }

    public static async Task<BrowserTabSession> AttachAsync(IBrowserProtocol protocol, BrowserTab tab, CancellationToken ct)
    {
        if (!BrowserTab.IsMailCandidate("page", tab.Url, tab.Title)) throw new InvalidOperationException("Elige una pestaña HTTPS de Outlook.");
        var response = await protocol.RequestAsync("Target.attachToTarget", new { targetId = tab.Id, flatten = true }, null, ct);
        var session = new BrowserTabSession(protocol, tab, response.GetProperty("sessionId").GetString()
            ?? throw new InvalidDataException("No se ha podido conectar con esa pestaña."));
        await session.CheckOriginAsync(ct);
        if (protocol is IBrowserEvents events)
        {
            var frames = await protocol.RequestAsync("Page.getFrameTree", new { }, session.sessionId, ct);
            var frame = frames.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()
                ?? throw new InvalidDataException("No se puede identificar la pestaña de Outlook.");
            session.authorization = new(events, session.sessionId, frame, session.Origin);
            try { await protocol.RequestAsync("Network.enable", new { }, session.sessionId, ct); }
            catch { session.Dispose(); throw; }
        }
        return session;
    }
    public async Task<OutlookAuthorization?> GetAuthorizationAsync(bool waitForFreshRequest, CancellationToken ct)
    {
        await CheckOriginAsync(ct);
        return authorization is null ? null : await authorization.GetAsync(waitForFreshRequest, ct);
    }
    public void InvalidateAuthorization(OutlookAuthorization value) => authorization?.Invalidate(value);
    public void Dispose() { authorization?.Dispose(); authorization = null; }

    private async Task CheckOriginAsync(CancellationToken ct)
    {
        var current = await protocol.RequestAsync("Target.getTargetInfo", new { targetId = tab.Id }, null, ct);
        var info = current.GetProperty("targetInfo");
        if (Text(info, "type") != "page" || Text(info, "targetId") != tab.Id ||
            !Uri.TryCreate(Text(info, "url"), UriKind.Absolute, out var address) || address.Scheme != "https" || address.UserInfo.Length > 0 ||
            address.GetLeftPart(UriPartial.Authority) != Origin)
            throw new InvalidOperationException("La pestaña ha cambiado de página. Vuelve a seleccionar la pestaña de Outlook; no se continúa la operación.");
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct)
    {
        await CheckOriginAsync(ct);
        // Check again inside the page to cover navigation between the metadata
        // check above and evaluation. Do not run the mail action on another site.
        var guarded = "(async()=>{if(location.origin!==" + JsonSerializer.Serialize(Origin) +
            ")throw new Error('La pestaña ha cambiado');return await (" + expression + ")})()";
        var response = await protocol.RequestAsync("Runtime.evaluate", new { expression = guarded, awaitPromise = true,
            returnByValue = true, timeout = 90000 }, sessionId, ct);
        if (response.TryGetProperty("exceptionDetails", out _))
            throw new InvalidOperationException("Outlook no ha completado la operación en la pestaña seleccionada. Comprueba que el correo siga abierto en el navegador.");
        var result = response.GetProperty("result");
        if (!result.TryGetProperty("value", out var value)) throw new InvalidDataException("Outlook no ha devuelto el resultado de la operación.");
        return value.Clone();
    }
    private static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
}
