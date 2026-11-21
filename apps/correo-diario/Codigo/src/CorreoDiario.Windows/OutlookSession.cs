using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CorreoDiario.Windows;

internal sealed class OutlookSession : Form, CorreoDiario.Core.IOutlookTransport
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => ready.Task;
    public string SiteOrigin => new Uri(browser.Source?.AbsoluteUri ?? throw new InvalidOperationException("Abre Outlook primero.")).GetLeftPart(UriPartial.Authority);

    public OutlookSession(Uri url, string profile)
    {
        Text = "Iniciar sesión en Outlook · CorreoDiario";
        Size = new(1120, 800);
        StartPosition = FormStartPosition.CenterScreen;
        var hint = new Label { Text = "Inicia sesión en tu Outlook. Después vuelve a CorreoDiario y pulsa «Cargar carpetas».", Dock = DockStyle.Top, Height = 46, Padding = new(12), BackColor = Color.FromArgb(235, 242, 253) };
        Controls.Add(browser);
        Controls.Add(hint);
        Shown += async (_, _) =>
        {
            try
            {
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "CorreoDiario", "Sesiones", profile);
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await browser.EnsureCoreWebView2Async(env);
                browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                browser.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; browser.CoreWebView2.Navigate(e.Uri); };
                browser.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                browser.Source = url;
                ready.TrySetResult();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ready.TrySetException(new InvalidOperationException("Este equipo no tiene el componente Edge WebView2 necesario para abrir la ventana de Outlook. La aplicación requiere ese componente de Windows."));
                Close();
            }
            catch (Exception ex) { ready.TrySetException(ex); Close(); }
        };
        FormClosing += (_, e) => { if (!Disposing && Visible) { e.Cancel = true; Hide(); } };
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (IsDisposed || browser.CoreWebView2 is null) throw new InvalidOperationException("Abre de nuevo la sesión de Outlook.");
        var arguments = JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true, timeout = 90000 });
        var text = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", arguments).WaitAsync(ct);
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("exceptionDetails", out _))
            throw new InvalidOperationException("Outlook no ha podido completar la operación. Comprueba que has iniciado sesión y que la conexión sigue activa.");
        return doc.RootElement.GetProperty("result").GetProperty("value").Clone();
    }

    public async Task ClearSessionAsync()
    {
        if (browser.CoreWebView2 is not null)
            await browser.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
    }

    public async Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
    {
        if (action is not ("GetFolder" or "FindFolder" or "FindItem" or "GetItem" or "GetAttachment" or "MoveItem"))
            throw new InvalidOperationException("Operación no permitida.");
        var envelope = new Dictionary<string, object>
        {
            ["__type"] = action + "JsonRequest:#Exchange",
            ["Header"] = new Dictionary<string, object> { ["__type"] = "JsonRequestHeaders:#Exchange", ["RequestServerVersion"] = "Exchange2016" },
            ["Body"] = body
        };
        var input = JsonSerializer.Serialize(new { action, envelope });
        var script = "(async()=>{const cfg=" + input + ";" + """
            if(location.protocol!=='https:')return {ok:false,error:'La conexión debe utilizar HTTPS.'};
            const match=document.cookie.match(/(?:^|;\s*)X-OWA-CANARY=([^;]+)/i);
            const headers={'Content-Type':'application/json; charset=utf-8','Action':cfg.action,'X-Requested-With':'XMLHttpRequest','X-OWA-ActionName':cfg.action};
            if(match)headers['X-OWA-CANARY']=decodeURIComponent(match[1]);
            const knownCloud=/^(outlook\.live\.com|outlook\.office\.com|outlook\.office365\.com)$/i.test(location.hostname);
            const candidates=knownCloud?['/owa/0/service.svc','/owa/service.svc']:['/owa/service.svc','/owa/0/service.svc'];
            const selected=window.__correoDiarioEndpoint;
            const endpoints=selected?[selected]:candidates;
            for(const endpoint of endpoints){
              const response=await fetch(endpoint+'?action='+encodeURIComponent(cfg.action)+'&app=Mail',{method:'POST',credentials:'same-origin',redirect:'error',headers,body:JSON.stringify(cfg.envelope),signal:AbortSignal.timeout(60000)});
              if(!response.ok){
                // Reads may probe the alternative path. A move is never retried automatically.
                if(cfg.action==='MoveItem'||selected||![404,405].includes(response.status))return {ok:false,status:response.status,error:'Outlook ha rechazado la conexión ('+response.status+').'};
                continue;
              }
              let value;try{value=await response.json();}catch{return {ok:false,error:'Inicia sesión y espera a que aparezca la bandeja de Outlook.'};}
              window.__correoDiarioEndpoint=endpoint;
              return {ok:true,data:value};
            }
            return {ok:false,error:'Esta versión de Outlook no ofrece una conexión compatible con esta primera versión.'};
            })()
            """;
        var response = await EvaluateAsync(script, ct);
        if (!response.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new InvalidOperationException(response.TryGetProperty("error", out var err) ? err.GetString() : "No se ha podido conectar a Outlook.");
        var data = response.GetProperty("data");
        ValidateResponse(data);
        return data.Clone();
    }

    private static void ValidateResponse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("ResponseCode", out var code) && code.GetString() is string value && value != "NoError")
                throw new InvalidOperationException("Outlook ha rechazado la operación: " + value + ". No se continúa.");
            if (element.TryGetProperty("ResponseClass", out var cls) && cls.GetString() == "Error")
                throw new InvalidOperationException("Outlook ha devuelto un error. No se continúa.");
            if (element.TryGetProperty("ExceptionName", out _) || element.TryGetProperty("ErrorCode", out var error) && error.ToString() is not ("0" or ""))
                throw new InvalidOperationException("La sesión de Outlook no permite esta operación. Vuelve a iniciar sesión o prueba con otra dirección de Outlook.");
            foreach (var property in element.EnumerateObject()) ValidateResponse(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) ValidateResponse(child);
    }
}
