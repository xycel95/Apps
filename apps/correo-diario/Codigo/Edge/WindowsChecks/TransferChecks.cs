using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using CorreoDiario.EdgeApp;
using OleData = System.Runtime.InteropServices.ComTypes.IDataObject;
using AE = System.Windows.Automation.AutomationElement;

internal static partial class Program
{
    const int BadFormat = unchecked((int)0x80040064);
    static short F(string name) => unchecked((short)DataFormats.GetFormat(name).Id);
    static FORMATETC Fmt(short id, TYMED medium, int index = -1) =>
        new() { cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = medium };

    // Native data object emulates Outlook's indexed virtual files. The first message
    // is an IStream, the second an HGLOBAL with a little extra allocation padding.
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    sealed class TestMailObject(bool truncated = false, bool atEnd = true, bool compound = false) : OleData
    {
        readonly short descriptor = F("FileGroupDescriptorW"), contents = F("FileContents");
        public static byte[] Message(int n)
        {
            var text = "From: Test <a@example.test>\r\nTo: b@example.test\r\nSubject: " + PrivateMarker + n
                + "\r\nDate: Thu, 26 Nov 2026 13:45:00 +0100\r\nMessage-ID: <test-" + n + "@example.test>\r\n"
                + "MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=prueba\r\n\r\n--prueba\r\nContent-Type: text/plain\r\n\r\n"
                + "Cuerpo del mensaje " + n + ".\r\n" + string.Concat(Enumerable.Range(1, 140).Select(i => "Apartado de prueba " + i + ".\r\n\r\n")) + "--prueba\r\nContent-Type: application/octet-stream\r\nContent-Disposition: attachment; filename=test.bin\r\n"
                + "Content-Transfer-Encoding: base64\r\n\r\nAQIDBA==\r\n--prueba--\r\n";
            return Encoding.ASCII.GetBytes(text);
        }
        public int QueryGetData(ref FORMATETC f)
        {
            if (f.dwAspect != DVASPECT.DVASPECT_CONTENT) return BadFormat;
            if (f.cfFormat == descriptor && f.lindex == -1 && f.tymed.HasFlag(TYMED.TYMED_HGLOBAL)) return 0;
            if (f.cfFormat == contents && (f.lindex == 0 && f.tymed.HasFlag(compound ? TYMED.TYMED_ISTORAGE : TYMED.TYMED_ISTREAM)
                || f.lindex == 1 && f.tymed.HasFlag(TYMED.TYMED_HGLOBAL))) return 0;
            return BadFormat;
        }
        static IntPtr Block(byte[] bytes)
        {
            var h = GlobalAlloc(0x42, (UIntPtr)bytes.Length);
            if (h == 0) throw new OutOfMemoryException();
            var p = GlobalLock(h);
            try { Marshal.Copy(bytes, 0, p, bytes.Length); }
            finally { GlobalUnlock(h); }
            return h;
        }
        public void GetData(ref FORMATETC f, out STGMEDIUM medium)
        {
            if (QueryGetData(ref f) != 0) throw new COMException("Formato", BadFormat);
            if (f.cfFormat == descriptor)
            {
                byte[] description = new byte[4 + 2 * 592];
                BitConverter.GetBytes(2).CopyTo(description, 0);
                for (int i = 0; i < 2; i++)
                {
                    int start = 4 + i * 592;
                    BitConverter.GetBytes(compound && i == 0 ? 0 : 0x40).CopyTo(description, start);
                    BitConverter.GetBytes(Message(i).Length + (truncated && i == 1 ? 200 : 0)).CopyTo(description, start + 68);
                    Encoding.Unicode.GetBytes("prueba" + i + (compound && i == 0 ? ".msg" : ".eml")).CopyTo(description, start + 72);
                }
                medium = new() { tymed = TYMED.TYMED_HGLOBAL, unionmember = Block(description) };
            }
            else if (f.lindex == 0 && compound)
            {
                using var file = new MemoryStream();
                using (var root = OpenMcdf.RootStorage.Create(file, OpenMcdf.Version.V3, OpenMcdf.StorageModeFlags.LeaveOpen))
                {
                    using (var item = root.CreateStream("__substg1.0_001A001F")) item.Write(Encoding.Unicode.GetBytes("IPM.Note"));
                    using (var subject = root.CreateStream("__substg1.0_0037001F")) subject.Write(Encoding.Unicode.GetBytes("Contenedor MSG"));
                    using (var body = root.CreateStream("__substg1.0_1000001F")) body.Write(Encoding.Unicode.GetBytes("Texto de almacenamiento."));
                    using var props = root.CreateStream("__properties_version1.0");
                    props.Write(new byte[32]);
                }
                Marshal.ThrowExceptionForHR(StructuredMail.CreateILockBytesOnHGlobal(Block(file.ToArray()), true, out var backing));
                try
                {
                    Marshal.ThrowExceptionForHR(StgOpenStorageOnILockBytes(backing, 0, 0x10, 0, 0, out var storage));
                    medium = new() { tymed = TYMED.TYMED_ISTORAGE, unionmember = Marshal.GetComInterfaceForObject(storage, typeof(INativeStorage)) };
                    Marshal.ReleaseComObject(storage);
                }
                finally { Marshal.Release(backing); }
            }
            else if (f.lindex == 0)
            {
                var h = Block(Message(0));
                Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(h, true, out var stream));
                // The Shell's stream size is the logical size, irrespective of allocation rounding.
                stream.SetSize(Message(0).Length);
                if (atEnd) stream.Seek(0, 2, IntPtr.Zero);
                medium = new() { tymed = TYMED.TYMED_ISTREAM, unionmember = Marshal.GetComInterfaceForObject(stream, typeof(IStream)) };
                Marshal.ReleaseComObject(stream);
            }
            else medium = new() { tymed = TYMED.TYMED_HGLOBAL, unionmember = Block(Message(1).Concat(new byte[16]).ToArray()) };
        }
        public IEnumFORMATETC EnumFormatEtc(DATADIR dir) => dir == DATADIR.DATADIR_GET
            ? new Formats([Fmt(descriptor, TYMED.TYMED_HGLOBAL), Fmt(contents, TYMED.TYMED_ISTREAM, 0), Fmt(contents, TYMED.TYMED_HGLOBAL, 1)])
            : throw new NotImplementedException();
        public void GetDataHere(ref FORMATETC f, ref STGMEDIUM m) => throw new NotImplementedException();
        public int GetCanonicalFormatEtc(ref FORMATETC i, out FORMATETC o) { o = default; return 0x40130; }
        public void SetData(ref FORMATETC f, ref STGMEDIUM m, bool release) => throw new NotImplementedException();
        public int DAdvise(ref FORMATETC f, ADVF flags, IAdviseSink sink, out int c) { c = 0; return unchecked((int)0x80040003); }
        public void DUnadvise(int connection) => throw new NotImplementedException();
        public int EnumDAdvise(out IEnumSTATDATA? result) { result = null; return unchecked((int)0x80040003); }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    sealed class Formats(FORMATETC[] items, int position = 0) : IEnumFORMATETC
    {
        int cursor = position;
        public int Next(int count, FORMATETC[] dest, int[]? fetched)
        {
            var n = Math.Min(count, items.Length - cursor);
            Array.Copy(items, cursor, dest, 0, n);
            cursor += n;
            if (fetched?.Length > 0) fetched[0] = n;
            return n == count ? 0 : 1;
        }
        public int Skip(int count) { int n = Math.Min(count, items.Length - cursor); cursor += n; return n == count ? 0 : 1; }
        public int Reset() { cursor = 0; return 0; }
        public void Clone(out IEnumFORMATETC copy) => copy = new Formats(items, cursor);
    }

