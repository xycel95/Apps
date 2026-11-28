using System.Diagnostics;
using System.Text.Json;

namespace CorreoDiario.EdgeApp;

internal sealed record WebRow(string Key, string Text, double Y, double Top, string? ParentKey=null, string[]? ParentTitles=null);
internal sealed record FolderPage(double Top, double Height, bool End, WebRow[] Rows);

internal sealed partial class OutlookSelection
{
    private async Task<WebRow[]?> ReadSelectedRows(string token, int count, Action<string,string> log,
        IProgress<string> progress, CancellationToken ct)
    {
        var start = await Run("rows-start", new { token, count }, ct);
        if (!start.GetProperty("available").GetBoolean())
        {
            log("Leer lista de la carpeta", $"selección {count} · lista con marcas no identificada");
            return null;
        }
        var saved = start.GetProperty("saved").GetDouble();
        log("Comenzar lectura de filas",$"selección {count} · página {(start.GetProperty("visible").GetBoolean()?"visible":"en pausa")}");
        var seen = new Dictionary<string,WebRow>(StringComparer.Ordinal);
        Dictionary<string,WebRow>? firstPass=null;
        var watch = Stopwatch.StartNew();
        var lastGrowth = Stopwatch.StartNew();
        int unchangedAtEnd = 0;
        double previousTop = -1;
        bool ambiguous = false;
        while (watch.Elapsed < TimeSpan.FromMinutes(10) && lastGrowth.Elapsed < TimeSpan.FromMinutes(2))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(260,ct);
            var page = (await Run("rows-page", new { token, count }, ct)).Deserialize<FolderPage>(Json)!;
            var before = seen.Count;
            var inPage = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in page.Rows)
            {
                if (row.Key.Length==0 || row.Text.Length==0 || !inPage.Add(row.Key)) { ambiguous=true; break; }
                if (!seen.TryGetValue(row.Key,out var first)) { seen.Add(row.Key,row); continue; }
                // Text identities at distinct positions can be two different messages with the same subject.
                if (first.Text!=row.Text || ((row.Key.StartsWith("texto=") || row.Key.StartsWith("etiqueta=") || row.Key.StartsWith("id=")) &&
                    Math.Abs(first.Y-row.Y)>8)) { ambiguous=true; break; }
            }
            if (ambiguous || seen.Count>count) break;
            if (seen.Count!=before)
            {
                lastGrowth.Restart();
                progress.Report(firstPass==null ? $"Localizados {seen.Count} de {count} seleccionados en la carpeta…" : $"Confirmados {seen.Count} de {count} seleccionados…");
            }
            if (seen.Count==count)
            {
                if(firstPass==null)
                {
                    firstPass=seen;
                    seen=new(StringComparer.Ordinal);
                    await Run("rows-scroll",new {token,top=0},ct);
                    previousTop=-1;
                    unchangedAtEnd=0;
                    continue;
                }
                if(!seen.All(pair=>firstPass.TryGetValue(pair.Key,out var original) && original.Text==pair.Value.Text))
                {
                    log("Confirmar selección de la carpeta",$"selección {count} · identidades cambiadas entre lecturas");
                    throw Fault("cambio-seleccion");
                }
                log("Leer lista de la carpeta",$"selección {count} · filas identificadas {seen.Count} · dos lecturas coincidentes");
                return seen.Values.OrderBy(r=>r.Y).ToArray();
            }
            if (page.End || page.Top==previousTop)
            {
                if (++unchangedAtEnd>=12) break;
            }
            else unchangedAtEnd=0;
            previousTop=page.Top;
            await Run("rows-scroll", new { token, top=page.Top+Math.Max(50,page.Height*0.65) }, ct);
        }
        log("Leer lista de la carpeta",$"selección {count} · filas identificadas {seen.Count} · identidades ambiguas {(ambiguous?"sí":"no")}");
        await Run("rows-scroll",new { token, top=saved },ct);
        if(firstPass!=null) throw Fault("cambio-seleccion");
        return null;
    }

    // The first activation consumes the original multi-selection even if its panel fails to load.
    private bool selectionUntouched;
    private JsonElement? activeRow;
    private WebRow? expandedByUs;

    private object RowArgs(string token, WebRow row, int count) =>
        new {token,row.Key,row.Text,row.ParentKey,row.ParentTitles,first=selectionUntouched,count};

    private async Task<JsonElement> LocateRow(string token, WebRow row, int count, CancellationToken ct)
    {
        var args=RowArgs(token,row,count);
        var found=await Run("rows-inspect",args,ct);
        if(found.ValueKind==JsonValueKind.Object) return found;
        await Run("rows-scroll",new {token,top=row.Top},ct);
        for(int pass=0;pass<4;pass++)
        {
            await Task.Delay(110,ct);
            found=await Run("rows-inspect",args,ct);
            if(found.ValueKind==JsonValueKind.Object) return found;
        }
        // Expanding conversations and Outlook's changing row heights can shift the saved viewport.
        await Run("rows-scroll",new {token,top=0},ct);
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed<TimeSpan.FromSeconds(35))
        {
            await Task.Delay(200,ct);
            found=await Run("rows-inspect",args,ct);
            if(found.ValueKind==JsonValueKind.Object) return found;
            var page=await Run("rows-metrics",new {token},ct);
            if(page.GetProperty("end").GetBoolean()) break;
            await Run("rows-scroll",new {token,top=page.GetProperty("top").GetDouble()+Math.Max(40,page.GetProperty("height").GetDouble()*0.7)},ct);
        }
        throw Fault("fila-ausente");
    }

    private async Task<WebRow[]> ReadMembers(string token, WebRow parent, JsonElement context, CancellationToken ct)
    {
        var args=new {token,parent.Key,expanded=false};
        if(!context.GetProperty("expanded").GetBoolean())
        {
            var control=await Run("rows-toggle",args,ct);
            if(control.ValueKind!=JsonValueKind.Object) throw Fault("hilo-sin-desglose");
            await edge.Click(control.GetProperty("element-6066-11e4-a52e-4f735466cecf").GetString()!,ct);
            selectionUntouched=false;
            expandedByUs=parent;
        }
        WebRow[]? previous=null;
        var titles=context.GetProperty("titles").Deserialize<string[]>(Json);
        return await Wait(async ()=>
        {
            var result=await Run("rows-members",args,ct);
            var current=result.ValueKind==JsonValueKind.Array?result.Deserialize<WebRow[]>(Json):null;
            if(current is {Length:>0} && previous!=null && current.Select(r=>(r.Key,r.Text)).SequenceEqual(previous.Select(r=>(r.Key,r.Text))))
                return current.Select(r=>r with { ParentTitles=titles }).ToArray();
            previous=current;
            return null;
        },"hilo-sin-desglose",5,ct);
    }

    private async Task CloseOwnGroup(string token, CancellationToken ct)
    {
        if(expandedByUs is not { } group) return;
        expandedByUs=null;
        var args=new {token,group.Key,expanded=true};
        var button=await Run("rows-toggle",args,ct);
        if(button.ValueKind==JsonValueKind.Object) await edge.Click(button.GetProperty("element-6066-11e4-a52e-4f735466cecf").GetString()!,ct);
    }

    private async Task OpenSelectedRow(string token, WebRow row, int count, JsonElement context, CancellationToken ct)
    {
        var args=RowArgs(token,row,count);
        var before=(await Run("rows-before",args,ct)).GetString();
        var element=await Run("rows-open",args,ct);
        await edge.Click(element.GetProperty("element-6066-11e4-a52e-4f735466cecf").GetString()!,ct);
        selectionUntouched=false;
        activeRow=context;
        var timer=Stopwatch.StartNew();
        string reason="cargando";
        while(timer.Elapsed<TimeSpan.FromSeconds(10))
        {
            ct.ThrowIfCancellationRequested();
            reason=(await Run("rows-ready",new {token,row.Key,row.ParentKey,before},ct)).GetString()!;
            if(reason=="listo") return;
            await Task.Delay(180,ct);
        }
        throw Fault("panel-"+reason);
    }
}
