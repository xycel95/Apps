using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CorreoDiario.EdgeApp;

internal sealed partial class OutlookSelection
{
    private static readonly CultureInfo Spanish=CultureInfo.GetCultureInfo("es-ES");

    private static DateTime? MessageTime(string text,string language)
    {
        var iso=Regex.Match(text,@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})\b",RegexOptions.IgnoreCase);
        if(iso.Success && DateTimeOffset.TryParse(iso.Value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var absolute))
            return TimeZoneInfo.ConvertTime(absolute,MailReader.Madrid).DateTime;
        var local=Regex.Match(text,@"\b(?:\d{4}-\d{2}-\d{2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{4}|\d{1,2}\s+(?:de\s+)?[\p{L}.]+\s+(?:de\s+)?\d{4})(?:T|[,\s]+(?:a las\s+|at\s+)?)(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\b",RegexOptions.IgnoreCase);
        var culture=language.StartsWith("en-US",StringComparison.OrdinalIgnoreCase)?CultureInfo.GetCultureInfo("en-US"):Spanish;
        return local.Success && DateTime.TryParse(local.Value.Replace("a las ","").Replace(" at "," "),culture,DateTimeStyles.AllowWhiteSpaces,out var date)?date:null;
    }

    private async Task<string> FolderName(string title,CancellationToken ct)
    {
        var token=Guid.NewGuid().ToString("N");
        var meta=await Run("metadata",new {token},ct);
        if(meta.ValueKind!=JsonValueKind.Object) throw Fault("cabecera-nombre");
        var dates=meta.GetProperty("dates").Deserialize<string[]>(Json)!;
        var language=meta.GetProperty("language").GetString() ?? "es";
        var times=dates.Select(d=>MessageTime(d,language)).OfType<DateTime>()
            .Select(t=>new DateTime(t.Year,t.Month,t.Day,t.Hour,t.Minute,0)).Distinct().ToArray();

        string sender=meta.GetProperty("sender").GetString() ?? "";
        if(sender.Length==0)
        {
            string person=meta.GetProperty("person").GetString() ?? "";
            if(person.Length>0)
            {
                var args=new {token,person};
                var control=await Run("sender-open",args,ct);
                if(control.ValueKind==JsonValueKind.Object)
                {
                    try
                    {
                        await edge.Click(ElementId(control),ct);
                        sender=await Wait(async ()=>(await Run("sender-value",args,ct)).GetString(),"direccion-del-remitente",3,ct);
                    }
                    catch(WebExportException e) when(e.Code=="direccion-del-remitente") { }
                    finally { await CloseDisclosure("sender-close",args); }
                }
            }
        }
        if(sender.Length==0)
        {
            var args=new {token};
            var more=await Run("sender-details",args,ct);
            if(more.ValueKind==JsonValueKind.Object)
            {
                try
                {
                    await edge.Click(ElementId(more),ct);
                    sender=await Wait(async ()=>(await Run("sender-details-value",args,ct)).GetString(),"direccion-del-remitente",2,ct);
                }
                catch(WebExportException e) when(e.Code=="direccion-del-remitente") { }
                finally { await CloseDisclosure("sender-details-close",args); }
            }
        }
        if(sender.Length>0 && times.Length==1) return MessageFolders.Name(times[0],sender,title);
        // Read only From and Date in Outlook's message-details dialog; never inspect the signature.
        var details=await MessageDetails(ct);
        if(details.ValueKind==JsonValueKind.Object)
        {
            if(!MimeKit.MailboxAddress.TryParse(details.GetProperty("sender").GetString(),out var address) ||
                sender.Length>0 && !sender.Equals(address.Address,StringComparison.OrdinalIgnoreCase))
                throw Fault("direccion-del-remitente");
            if(!MimeKit.Utils.DateUtils.TryParse(details.GetProperty("date").GetString(),out var sent))
                throw Fault("fecha-del-correo");
            return MessageFolders.Name(times.Length==1?times[0]:TimeZoneInfo.ConvertTime(sent,MailReader.Madrid).DateTime,address.Address,title);
        }
        throw Fault(times.Length==1?"direccion-del-remitente":"fecha-del-correo");
    }

    private static string ElementId(JsonElement el)=>el.GetProperty("element-6066-11e4-a52e-4f735466cecf").GetString()!;
    private async Task CloseDisclosure(string operation, object args)
    {
        using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var close=await Run(operation,args,cleanup.Token);
        if(close.ValueKind==JsonValueKind.Object) await edge.Click(ElementId(close),cleanup.Token);
        else await edge.Escape(cleanup.Token);
        await Run("control-clear",args,cleanup.Token);
    }

    private async Task<JsonElement> MessageDetails(CancellationToken ct)
    {
        var token=Guid.NewGuid().ToString("N");
        var args=new {token};
        var trigger=await Run("details-start",args,ct);
        if(trigger.ValueKind!=JsonValueKind.Object) return default;
        try
        {
            await edge.Click(ElementId(trigger),ct);
            for(int step=0;step<2;step++)
            {
                var item=await Wait(async ()=>
                {
                    var v=await Run("details-action",new {token,submenu=step!=0},ct);
                    return v.ValueKind==JsonValueKind.Object?v.GetRawText():null;
                },"cabecera-nombre",2,ct);
                var action=JsonSerializer.Deserialize<JsonElement>(item);
                await edge.Click(ElementId(action.GetProperty("element")),ct);
                if(action.GetProperty("kind").GetString()=="details") break;
            }
            string data=await Wait(async ()=>
            {
                var m=await Run("details-values",args,ct);
                return m.ValueKind==JsonValueKind.Object?m.GetRawText():null;
            },"cabecera-nombre",3,ct);
            return JsonSerializer.Deserialize<JsonElement>(data);
        }
        catch(WebExportException e) when(e.Code=="cabecera-nombre") { return default; }
        finally { await CloseDisclosure("details-close",args); }
    }
}
