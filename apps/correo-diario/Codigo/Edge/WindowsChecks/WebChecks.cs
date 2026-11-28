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
  for(let n=1;n<=(mode==='partial'?12:mode==='empty'?0:total);n++)get('cards').append(card('Asunto ficticio '+n,'Elemento de Outlook',()=>menu('Vista previa',()=>openMail(n))));};
function closePreview(){get('preview').hidden=true;get('message').replaceChildren();get('menu').replaceChildren();}
function openMail(n){current=n;const msg=get('message');get('preview').hidden=false;
  msg.innerHTML='<div role="heading" aria-level="2">Asunto ficticio '+n+'</div><section><div role="heading" aria-level="3">De: Origen de prueba &lt;archivo@example.test&gt;</div><div role="heading" aria-level="3">Para: Destino de prueba</div><div role="heading" aria-level="3">27/11/2026 08:12</div><div id="files" role="listbox" aria-label="archivos adjuntos"></div><div role="document" aria-label="Cuerpo del mensaje"><p>Cuerpo independiente '+n+'</p><table><tr><td>Tabla '+n+'</td></tr></table><img alt="Imagen de muestra" src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFUlEQVR4nGPYx6BxwkaDAUhpuJ0AAB14BC+fQndGAAAAAElFTkSuQmCC" width="24" height="24"><button role="menuitem" onclick="clickCount++">Descargar</button></div></section>';
  const many=n===1?2:1;
  for(let a=1;a<=many;a++){ const filename=mode==='protected'?'protegido.bin':'informe.txt';
    get('files').append(card(filename,'12 B',()=>menu(mode==='protected'?'Guardar en OneDrive':'Descargar',()=>{
      const link=document.createElement('a');link.download=filename;link.href=URL.createObjectURL(new Blob(['Adjunto '+n+'/'+a],{type:'text/plain'}));
      document.body.append(link);link.click();link.remove();})));}}
