using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CorreoDiario.EdgeApp;

internal sealed record WebState(string Kind, int Count, string[] Keys)
{
    internal WebRow[] Rows { get; init; } = [];
}
internal sealed record WebFile(string Name, string Key, bool Mail);
internal sealed record WebMessage(string Title, string[] Headers, string Body, WebFile[] Files, int Images);
internal sealed record WebResult(string Folder, int Total, int Saved, int Attachments, bool Cancelled, string? Problem, int UnresolvedGroups);

internal sealed partial class OutlookSelection(EdgeSession session)
{
    private readonly EdgeSession edge = session;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string Page = LoadScript("OutlookPage.js") + "\n" + LoadScript("OutlookRows.js");
    private int expectedDraft;
    private string origin = "";

    private static string LoadScript(string name)
    {
        using var resource = typeof(OutlookSelection).Assembly.GetManifestResourceStream("CorreoDiario." + name)!;
        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }

    private static WebExportException Fault(string code) => new(code, code switch
    {
        "fuera-del-correo" or "origen" => "Abre tu buzón en la ventana de exportación de Edge.",
        "reenviar" => "Outlook no muestra una acción única de Reenviar. Pulsa Reenviar en esa ventana y deja abierto el borrador con los mensajes adjuntos.",
        "cambio-seleccion" or "cambio-lote" => "La selección o el borrador ha cambiado. Comprueba los correos en la ventana de exportación.",
        "cantidad" => "La cantidad de correos que entregó Outlook no coincide con la selección. Consulta los dos contadores en el diagnóstico.",
        "borrador-vacío" => "Outlook abrió un borrador sin correos adjuntos. Vuelve a la pestaña de conversaciones seleccionadas que aparece al pie de Outlook.",
        "lista-cambiada" or "fila-cambiada" or "fila-ambigua" => "La lista de la carpeta cambió o contiene filas que no se pueden distinguir. Revisa la selección en Outlook.",
        "fila-ausente" => "Outlook ya no muestra esta fila en la carpeta.",
        "hilo-sin-desglose" or "panel-varios-cuerpos" => "Outlook no mostró todos los mensajes del hilo por separado. Puedes reintentar esos correos en la vista por mensajes.",
        "panel-cuerpo-sin-identificar" => "Se abrió la fila, pero no se pudo identificar el cuerpo del mensaje.",
        "panel-cabecera-sin-identificar" => "Se identificó el cuerpo, pero no se pudo verificar su cabecera.",
        "panel-asunto-distinto" or "panel-identidad-distinta" => "El mensaje mostrado no coincide con la fila elegida.",
        "panel-anterior" or "panel-cargando" => "Outlook conservó el panel anterior o dejó el mensaje en carga.",
        "panel-fila-fuera-de-vista" or "panel-fila-sin-marca-activa" or "panel-seleccion-multiple" => "Outlook no confirmó cuál era la fila abierta.",
        "seleccion" => "Marca varios correos en la ventana de exportación de Edge o abre un único mensaje completo.",
        "borrador" => "El borrador debe contener la selección como elementos de Outlook. Vuelve a la carpeta, marca los correos y pulsa Exportar selección web.",
        "vista-abierta" => "Cierra la vista previa del adjunto hasta ver las tarjetas del borrador y vuelve a exportar.",
        "imagen" => "Hay una imagen que Outlook aún no ha cargado en el cuerpo del mensaje.",
        "cuerpo-incrustado" => "El cuerpo contiene un documento incrustado que no se puede verificar en el PDF.",
        "protegido" => "Outlook indica que el mensaje tiene restricciones.",
        "descarga" => "Outlook no ofrece Descargar para este adjunto. Puede ser un vínculo o un archivo protegido.",
        "descarga-incierta" => "Edge no ha entregado el adjunto identificable en la carpeta temporal. Comprueba la configuración de descargas del equipo.",
        "adjunto-correo" => "El propio mensaje contiene otro correo adjunto que requiere revisión.",
        "pdf-texto" => "El texto extraído del PDF no coincide con el mensaje mostrado por Outlook.",
        "pdf-imagen" => "El PDF no contiene todas las imágenes que muestra Outlook.",
        "cambio-mensaje" => "La vista del mensaje cambió durante la exportación.",
        "cerrar-vista" => "Outlook no ha cerrado la vista previa. Abre el borrador antes de volver a intentarlo.",
        "preview" or "acciones-correo" => "Outlook no ofrece la vista previa del correo seleccionado.",
        "lista-adjuntos" or "nombre-adjunto" or "acciones-adjunto" or "cambio-adjunto" => "No se ha podido identificar la lista completa de adjuntos de este correo.",
        _ => "La vista de Outlook no permite comprobar el mensaje. Copia el diagnóstico para identificar la fase."
    });

