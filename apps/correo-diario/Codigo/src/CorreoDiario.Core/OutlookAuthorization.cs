using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CorreoDiario.Core;

public interface IOutlookAuthorizationSource
{
    Task<OutlookAuthorization?> GetAuthorizationAsync(bool waitForFreshRequest, CancellationToken ct);
    void InvalidateAuthorization(OutlookAuthorization authorization);
}

// Session material is deliberately opaque to diagnostics and serializers. It is
// kept in memory only and never written to settings, receipts, reports or logs.
public sealed class OutlookAuthorization
{
    [JsonIgnore] public Uri Endpoint { get; }
    [JsonIgnore] public string ServerVersion { get; }
    [JsonIgnore] public bool UrlPostData { get; }
    [JsonIgnore] public DateTimeOffset ObservedAt { get; }
    private readonly Dictionary<string, string> headers;
    internal OutlookAuthorization(Uri endpoint, Dictionary<string, string> headers, string version, bool urlPostData, DateTimeOffset observed)
    { Endpoint = endpoint; this.headers = headers; ServerVersion = version; UrlPostData = urlPostData; ObservedAt = observed; }
    internal Dictionary<string, string> CopyRequestHeaders() => new(headers, StringComparer.OrdinalIgnoreCase);
    public override string ToString() => "Autorización temporal de la pestaña de Outlook";
}

public sealed class OutlookAuthorizationCapture : IDisposable
{
    private readonly IBrowserEvents events;
    private readonly string sessionId, frameId, origin;
    private readonly object gate = new();
    private readonly Dictionary<string, PendingRequest> requests = new(StringComparer.Ordinal);
    private TaskCompletionSource<OutlookAuthorization> ready = NewReady();
    private OutlookAuthorization? current;
    private bool disposed;
    private static readonly string[] AllowedHeaders = ["Authorization", "X-OWA-CANARY", "X-OWA-SessionId", "X-AnchorMailbox",
        "X-TenantId", "X-OWA-ExplicitLogonUser", "X-OWA-ClientBuildVersion", "X-OWA-ClientId", "Prefer"];

    public OutlookAuthorizationCapture(IBrowserEvents events, string sessionId, string frameId, string origin)
    {
        this.events = events; this.sessionId = sessionId; this.frameId = frameId; this.origin = origin;
        events.EventReceived += OnEvent;
    }