// A large folder renders only one viewport and refuses to attach a bulk selection.
let forwards=0;
if(mode==='direct'||mode==='stale') {
  const folder=get('folder'),main=document.querySelector('main'),article=get('message'),limit=mode==='stale'?2:123;
  let opened=0;
  folder.replaceChildren();folder.setAttribute('role','listbox');folder.setAttribute('aria-label','Lista de mensajes');
  folder.style.cssText='position:relative;overflow:auto;height:416px;width:420px';
  const spacer=document.createElement('div');spacer.style.height=(32*limit)+'px';folder.append(spacer);
  main.append(article);
  get('count').textContent=(mode==='stale'?2:106)+' conversaciones seleccionadas';
  get('forward').onclick=()=>{forwards++;get('selection').hidden=true;get('compose').hidden=false;};
  function subject(n){return n===2||n===3?'Asunto duplicado':'Asunto ficticio '+n;}
  function render() {
    for(const r of folder.querySelectorAll('[role=option]'))r.remove();
    const start=Math.floor(folder.scrollTop/32)+1;
    for(let n=start;n<=Math.min(limit,start+12);n++){
      const r=document.createElement('div');r.setAttribute('role','option');r.setAttribute('data-convid','fila-ficticia-'+n);
      r.setAttribute('aria-selected',String(opened===0?n%7!==0:n===opened));
      r.style.cssText='position:absolute;left:0;right:0;top:'+((n-1)*32)+'px;height:32px';
      const from=document.createElement('span');from.textContent='Remitente de ejemplo';r.append(from);
      const title=document.createElement('span');title.textContent=subject(n);r.append(title);
      r.onclick=()=>{opened=n;get('selection').hidden=true;render();
        const show=()=>{openMail(n);get('preview').hidden=true;article.querySelector('[aria-level="2"]').textContent=mode==='stale'?'Mensaje distinto':subject(n);};
        if(n===3)setTimeout(show,550);else show();};
      folder.append(r);
    }
  }
  folder.onscroll=()=>requestAnimationFrame(render);folder.scrollTop=(limit-13)*32;render();
}
// Normal reading pane: a separate subject strip, a sender/recipient/date card and a file tile.
// The second group advertises three children but only exposes two. A later row keeps the old panel.
if(mode==='reader' || mode==='first-failure' || mode==='missing-file' || mode==='normal-single') {
    const folder=get('folder'), article=get('message'), parents=mode==='reader'?[1,2,3,4,5]:[4,5];
    folder.replaceChildren();folder.setAttribute('role','listbox');folder.setAttribute('aria-label','Lista de mensajes');
    document.querySelector('main').append(article);article.setAttribute('aria-label','Panel de lectura');
    get('count').textContent=parents.length+' conversaciones seleccionadas';
    function subject(id){return id===11||id===12?'Revisión semanal':id===4?'Otro asunto':'Solicitud '+id;}
    function read(id,row) {
        for(const item of folder.querySelectorAll('[role="option"]'))item.setAttribute('aria-selected',String(item===row));
        get('selection').hidden=true;
        if(id===4 && mode!=='missing-file')return;
        article.innerHTML='<div><span id="tit"></span><button>Resumir este correo electrónico</button></div><section><div><span>RP</span><span data-app-section="Sender" title="normal@example.test">Origen de muestra</span><span>Para: Destino de muestra</span><button aria-label="Responder">Responder</button><time>Lun 09/03/2026 09:24</time></div><div id="tile"></div><div id="body-name" hidden>Cuerpo del mensaje</div><div role="region" aria-labelledby="body-name"><p>Cuerpo normal '+id+'</p><button onclick="clickCount++">Descargar</button></div></section>';
        get('tit').textContent=subject(id);
        if(id===4 && mode==='missing-file')return;
        const full='documento de muestra '+id+'.txt';
        const tile=get('tile');
        tile.innerHTML='<div title="'+full+'">documento de m…</div><small>36 KB</small><button aria-label="Más opciones para el archivo" aria-haspopup="menu">⌄</button>';
        tile.querySelector('button').onclick=()=>menu('Descargar',()=>{
            const a=document.createElement('a');a.download=full;a.href=URL.createObjectURL(new Blob(['Contenido '+id]));
            document.body.append(a);a.click();a.remove();
        });
    }
    function makeRow(id,child=false) {
        const r=document.createElement('div');r.setAttribute('role','option');r.setAttribute('data-item-id','muestra-'+id);
        r.setAttribute('aria-selected',String(!child));if(child)r.setAttribute('aria-setsize',id<20?'2':'3');
        const title=document.createElement('span');title.textContent=subject(id);r.append(title);
        if(id>2){const clip=document.createElement('span');clip.setAttribute('aria-label','Tiene archivos adjuntos');clip.textContent='·';r.append(clip);}
        r.onclick=()=>read(id,r);return r;
    }
    for(const id of parents){
        const row=makeRow(id);folder.append(row);
        if(id>2)continue;
        const count=id===1?2:3;
        const tree=document.createElement('div');tree.id='miembros-muestra-'+id;tree.setAttribute('role','group');tree.hidden=true;folder.append(tree);
        const exp=document.createElement('button');exp.setAttribute('aria-controls',tree.id);
        const switchState=open=>{exp.setAttribute('aria-expanded',String(open));exp.setAttribute('aria-label',(open?'Contraer':'Expandir')+' conversación, '+count+' mensajes');tree.hidden=!open;};
        exp.textContent='›';switchState(false);
        exp.onclick=e=>{e.stopPropagation();switchState(tree.hidden);};row.append(exp);
        for(let child=1;child<=2;child++)tree.append(makeRow(id*10+child,true));
    }
    if(mode==='normal-single')read(5,folder.lastElementChild);
}
// Exercise a reading pane with quoted content and a separate, collapsed conversation member.
if(['cards','quoted','header-details','metadata-absent','profile-address'].includes(mode)){
    const folder=get('folder'),article=get('message');document.querySelector('main').append(article);
    article.setAttribute('aria-label','Panel de lectura');folder.replaceChildren();
    folder.setAttribute('role','listbox');folder.setAttribute('aria-label','Lista de mensajes');
    let remembered=0;
    function read(n){
        get('selection').hidden=true;
        for(const r of folder.children)r.setAttribute('aria-selected',String(Number(r.dataset.number)===n));
        article.replaceChildren();
        const h=document.createElement('h2');h.textContent='Solicitud de muestra '+n;article.append(h);
        const box=document.createElement('div');box.setAttribute('role','list');box.setAttribute('aria-label',mode==='cards'&&n===1?'2 mensajes':'Mensajes');article.append(box);
        function add(id,collapsed){
            const card=document.createElement('article');card.setAttribute('role','listitem');card.setAttribute('data-item-id','mensaje-sintetico-'+id);box.append(card);
            function render(){
                card.replaceChildren();let header=document.createElement('header');card.append(header);
                header.innerHTML='<span data-app-section="Sender">Persona de ejemplo</span><span>Para: Recipiente de ejemplo</span><time>Mar 03/03/2026 11:'+String(id).padStart(2,'0')+'</time><button aria-label="Responder">Responder</button>';
                const sender=header.querySelector('span');
                if(mode==='profile-address'){
                    sender.setAttribute('role','button');sender.setAttribute('aria-haspopup','dialog');
                    sender.onclick=()=>{let pop=document.createElement('div');pop.setAttribute('role','dialog');pop.innerHTML='<h2>Persona de ejemplo</h2><a href="mailto:persona@example.test">Correo electrónico</a><button>Cerrar</button>';document.body.append(pop);pop.querySelector('button').onclick=()=>pop.remove();};
                }else if(mode==='header-details' || mode==='metadata-absent'){
                    header.querySelector('time').textContent='Hoy';
                    if(mode==='header-details'){
                        const more=document.createElement('button');more.setAttribute('aria-label','Más acciones');header.append(more);
                        more.onclick=()=>menu('Ver',()=>menu('Ver detalles del mensaje',()=>{
                            const dialog=document.createElement('div');dialog.setAttribute('role','dialog');dialog.setAttribute('aria-label','Detalles del mensaje');dialog.innerHTML='<textarea readonly></textarea><button>Cerrar</button>';document.body.append(dialog);
                            dialog.querySelector('textarea').value='From: Persona <cabecera@example.test>\nDate: Tue, 3 Mar 2026 10:11:00 +0000\nSubject: Prueba';dialog.querySelector('button').onclick=()=>dialog.remove();
                        }));
                    }
                }else sender.title='autor'+id+'@example.test';
                const file=card.appendChild(document.createElement('div'));
                file.innerHTML='<span title="imagen de muestra.png">imagen de muestra.png</span><small>12 KB</small><button aria-haspopup="menu" aria-label="Más opciones">⌄</button>';
                file.querySelector('button').onclick=()=>{
                    let popup=document.createElement('div');popup.style.position='fixed';popup.style.zIndex=10;popup.style.background='white';document.body.append(popup);
                    for(const label of ['Vista previa','Guardar en OneDrive - Organización de ejemplo','Copiar','Descargar']){
                        let cmd=document.createElement('div');cmd.tabIndex=0;cmd.innerHTML='<span>'+label+'</span>';popup.append(cmd);
                        cmd.onclick=()=>{popup.remove();if(label!=='Descargar'){clickCount++;return;}const a=document.createElement('a');a.download='imagen de muestra.png';const png=document.createElement('canvas');png.width=png.height=2;const ctx=png.getContext('2d');ctx.fillStyle='rgb('+id+',80,160)';ctx.fillRect(0,0,2,2);a.href=png.toDataURL('image/png');document.body.append(a);a.click();a.remove();};
                    }
                };
                const body=document.createElement('div');body.setAttribute('role','document');body.setAttribute('aria-label','Cuerpo del mensaje');body.innerHTML='<p>Contenido independiente '+id+'</p>';card.append(body);
                if(mode==='quoted' || mode==='metadata-absent'){
                    const hint=document.createElement('div');hint.className='allowTextSelection';hint.innerHTML='<p>Control visual externo</p>';card.append(hint);
                    const quote=document.createElement('blockquote');quote.innerHTML='<div role="document" aria-label="Cuerpo del mensaje">De: Persona anterior &lt;ajeno@example.test&gt;<br>Enviado el: 03/02/2025 22:00<br>Para: Otro<br>Asunto: Consulta anterior<br>Contenido citado íntegro.</div>';card.append(quote);
                }
            }
            if(collapsed){let open=document.createElement('button');open.setAttribute('aria-label','Expandir mensaje');open.setAttribute('aria-expanded','false');open.onclick=render;card.append(open);}
            else render();
        }
        add(n*10+1,false);if(mode==='cards'&&n===1)add(12,true);
    }
    for(const n of [1,2]){
        const r=document.createElement('div');r.setAttribute('role','option');r.setAttribute('aria-selected','true');r.setAttribute('data-convid','grupo-'+n);r.dataset.number=String(n);
        const title=document.createElement('span');title.textContent='Solicitud de muestra '+n;r.append(title);r.onclick=()=>read(n);folder.append(r);
    }
    get('count').textContent='2 conversaciones seleccionadas';
    if(mode!=='cards')read(1);
}
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
        string Status()=>(string)form.Invoke(()=>Desc(form).OfType<Label>().Single(x=>x.AccessibleName=="Estado de la exportación").Text);
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
            var root=destination;
            for(int n=1;n<=106;n++)
            {
                var dir=Path.Combine(root,$"2026-11-27_08.12_archivo@example.test_Asunto ficticio {n}");
                using var pdf=PdfDocument.Open(Path.Combine(dir,"Correo.pdf"));
                var visible=string.Join("",pdf.GetPages().Select(p=>p.Text));
                if(!visible.Contains("Cuerpo independiente "+n) || visible.Contains("OTRAS FILAS") || visible.Contains("FIRMA DE BORRADOR")
                    || pdf.GetPages().Sum(p=>p.NumberOfImages)<1 || File.ReadAllText(Path.Combine(dir,"informe.txt"))!=$"Adjunto {n}/1")
                    throw new Exception("Salida de muestra incorrecta "+n);
            }
            Check(File.ReadAllText(Path.Combine(root,"2026-11-27_08.12_archivo@example.test_Asunto ficticio 1","informe (2).txt"))=="Adjunto 1/2","dos adjuntos homónimos conservan ambos contenidos");
            Check(Directory.GetFiles(root,"*.eml",SearchOption.AllDirectories).Length+Directory.GetFiles(root,"*.msg",SearchOption.AllDirectories).Length==0
                && Directory.GetDirectories(root).Length==106 && !File.Exists(Path.Combine(root,"Pendientes.txt")),"carpetas independientes con PDF, imagen y adjuntos, sin originales ni falsos pendientes");
            var fake=session.Evaluate("return clickCount;",[],default).GetAwaiter().GetResult();
            Check(fake.GetInt32()==0,"los controles simulados dentro del cuerpo nunca intervienen en la captura");
            Go("Copiar diagnóstico");
            Check(Wait(()=>Clipboard.GetText().Contains("verificados 106/106"))&&!Clipboard.GetText().Contains("Asunto ficticio")&&!Clipboard.GetText().Contains(url.AbsoluteUri),"diagnóstico web con cantidades y sin información del correo ni rutas");
            void Navigate(string scenario)=>session.Command(HttpMethod.Post,"/url",new {url=url.AbsoluteUri+"?case="+scenario},default).GetAwaiter().GetResult();
            var priorRoots=Directory.GetDirectories(destination);
            Navigate("direct");
            SetForegroundWindow(other);
            Check(Wait(()=>GetForegroundWindow()==other,8),"la prueba de lista empieza con otra ventana delante");
            var listCursor=Cursor.Position;
            using var observeList=new CancellationTokenSource();
            int focusChanges=0,cursorChanges=0;
            var listMonitor=new Thread(()=>{while(!observeList.Token.WaitHandle.WaitOne(15)){
                if(GetForegroundWindow()!=other)Interlocked.Increment(ref focusChanges);
                if(Cursor.Position!=listCursor)Interlocked.Increment(ref cursorChanges);}});
            listMonitor.Start();
            bool completeList;
            try
            {
                Go("Exportar selección web");
                completeList=Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),340);
            }
            finally {observeList.Cancel();listMonitor.Join();}
            Console.WriteLine("ESTADO LISTA DIRECTA "+Status());
            if(!completeList || !Status().Contains("Verificados 106 de 106")) { Go("Copiar diagnóstico");Console.WriteLine(Clipboard.GetText()); }
            Check(completeList && Status().Contains("Verificados 106 de 106"),"106 seleccionados entre 123 filas con 13 visibles y un borrador masivo vacío");
            Check(focusChanges==0 && cursorChanges==0,"la lectura directa conserva el foco y el cursor durante los 106 correos");
            Check((session.Evaluate("return forwards;",[],default).GetAwaiter().GetResult()).GetInt32()==0,"el lote directo no crea borradores ni pulsa Reenviar");
            var directRoot=destination;
            var savedFolders=Directory.GetDirectories(destination).Except(priorRoots).ToArray();
            var uniqueRows=new HashSet<int>();
            var expectedRows=Enumerable.Range(1,123).Where(n=>n%7!=0).ToArray();
            Check(savedFolders.Length==106 && !File.Exists(Path.Combine(directRoot,"Pendientes.txt")),"se guardan todas y solo las filas seleccionadas");
            for(int i=0;i<savedFolders.Length;i++)
            {
                using var savedPdf=PdfDocument.Open(Path.Combine(savedFolders[i],"Correo.pdf"));
                int n=int.Parse(File.ReadAllText(Path.Combine(savedFolders[i],"informe.txt")).Split(' ')[1].Split('/')[0]);
                if(!uniqueRows.Add(n) || !expectedRows.Contains(n)) throw new Exception("Se exportó una fila ajena a la selección.");
                if(!savedPdf.GetPages().Any(pg=>pg.Text.Contains("Cuerpo independiente "+n)) ||
                    File.ReadAllText(Path.Combine(savedFolders[i],"informe.txt"))!=$"Adjunto {n}/1")
                    throw new Exception("El PDF o el adjunto no corresponde a la fila ficticia "+n);
            }
            Check(true,"cada PDF y adjunto corresponden a su fila, incluso con asuntos repetidos y actualización tardía del panel");
            Go("Copiar diagnóstico");
            Check(Clipboard.GetText().Contains("filas identificadas 106") && !Clipboard.GetText().Contains("fila-ficticia-") &&
                !Clipboard.GetText().Contains("Asunto duplicado"),"la comprobación de las filas solo registra contadores");
            Navigate("single");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),40) && Status().Contains("Verificados 1 de 1"),"un mensaje abierto se exporta sin crear borrador");
            Navigate("protected");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),30) && Status().Contains("Verificados 0 de 1") &&
                Directory.GetFiles(destination).Length==0 && Ui(()=>ButtonFor("Ver pendientes").Enabled),"la ausencia de Descargar deja el mensaje pendiente y no publica un PDF suelto");
            Navigate("partial");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),38) && Status().Contains("no coincide"),"se rechaza un borrador incompleto con menos elementos que la selección");
            Go("Copiar diagnóstico");
            Check(Clipboard.GetText().Contains("seleccionados 106 · tarjetas 12"),"el diagnóstico distingue cuántas tarjetas devolvió realmente el borrador");
            Navigate("empty");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),45) && Status().Contains("borrador sin correos adjuntos"),
                "un borrador con solo la firma identifica la entrega vacía de Outlook");
            Go("Copiar diagnóstico");
            Check(Clipboard.GetText().Contains("vista compose · seleccionados 106 · tarjetas 0") && Clipboard.GetText().Contains("borrador-vacío"),
                "el diagnóstico conserva el contador y los cero elementos tras abrir el borrador");
            Navigate("stale");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),35) && Status().Contains("Verificados 0 de 2"),
                "un panel que muestra otro asunto deja el lote pendiente sin publicar otro correo");
            priorRoots=Directory.GetDirectories(destination);
            Navigate("reader");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),68) && Status().Contains("Verificados 4 de 6"),
                "el panel normal archiva cuatro mensajes y continúa tras un hilo incompleto y un panel anterior");
            var readerRoot=destination;
            int[] readerIds=[11,12,3,5];
            var readerDirs=Directory.GetDirectories(readerRoot).Except(priorRoots).ToArray();
            for(int i=0;i<readerIds.Length;i++)
            {
                int id=int.Parse(File.ReadAllText(Directory.GetFiles(readerDirs[i],"*.txt").Single()).Split(' ')[1]);
                if(!readerIds.Contains(id)) throw new Exception("Entrada inesperada en la carpeta elegida.");
                using var pdf=PdfDocument.Open(Path.Combine(readerDirs[i],"Correo.pdf"));
                var text=string.Join("",pdf.GetPages().Select(p=>p.Text));
                Check(text.Contains("Cuerpo normal "+id) && text.Contains("Origen de muestra") && text.Contains("Destino de muestra") &&
                    text.Contains("09:24") && !text.Contains("Resumir este") && File.ReadAllText(Path.Combine(readerDirs[i],$"documento de muestra {id}.txt"))=="Contenido "+id,
                    $"PDF con cabecera y archivo completo del mensaje individual de prueba {i+1}");
            }
            Go("Copiar diagnóstico");
            Check(Clipboard.GetText().Contains("hilo-sin-desglose") && Clipboard.GetText().Contains("panel-anterior") &&
                !Clipboard.GetText().Contains("Revisión semanal") && !Clipboard.GetText().Contains("Origen de muestra"),"el informe de pendientes distingue el hilo y el panel sin copiar contenido");
            Navigate("normal-single");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),30) && Status().Contains("Verificados 1 de 1"),
                "el correo ya abierto en el panel normal se lee con su adjunto desde el mismo botón");
            Navigate("first-failure");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),38) && Status().Contains("Verificados 1 de 2"),
                "un fallo en la primera apertura permite archivar el siguiente seleccionado");
            Navigate("missing-file");Go("Exportar selección web");
            Check(Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),35) && Status().Contains("Verificados 1 de 2"),
                "una fila con clip y sin archivo identificable queda pendiente mientras se guarda el siguiente");
            var clean=Directory.CreateDirectory(Path.Combine(sandbox,"salida-plana")).FullName;
            form.Invoke(()=>Desc(form).OfType<TextBox>().Single(x=>x.AccessibleName=="Carpeta de destino").Text=clean);
            void Scenario(string scenario,string result)
            {
                Navigate(scenario);Go("Exportar selección web");
                var ended=Wait(()=>Ui(()=>ButtonFor("Exportar selección web").Enabled),55);
                if(!ended || !Status().Contains(result)){Console.WriteLine(Status());Go("Copiar diagnóstico");Console.WriteLine(Clipboard.GetText());}
                Check(ended && Status().Contains("Lote terminado") && Status().Contains(result),scenario+" entrega el resultado esperado en la ventana principal");
            }
            Scenario("cards","Verificados 3 de 3");
            foreach(int id in new[] {11,12,21})
            {
                var dir=Path.Combine(clean,$"2026-03-03_11.{id}_autor{id}@example.test_Solicitud de muestra {id/10}");
                using var pdf=PdfDocument.Open(Path.Combine(dir,"Correo.pdf"));
                using var attachment=new System.Drawing.Bitmap(Path.Combine(dir,"imagen de muestra.png"));
                Check(pdf.GetPages().Any(pg=>pg.Text.Contains("Contenido independiente "+id)) &&
                    !pdf.GetPages().Any(pg=>pg.Text.Contains("Contenido independiente "+(id==11?12:11))) &&
                    attachment.Width==2 && attachment.Height==2 && attachment.GetPixel(1,1).ToArgb()==System.Drawing.Color.FromArgb(id,80,160).ToArgb() &&
                    Directory.GetFiles(dir).Length==2,"una tarjeta de conversación conserva exclusivamente su PDF y su archivo");
            }
            Scenario("quoted","Verificados 1 de 1");
            string existing=Path.Combine(clean,"2026-03-03_11.11_autor11@example.test_Solicitud de muestra 1");
            using(var pdf=PdfDocument.Open(Path.Combine(existing+" (2)","Correo.pdf")))
                Check(pdf.GetPages().Any(pg=>pg.Text.Contains("Contenido citado íntegro")) && !pdf.GetPages().Any(pg=>pg.Text.Contains("Control visual externo")),
                    "una cita del mismo mensaje entra en el PDF y los dos nombres coincidentes se conservan");
            Scenario("header-details","Verificados 1 de 1");
            Check(Directory.Exists(Path.Combine(clean,"2026-03-03_11.11_cabecera@example.test_Solicitud de muestra 1")),
                "las cabeceras oficiales aportan la fecha y el correo del remitente");
            Scenario("profile-address","Verificados 1 de 1");
            Check(Directory.Exists(Path.Combine(clean,"2026-03-03_11.11_persona@example.test_Solicitud de muestra 1")),"la ficha del remitente resuelve un nombre visible sin dirección");
            var beforeMissing=Directory.GetDirectories(clean).Length;
            Scenario("metadata-absent","Verificados 0 de 1");
            Check(Ui(()=>ButtonFor("Ver pendientes").Enabled) && Directory.GetFiles(clean).Length==0 &&
                Directory.GetDirectories(clean).Length==beforeMissing && Directory.GetDirectories(clean).All(d=>File.Exists(Path.Combine(d,"Correo.pdf"))),
                "el destino acumula únicamente carpetas de correo y no usa una fecha o dirección citadas");
            Go("Cerrar ventana web");
            Check(Wait(()=>Ui(()=>Desc(form).OfType<Button>().Any(b=>b.Text=="Abrir Outlook web" && b.Enabled)
                && !ButtonFor("Exportar selección web").Enabled && !ButtonFor("Cerrar ventana web").Enabled),20),"la sesión dedicada se cierra desde la aplicación");
        }
        finally {form.BeginInvoke(form.Close);thread.Join(TimeSpan.FromSeconds(12));}
    }
}