    private async Task<JsonElement> Run(string operation, object args, CancellationToken ct)
    {
        var result = await edge.Evaluate(Page, [origin, operation, args, (object?)activeRow ?? new {}], ct);
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("error", out var code)) throw Fault(code.GetString()!);
        return result;
    }
    private async Task<WebState> State(CancellationToken ct) => (await Run("state",new {},ct)).Deserialize<WebState>(Json)!;
    private static async Task<T> Wait<T>(Func<Task<T?>> get, string failure, int seconds, CancellationToken ct) where T : class
    {
        var watch = Stopwatch.StartNew();
        do
        {
            ct.ThrowIfCancellationRequested();
            var value = await get();
            if (value != null) return value;
            await Task.Delay(160, ct);
        } while (watch.Elapsed < TimeSpan.FromSeconds(seconds));
        throw Fault(failure);
    }
    private async Task Click(string task, object args, string failure, CancellationToken ct)
    {
        var element = await Wait(async () =>
        {
            var value = await Run(task, args, ct);
            return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("element-6066-11e4-a52e-4f735466cecf", out var id) ? id.GetString() : null;
        },failure,6,ct);
        await edge.Click(element,ct);
    }

    private async Task<WebState> Prepare(string token, Action<string,string> log, IProgress<string> progress, CancellationToken ct)
    {
        var url = await edge.Current(ct);
        origin = url.IsFile ? "null" : url.GetLeftPart(UriPartial.Authority);
        var state = await State(ct);
        log("Reconocer vista de Outlook",$"vista {state.Kind} · contador {state.Count} · tarjetas {state.Keys.Length}");
        if (state.Kind is "selection" or "draft" or "single") edge.SendToBackground(ct);
        if (state.Kind == "selection")
        {
            var rows = await ReadSelectedRows(token,state.Count,log,progress,ct);
            if (rows != null)
            {
                expectedDraft=0;
                return state with { Kind="rows", Rows=rows };
            }
            expectedDraft = state.Count;
            log("Preparar selección web", $"selección de Outlook {expectedDraft} · lectura mediante borrador");
            await Click("forward",new { count=expectedDraft },"reenviar",ct);
            state=await AwaitDraft(log,progress,ct);
        }
        if (state.Kind == "draft")
        {
            log("Leer lote web",$"elementos de Outlook {state.Count} · esperados {(expectedDraft>0?expectedDraft:state.Count)}");
            if (state.Count < 1 || state.Keys.Length != state.Count || expectedDraft > 0 && expectedDraft != state.Count) throw Fault("cantidad");
            return state;
        }
        if (state.Kind == "single") { expectedDraft = 0; return state with { Count = 1 }; }
        throw Fault(state.Kind switch { "compose" when state.Count==0 => "borrador-vacío", "compose" => "borrador", "preview" => "vista-abierta", _ => "seleccion" });
    }

    private async Task<WebState> AwaitDraft(Action<string,string> log, IProgress<string> progress, CancellationToken ct)
    {
        var clock=Stopwatch.StartNew();
        var idle=Stopwatch.StartNew();
        int peak=0;
        WebState last;
        do
        {
            ct.ThrowIfCancellationRequested();
            last=await State(ct);
            if(last.Kind=="draft" && last.Count==expectedDraft) return last;
            if(last.Kind=="draft" && last.Count>peak)
            {
                peak=last.Count;
                idle.Restart();
                progress.Report($"Outlook ha preparado {peak} de {expectedDraft} elementos…");
            }
            if(last.Kind=="draft" && last.Count>expectedDraft) break;
            await Task.Delay(300,ct);
        } while(idle.Elapsed.TotalSeconds<30 && clock.Elapsed.TotalMinutes<4);
        log("Comprobar entrega del borrador",$"vista {last.Kind} · seleccionados {expectedDraft} · tarjetas {last.Keys.Length} · elementos en la lista {(last.Kind is "compose" or "draft" ? last.Count : 0)} · máximo {peak}");
        throw Fault(last.Kind=="compose" && last.Count==0 && peak==0 ? "borrador-vacío" : "cantidad");
    }

    private async Task<WebMessage> Info(bool dialog, CancellationToken ct)
    {
        bool imagePending = false;
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 12)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var v = await Run("info",new { dialog },ct);
                if (v.ValueKind==JsonValueKind.Object) return v.Deserialize<WebMessage>(Json)!;
            }
            catch(WebExportException e) when(e.Code=="imagen") { imagePending = true; }
            await Task.Delay(200,ct);
        }
        throw Fault(imagePending ? "imagen" : "preview");
    }

    private static string Normalize(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormKC).Replace("\uFE0E","").Replace("\uFE0F","").ToLowerInvariant(), @"[^\p{L}\p{M}\p{N}]", "");
    private static bool Same(WebMessage a, WebMessage b) =>
        a.Title==b.Title && a.Body==b.Body && a.Images==b.Images && a.Headers.SequenceEqual(b.Headers) && a.Files.SequenceEqual(b.Files);

    private async Task WritePdf(WebMessage message, string path, bool dialog, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N");
        byte[] bytes;
        try
        {
            var printing = await Run("print",new { token,dialog },ct);
            if (printing.ValueKind != JsonValueKind.Object || !Same(message,printing.Deserialize<WebMessage>(Json)!)) throw Fault("cambio-mensaje");
            bytes = await edge.Pdf(ct);
        }
        finally
        {
            using var reset = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await Run("clear",new { token },reset.Token);
        }
        ct.ThrowIfCancellationRequested();
        if (!Same(message,await Info(dialog,ct))) throw Fault("cambio-mensaje");
        await File.WriteAllBytesAsync(path,bytes,ct);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(MailReader.FileHash(path)))) throw new IOException("No se ha podido comprobar el PDF en el destino.");
        using var pdf = PdfDocument.Open(path);
        if (pdf.NumberOfPages<1) throw Fault("pdf-texto");
        var pages = pdf.GetPages().ToArray();
        var expected = Normalize(string.Join(" ",new[] {message.Title}.Concat(message.Headers).Append(message.Body)));
        if (Normalize(string.Concat(pages.Select(p=>p.Text)))!=expected &&
            Normalize(string.Concat(pages.Select(p=>ContentOrderTextExtractor.GetText(p))))!=expected) throw Fault("pdf-texto");
        if (message.Images>0 && pages.Sum(p=>p.NumberOfImages)<message.Images) throw Fault("pdf-imagen");
    }

    private static bool SourceMail(string name) => Path.GetExtension(name).ToLowerInvariant() is ".eml" or ".msg";
    private static string FileName(string name)
    {
        var ext = Path.GetExtension(name);
        if (ext.Length is 0 or > 16) return MailReader.SafeName(name,100);
        return MailReader.SafeName(name[..^ext.Length],85) + "." + MailReader.SafeName(ext[1..],15);
    }
    private static bool BrowserName(string actual, string suggested)
    {
        if(actual.Equals(suggested,StringComparison.OrdinalIgnoreCase)) return true;
        var withoutCopy=Regex.Replace(Path.GetFileNameWithoutExtension(actual),@" \(\d+\)$","")+ Path.GetExtension(actual);
        var expected=MailReader.SafeName(suggested,240);
        return withoutCopy.Equals(expected,StringComparison.OrdinalIgnoreCase) || actual.Equals(expected,StringComparison.OrdinalIgnoreCase);
    }

    private async Task SaveFile(WebFile file, int index, bool dialog, string staging, CancellationToken ct)
    {
        if(file.Mail || SourceMail(file.Name)) throw Fault("adjunto-correo");
        if(Directory.EnumerateFileSystemEntries(edge.Downloads).Any()) { edge.Invalid=true; throw Fault("descarga-incierta"); }
        await Click("attachment",new {index,key=file.Key,dialog},"acciones-adjunto",ct);
        bool awaitingFile=false;
        try
        {
            await Click("menu",new {type="download"},"descarga",ct);
            awaitingFile=true;
            string? received=null;
            var timer=Stopwatch.StartNew();
            while(timer.Elapsed.TotalMinutes<3 && received==null)
            {
                ct.ThrowIfCancellationRequested();
                var files=Directory.GetFiles(edge.Downloads);
                if(files.Length==1 && BrowserName(Path.GetFileName(files[0]),file.Name) && !SourceMail(files[0]))
                {
                    try { using var exclusive=File.Open(files[0],FileMode.Open,FileAccess.Read,FileShare.None); received=files[0]; }
                    catch(IOException) { }
                }
                if(received==null) await Task.Delay(240,ct);
            }
            if(received==null) throw Fault("descarga-incierta");
            var name=FileName(file.Name);
            var target=Path.Combine(staging,name);
            for(int n=2;File.Exists(target);n++) target=Path.Combine(staging,Path.GetFileNameWithoutExtension(name)+" ("+n+")"+Path.GetExtension(name));
            using(var input=File.OpenRead(received))
            await using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { await input.CopyToAsync(output,ct); output.Flush(true); }
            if(MailReader.FileHash(received)!=MailReader.FileHash(target)) throw new IOException("La copia del adjunto no coincide.");
            File.Delete(received);
            awaitingFile=false;
        }
        finally { if(awaitingFile) edge.Invalid=true; }
    }

    internal async Task<WebResult> Export(string destination, IProgress<string> progress, DiagnosticLog log, CancellationToken ct)
    {
        var token=Guid.NewGuid().ToString("N");
        try { return await ExportBatch(destination,progress,log,token,ct); }
        finally
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { if(origin.Length>0 && !edge.Invalid) await Run("rows-clear",new {token},cleanup.Token); }
            catch(Exception error) { log.Fault("Retirar marcas de lectura",error); }
        }
    }

    private async Task<WebResult> ExportBatch(string destination,IProgress<string> progress,DiagnosticLog log,string token,CancellationToken ct)
    {
        // Check the destination before changing the selection in this window.
        Directory.CreateDirectory(destination);
        string runId=Guid.NewGuid().ToString("N");
        var probe=Path.Combine(destination,".CorreoDiario-"+runId+".tmp");
        using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)) { }
        activeRow=null;
        expandedByUs=null;
        selectionUntouched=true;
        var state=await Prepare(token,log.Event,progress,ct);
        bool direct=state.Kind=="rows";
        bool draft=state.Kind=="draft";
        string root=Path.Combine(destination,"CorreoDiario_"+DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss")+"_"+runId[..8]);
        Directory.CreateDirectory(root);
        var pending=new List<string>();
        int saved=0,attachments=0,processed=0;
        var queue=state.Rows.ToList();
        int total=state.Count;
        int unresolvedGroups=0;
        var reasons=new Dictionary<string,int>();
        bool cancelled=false;
        string? problem=null;
        string phase="Preparar lote web";
        try
        {
            for(int index=0;index<total;index++)
            {
                ct.ThrowIfCancellationRequested();
                progress.Report($"Exportando correo {index+1} de {total} en segundo plano…");
                string staging=Path.Combine(root,".en-curso-"+Guid.NewGuid().ToString("N"));
                string heading=$"Correo {index+1}";
                bool opened=false;
                try
                {
                    if(direct)
                    {
                        phase="Abrir correo de la lista";
                        var row=queue[index];
                        if(expandedByUs!=null && expandedByUs.Key!=row.ParentKey) await CloseOwnGroup(token,ct);
                        var context=await LocateRow(token,row,state.Count,ct);
                        if(context.GetProperty("group").GetBoolean())
                        {
                            phase="Leer mensajes de la conversación";
                            var members=await ReadMembers(token,row,context,ct);
                            queue.RemoveAt(index);
                            queue.InsertRange(index,members);
                            total=queue.Count;
                            log.Event(phase,$"mensajes identificados {members.Length}");
                            index--;
                            continue;
                        }
                        await OpenSelectedRow(token,row,state.Count,context,ct);
                    }
                    if(draft)
                    {
                        phase="Abrir vista previa de Outlook";
                        await Click("card",new {index,count=state.Count,key=state.Keys[index]},"acciones-correo",ct);
                        await Click("menu",new {type="preview"},"preview",ct);
                        opened=true;
                    }
                    phase="Leer cuerpo y adjuntos en Outlook";
                    var mail=await Info(draft,ct);
                    heading=mail.Title;
                    Directory.CreateDirectory(staging);
                    phase="Imprimir y comprobar PDF web";
                    await WritePdf(mail,Path.Combine(staging,"Correo.pdf"),draft,ct);
                    phase="Descargar adjunto web";
                    for(int a=0;a<mail.Files.Length;a++) await SaveFile(mail.Files[a],a,draft,staging,ct);
                    if(!Same(mail,await Info(draft,ct))) throw Fault("cambio-mensaje");
                    if(direct && (await Run("rows-ready",new {token,queue[index].Key,queue[index].ParentKey,before=(string?)null},ct)).GetString()!="listo") throw Fault("cambio-mensaje");
                    ct.ThrowIfCancellationRequested();
                    Directory.Move(staging,Path.Combine(root,$"{index+1:D3} - "+MailReader.SafeName(mail.Title,75)));
                    attachments+=mail.Files.Length;
                    saved++;
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
                catch(Exception error)
                {
                    log.Fault(phase,error);
                    problem=error is WebExportException ? error.Message : "Comprueba el diagnóstico de la operación.";
                    pending.Add($"{index+1}. {heading} · {problem}");
                    if(error is WebExportException web)
                    {
                        if(direct && !edge.Invalid && !reasons.ContainsKey(web.Code) && web.Code.StartsWith("panel-",StringComparison.Ordinal))
                        {
                            var evidence=await Run("rows-diagnostic",new {token},ct);
                            log.Event("Estructura del panel",$"cuerpos {evidence.GetProperty("bodies")} · documentos {evidence.GetProperty("documents")} · asuntos posibles {evidence.GetProperty("headings")} · acciones de respuesta {evidence.GetProperty("replies")} · tamaños de archivo {evidence.GetProperty("sizes")}");
                        }
                        reasons[web.Code]=reasons.GetValueOrDefault(web.Code)+1;
                        if(web.Code is "hilo-sin-desglose" or "panel-varios-cuerpos") unresolvedGroups++;
                    }
                    if((!direct && !opened) || edge.Invalid || error is not WebExportException ||
                        ((WebExportException)error).Code is "origen" or "cambio-lote" or "lista-cambiada" or "cambio-seleccion")
                    { processed=index+1; break; }
                }
                finally
                {
                    if(Directory.Exists(staging)) Directory.Delete(staging,true);
                }
                processed=index+1;
                if(opened)
                {
                    phase="Cerrar vista previa web";
                    await Click("close",new {},"cerrar-vista",ct);
                    await Wait(async()=> (await State(ct)).Kind=="draft" ? "ok":null,"cerrar-vista",8,ct);
                }
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { cancelled=true; }
        catch(Exception error) { log.Fault(phase,error); problem=error is WebExportException ? error.Message : "Revisa el diagnóstico de la operación."; }
        if(!ct.IsCancellationRequested && !edge.Invalid && expandedByUs!=null)
        {
            try { await CloseOwnGroup(token,ct); }
            catch(Exception error) { log.Fault("Cerrar desglose de la lista",error); }
        }
        if(saved!=total || cancelled || problem!=null)
        {
            for(int i=processed;i<total;i++) pending.Add($"{i+1}. Pendiente");
            File.WriteAllLines(Path.Combine(root,"Pendientes.txt"),new[] {$"Correos verificados: {saved} de {total}. Conversaciones pendientes de desglosar: {unresolvedGroups}.",problem??(cancelled?"Exportación cancelada.":"")}.Concat(pending));
        }
        if(saved==total && problem==null) expectedDraft=0;
        if(reasons.Count>0) log.Event("Pendientes del lote",string.Join(" · ",reasons.Select(p=>$"{p.Key} {p.Value}")));
        log.Event("Lote de Outlook web",$"verificados {saved}/{total} · adjuntos {attachments} · cancelado {(cancelled?"sí":"no")}");
        if(unresolvedGroups>0) problem=$"Quedan {unresolvedGroups} conversaciones por desglosar. Consulta Pendientes.txt.";
        else if(problem!=null && processed==total && total>1) problem="Consulta los mensajes pendientes en Pendientes.txt.";
        return new(root,total,saved,attachments,cancelled,problem,unresolvedGroups);
    }
}
