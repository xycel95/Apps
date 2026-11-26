using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MimeKit;

// Synthetic HTTP service used only by the verification projects. Production has no test endpoint.
internal sealed class FixtureMailbox : HttpMessageHandler
{
    internal const string Base = "https://graph.microsoft.com/v1.0/me/";
    internal const string Account = "8dc7271a-8250-4668-9f24-496fa03c8fa7";
    internal int Total = 30, MimeReads, Retries, RawReads;
    internal bool Throttle { get; set; }
    internal bool FailFolder { get; set; }
    internal bool ChangeOne { get; set; }
    internal bool RefOne { get; set; }
    internal bool DenyOne { get; set; }
    internal bool OmitPart { get; set; }
    internal bool UnsafeNext { get; set; }
    internal bool RepeatNext { get; set; }
    internal bool TimeoutOne { get; set; }
    internal string Revision = "R1";
    internal TaskCompletionSource? PauseFirst;
    internal TaskCompletionSource Reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal List<Uri> Calls { get; } = [];
    internal bool AuthorizedReadsOnly = true;
    static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    static HttpResponseMessage Page(IEnumerable<object> items, string? next = null)
    {
        var fields = new Dictionary<string, object> { ["value"] = items };
        if (next != null) fields["@odata.nextLink"] = next;
        return Json(fields);
    }
    internal static byte[] Data(int n) => [(byte)n, 0x24, 0xba, 0xf0];
    internal byte[] Mime(int n)
    {
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress("Uno", "uno@example.test"));
        mail.To.Add(new MailboxAddress("Dos", "dos@example.test"));
        mail.Subject = "Asunto de prueba " + n; mail.MessageId = n + "@example.test";
        mail.Date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(n);
        var body = new BodyBuilder { TextBody = "Cuerpo completo de prueba " + n + "." };
        if (!(OmitPart && n == 1)) body.Attachments.Add("Prueba.bin", Data(n));
        mail.Body = body.ToMessageBody();
        using var output = new MemoryStream(); mail.WriteTo(output); return output.ToArray();
    }
    object Summary(int n) => new { id = n.ToString(), parentFolderId = "elegida", changeKey = n == 1 ? Revision : "R1",
        subject = "Asunto de prueba " + n, conversationId = n <= 2 ? "grupo" : "", receivedDateTime = "2026-01-01T12:00:00Z",
        internetMessageId = "<" + n + "@example.test>", hasAttachments = true };
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!; Calls.Add(uri);
        AuthorizedReadsOnly &= request.Method == HttpMethod.Get && request.Headers.Authorization?.ToString() == "Bearer token-de-prueba"
            && uri.Host == "graph.microsoft.com" && request.Headers.GetValues("Prefer").Single() == "IdType=\"ImmutableId\"";
        string path = Uri.UnescapeDataString(uri.AbsolutePath).Replace("/v1.0/users('" + Account + "')/mailfolders('elegida')/", "mailFolders/elegida/").Replace("/v1.0/users/" + Account + "/", "").Replace("/v1.0/me/", "");
        bool second = uri.Query.Contains("page=2");
        if (path == "mailFolders")
            return second ? Page([new { id = "vacia", displayName = "Vacía", childFolderCount = 0 }])
                : Page([new { id = "raiz", displayName = "Principal", childFolderCount = 1 }], Base + "mailFolders?page=2");
        if (path == "mailFolders/raiz/childFolders") return Page([new { id = "elegida", displayName = "Tramitados", childFolderCount = 0 }]);
        if (path == "mailFolders/vacia/messages") return Page([]);
        if (path == "mailFolders/rota/messages") return new(HttpStatusCode.Forbidden);
        if (path == "mailFolders/elegida/messages")
        {
            if (Throttle && Retries++ == 0)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero); return limited;
            }
            int split = Math.Min(15, Total);
            if (second && FailFolder) return new(HttpStatusCode.Forbidden);
            var next = "https://graph.microsoft.com/v1.0/users('" + Account + "')/mailfolders('elegida')/messages?page=2";
            if (UnsafeNext) next = "https://otro.example.test/" + path + "?page=2";
            if (!second || RepeatNext) return Page(Enumerable.Range(1, split).Select(Summary), Total > split ? next : null);
            return Page(Enumerable.Range(split, Total - split + 1).Select(Summary));
        }
        var bits = path.Split('/');
        if (bits.Length >= 2 && bits[0] == "messages" && int.TryParse(bits[1], out int n))
        {
            if (bits.Length == 2) return Json(new { id = n.ToString(), parentFolderId = "elegida", changeKey = n == 1 ? (ChangeOne ? "CAMBIO" : Revision) : "R1" });
            if (bits[2] == "$value")
            {
                if (DenyOne && n == 1) return new(HttpStatusCode.Forbidden);
                if (TimeoutOne && n == 1) throw new TaskCanceledException("Tiempo simulado");
                Interlocked.Increment(ref MimeReads);
                if (n == 1) { Reading.TrySetResult(); if (PauseFirst != null) await PauseFirst.Task.WaitAsync(ct); }
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Mime(n)) };
            }
            if (bits.Length == 5 && bits[2] == "attachments" && bits[3] == "a" && bits[4] == "$value")
            { RawReads++; return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Data(n)) }; }
            if (bits.Length == 3 && bits[2] == "attachments")
            {
                var file = new Dictionary<string, object> { ["id"]="a", ["@odata.type"]="#microsoft.graph.fileAttachment", ["name"]="Prueba.bin", ["size"]=4, ["isInline"]=false, ["contentType"]="application/octet-stream" };
                if (!RefOne || n != 1) return Page([file]);
                var link = new Dictionary<string, object>(file) { ["id"]="b", ["@odata.type"]="#microsoft.graph.referenceAttachment", ["name"]="Documento enlazado" };
                return Page([file, link]);
            }
        }
        throw new InvalidOperationException("Ruta inesperada en la prueba: " + path);
    }
}
