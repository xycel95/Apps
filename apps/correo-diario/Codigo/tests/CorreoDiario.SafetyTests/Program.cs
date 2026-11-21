using System.Text;
using System.Text.Json;
using CorreoDiario.Core;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

if (args.Length == 2 && args[0] == "--verify-render")
{
    PdfVerifier.Validate(await File.ReadAllBytesAsync(args[1] + ".pdf"), await File.ReadAllTextAsync(args[1] + ".txt"));
    Console.WriteLine("PASS PDF generado con Chromium: coincide todo el texto visible.");
    return 0;
}
if (args.Length == 1 && args[0] == "--verify-browser-bridge")
{
    await BrowserWireCheck.RunAsync();
    Console.WriteLine("PASS Conexión local: WebSocket, respuestas fragmentadas y peticiones simultáneas.");
    return 0;
}

var tests = new (string Name, Func<Task> Test)[]
{
    ("Casilla desmarcada: cero movimientos", async () =>
    {
        using var env = new TestRun();
        var result = await env.Run(false);
        Check(result.Messages == 2 && result.Attachments == 2 && env.Source.Moved.Count == 0, "Exportación sin borrado");
        Check(Directory.GetFiles(result.Directory, "*_CORREO.pdf").Length == 2, "PDF separados");
    }),
    ("Casilla marcada: termina todo antes de mover", async () =>
    {
        using var env = new TestRun();
        env.Source.OnMove = () => Check(env.Source.Reads == 4 && env.Source.Downloads == 2 && env.Renderer.Renders == 2, "Borrado prematuro");
        var result = await env.Run(true);
        Check(result.Moved == 2 && env.Source.Moved.Count == 2, "Movimientos confirmados");
    }),
    ("Sin versión del correo: guarda la copia y no mueve nada", async () =>
    {
        using var env = new TestRun();
        env.Source.Items[1] = env.Source.Items[1] with { Revision = "" };
        var ex = await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0 && ex is ExportException, "Sin verificación de versión no hay movimiento");
        Check(Directory.GetFiles(((ExportException)ex).Directory, "*_CORREO.pdf").Length == 2, "Copias conservadas");
    }),
    ("Fallo en el último adjunto: no mueve ningún correo", async () =>
    {
        using var env = new TestRun();
        env.Source.FailAttachment = "a2";
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "No debe mover el primer correo ya guardado");
    }),
    ("Tamaño de adjunto incorrecto: no mueve nada", async () =>
    {
        using var env = new TestRun();
        env.Source.BadSize = true;
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "Adjunto incompleto");
    }),
    ("Cambio de contenido remoto durante la exportación: no mueve", async () =>
    {
        using var env = new TestRun();
        env.Source.ChangeAfterExport = true;
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "Snapshot modificado");
    }),
    ("Archivo alterado después de guardar: no mueve", async () =>
    {
        using var env = new TestRun();
        env.Source.OnVerify = () =>
        {
            var file = Directory.GetFiles(env.Directory, "*.pdf", SearchOption.AllDirectories).First();
            File.WriteAllText(file, "alterado");
        };
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "Comprobación de bytes en disco");
    }),
    ("PDF incompleto: no mueve nada", async () =>
    {
        using var env = new TestRun();
        env.Renderer.Broken = true;
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "PDF sin EOF");
    }),
    ("PDF válido: compara el texto visible completo", () =>
    {
        var bytes = FakeRenderer.ValidPdf("Contenido completo 123");
        PdfVerifier.Validate(bytes, "Contenido completo 123");
        return Task.CompletedTask;
    }),
    ("PDF con texto omitido: rechaza la comprobación", () =>
    {
        var bytes = FakeRenderer.ValidPdf("Contenido completo 123");
        try { PdfVerifier.Validate(bytes, "Contenido completo 123 solicitud"); }
        catch (InvalidDataException) { return Task.CompletedTask; }
        throw new Exception("Debía detectar la palabra omitida");
    }),
    ("Cancelación tras guardar: no mueve nada", async () =>
    {
        using var env = new TestRun();
        using var token = new CancellationTokenSource();
        env.Source.OnVerify = token.Cancel;
        await MustFail(() => env.Run(true, token.Token));
        Check(env.Source.Moved.Count == 0, "Cancelación antes de fase de movimiento");
    }),
    ("Coincidencia de hora y remitente: conserva todos los archivos", async () =>
    {
        using var env = new TestRun();
        env.Source.Items[1] = env.Source.Items[1] with { Timestamp = env.Source.Items[0].Timestamp, From = env.Source.Items[0].From };
        var result = await env.Run(false);
        Check(Directory.GetFiles(result.Directory, "*_CORREO.pdf").Length == 2, "Sin sobrescritura");
        Check(Directory.GetFiles(result.Directory, "*_02_CORREO.pdf").Length == 1, "Sufijo de colisión");
    }),
    ("Identificadores de correos repetidos: rechaza la selección", async () =>
    {
        using var env = new TestRun();
        env.Source.Items[1] = env.Source.Items[0];
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0 && env.Source.Reads == 0, "Selección repetida");
    }),
    ("Respuesta de otro correo: detiene la exportación", async () =>
    {
        using var env = new TestRun();
        env.Source.WrongId = true;
        await MustFail(() => env.Run(true));
        Check(env.Source.Moved.Count == 0, "Identidad del correo");
    }),
    ("Fallo de movimiento: conserva comprobación de los movimientos confirmados", async () =>
    {
        using var env = new TestRun();
        env.Source.FailSecondMove = true;
        var ex = await MustFail(() => env.Run(true));
        Check(ex is ExportException { Moved: 1 }, "Resultado parcial explícito");
        var dir = ((ExportException)ex).Directory;
        using var receipt = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "Comprobacion.json")));
        Check(receipt.RootElement.GetProperty("Status").GetString() == "ERROR", "Registro del error");
        Check(receipt.RootElement.GetProperty("MovedToDeleted").GetArrayLength() == 1, "Registro del primer movimiento");
        Check(env.Source.MoveCalls == 2, "No reintenta un movimiento fallido");
    }),
    ("Nombres incompatibles con Windows se normalizan", () =>
    {
        Check(ExportEngine.SafeName("../../NUL.txt").Contains('/') == false, "Ruta fuera del destino");
        Check(ExportEngine.SafeName("NUL.txt") == "_NUL.txt", "Nombre reservado");
        Check(ExportEngine.SafeName("a:b?.pdf") == "a_b_.pdf", "Caracteres reservados");
        Check(ExportEngine.SafeName(new string('x', 200) + ".pdf", 95).EndsWith(".pdf"), "Extensión conservada");
        return Task.CompletedTask;
    })
}.Concat(OwaChecks.All()).Concat(BrowserChecks.All()).ToArray();
var failures = 0;
foreach (var test in tests)
{
    try { await test.Test(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + test.Name + ": " + ex); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} comprobaciones superadas.");
return failures == 0 ? 0 : 1;

static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
static async Task<Exception> MustFail(Func<Task<ExportResult>> run)
{
    try { await run(); } catch (Exception ex) { return ex; }
    throw new Exception("La operación debía detenerse.");
}

sealed class TestRun : IDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "cddi-" + Guid.NewGuid().ToString("N"));
    public FakeSource Source { get; } = new();
    public FakeRenderer Renderer { get; } = new();
    public TestRun() => System.IO.Directory.CreateDirectory(Directory);
    public Task<ExportResult> Run(bool delete, CancellationToken ct = default) => new ExportEngine(Source, Renderer).RunAsync(Source.Items, new(Directory, delete), null, ct);
    public void Dispose() { try { System.IO.Directory.Delete(Directory, true); } catch (IOException) { } }
}

