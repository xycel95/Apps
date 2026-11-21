using System.Text.Json;

namespace CorreoDiario.Core;

public sealed class OwaPageTransport(IOutlookPage page) : IOutlookTransport
{
    public async Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
    {
        if (action is not ("GetFolder" or "FindFolder" or "FindItem" or "GetItem" or "GetAttachment" or "MoveItem"))
            throw new InvalidOperationException("Operación no permitida.");
        var operationLabel = action switch { "GetFolder" => "la lectura de carpetas", "FindFolder" => "la lista de carpetas", "FindItem" => "la búsqueda de correos",
            "GetItem" => "la lectura del correo", "GetAttachment" => "la descarga del adjunto", _ => "el movimiento a Eliminados" };
        var source = page as IOutlookAuthorizationSource;
        var authorization = source is null ? null : await source.GetAuthorizationAsync(false, ct);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var envelope = new Dictionary<string, object>
            {
                ["__type"] = action + "JsonRequest:#Exchange",
                ["Header"] = new Dictionary<string, object> { ["__type"] = "JsonRequestHeaders:#Exchange", ["RequestServerVersion"] = authorization?.ServerVersion ?? "Exchange2016" },
                ["Body"] = body
            };
            var input = JsonSerializer.Serialize(new { action, operationLabel, envelope, serviceUrl = authorization?.Endpoint.AbsoluteUri,
                sessionHeaders = authorization?.CopyRequestHeaders() ?? new Dictionary<string, string>(), urlPostData = authorization?.UrlPostData ?? false });
            var script = "(async()=>{const cfg=" + input + ";" + """
                if(location.protocol!=='https:')return {ok:false,error:'La conexión debe utilizar HTTPS.'};
                const match=document.cookie.match(/(?:^|;\s*)X-OWA-CANARY=([^;]+)/i);
                const headers={'Accept':'application/json','Content-Type':'application/json; charset=utf-8','Action':cfg.action,'X-Requested-With':'XMLHttpRequest','X-Req-Source':'Mail','X-OWA-ActionName':cfg.action};
                if(match)headers['X-OWA-CANARY']=decodeURIComponent(match[1]);
                Object.assign(headers,cfg.sessionHeaders);
                if(cfg.urlPostData)headers['X-OWA-UrlPostData']=encodeURIComponent(JSON.stringify(cfg.envelope));
                const knownCloud=/^(outlook\.live\.com|outlook\.office\.com|outlook\.office365\.com)$/i.test(location.hostname);
                const candidates=knownCloud?['/owa/0/service.svc','/owa/service.svc']:['/owa/service.svc','/owa/0/service.svc'];
                const selected=window.__correoDiarioEndpointV3;
                const endpoints=cfg.serviceUrl?[cfg.serviceUrl]:(candidates.includes(selected)?[selected]:candidates).map(p=>location.origin+p);
                for(const endpoint of endpoints){
                  const response=await fetch(endpoint+'?action='+encodeURIComponent(cfg.action)+'&app=Mail',{method:'POST',credentials:cfg.serviceUrl?'include':'same-origin',redirect:'error',headers,body:JSON.stringify(cfg.envelope),signal:AbortSignal.timeout(60000)});
                  if(!response.ok){
                    if(cfg.action==='MoveItem'||cfg.serviceUrl||candidates.includes(selected)||![404,405].includes(response.status))return {ok:false,status:response.status,error:'Outlook ha rechazado '+cfg.operationLabel+' ('+cfg.action+', '+response.status+').'};
                    continue;
                  }
                  let value;try{value=await response.json();}catch{return {ok:false,error:'La pestaña no muestra un buzón de Outlook disponible. Abre tu correo en el navegador y vuelve a seleccionarlo.'};}
                  if(!cfg.serviceUrl)window.__correoDiarioEndpointV3=new URL(endpoint).pathname;
                  return {ok:true,data:value};
                }
                return {ok:false,error:'El servicio de esta versión de Outlook no es compatible con esta beta.'};
                })()
                """;
            var response = await page.EvaluateAsync(script, ct);
            if (!response.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                var rejected = response.TryGetProperty("status", out var status) && status.TryGetInt32(out var code) && code is 401 or 440;
                if (rejected && action != "MoveItem" && attempt == 0 && source is not null)
                {
                    if (authorization is not null) source.InvalidateAuthorization(authorization);
                    authorization = await source.GetAuthorizationAsync(true, ct);
                    if (authorization is not null) continue;
                }
                if (rejected && action == "MoveItem") throw new InvalidOperationException("Outlook no ha autorizado el movimiento. El programa se ha detenido sin reintentarlo; comprueba el buzón antes de repetirlo.");
                if (rejected) throw new InvalidOperationException("Outlook no ha autorizado esta lectura. Pulsa Actualizar en la pestaña de Outlook y vuelve a «Usar esta pestaña» en CorreoDiario. No necesitas iniciar sesión en el programa.");
                throw new InvalidOperationException(response.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : "Outlook no ha podido completar la operación.");
            }
            var data = response.GetProperty("data");
            ValidateResponse(data);
            return data.Clone();
        }
        throw new InvalidOperationException("Outlook no ha podido completar la operación autorizada.");
    }

    private static void ValidateResponse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("ResponseCode", out var code) && code.ValueKind == JsonValueKind.String && code.GetString() != "NoError")
                throw new InvalidOperationException("Outlook ha rechazado la operación: " + code.GetString() + ". No se continúa.");
            if (element.TryGetProperty("ResponseClass", out var cls) && cls.ToString() == "Error")
                throw new InvalidOperationException("Outlook ha devuelto un error. No se continúa.");
            if (element.TryGetProperty("ExceptionName", out _) || element.TryGetProperty("ErrorCode", out var error) && error.ToString() is not ("0" or ""))
                throw new InvalidOperationException("La pestaña de Outlook no permite esta operación. Comprueba el correo en el navegador.");
            foreach (var property in element.EnumerateObject()) ValidateResponse(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) ValidateResponse(child);
    }
}
