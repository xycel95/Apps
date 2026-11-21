using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CorreoDiario.Core;

public interface IBrowserProtocol
{
    Task<JsonElement> RequestAsync(string method, object? parameters, string? sessionId, CancellationToken ct);
}

public static class BrowserEndpoint
{
    // Read only this connection descriptor. Never read browser cookies, passwords,
    // Local State or the authenticated profile databases.
    public static Uri Parse(string descriptor)
    {
        var lines = descriptor.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 2 || !int.TryParse(lines[0], out var port) || port is < 1 or > 65535 ||
            !Regex.IsMatch(lines[1], @"^/devtools/browser/[A-Za-z0-9_-]+$"))
            throw new InvalidDataException("El navegador no ha publicado una conexión local válida. Revisa la opción de conexión del navegador.");
        return new Uri($"ws://127.0.0.1:{port}{lines[1]}");
    }
    public static bool IsLocal(Uri endpoint) => endpoint.Scheme == "ws" && endpoint.Host == "127.0.0.1" &&
        endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0 &&
        Regex.IsMatch(endpoint.AbsolutePath, @"^/devtools/browser/[A-Za-z0-9_-]+$");
}

public sealed class BrowserSocket : IBrowserProtocol, IDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim sending = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pending = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task? reader;
    private long sequence;
    private bool disposed;

    public async Task ConnectAsync(Uri endpoint, CancellationToken ct)
    {
        if (!BrowserEndpoint.IsLocal(endpoint)) throw new InvalidOperationException("La conexión debe pertenecer al navegador de este equipo.");
        socket.Options.SetRequestHeader("User-Agent", "CorreoDiario/0.2");
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try { await socket.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
            throw new InvalidOperationException("No se ha autorizado la conexión. Comprueba el aviso de Edge o Chrome y vuelve a intentarlo.");
        }
        reader = ReceiveAsync();
    }

    public async Task<JsonElement> RequestAsync(string method, object? parameters, string? sessionId, CancellationToken ct)
    {
        if (method is not ("Browser.getVersion" or "Target.getTargets" or "Target.getTargetInfo" or
            "Target.attachToTarget" or "Target.detachFromTarget" or "Runtime.evaluate"))
            throw new InvalidOperationException("Operación de navegador no permitida.");
        if (disposed || socket.State != WebSocketState.Open) throw new InvalidOperationException("Se ha perdido la conexión con el navegador.");
        var id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        var request = new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } };
        if (sessionId is not null) request["sessionId"] = sessionId;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        try
        {
            await sending.WaitAsync(ct).ConfigureAwait(false);
            try { await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, ct).ConfigureAwait(false); }
            finally { sending.Release(); }
            // Never retry a protocol request: a mail movement could have completed
            // even if its response was lost or the user pressed Cancel.
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(95), ct).ConfigureAwait(false);
        }
        finally { pending.TryRemove(id, out _); }
    }

    private async Task ReceiveAsync()
    {
        var buffer = new byte[65536];
        try
        {
            while (!lifetime.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var data = new MemoryStream();
                ValueWebSocketReceiveResult frame;
                do
                {
                    frame = await socket.ReceiveAsync(buffer.AsMemory(), lifetime.Token).ConfigureAwait(false);
                    if (frame.MessageType == WebSocketMessageType.Close) return;
                    if (frame.MessageType != WebSocketMessageType.Text) throw new InvalidDataException("Respuesta de navegador no reconocida.");
                    data.Write(buffer, 0, frame.Count);
                    if (data.Length > 512L * 1024 * 1024) throw new InvalidDataException("La respuesta del correo es demasiado grande para procesarla.");
                } while (!frame.EndOfMessage);
                using var message = JsonDocument.Parse(data.ToArray());
                if (!message.RootElement.TryGetProperty("id", out var id) || !id.TryGetInt64(out var number) || !pending.TryGetValue(number, out var completion)) continue;
                if (message.RootElement.TryGetProperty("error", out _))
                    completion.TrySetException(new InvalidOperationException("El navegador ha rechazado la operación. Vuelve a seleccionar la pestaña de Outlook."));
                else if (message.RootElement.TryGetProperty("result", out var result)) completion.TrySetResult(result.Clone());
                else completion.TrySetException(new InvalidDataException("Falta la respuesta del navegador."));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or JsonException or InvalidDataException or ObjectDisposedException)
        {
            // Connection failures deliberately omit raw protocol contents and URLs.
        }
        finally
        {
            foreach (var request in pending.Values) request.TrySetException(new InvalidOperationException("Se ha cerrado la conexión con el navegador. Comprueba el buzón antes de repetir un movimiento."));
            try { socket.Abort(); } catch (ObjectDisposedException) { }
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        socket.Abort();
        socket.Dispose();
        // No Browser.close, tab closing, account logout or profile deletion.
    }
}
