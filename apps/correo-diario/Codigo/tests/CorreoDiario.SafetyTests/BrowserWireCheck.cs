using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using CorreoDiario.Core;

internal static class BrowserWireCheck
{
    // Run on Windows CI. Uses a synthetic local protocol peer, never user mail.
    public static async Task RunAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        var reserve = new TcpListener(IPAddress.Loopback, 0);
        reserve.Start(); var port = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var server = ServeAsync(listener, ct);
        using var client = new BrowserSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/devtools/browser/fixture"), ct);
        var responses = await Task.WhenAll(Enumerable.Range(1, 12).Select(async probe =>
        {
            var result = await client.RequestAsync("Browser.getVersion", new { probe }, null, ct);
            if (result.GetProperty("probe").GetInt32() != probe) throw new Exception("Respuesta asignada a otra petición");
            return probe;
        }));
        if (responses.Length != 12) throw new Exception("Faltan respuestas");
        client.Dispose();
        cancellation.Cancel(); listener.Stop();
        try { await server; } catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or HttpListenerException) { }
    }

    private static async Task ServeAsync(HttpListener listener, CancellationToken ct)
    {
        var context = await listener.GetContextAsync().WaitAsync(ct);
        if (context.Request.Headers["User-Agent"] != "CorreoDiario/0.3") throw new Exception("Cliente sin identificar");
        var connection = await context.AcceptWebSocketAsync(null);
        using var socket = connection.WebSocket;
        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var data = new MemoryStream();
            ValueWebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(buffer.AsMemory(), ct);
                if (frame.MessageType == WebSocketMessageType.Close) return;
                data.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);
            using var request = JsonDocument.Parse(data.ToArray());
            var response = JsonSerializer.SerializeToUtf8Bytes(new { id = request.RootElement.GetProperty("id").GetInt64(),
                result = new { probe = request.RootElement.GetProperty("params").GetProperty("probe").GetInt32() } });
            var half = response.Length / 2;
            await socket.SendAsync(response.AsMemory(0, half), WebSocketMessageType.Text, false, ct);
            await socket.SendAsync(response.AsMemory(half), WebSocketMessageType.Text, true, ct);
        }
    }
}
