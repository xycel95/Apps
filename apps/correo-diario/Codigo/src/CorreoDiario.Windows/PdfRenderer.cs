using System.Net;
using System.Text.Json;
using CorreoDiario.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CorreoDiario.Windows;

internal sealed class PdfRenderer : Form, IVerifiedPdfRenderer
{
    internal const string SimplifiedNotice = "Formato simplificado: se conserva el texto del correo. Las imágenes incluidas se muestran al final.";
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

    public async Task<byte[]> RenderAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct) =>
        (await RenderVerifiedAsync(item, attachments, ct)).Bytes;

    public async Task<PdfRenderOutput> RenderVerifiedAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
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
              if(node.style){for(const p of ['position','top','left','right','bottom','z-index','transform','float','columns','column-count','column-width','width','min-width','max-width','height','min-height','max-height','overflow','overflow-x','overflow-y','white-space','text-overflow','clip','clip-path'])node.style.removeProperty(p);}
              if(['TABLE','TR','TD','TH','DIV','P','PRE'].includes(node.tagName)){
                node.removeAttribute('width');node.removeAttribute('height');node.removeAttribute('nowrap');
              }
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
            <style>
            @page{size:A4;margin:15mm}
            body{margin:0;max-width:180mm;font:11pt 'Segoe UI',Arial,sans-serif;color:#111;line-height:1.4;overflow-wrap:anywhere}
            h1{font-size:18pt;margin:0 0 18px}
            table.header{width:100%;table-layout:fixed;border-bottom:1px solid #aaa;padding-bottom:14px;margin-bottom:22px}
            table.header th{width:85px;text-align:left;vertical-align:top;padding:2px 14px 2px 0}table.header td{padding:2px 0}
            .message,.message *{box-sizing:border-box;min-width:0!important;max-width:100%!important;overflow-wrap:anywhere!important}
            .message *:not(img){height:auto!important;min-height:0!important;max-height:none!important;overflow:visible!important;white-space:normal!important;text-overflow:clip!important;float:none!important;columns:auto!important}
            .message img{height:auto!important}
            .message table{width:100%!important;table-layout:fixed!important}
            .message thead,.message tfoot{display:table-row-group}
            .message pre{white-space:pre-wrap!important}
            .notice{margin-top:20px;color:#555;font-size:9pt}
            </style></head><body>
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
        await ReadyAsync(ct);
        var visibleText = JsonSerializer.Deserialize<string>(await browser.CoreWebView2.ExecuteScriptAsync("document.body.innerText").WaitAsync(ct)) ?? "";
        var formatted = await PrintAsync(ct);
        try
        {
            return new(formatted, new("formatted", PdfVerifier.ValidateText(formatted, visibleText)));
        }
        catch (InvalidDataException ex) when (ex.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck)
        {
            var first = (PdfTextCheck)ex.Data[PdfVerifier.MismatchDataKey]!;
            try
            {
                ct.ThrowIfCancellationRequested();
                // The reference is taken from the SAME local document. Never derive
                // expected text from the PDF, and never accept the failed first PDF.
                var snapshot = await browser.CoreWebView2.ExecuteScriptAsync("""
                    (()=>{
                      const original=document.body.innerText;
                      const counter=(n,type)=>{
                        if(/^(lower|upper)-(alpha|latin)$/.test(type)&&n>0){let s='';for(let x=n;x>0;x=Math.floor((x-1)/26))s=String.fromCharCode(97+(x-1)%26)+s;return type.startsWith('upper')?s.toUpperCase():s;}
                        if(/^(lower|upper)-roman$/.test(type)&&n>0&&n<4000){let s='',x=n;for(const [v,c] of [[1000,'M'],[900,'CM'],[500,'D'],[400,'CD'],[100,'C'],[90,'XC'],[50,'L'],[40,'XL'],[10,'X'],[9,'IX'],[5,'V'],[4,'IV'],[1,'I']])while(x>=v){s+=c;x-=v;}return type.startsWith('lower')?s.toLowerCase():s;}
                        return type==='decimal-leading-zero'&&n>=0&&n<10?'0'+n:String(n);
                      };
                      // innerText omits CSS-generated list markers. Materialize the
                      // numbers before flattening so list references are retained.
                      for(const list of document.querySelectorAll('.message ol,.message ul')){
                        const items=[...list.children].filter(n=>n.tagName==='LI'),ordered=list.tagName==='OL',step=list.reversed?-1:1;
                        let n=list.hasAttribute('start')?list.start:(list.reversed?items.length:1);
                        for(const li of items){
                          if(li.hasAttribute('value'))n=li.value;
                          const type=getComputedStyle(li).listStyleType;
                          if(type!=='none'){
                            const marker=document.createElement('span');marker.textContent=ordered?counter(n,type)+'. ':'• ';
                            li.prepend(marker);li.style.setProperty('list-style-type','none','important');
                          }
                          n+=step;
                        }
                      }
                      return {original,text:document.body.innerText,images:[...document.querySelectorAll('.message img')].map(i=>i.getAttribute('src')),links:[...document.querySelectorAll('.message a[href]')].map(a=>a.getAttribute('href'))};
                    })()
                    """).WaitAsync(ct);
                using var captured = JsonDocument.Parse(snapshot);
                if (captured.RootElement.GetProperty("original").GetString() != visibleText)
                    throw new InvalidDataException("El contenido local ha cambiado al preparar el PDF. Se conserva el correo original.");
                var reference = captured.RootElement.GetProperty("text").GetString() ?? "";
                var images = captured.RootElement.GetProperty("images").EnumerateArray().Select(i => i.GetString() ?? "").ToArray();
                var simple = """
                    <!doctype html><html lang="es"><head><meta charset="utf-8">
                    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
                    <style>@page{size:A4;margin:15mm}body{margin:0;max-width:180mm;font:11pt Arial,sans-serif;color:#111;line-height:1.4;font-variant-ligatures:none}
                    pre{margin:0;white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}p{margin:0 0 16px}figure{margin:16px 0;break-inside:avoid}img{display:block;max-width:100%;max-height:230mm;width:auto;height:auto}figcaption{margin-bottom:8px}</style>
                    </head><body>
                    """;
                simple += "<p>" + E(SimplifiedNotice) + "</p><pre>" + E(reference) + "</pre>";
                var expected = SimplifiedNotice + "\n" + reference;
                var links = captured.RootElement.GetProperty("links").EnumerateArray().Select(a => a.GetString() ?? "").Where(a => a.Length > 0).ToArray();
                if (links.Length > 0)
                {
                    simple += "<p>Enlaces del correo</p>";
                    expected += "\nEnlaces del correo";
                    foreach (var link in links)
                    {
                        simple += "<p><a href=\"" + E(link) + "\">" + E(link) + "</a></p>";
                        expected += "\n" + link;
                    }
                }
                if (images.Length > 0)
                {
                    simple += "<p>Imágenes del correo</p>";
                    expected += "\nImágenes del correo";
                    for (var i = 0; i < images.Length; i++)
                    {
                        if (!images[i].StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Una imagen no puede incluirse en el PDF simplificado. Se conserva el correo.");
                        var caption = "Imagen " + (i + 1);
                        simple += "<figure><figcaption>" + caption + "</figcaption><img src=\"" + E(images[i]) + "\"></figure>";
                        expected += "\n" + caption;
                    }
                }
                simple += "</body></html>";
                await NavigateAsync(simple, ct);
                await ReadyAsync(ct);
                var simplified = await PrintAsync(ct);
                try
                {
                    var check = PdfVerifier.ValidateText(simplified, expected, exactSequence: true);
                    return new(simplified, new("simplified", check, first));
                }
                catch (InvalidDataException second) when (second.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck)
                {
                    second.Data[PdfVerifier.FailureDataKey] = new PdfRenderFailure(first, (PdfTextCheck)second.Data[PdfVerifier.MismatchDataKey]!);
                    throw;
                }
            }
            catch (Exception failure)
            {
                failure.Data[PdfVerifier.FailureDataKey] ??= new PdfRenderFailure(first, failure.Data[PdfVerifier.MismatchDataKey] as PdfTextCheck);
                throw;
            }
        }
    }

    private async Task ReadyAsync(CancellationToken ct)
    {
        // Measure the local print document under print media too, rather than
        // comparing a wide screen layout with the A4 output.
        await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", "{\"media\":\"print\"}").WaitAsync(ct);
        var ready = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new
        {
            expression = "(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode().catch(()=>{throw new Error('Imagen incompleta')})));return true})()",
            awaitPromise = true, returnByValue = true
        })).WaitAsync(ct);
        using (var parsed = JsonDocument.Parse(ready))
            if (parsed.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidDataException("No se han podido incluir todas las imágenes adjuntas en el PDF.");
    }

    private async Task<byte[]> PrintAsync(CancellationToken ct)
    {
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
            settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = 15 / 25.4;
            if (!await browser.CoreWebView2.PrintToPdfAsync(path, settings).WaitAsync(ct))
                throw new InvalidDataException("No se ha podido imprimir el correo a PDF.");
            return await File.ReadAllBytesAsync(path, ct);
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