    public static bool IsAllowedService(string origin, Uri endpoint)
    {
        if (endpoint.Scheme != "https" || endpoint.UserInfo.Length != 0 ||
            !Regex.IsMatch(endpoint.AbsolutePath, @"^/owa/(?:[0-9]+/)?service\.svc$", RegexOptions.IgnoreCase)) return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var source)) return false;
        if (endpoint.GetLeftPart(UriPartial.Authority) == source.GetLeftPart(UriPartial.Authority)) return true;
        static bool MicrosoftMail(string host) => host is "outlook.cloud.microsoft" or "outlook.office.com" or "outlook.office365.com" or "outlook.live.com";
        return source.IsDefaultPort && endpoint.IsDefaultPort && MicrosoftMail(source.Host) && MicrosoftMail(endpoint.Host);
    }

    private void OnEvent(string method, string? eventSession, JsonElement data)
    {
        if (eventSession != sessionId || method is not ("Network.requestWillBeSent" or "Network.requestWillBeSentExtraInfo" or
            "Network.responseReceived" or "Network.responseReceivedExtraInfo" or "Network.loadingFailed") ||
            !data.TryGetProperty("requestId", out var idValue) || idValue.ValueKind != JsonValueKind.String) return;
        var id = idValue.GetString()!;
        lock (gate)
        {
            if (disposed) return;
            if (!requests.TryGetValue(id, out var request))
            {
                if (requests.Count >= 64) requests.Remove(requests.Keys.First());
                requests[id] = request = new();
            }
            if (method == "Network.requestWillBeSent")
            {
                if (!data.TryGetProperty("frameId", out var frame) || frame.GetString() != frameId ||
                    !data.TryGetProperty("request", out var item) || !item.TryGetProperty("method", out var verb) || verb.GetString() != "POST" ||
                    !item.TryGetProperty("url", out var url) || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var endpoint) || !IsAllowedService(origin, endpoint))
                { requests.Remove(id); return; }
                request.Endpoint = new Uri(endpoint.GetLeftPart(UriPartial.Path));
                request.ValidRequest = true;
                if (item.TryGetProperty("headers", out var headers)) MergeHeaders(request, headers);
                if (item.TryGetProperty("postData", out var post) && post.ValueKind == JsonValueKind.String)
                    ReadVersion(request, post.GetString()!);
            }
            else if (method == "Network.requestWillBeSentExtraInfo")
            {
                if (data.TryGetProperty("headers", out var headers)) MergeHeaders(request, headers);
            }
            else if (method == "Network.responseReceived")
            {
                if (data.TryGetProperty("response", out var response) && response.TryGetProperty("status", out var status))
                    request.Success = status.TryGetDouble(out var code) && code is >= 200 and < 300;
            }
            else if (method == "Network.responseReceivedExtraInfo")
            {
                if (data.TryGetProperty("statusCode", out var status) && status.TryGetInt32(out var code)) request.Success = code is >= 200 and < 300;
            }
            else if (method == "Network.loadingFailed") { requests.Remove(id); return; }
            if ((request.Published && method != "Network.requestWillBeSentExtraInfo") || !request.ValidRequest || request.Success != true || request.Endpoint is null ||
                !request.Headers.TryGetValue("Authorization", out var authorization) ||
                !(authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || authorization.StartsWith("MSAuth1.0 ", StringComparison.OrdinalIgnoreCase))) return;
            current = new(request.Endpoint, new(request.Headers, StringComparer.OrdinalIgnoreCase), request.ServerVersion, request.UrlPostData, DateTimeOffset.UtcNow);
            request.Published = true;
            ready.TrySetResult(current);
        }
    }

    private static void MergeHeaders(PendingRequest request, JsonElement headers)
    {
        if (headers.ValueKind != JsonValueKind.Object) return;
        foreach (var field in headers.EnumerateObject())
        {
            if (field.Name.Equals("X-OWA-UrlPostData", StringComparison.OrdinalIgnoreCase)) { request.UrlPostData = true; continue; }
            var name = AllowedHeaders.FirstOrDefault(h => h.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
            if (name is null || field.Value.ValueKind != JsonValueKind.String) continue;
            var value = field.Value.GetString()!;
            if (value.Length is > 0 and < 32768 && !value.Contains('\r') && !value.Contains('\n')) request.Headers[name] = value;
        }
    }
    private static void ReadVersion(PendingRequest request, string text)
    {
        try
        {
            using var body = JsonDocument.Parse(text);
            if (body.RootElement.TryGetProperty("Header", out var header) && header.TryGetProperty("RequestServerVersion", out var version) &&
                version.ValueKind == JsonValueKind.String && Regex.IsMatch(version.GetString()!, @"^(?:Exchange20[0-9]{2}(?:_SP[0-9])?|V[0-9_]+)$"))
                request.ServerVersion = version.GetString()!;
        }
        catch (JsonException) { }
    }

    public async Task<OutlookAuthorization?> GetAsync(bool wait, CancellationToken ct)
    {
        Task<OutlookAuthorization> next;
        lock (gate)
        {
            if (disposed) return null;
            if (current is not null && DateTimeOffset.UtcNow - current.ObservedAt < TimeSpan.FromMinutes(10)) return current;
            if (current is not null) { current = null; ready = NewReady(); }
            if (!wait) return null;
            next = ready.Task;
        }
        try { return await next.WaitAsync(TimeSpan.FromSeconds(12), ct).ConfigureAwait(false); }
        catch (TimeoutException) { return null; }
    }
    public void Invalidate(OutlookAuthorization authorization)
    { lock (gate) if (ReferenceEquals(current, authorization)) { current = null; ready = NewReady(); } }
    private static TaskCompletionSource<OutlookAuthorization> NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose()
    {
        events.EventReceived -= OnEvent;
        lock (gate) { disposed = true; current = null; requests.Clear(); ready.TrySetCanceled(); }
    }
    private sealed class PendingRequest
    {
        public Uri? Endpoint;
        public readonly Dictionary<string, string> Headers = new(StringComparer.OrdinalIgnoreCase);
        public bool ValidRequest, Published, UrlPostData;
        public bool? Success;
        public string ServerVersion = "Exchange2016";
    }
}
