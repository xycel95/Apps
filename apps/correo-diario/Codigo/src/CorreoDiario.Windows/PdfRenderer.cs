using System.Net;
using System.Text.Json;
using CorreoDiario.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CorreoDiario.Windows;

internal sealed class PdfRenderer : Form, IPdfRenderer
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
    private bool initialized;
    public PdfRenderer()
    {
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new(-30000, -30000);
        Size = new(900, 800);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Text = "Preparando PDF · CorreoDiario";
        Controls.Add(browser);
    }

    public async Task<byte[]> RenderAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!initialized)
        {
            Show();
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "CorreoDiario", "Impresion");
            await browser.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null, data));
            browser.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            await NavigateAsync("<!doctype html><html><body>CorreoDiario</body></html>", ct);
            initialized = true;
        }
        var inline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in item.Attachments.Where(a => a.ContentId.Length > 0))
        {
            if (!attachments.TryGetValue(a.Id, out var bytes)) throw new InvalidDataException("Falta un adjunto necesario para componer el PDF.");
            var mime = a.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? a.MimeType : "application/octet-stream";
            inline[a.ContentId.Trim('<', '>')] = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
        }
        var input = JsonSerializer.Serialize(new { html = item.Html, inline });
        // Parse the original HTML in an inert document. No email-provided script runs.
        var script = "(()=>{const input=" + input + ";" + """
            const doc=new DOMParser().parseFromString(input.html,'text/html');
            for(const n of doc.querySelectorAll('script,style,iframe,frame,frameset,object,embed,form,input,button,textarea,select,meta,link,base,svg,math'))n.remove();
            let external=0,missing=0;
            for(const node of doc.querySelectorAll('*')){
              for(const a of [...node.attributes]){
                if(/^on/i.test(a.name)||['srcset','background','formaction','action','autofocus','contenteditable','srcdoc'].includes(a.name.toLowerCase()))node.removeAttribute(a.name);
              }
              if(node.style){for(const p of ['position','top','left','right','bottom','z-index','transform'])node.style.removeProperty(p);}
              if(node.tagName==='A'){
                const href=node.getAttribute('href')||'';
                if(!/^(https?:|mailto:)/i.test(href))node.removeAttribute('href');
              }
              if(node.tagName==='IMG'){
                const src=node.getAttribute('src')||'';
                if(/^cid:/i.test(src)){
                  let id=src.slice(4).replace(/^<|>$/g,'');try{id=decodeURIComponent(id);}catch{}
                  const key=Object.keys(input.inline).find(k=>k.toLowerCase()===id.toLowerCase());
                  if(key)node.setAttribute('src',input.inline[key]);else{missing++;node.remove();}
                }else if(!/^data:image\/(png|jpe?g|gif|webp|bmp);base64,/i.test(src)){
                  const text=doc.createElement('span');text.textContent='[Imagen externa'+(node.alt?': '+node.alt:'')+']';node.replaceWith(text);external++;
                }
              }
            }
            return {html:doc.body.innerHTML,external,missing};
            })()
            """;
        var raw = await browser.CoreWebView2.ExecuteScriptAsync(script).WaitAsync(ct);
        using var cleaned = JsonDocument.Parse(raw);
        if (cleaned.RootElement.GetProperty("missing").GetInt32() != 0)
            throw new InvalidDataException("Falta una imagen adjunta que forma parte del correo. No se considera completa la exportación.");
        string E(string s) => WebUtility.HtmlEncode(s);
        var local = TimeZoneInfo.ConvertTime(item.SentAt ?? item.Timestamp, TimeZoneInfo.Local);
        var body = cleaned.RootElement.GetProperty("html").GetString();
        var html = """
            <!doctype html><html lang="es"><head><meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:; base-uri 'none'; form-action 'none'">
            <style>@page{size:A4;margin:15mm}body{font:11pt 'Segoe UI',Arial,sans-serif;color:#111;line-height:1.4;overflow-wrap:anywhere}h1{font-size:18pt;margin:0 0 18px}table.header{width:100%;border-bottom:1px solid #aaa;padding-bottom:14px;margin-bottom:22px}table.header th{width:85px;text-align:left;vertical-align:top;padding:2px 14px 2px 0}table.header td{padding:2px 0}.message img{max-width:100%;height:auto}.message table{max-width:100%}.notice{margin-top:20px;color:#555;font-size:9pt}pre{white-space:pre-wrap}</style></head><body>
            """;
        html += "<h1>" + E(item.Subject) + "</h1><table class=header>";
        foreach (var row in new[] { ("De:", item.From), ("Enviado:", local.ToString("dd/MM/yyyy HH:mm:ss")), ("Para:", item.To), ("CC:", item.Cc), ("CCO:", item.Bcc),
            ("Adjuntos:", string.Join("; ", item.Attachments.Select(a => a.Name))) })
            if (!string.IsNullOrEmpty(row.Item2)) html += "<tr><th>" + E(row.Item1) + "</th><td>" + E(row.Item2) + "</td></tr>";
        html += "</table><main class=message>" + body + "</main>";
        if (cleaned.RootElement.GetProperty("external").GetInt32() > 0)
            html += "<p class=notice>Las imágenes externas se han dejado indicadas en el texto. Las imágenes adjuntas están incluidas.</p>";
        html += "</body></html>";
        await NavigateAsync(html, ct);
        var ready = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new
        {
            expression = "(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode().catch(()=>{throw new Error('Imagen incompleta')})));return true})()",
            awaitPromise = true, returnByValue = true
        })).WaitAsync(ct);
        using (var parsed = JsonDocument.Parse(ready))
            if (parsed.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidDataException("No se han podido incluir todas las imágenes adjuntas en el PDF.");
        var visibleText = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText").WaitAsync(ct)) ?? "";
        var temp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "CorreoDiario", "Temporales");
        Directory.CreateDirectory(temp);
        var path = Path.Combine(temp, Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var settings = browser.CoreWebView2.Environment.CreatePrintSettings();
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintHeaderAndFooter = false;
            settings.PageWidth = 8.2677;
            settings.PageHeight = 11.6929;
            if (!await browser.CoreWebView2.PrintToPdfAsync(path, settings).WaitAsync(ct))
                throw new InvalidDataException("No se ha podido imprimir el correo a PDF.");
            var bytes = await File.ReadAllBytesAsync(path, ct);
            PdfVerifier.Validate(bytes, visibleText);
            return bytes;
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private async Task NavigateAsync(string html, CancellationToken ct)
    {
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Finished(object? sender, CoreWebView2NavigationCompletedEventArgs e) => ready.TrySetResult(e.IsSuccess);
        browser.NavigationCompleted += Finished;
        try
        {
            browser.NavigateToString(html);
            if (!await ready.Task.WaitAsync(TimeSpan.FromSeconds(45), ct)) throw new InvalidDataException("No se ha podido preparar el contenido del PDF.");
        }
        finally { browser.NavigationCompleted -= Finished; }
    }
}
