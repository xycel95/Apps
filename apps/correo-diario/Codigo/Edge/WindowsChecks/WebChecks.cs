using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CorreoDiario.EdgeApp;
using UglyToad.PdfPig;

internal static partial class Program
{
    // The browser fixture uses the publicly rendered roles of the draft and preview.
    // A nested fake action inside a message must never be used as an Outlook action.
    private const string WebFixture = """
<!doctype html><html><head><meta charset="utf-8"><title>Selección ficticia</title></head>
<body><main><div id="folder">OTRAS FILAS FUERA DEL PDF</div>
<div id="selection"><span id="count"></span><button id="forward" aria-label="Reenviar">Reenviar</button></div>
<div id="compose" hidden><div role="textbox" contenteditable="true" aria-label="Cuerpo del mensaje">FIRMA DE BORRADOR AJENA</div>
<div role="listbox" id="cards" aria-label="archivos adjuntos"></div></div></main>
<div id="preview" role="dialog" hidden><button onclick="closePreview()" aria-label="Cerrar">Cerrar</button><article id="message"></article></div>
<div id="menu" role="menu"></div>
<script>
const get=id=>document.getElementById(id);
const mode=new URLSearchParams(location.search).get('case')||'batch';
const total=106;
let current=0, clickCount=0;
get('count').textContent=total+' conversaciones seleccionadas';
for(let i=0;i<12;i++){const row=document.createElement('div');row.setAttribute('role','option');row.textContent='Fila visible '+i;get('folder').append(row);}
function menu(label,action){const e=document.createElement('button');e.setAttribute('role','menuitem');e.textContent=label;
  e.onclick=()=>{get('menu').replaceChildren();action();};get('menu').replaceChildren(e);}
function card(name,kind,action){const c=document.createElement('div');c.setAttribute('role','option');c.setAttribute('aria-label',name+' '+kind);
  for(const t of [name,kind]){const line=document.createElement('div');line.textContent=t;c.append(line);}
  const b=document.createElement('button');b.setAttribute('aria-label','Más acciones');b.textContent='Más acciones';b.onclick=action;c.append(b);return c;}
get('forward').onclick=()=>{get('selection').hidden=true;get('compose').hidden=false;
  for(let n=1;n<=(mode==='partial'?12:total);n++)get('cards').append(card('Asunto ficticio '+n,'Elemento de Outlook',()=>menu('Vista previa',()=>openMail(n))));};
function closePreview(){get('preview').hidden=true;get('message').replaceChildren();get('menu').replaceChildren();}
function openMail(n){current=n;const msg=get('message');get('preview').hidden=false;
  msg.innerHTML='<div role="heading" aria-level="2">Asunto ficticio '+n+'</div><section><div role="heading" aria-level="3">De: Origen de prueba</div><div role="heading" aria-level="3">Para: Destino de prueba</div><div role="heading" aria-level="3">27/11/2026</div><div id="files" role="listbox" aria-label="archivos adjuntos"></div><div role="document" aria-label="Cuerpo del mensaje"><p>Cuerpo independiente '+n+'</p><table><tr><td>Tabla '+n+'</td></tr></table><img alt="Imagen de muestra" src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFUlEQVR4nGPYx6BxwkaDAUhpuJ0AAB14BC+fQndGAAAAAElFTkSuQmCC" width="24" height="24"><button role="menuitem" onclick="clickCount++">Descargar</button></div></section>';
  const many=n===1?2:1;
  for(let a=1;a<=many;a++){ const filename=mode==='protected'?'protegido.bin':'informe.txt';
    get('files').append(card(filename,'12 B',()=>menu(mode==='protected'?'Guardar en OneDrive':'Descargar',()=>{
      const link=document.createElement('a');link.download=filename;link.href=URL.createObjectURL(new Blob(['Adjunto '+n+'/'+a],{type:'text/plain'}));
      document.body.append(link);link.click();link.remove();})));}}
if(mode==='single'||mode==='protected'){get('selection').hidden=true;openMail(1);const article=get('message');document.querySelector('main').append(article);get('preview').remove();}
</script></body></html>
""";