    [DllImport("ole32.dll")] static extern int StgOpenStorageOnILockBytes(nint backing, nint priority, uint flags, nint exclude, uint reserved, out INativeStorage storage);
    [DllImport("ole32.dll")] static extern int CreateStreamOnHGlobal(nint h, bool delete, out IStream stream);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string className,string title);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("kernel32.dll")] static extern nint GlobalAlloc(uint flags, UIntPtr size);
    [DllImport("kernel32.dll")] static extern nint GlobalLock(nint h);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(nint h);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint handle);

    static void TransferRules()
    {
        var result = MailTransfer.Read(new TestMailObject());
        Check(result.Inputs.Count == 2 && result.Inputs.All(i => i.PathOnDisk == null), "dos correos nativos, recibidos en memoria por índice");
        Check(MailTransfer.Read(new TestMailObject(atEnd: false)).Inputs.Count == 2, "también se recibe un flujo situado al principio");
        var mails = result.Inputs.Select(i => CorreoDiario.MailReader.Inspect(i, new("", "", "", "Prueba"))).ToArray();
        Check(mails[0].Subject.EndsWith("0") && mails[1].Subject.EndsWith("1"), "el flujo y la memoria global corresponden a sus descriptores");
        Check(mails.All(m => CorreoDiario.MailReader.ReadContent(m).Parts.Single().Bytes.SequenceEqual(new byte[]{1,2,3,4})), "se conservan los bytes de los adjuntos de ambos mensajes");
        try { MailTransfer.Read(new TestMailObject(true)); throw new Exception("Lote incompleto aceptado"); }
        catch (TransferException e) { Check(e.Reason == "entrega incompleta", "un segundo elemento truncado invalida todo el lote"); }
        var compound = MailTransfer.Read(new TestMailObject(compound: true));
        var msg = CorreoDiario.MailReader.Inspect(compound.Inputs[0], new("", "", "", "Prueba"));
        Check(msg.Input.PathOnDisk == null && msg.Subject == "Contenedor MSG" && CorreoDiario.MailReader.ReadContent(msg).Plain.Contains("Texto de almacenamiento."), "mensaje MSG recibido en memoria como almacenamiento compuesto");
        var paths = new System.Collections.Specialized.StringCollection();
        paths.Add(Path.Combine(Path.GetTempPath(), "correo-de-prueba.eml"));
        var fileList = new DataObject();
        fileList.SetFileDropList(paths);
        var pathsResult = MailTransfer.Read((OleData)fileList);
        Check(pathsResult.Inputs.Count == 1 && pathsResult.Inputs[0].PathOnDisk == paths[0], "la lista estándar de archivos Windows se recibe en modo de lectura");
        var onlyText = new DataObject();
        onlyText.SetText("https://example.test/" + PrivateMarker);
        try { MailTransfer.Read((OleData)onlyText); throw new Exception("Enlace aceptado"); }
        catch (TransferException e) { Check(e.Reason == "solo texto o enlace", "una URL no se convierte en correo"); }
    }

    static void NativeExport(AE form,string folder,IntPtr other,int processId)
    {
        Invoke(Named(form,"Archivos locales")!);
        Check(Wait(()=>Named(form,"Añadir archivos…") is { } button && !button.Current.IsOffscreen),
            "Archivos locales despliega la selección de archivos en el ejecutable");
        string samples=Directory.CreateTempSubdirectory("CorreoDiario-muestras-").FullName;
        try
        {
            var paths=Enumerable.Range(0,2).Select(i=>Path.Combine(samples,$"muestra {i}.eml")).ToArray();
            for(int i=0;i<paths.Length;i++) File.WriteAllBytes(paths[i],TestMailObject.Message(i));
            var open=Named(form,"Añadir archivos…")!;
            PostMessage((IntPtr)open.Current.NativeWindowHandle,0x00F5,0,0);
            IntPtr window=0;
            Check(Wait(()=>
            {
                window=FindWindow("#32770","Añadir correos existentes");
                return window!=0 && GetWindowThreadProcessId(window,out uint pid)!=0 && pid==processId;
            }),"Añadir archivos abre el selector nativo en Windows");
            var dialog=AE.FromHandle(window);
            var combo=dialog.FindFirst(TreeScope.Descendants,new PropertyCondition(AE.AutomationIdProperty,"1148"));
            var field=(combo??dialog).FindFirst(TreeScope.Descendants,new PropertyCondition(AE.ControlTypeProperty,ControlType.Edit))??combo;
            if(field==null || !field.TryGetCurrentPattern(ValuePattern.Pattern,out var value)) throw new Exception("Falta el campo de nombre del archivo.");
            ((ValuePattern)value).SetValue(string.Join(" ",paths.Select(path=>"\""+path+"\"")));
            var accept=dialog.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AE.AutomationIdProperty,"1"),
                new PropertyCondition(AE.ControlTypeProperty,ControlType.Button)));
            if(accept==null) throw new Exception("Falta el botón de apertura.");
            Invoke(accept);
            Check(Wait(() => Named(form,"2 marcados de 2 correos recibidos") != null), "el ejecutable carga los dos archivos seleccionados");
            // Start a normal export, then switch to the browser while the worker is running.
            // UI Automation itself may focus the control it invokes, just like a user click.
            Invoke(Named(form, "Exportar archivos añadidos")!);
            Check(Wait(() => Named(form, "Cancelar")?.Current.IsEnabled == true, 10), "la exportación sigue en curso al cambiar de ventana");
            SetForegroundWindow(other);
            Check(Wait(() => GetForegroundWindow() == other, 5), "Edge recibe el foco mientras la aplicación trabaja");
            var pos = Cursor.Position;
            var fg = GetForegroundWindow();
            bool keptFocus = true, keptPointer = true;
            Check(Wait(() =>
                {
                    keptFocus &= GetForegroundWindow() == fg;
                    keptPointer &= Cursor.Position == pos;
                    return Directory.EnumerateFiles(folder, "Correo.pdf", SearchOption.AllDirectories).Count()==2
                        && Named(form,"Abrir resultado")?.Current.IsEnabled == true;
                }), "exportación finalizada desde el ejecutable publicado");
            Check(keptPointer && Cursor.Position == pos, "el ratón permanece en su posición durante la exportación");
            Check(keptFocus && GetForegroundWindow() == fg, "la ventana en uso conserva el foco hasta después de terminar");
            var pdfs = Directory.GetFiles(folder,"Correo.pdf",SearchOption.AllDirectories);
            Check(pdfs.Length == 2 && pdfs.All(f => CorreoDiario.MailPrinter.VerifyPdf(f) > 0), "dos PDF legibles en dos carpetas");
            var parts = Directory.GetFiles(folder,"test.bin",SearchOption.AllDirectories);
            Check(parts.Length == 2 && parts.All(p => File.ReadAllBytes(p).SequenceEqual(new byte[]{1,2,3,4})), "ambos adjuntos se recuperan íntegros en la exportación real");
            Check(!Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Any(f => Path.GetExtension(f) is ".msg" or ".eml"), "el lote del ejecutable carece de copias de mensajes originales");
            Invoke(Named(form,"Copiar diagnóstico")!);
            Check(Wait(() => Clipboard.GetText().Contains("Guardados 2 de 2") && !Clipboard.GetText().Contains(PrivateMarker)),
                "el diagnóstico local recoge los contadores sin incluir asuntos");
            Invoke(Named(form,"Ocultar archivos locales")!);
            Check(Wait(()=>Named(form,"Archivos locales")!=null),"el usuario puede volver a plegar los archivos locales");
        }
        finally { Directory.Delete(samples,true); }
    }
}