sealed class FakeSource : IMailSource
{
    public List<MailInfo> Items { get; } = [Make(1), Make(2)];
    public List<string> Moved { get; } = [];
    public int Reads, Downloads, MoveCalls;
    public string? FailAttachment;
    public bool BadSize, ChangeAfterExport, WrongId, FailSecondMove;
    public Action? OnVerify, OnMove;
    public Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MailFolder>>([new("inbox", "Entrada")]);
    public Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, DateOnly date, TimeZoneInfo zone, CancellationToken ct) => Task.FromResult<IReadOnlyList<MailInfo>>(Items);
    public Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct)
    {
        Reads++;
        if (Reads == 3) OnVerify?.Invoke();
        ct.ThrowIfCancellationRequested();
        if (WrongId) return Task.FromResult(item with { Id = "otro" });
        return Task.FromResult(ChangeAfterExport && Reads > 2 ? item with { Html = "cambiado" } : item);
    }
    public Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo a, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Downloads++;
        if (a.Id == FailAttachment) throw new IOException("Fallo simulado de descarga");
        return Task.FromResult(Encoding.UTF8.GetBytes(BadSize ? "incompleto" : "original"));
    }
    public Task MoveToDeletedAsync(MailInfo item, CancellationToken ct)
    {
        MoveCalls++;
        ct.ThrowIfCancellationRequested();
        OnMove?.Invoke();
        if (FailSecondMove && MoveCalls == 2) throw new IOException("Fallo simulado de movimiento");
        Moved.Add(item.Id);
        return Task.CompletedTask;
    }
    private static MailInfo Make(int id) => new("m" + id, "rev1", "inbox", "Mensaje " + id, "ana@ejemplo.es", "rafa@ejemplo.es", "",
        DateTimeOffset.Now.AddMinutes(-id), false, "<p>Contenido completo</p>", [new("a" + id, "solicitud.pdf", 8, "application/pdf", false, "")]);
}

sealed class FakeRenderer : IPdfRenderer
{
    public int Renders;
    public bool Broken;
    public Task<byte[]> RenderAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
    {
        Renders++;
        // The renderer is substituted here: these tests exercise export transactions,
        // not the Windows/Edge PDF engine. Live rendering must be checked on Windows.
        return Task.FromResult(Broken ? Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string(' ', 300)) : ValidPdf("Contenido completo"));
    }
    public static byte[] ValidPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText(text, 12, new PdfPoint(40, 760), font);
        return builder.Build();
    }
}