    private sealed class SampleSite : IDisposable
    {
        private readonly TcpListener server = new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task serving;
        internal Uri Url { get; }
        internal SampleSite()
        {
            server.Start();
            Url = new Uri("http://127.0.0.1:"+((IPEndPoint)server.LocalEndpoint).Port+"/muestra");
            serving=Task.Run(Serve);
        }
        private async Task Serve()
        {
            var handlers=new List<Task>();
            try
            {
                while(!stop.IsCancellationRequested)
                {
                    var client=await server.AcceptTcpClientAsync(stop.Token);
                    handlers.Add(Respond(client));
                }
            }
            finally { await Task.WhenAll(handlers); }
        }
        private async Task Respond(TcpClient client)
        {
            using(client)
            using(var data=client.GetStream())
            using(var reader=new StreamReader(data,Encoding.ASCII,false,1024,true))
            {
                try
                {
                    var first=await reader.ReadLineAsync(stop.Token);
                    if(first==null) return;
                    while(await reader.ReadLineAsync(stop.Token) is {Length:>0}) {}
                    var bytes=first.StartsWith("GET /muestra")?Encoding.UTF8.GetBytes(WebFixture):Array.Empty<byte>();
                    var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+(bytes.Length>0?"200 OK":"404 Not Found")+"\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");
                    await data.WriteAsync(header,stop.Token);
                    await data.WriteAsync(bytes,stop.Token);
                }
                catch(IOException) { }
            }
        }
        public void Dispose()
        {
            stop.Cancel();server.Stop();
            try{serving.GetAwaiter().GetResult();}catch(OperationCanceledException){}catch(SocketException) when(stop.IsCancellationRequested){}
            stop.Dispose();
        }
    }

    private static void NativeWeb(string sandbox, IntPtr other)
    {
        using var site = new SampleSite();
        var url = site.Url;
        var destination=Directory.CreateDirectory(Path.Combine(sandbox,"seleccion-web")).FullName;
        var windowReady=new TaskCompletionSource<MainWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
        var browserReady=new TaskCompletionSource<EdgeSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                async Task<EdgeSession> Start(Action<string> progress,CancellationToken ct)
                {
                    try
                    {
                        var s=await EdgeSession.Start(url,u=>u.GetLeftPart(UriPartial.Authority)==url.GetLeftPart(UriPartial.Authority) && u.AbsolutePath==url.AbsolutePath,
                            line=>{Console.WriteLine("INICIO WEB DE PRUEBA "+line);progress(line);},ct);
                        browserReady.TrySetResult(s); return s;
                    }
                    catch(Exception error){browserReady.TrySetException(error);throw;}
                }
                using var w=new MainWindow(Start);
                w.Shown+=(_,_)=>windowReady.TrySetResult(w);
                Application.Run(w);
            }
            catch(Exception e){windowReady.TrySetException(e);browserReady.TrySetException(e);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        var form=windowReady.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        static IEnumerable<Control> Desc(Control w) => w.Controls.Cast<Control>().SelectMany(c=>new[] { c }.Concat(Desc(c)));
        Button ButtonFor(string caption)=>Desc(form).OfType<Button>().Single(b=>b.Text==caption);
        bool Ui(Func<bool> test)=>(bool)form.Invoke(test);
        string Status()=>(string)form.Invoke(()=>Desc(form).OfType<Label>().Last().Text);
        void Go(string caption)=>form.Invoke(()=>ButtonFor(caption).PerformClick());
        try
        {
            form.Invoke(()=>Desc(form).OfType<TextBox>().Single(x=>x.AccessibleName=="Carpeta de destino").Text=destination);
            Go("Abrir Outlook web");
            var session=browserReady.Task.WaitAsync(TimeSpan.FromSeconds(100)).GetAwaiter().GetResult();
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled)),"una sesión de Edge habilita el botón de exportación web");
            SetForegroundWindow(other);
            Check(Wait(()=>GetForegroundWindow()==other,8),"otra ventana en primer plano antes del lote web");
            var cursor=Cursor.Position;int lost=0,moved=0;
            using var observing=new CancellationTokenSource();
            var monitor=new Thread(()=>{while(!observing.Token.WaitHandle.WaitOne(15)){if(GetForegroundWindow()!=other)Interlocked.Increment(ref lost);if(Cursor.Position!=cursor)Interlocked.Increment(ref moved);}});
            monitor.Start();
            bool complete;
            try
            {
                Go("Exportar selección web");
                complete=Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),340);
            }
            finally { observing.Cancel();monitor.Join(); }
            Console.WriteLine("ESTADO LOTE FICTICIO "+Status());
            if (!complete || !Status().Contains("Verificados 106 de 106")) { Go("Copiar diagnóstico"); Console.WriteLine(Clipboard.GetText()); }
            Check(complete && Ui(()=>ButtonFor("Abrir resultado").Enabled) && Directory.GetFiles(destination,"Correo.pdf",SearchOption.AllDirectories).Length==106,"106 mensajes exportados desde el botón aunque la carpeta solo representa 12 filas");
            Check(lost==0 && moved==0,"el lote web conserva la otra ventana y el cursor durante toda la exportación");
            var root=Directory.GetDirectories(destination).Single();
            for(int n=1;n<=106;n++)
            {
                var dir=Path.Combine(root,$"{n:D3} - Asunto ficticio {n}");
                using var pdf=PdfDocument.Open(Path.Combine(dir,"Correo.pdf"));
                var visible=string.Join("",pdf.GetPages().Select(p=>p.Text));
                if(!visible.Contains("Cuerpo independiente "+n) || visible.Contains("OTRAS FILAS") || visible.Contains("FIRMA DE BORRADOR")
                    || pdf.GetPages().Sum(p=>p.NumberOfImages)<1 || File.ReadAllText(Path.Combine(dir,"informe.txt"))!=$"Adjunto {n}/1")
                    throw new Exception("Salida de muestra incorrecta "+n);
            }
            Check(File.ReadAllText(Path.Combine(root,"001 - Asunto ficticio 1","informe (2).txt"))=="Adjunto 1/2","dos adjuntos homónimos conservan ambos contenidos");
            Check(Directory.GetFiles(root,"*.eml",SearchOption.AllDirectories).Length+Directory.GetFiles(root,"*.msg",SearchOption.AllDirectories).Length==0
                && Directory.GetDirectories(root).Length==106 && !File.Exists(Path.Combine(root,"Pendientes.txt")),"carpetas independientes con PDF, imagen y adjuntos, sin originales ni falsos pendientes");
            var fake=session.Evaluate("return clickCount;",[],default).GetAwaiter().GetResult();
            Check(fake.GetInt32()==0,"los controles simulados dentro del cuerpo nunca intervienen en la captura");
            Go("Copiar diagnóstico");
            Check(Wait(()=>Clipboard.GetText().Contains("verificados 106/106"))&&!Clipboard.GetText().Contains("Asunto ficticio")&&!Clipboard.GetText().Contains(url.AbsoluteUri),"diagnóstico web con cantidades y sin información del correo ni rutas");
            void Navigate(string scenario)=>session.Command(HttpMethod.Post,"/url",new {url=url.AbsoluteUri+"?case="+scenario},default).GetAwaiter().GetResult();
            Navigate("single");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),40) && Status().Contains("Verificados 1 de 1"),"un mensaje abierto se exporta sin crear borrador");
            Navigate("protected");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),30) && Status().Contains("Verificados 0 de 1") &&
                Directory.GetFiles(destination,"Pendientes.txt",SearchOption.AllDirectories).Length==1,"la ausencia de Descargar deja el mensaje pendiente y no publica un PDF suelto");
            Navigate("partial");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),38) && Status().Contains("no coincide"),"se rechaza un borrador incompleto con menos elementos que la selección");
            Go("Cerrar ventana web");
            Check(Wait(()=>Ui(()=>Desc(form).OfType<Button>().Any(b=>b.Text=="Abrir Outlook web" && b.Enabled)
                && !ButtonFor("Exportar selección web").Enabled && !ButtonFor("Cerrar ventana web").Enabled),20),"la sesión dedicada se cierra desde la aplicación");
        }
        finally {form.BeginInvoke(form.Close);thread.Join(TimeSpan.FromSeconds(12));}
    }
}
