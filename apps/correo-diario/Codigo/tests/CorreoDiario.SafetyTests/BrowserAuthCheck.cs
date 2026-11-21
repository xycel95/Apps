using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CorreoDiario.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

internal static class BrowserAuthCheck
{
    // CI only: isolated Edge profile and synthetic data. No real mailbox, login,
    // TLS bypass, profile copying or user certificate is involved.
    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Esta prueba requiere Windows y Edge.");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") throw new InvalidOperationException("Esta prueba se ejecuta únicamente en el Windows efímero de CI.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        var directory = Path.Combine(Path.GetTempPath(), "correo-auth-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=CorreoDiario CI " + Guid.NewGuid().ToString("N"), rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var publicRoot = X509CertificateLoader.LoadCertificate(root.Export(X509ContentType.Cert));
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var signed = request.Create(root, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(2), RandomNumberGenerator.GetBytes(16));
        using var transientCertificate = signed.CopyWithPrivateKey(key);
        // SChannel's server credential needs a Windows key-container handle.
        // DefaultKeySet imports it temporarily and removes it on disposal.
        using var certificate = X509CertificateLoader.LoadPkcs12(transientCertificate.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.DefaultKeySet);
        // CurrentUser Root may show an interactive Windows trust prompt. The
        // ephemeral CI machine runs elevated; its machine store has no prompt.
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite); store.Add(publicRoot);
        Console.WriteLine("Fixture: CA pública temporal registrada en el equipo efímero de CI.");
        Process? browser = null;
        WebApplication? app = null;
        var rejected = 0; var accepted = 0;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
            builder.Logging.ClearProviders();
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
            app = builder.Build();
            app.MapGet("/mail", () => Results.Content("""
                <!doctype html><title>Outlook · prueba local</title><button onclick="fixtureRefresh()">Actualizar</button>
                <script>
                window.fixtureRefresh=async()=>{
                  const envelope={Header:{RequestServerVersion:'Exchange2016'},Body:{FolderIds:[{Id:'inbox'}]}};
                  const response=await fetch('/owa/0/service.svc?action=GetFolder&native=1',{method:'POST',headers:{
                    'Content-Type':'application/json','Authorization':'Bearer fixture-session','X-OWA-CANARY':'fixture-canary',
                    'X-OWA-SessionId':'fixture-session-id','X-AnchorMailbox':'fixture@example.invalid',
                    'X-OWA-UrlPostData':encodeURIComponent(JSON.stringify(envelope))},body:JSON.stringify(envelope)});
                  return response.status;
                };
                </script>
                """, "text/html"));
            app.MapPost("/owa/0/service.svc", async (HttpContext context) =>
            {
                if (context.Request.Headers.Authorization != "Bearer fixture-session")
                { Interlocked.Increment(ref rejected); context.Response.StatusCode = 401; return; }
                var text = await new StreamReader(context.Request.Body).ReadToEndAsync(ct);
                if (context.Request.Headers["X-OWA-CANARY"] != "fixture-canary" ||
                    context.Request.Headers["X-OWA-SessionId"] != "fixture-session-id" ||
                    context.Request.Headers["X-AnchorMailbox"] != "fixture@example.invalid" ||
                    Uri.UnescapeDataString(context.Request.Headers["X-OWA-UrlPostData"].ToString()) != text)
                { context.Response.StatusCode = 400; return; }
                using var body = JsonDocument.Parse(text);
                if (body.RootElement.GetProperty("Header").GetProperty("RequestServerVersion").GetString() != "Exchange2016")
                { context.Response.StatusCode = 400; return; }
                Interlocked.Increment(ref accepted);
                object response;
                if (context.Request.Query["action"] == "FindFolder")
                    response = new { Body = new { RootFolder = new { IncludesLastItemInRange = true, Folders = Array.Empty<object>() } } };
                else
                {
                    var id = body.RootElement.GetProperty("Body").GetProperty("FolderIds")[0].GetProperty("Id").GetString();
                    response = new { Body = new { Folders = new[] { new { FolderId = new { Id = "fixture-" + id }, DisplayName = id } } } };
                }
                context.Response.ContentType = "application/json";
                // OWA uses PascalCase, unlike ASP.NET's web JSON defaults.
                await context.Response.WriteAsync(JsonSerializer.Serialize(response), ct);
            });
            await app.StartAsync(ct);
            Console.WriteLine("Fixture: servicio HTTPS preparado.");
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var landing = await http.GetStringAsync(address + "/mail", ct);
            if (!landing.Contains("Outlook")) throw new Exception("El servidor HTTPS de prueba no devuelve la página esperada.");
            Console.WriteLine("Fixture: certificado y respuesta HTTPS comprobados sin omitir la validación TLS.");
            var edge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");
            if (!File.Exists(edge)) edge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe");
            var start = new ProcessStartInfo(edge) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "--headless=new", "--no-first-run", "--no-default-browser-check", "--remote-debugging-port=0", "--user-data-dir=" + directory, address + "/mail" }) start.ArgumentList.Add(arg);
            browser = Process.Start(start) ?? throw new Exception("No se pudo iniciar Edge para la prueba.");
            Console.WriteLine("Fixture: Edge de prueba iniciado.");
            var descriptor = Path.Combine(directory, "DevToolsActivePort");
            while (!File.Exists(descriptor)) { ct.ThrowIfCancellationRequested(); if (browser.HasExited) throw new Exception("Edge terminó antes de conectar."); await Task.Delay(100, ct); }
            using var protocol = new BrowserSocket();
            await protocol.ConnectAsync(BrowserEndpoint.Parse(await File.ReadAllTextAsync(descriptor, ct)), ct);
            Console.WriteLine("Fixture: conexión CDP establecida.");
            BrowserTab? tab = null;
            var navigationDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (tab is null)
            {
                tab = (await BrowserTabSession.FindAsync(protocol, ct)).SingleOrDefault(t => t.Url.StartsWith(address + "/mail", StringComparison.Ordinal));
                if (tab is null && DateTimeOffset.UtcNow > navigationDeadline)
                {
                    var targets = await protocol.RequestAsync("Target.getTargets", new { }, null, ct);
                    var titles = targets.GetProperty("targetInfos").EnumerateArray().Select(t => t.GetProperty("title").GetString());
                    throw new Exception("Edge no muestra la página HTTPS sintética. Títulos de la instancia de prueba: " + string.Join("; ", titles));
                }
                if (tab is null) await Task.Delay(100, ct);
            }
            using var session = await BrowserTabSession.AttachAsync(protocol, tab, ct);
            Console.WriteLine("Fixture: pestaña sintética conectada.");
            var baseline = await session.EvaluateAsync("(async()=>{const r=await fetch('/owa/0/service.svc?action=GetFolder',{method:'POST',body:'{}'});return r.status;})()", ct);
            if (baseline.GetInt32() != 401) throw new Exception("La prueba no reproduce el 401 original.");
            Console.WriteLine("Fixture: 401 sin autorización reproducido.");
            // Equivalent to the user clicking Refresh in their already open tab.
            var status = await session.EvaluateAsync("fixtureRefresh()", ct);
            if (status.GetInt32() != 200 || await session.GetAuthorizationAsync(true, ct) is null) throw new Exception("No se observó la autorización de la pestaña.");
            Console.WriteLine("Fixture: autorización observada en la solicitud correcta.");
            var folders = await new OwaSource(new OwaPageTransport(session)).FoldersAsync(ct);
            if (folders.Count != 2 || folders[0].Id != "fixture-inbox" || folders[1].Id != "fixture-sentitems" || rejected != 1 || accepted < 4)
                throw new Exception("La lectura autenticada no conserva las carpetas completas.");
        }
        finally
        {
            if (browser is not null) { try { if (!browser.HasExited) browser.Kill(true); await browser.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); } catch (InvalidOperationException) { } browser.Dispose(); }
            if (app is not null) await app.DisposeAsync();
            store.Remove(publicRoot);
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }
}
