using CorreoDiario;

internal static class FolderChecks
{
    internal static async Task Run(string temp, Action<bool,string> check)
    {
        var fixture = new FixtureMailbox { Throttle = true };
        using var http = new HttpClient(fixture);
        var graph = new GraphMailbox(http, _ => Task.FromResult("token-de-prueba"), FixtureMailbox.Account);
        var folders = await graph.Folders(default);
        var chosen = folders.Single(f => f.Path == "Principal / Tramitados");
        check(folders.Count == 3 && folders.Any(f => f.Path == "Vacía"), "carpetas paginadas y ruta completa de la subcarpeta");
        var dest = Path.Combine(temp, "nube");
        Task<FolderResult> Write(string output, CancellationToken ct = default) =>
            FolderArchive.WriteAsync(graph, "cuenta-de-prueba", [chosen], output, null, ct);
        var r = await Write(dest);
        check(r.Complete && r.Listed == 30 && r.Saved == 30 && fixture.MimeReads == 30, "lote de 30 mensajes completos paginado con un ID repetido");
        check(fixture.Retries >= 2 && fixture.Calls.Any(x=>x.AbsolutePath.Contains("/users(")), "límite temporal y continuación oficial del mismo usuario");
        var pdfs = Directory.GetFiles(r.Directory, "Correo.pdf", SearchOption.AllDirectories);
        var parts = Directory.GetFiles(r.Directory, "Prueba.bin", SearchOption.AllDirectories);
        check(pdfs.Length == 30 && pdfs.All(f => MailPrinter.VerifyPdf(f) == 1), "30 carpetas con un PDF legible por correo");
        check(parts.Length == 30 && parts.All(f => File.ReadAllBytes(f).SequenceEqual(FixtureMailbox.Data(File.ReadAllBytes(f)[0]))), "30 adjuntos con sus bytes recuperados");
        check(Directory.GetFiles(r.Directory, "*.eml", SearchOption.AllDirectories).Length == 0 && Directory.GetFiles(r.Directory, "*.msg", SearchOption.AllDirectories).Length == 0, "la ruta de Microsoft 365 guarda únicamente el PDF y adjuntos en cada carpeta de mensaje");
        check(Directory.GetFiles(Path.Combine(r.Directory, "Hilos"), "*.pdf").Single() is string combined && MailPrinter.VerifyPdf(combined) == 2, "hilo con las dos páginas de mensajes incluidos");
        check(fixture.AuthorizedReadsOnly && fixture.Calls.All(u=>!u.AbsolutePath.Contains("/raiz/messages")), "todas las peticiones son lecturas autorizadas de las carpetas elegidas");
        var again = await Write(dest);
        check(again.Complete && again.Reused == 30 && again.Saved == 0 && fixture.MimeReads == 30, "repetir el lote verifica los archivos previos y evita descargar otra vez");
        File.WriteAllText(parts[0], "modificado por el usuario");
        var fixedResult = await Write(dest);
        check(fixedResult.Complete && fixedResult.Reused == 29 && fixedResult.Saved == 1 && File.ReadAllText(parts[0]) == "modificado por el usuario", "se recupera una copia dañada sin sobrescribir los archivos del usuario");
        fixture.Total = 2;
        fixture.DenyOne = true;
        var partial = await Write(Path.Combine(temp,"denegado"));
        check(!partial.Complete && partial.Pending == 1 && partial.Saved == 1, "un 403 deja el correo pendiente y permite terminar el siguiente");
        fixture.DenyOne = false; fixture.ChangeOne = true;
        var changing = await Write(Path.Combine(temp,"cambio"));
        check(changing.Pending == 1 && Directory.GetFiles(changing.Directory,"Correo.pdf",SearchOption.AllDirectories).Length == 1, "un cambio de versión impide publicar la copia afectada");
        fixture.ChangeOne = false;
        var retry = await Write(Path.Combine(temp,"cambio"));
        check(retry.Complete && retry.Reused == 1 && retry.Saved == 1, "reanudar termina únicamente el correo pendiente");
        fixture.RefOne = true;
        var linked = await Write(Path.Combine(temp,"enlace"));
        check(linked.Review == 1 && !linked.Complete, "un adjunto enlazado queda señalado para revisión");
        fixture.RefOne = false; fixture.OmitPart = true;
        var recovered = await Write(Path.Combine(temp,"adjunto"));
        check(recovered.Complete && fixture.RawReads == 1, "un adjunto omitido en el MIME se recupera mediante su identificador de Microsoft 365");
        fixture.OmitPart = false; fixture.TimeoutOne = true;
        var timeout = await Write(Path.Combine(temp,"tiempo"));
        check(timeout.Pending == 1 && timeout.Saved == 1 && !timeout.Cancelled, "el tiempo de espera de un mensaje se distingue de cancelar el lote");
        fixture.TimeoutOne = false;
        var empty = await FolderArchive.WriteAsync(graph, "cuenta", [folders.Single(x => x.Id == "vacia")], dest, null, default);
        check(empty.Complete && empty.Listed == 0, "carpeta vacía confirmada");
        fixture.Total = 20; fixture.FailFolder = true;
        var broken = await Write(Path.Combine(temp,"listado"));
        check(!broken.Complete && broken.IncompleteFolders == 1 && broken.Listed == 0, "una segunda página inaccesible impide declarar buena una carpeta incompleta");
        fixture.FailFolder = false; fixture.UnsafeNext = true;
        var unsafeResult = await Write(Path.Combine(temp,"origen"));
        check(!unsafeResult.Complete && fixture.Calls.All(u => u.IdnHost == "graph.microsoft.com"), "un enlace de paginación externo jamás recibe la autorización");
        fixture.UnsafeNext = false; fixture.RepeatNext = true;
        var loop = await Write(Path.Combine(temp,"bucle"));
        check(loop.IncompleteFolders == 1, "se detecta una página repetida");
        fixture.RepeatNext = false; fixture.Total = 2; fixture.PauseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var pending = Write(Path.Combine(temp, "cancelar"), cancel.Token);
        await fixture.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { await Write(Path.Combine(temp,"cancelar")); throw new Exception("Se permitieron dos escritores"); }
        catch (IOException) { check(true, "solo un lote escribe en el mismo destino a la vez"); }
        cancel.Cancel();
        var stopped = await pending;
        check(stopped.Cancelled && !stopped.Complete && stopped.Pending == 2 && !Directory.GetFiles(stopped.Directory,"Correo.pdf",SearchOption.AllDirectories).Any(), "la cancelación genera un informe y evita publicar una carpeta a medias");
    }
}
