using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using CorreoDiario.EdgeApp;
using AE = System.Windows.Automation.AutomationElement;

internal static partial class Program
{
    const string PrivateMarker = "CASO_PRIVADO_SOLO_DE_PRUEBA";
    const string Outlook = "https://outlook.cloud.microsoft/mail/inbox/id/" + PrivateMarker;
    static int passed;

    static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        Console.WriteLine("OK " + label);
        passed++;
    }

    static bool Wait(Func<bool> check, int seconds = 35)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            try { if (check()) return true; }
            catch (ElementNotAvailableException) { }
            Thread.Sleep(300);
        } while (clock.Elapsed < TimeSpan.FromSeconds(seconds));
        return false;
    }

    static void Rules()
    {
        foreach (var name in new[] { "Barra de búsqueda y direcciones", "Dirección y barra de búsqueda", "Barra de direcciones y búsqueda", "Address and search bar" })
            Check(EdgeWindow.IsAddressName(name), "etiqueta de la barra: " + name);
        Check(!EdgeWindow.IsAddressName("Buscar") && !EdgeWindow.IsAddressName("Barra de búsqueda"), "los buscadores genéricos no identifican la barra");
        Check(EdgeWindow.OutlookAddress(Outlook, false)?.Host == "outlook.cloud.microsoft", "URL HTTPS de la bandeja");
        Check(EdgeWindow.OutlookAddress("outlook.cloud.microsoft/mail/inbox", true) != null, "esquema oculto en la barra");
        Check(EdgeWindow.OutlookAddress("outlook.cloud.microsoft/mail/inbox", false) == null, "el documento necesita una URL completa");
        foreach (var address in new[] { "http://outlook.cloud.microsoft/mail", "https://outlook.cloud.microsoft.example.test/mail", "https://outlook.cloud.microsoft@otro.test/", "https://usuario@outlook.cloud.microsoft/", "https://outlook.cloud.microsoft:444/mail" })
            Check(EdgeWindow.OutlookAddress(address, true) == null, "se descarta un origen ajeno o ambiguo");
        var log = new DiagnosticLog();
        log.DetectionResult(new([], [], 0));
        try { throw new IOException("C:\\Users\\" + PrivateMarker + "\\persona@example.test.eml"); }
        catch (IOException e) { log.Fault("Leer archivo EML/MSG", e); }
        var report = log.Report();
        Check(report.Contains("Procesos Edge 0") && report.Contains("IOException") && report.Contains("0x"), "el diagnóstico identifica la fase y el error aunque no haya ventana");
        Check(!report.Contains(PrivateMarker) && !report.Contains("example.test"), "el informe descarta los mensajes de excepción y las rutas");
    }

    static Process Browser(string edge, string profile, string url)
    {
        var start = new ProcessStartInfo(edge) { UseShellExecute = false };
        foreach (var arg in new[] {"--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check", "--disable-sync", "--disable-background-networking", "--lang=es", "--proxy-server=http://127.0.0.1:9", "--new-window", url}) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }

    static AE? Named(AE root, string name) => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AE.NameProperty, name));
    static void Invoke(AE button) => ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    static void Desktop(string exe, string edge, string folder)
    {
        var profile = Path.Combine(folder,"perfil");
        var fixture = Path.Combine(folder,"bandeja-de-prueba.html");
        File.WriteAllText(fixture, $"<title>Bandeja de entrada - Outlook - {PrivateMarker}</title><input id='addressEditBox' aria-label='Barra de direcciones' value='{Outlook}'><iframe srcdoc=\"<input aria-label='Address and search bar' value='{Outlook}'>\"></iframe>");
        using var first = Browser(edge,profile,new Uri(fixture).AbsoluteUri);
        try
        {
            IntPtr fake = IntPtr.Zero;
            Check(Wait(() => { first.Refresh(); fake=first.MainWindowHandle; return fake!=IntPtr.Zero && first.MainWindowTitle.Contains(PrivateMarker); }), "Edge real abrió la página local que imita el título y la barra");
            var negative = EdgeWindow.Discover();
            Check(negative.Probes.Count > 0 && negative.Windows.All(w => w.Hwnd != fake), "el título, el campo web y el marco no se aceptan como origen");
            using var second = Browser(edge,profile,Outlook);
            Detection? last = null;
            bool found = Wait(() => { last = EdgeWindow.Discover(); return last.Windows.Any(w=> w.Site=="outlook.cloud.microsoft" && w.Hwnd!=fake); });
            if (!found && last!=null) { var d=new DiagnosticLog(); d.DetectionResult(last); Console.WriteLine(d.Report()); }
            Check(found, "Edge real reconoce la dirección de Outlook con una pestaña sin conexión");
            var selected = last!.Windows.First(w=> w.Site=="outlook.cloud.microsoft" && w.Hwnd!=fake);
            selected.Verify();
            Check(last.Windows.Count(w=>w.Hwnd==selected.Hwnd)==1, "una sola entrada para la ventana detectada");
            var output = Directory.CreateDirectory(Path.Combine(folder,"exportaciones")).FullName;
            // Isolate the packaged app's legacy settings from any workstation settings.
            // The native CI desktop is disposable. Restore the previous value after the test.
            var settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "CorreoDiarioEdge", "carpetas.json");
            var saved = File.Exists(settings) ? File.ReadAllBytes(settings) : null;
            Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
            File.WriteAllText(settings, System.Text.Json.JsonSerializer.Serialize(new[] { output, folder }));
            try
            {
            using var app = Process.Start(new ProcessStartInfo(exe){UseShellExecute=false})!;
            try
            {
                Check(Wait(()=>{ app.Refresh(); return app.MainWindowHandle!=IntPtr.Zero && app.MainWindowTitle=="CorreoDiario para Edge"; }), "el ejecutable publicado abre su ventana");
                var form=AE.FromHandle(app.MainWindowHandle);
                Check(Named(form,"Abrir Outlook web")?.Current.IsEnabled==true && Named(form,"Exportar selección web")?.Current.IsEnabled==false, "el ejecutable ofrece la selección web desde la ventana principal");
                var detect=Named(form,"Detectar Edge");
                Check(detect!=null && Wait(()=>detect.Current.IsEnabled), "botón Detectar Edge operativo");
                Invoke(detect!);
                Check(Wait(() => Named(form,"Detectar Edge")?.Current.IsEnabled == true), "finaliza el intento de detección en la aplicación");
                var copy=Named(form,"Copiar diagnóstico");
                Check(copy!=null && copy.Current.IsEnabled, "botón Copiar diagnóstico disponible");
                Invoke(copy!);
                string text="";
                Check(Wait(()=> { try { text=Clipboard.GetText(); return text.StartsWith("CorreoDiario para Edge 0.8.2 · Diagnóstico" + Environment.NewLine, StringComparison.Ordinal); } catch(ExternalException) { return false; } }), "el botón copia el informe real al portapapeles de Windows");
                Console.WriteLine(text);
                var picker = Named(form,"Ventanas de Outlook en Edge");
                bool selectedInApp = picker != null && (
                    picker.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && ((ValuePattern)value).Current.Value.Contains("outlook.cloud.microsoft") ||
                    picker.TryGetCurrentPattern(SelectionPattern.Pattern, out var selection) && ((SelectionPattern)selection).Current.GetSelection().Any(item=>item.Current.Name.Contains("outlook.cloud.microsoft")));
                if (!selectedInApp && picker?.Current.NativeWindowHandle is int hwnd && hwnd != 0)
                {
                    // A read-only WinForms combo can expose its selection via the native control only.
                    selectedInApp = SendMessage((IntPtr)hwnd, 0x147, IntPtr.Zero, IntPtr.Zero).ToInt64() >= 0 &&
                        SendMessage((IntPtr)hwnd, 0x146, IntPtr.Zero, IntPtr.Zero).ToInt64() > 0;
                }
                Check(selectedInApp && text.Contains("Outlook reconocido"), "Outlook figura en el selector de la aplicación con su ventana delante");
                Check(text.Contains("ventanas Outlook") && !text.Contains(PrivateMarker) && !text.Contains("https://") && !text.Contains(profile), "el informe conserva causas y contadores y omite título, URL y rutas");
                NativeExport(form,output,selected.Hwnd);
                PackagedFolders(form, app.Id);
                NativeFolders(Path.Combine(folder,"lote-nube"), selected.Hwnd);
                NativeWeb(folder, selected.Hwnd);
            }
            finally { if (!app.HasExited) { app.CloseMainWindow(); if(!app.WaitForExit(5000)) app.Kill(true); } }
            }
            finally { if (saved != null) File.WriteAllBytes(settings,saved); else File.Delete(settings); }
        }
        finally { if (!first.HasExited) first.Kill(true); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length!=2) { Console.Error.WriteLine("Indica las rutas del ejecutable de CorreoDiario y de Microsoft Edge."); return 2; }
        var version = FileVersionInfo.GetVersionInfo(args[0]);
        Check(version.ProductVersion == "0.8.2" && !version.IsPreRelease,
            "el ejecutable de Windows se identifica como versión estable 0.8.2");
        var updateDir = Directory.CreateTempSubdirectory("CorreoDiario-update-").FullName;
        try
        {
            var target = Path.Combine(updateDir, "CorreoDiario.exe");
            File.Copy(args[0], target);
            using var first = Process.Start(new ProcessStartInfo(target) { UseShellExecute = false })!;
            try
            {
                Check(Wait(() => { first.Refresh(); return first.MainWindowHandle != 0; }), "la copia portátil que se actualizará está abierta");
                var staged = target + ".tmp";
                File.Copy(args[0], staged);
                using var next = Updates.Install(target, staged);
                try { Check(Wait(() => { next.Refresh(); return next.MainWindowHandle != 0; }) && File.Exists(target + ".anterior"), "el reemplazo del ejecutable abierto arranca la nueva copia"); }
                finally { if (!next.HasExited) { next.Kill(); next.WaitForExit(); } }
            }
            finally { if (!first.HasExited) { first.Kill(); first.WaitForExit(); } }
        }
        finally { Directory.Delete(updateDir, true); }
        Rules();
        TransferRules();
        string dir=Directory.CreateTempSubdirectory("CorreoDiario-Edge-prueba-").FullName;
        try { Desktop(Path.GetFullPath(args[0]),Path.GetFullPath(args[1]),dir); }
        finally { try { Directory.Delete(dir,true); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
        Console.WriteLine($"{passed} comprobaciones de detección y diagnóstico superadas.");
        return 0;
    }
}
