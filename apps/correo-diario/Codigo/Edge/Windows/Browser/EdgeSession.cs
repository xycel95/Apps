using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace CorreoDiario.EdgeApp;

// A Windows job closes only this driver's temporary browser when the application exits.
internal sealed class DriverJob : SafeHandleZeroOrMinusOneIsInvalid
{
    [StructLayout(LayoutKind.Sequential)] private struct Limits
    {
        public long Process, Job; public uint Flags; public nuint Minimum, Maximum; public uint Active;
        public nuint Affinity; public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Extended
    {
        public Limits Limits; public ulong Read, Write, Other, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcess, PeakJob;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr security, string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int kind, ref Extended value, uint size);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    protected override bool ReleaseHandle() => CloseHandle(handle);
    internal DriverJob(Process process) : base(true)
    {
        SetHandle(CreateJobObject(0, null));
        var limits = new Extended { Limits = new Limits { Flags = 0x2000 } };
        if (IsInvalid || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<Extended>()) || !AssignProcessToJobObject(handle, process.Handle))
        {
            Dispose();
            throw new WebExportException("aislamiento", "Windows no permite iniciar la ventana de exportación aislada.");
        }
    }
}

internal sealed class EdgeSession : IAsyncDisposable
{
    internal static readonly Uri Inbox = new("https://outlook.cloud.microsoft/mail/");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string directory;
    private readonly HttpClient client;
    private string session = "";
    private Process? driver;
    private DriverJob? job;
    private bool disposed;
    internal string Downloads { get; }
    internal bool Invalid { get; set; }
    internal Func<Uri, bool> Accept { get; }

    private EdgeSession(string directory, int port, Func<Uri, bool> accept)
    {
        this.directory = directory;
        Accept = accept;
        Downloads = Directory.CreateDirectory(Path.Combine(directory, "adjuntos")).FullName;
        client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(60),
            MaxResponseContentBufferSize = 200L * 1024 * 1024
        };
    }

    internal static Task<EdgeSession> Open(Action<string> progress, CancellationToken ct) =>
        Start(Inbox, u => EdgeWindow.OutlookAddress(u.AbsoluteUri, false) != null &&
            (u.AbsolutePath.StartsWith("/mail/", StringComparison.OrdinalIgnoreCase) || u.AbsolutePath.StartsWith("/owa/", StringComparison.OrdinalIgnoreCase)), progress, ct);

    // Only WindowsChecks supplies a fixture address and validator. The production entry point is fixed above.
    internal static async Task<EdgeSession> Start(Uri initial, Func<Uri, bool> accept, Action<string> progress, CancellationToken ct)
    {
        var edge = DriverPackage.EdgePath();
        var dir = Directory.CreateTempSubdirectory("CorreoDiario-Web-").FullName;
        int port;
        using (var socket = new TcpListener(IPAddress.Loopback, 0)) { socket.Start(); port = ((IPEndPoint)socket.LocalEndpoint).Port; }
        var instance = new EdgeSession(dir, port, accept);
        try
        {
            var binary = await DriverPackage.Find(edge, dir, progress, ct);
            var start = new ProcessStartInfo(binary) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--port=" + port);
            instance.driver = Process.Start(start) ?? throw new WebExportException("inicio-controlador", "Windows no ha iniciado el controlador de Edge.");
            instance.job = new DriverJob(instance.driver);
            var timer = Stopwatch.StartNew();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (instance.driver.HasExited || timer.Elapsed.TotalSeconds > 15) throw new WebExportException("inicio-controlador", "El controlador de Edge no responde en este equipo.");
                try { await instance.Request(HttpMethod.Get, "/status", null, ct); break; }
                catch (HttpRequestException) { await Task.Delay(150, ct); }
            }
            progress("Abriendo Outlook en la ventana de exportación de Edge…");
            var preferences = new Dictionary<string, object> { ["download.default_directory"] = instance.Downloads, ["download.prompt_for_download"] = false };
            var caps = new Dictionary<string, object>
            {
                ["browserName"] = "MicrosoftEdge", ["pageLoadStrategy"] = "eager",
                ["ms:edgeOptions"] = new { binary = edge, args = new[] { "--no-first-run", "--no-default-browser-check", "--user-data-dir=" + Path.Combine(dir, "perfil") }, prefs = preferences }
            };
            var result = await instance.Request(HttpMethod.Post, "/session", new { capabilities = new { alwaysMatch = caps } }, ct);
            instance.session = "/session/" + Uri.EscapeDataString(result.GetProperty("sessionId").GetString()!);
            await instance.Command(HttpMethod.Post, "/url", new { url = initial.AbsoluteUri }, ct);
            return instance;
        }
        catch { try { await instance.DisposeAsync(); } catch (WebExportException) { } throw; }
    }

    private async Task<JsonElement> Request(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        var value = document.RootElement.GetProperty("value");
        if (!response.IsSuccessStatusCode)
        {
            string? code = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("error", out var error) ? error.GetString() : null;
            if (code is "invalid session id" or "no such window") Invalid = true;
            // Driver messages can contain URLs and rendered content. Keep only known protocol codes.
            if (code is not ("invalid session id" or "no such window" or "session not created" or "element click intercepted" or "element not interactable" or "stale element reference" or "timeout" or "unknown command")) code = "operación rechazada";
            throw new WebExportException(code, Invalid ? "La ventana de exportación se ha cerrado. Pulsa Abrir Outlook web." : "Edge ha rechazado la operación (" + code + "). Puedes copiar el diagnóstico.");
        }
        return value.Clone();
    }

    internal Task<JsonElement> Command(HttpMethod method, string path, object? body, CancellationToken ct) => Request(method, session + path, body, ct);
    internal async Task<Uri> Current(CancellationToken ct)
    {
        var result = await Command(HttpMethod.Get, "/url", null, ct);
        if (!Uri.TryCreate(result.GetString(), UriKind.Absolute, out var url) || !Accept(url))
            throw new WebExportException("fuera-del-correo", "Abre el buzón en la ventana de exportación de Edge. Si Microsoft lo pide, identifícate allí.");
        return url;
    }
    internal Task<JsonElement> Evaluate(string code, object[] args, CancellationToken ct) => Command(HttpMethod.Post,"/execute/sync",new { script = code, args },ct);
    internal Task Click(string id, CancellationToken ct) => Command(HttpMethod.Post, "/element/" + Uri.EscapeDataString(id) + "/click", new { }, ct);
    internal Task Minimize(CancellationToken ct) => Command(HttpMethod.Post, "/window/minimize", new { }, ct);
    internal Task Show(CancellationToken ct) => Command(HttpMethod.Post, "/window/maximize", new { }, ct);
    internal async Task<byte[]> Pdf(CancellationToken ct)
    {
        var value = await Command(HttpMethod.Post, "/print", new { background = true, shrinkToFit = true, page = new { width = 21.0, height = 29.7 } }, ct);
        return Convert.FromBase64String(value.GetString()!);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (session.Length != 0)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await Request(HttpMethod.Delete, session, null, deadline.Token); } catch (Exception) { }
        }
        job?.Dispose();
        if (driver != null)
        {
            try { if (!driver.HasExited) driver.Kill(true); await driver.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException) { }
            driver.Dispose();
        }
        client.Dispose();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try { Directory.Delete(directory, true); return; }
            catch (DirectoryNotFoundException) { return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { await Task.Delay(150); }
        }
        throw new WebExportException("limpieza-temporal","Windows mantiene abierta la carpeta temporal de esta sesión de Edge. Revisa los archivos temporales del equipo.");
    }
}
