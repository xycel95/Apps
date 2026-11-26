using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using CorreoDiario;
using CorreoDiario.EdgeApp;
using AE = System.Windows.Automation.AutomationElement;

internal static partial class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string? className, string windowName);
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    static void PackagedFolders(AE form, int appId)
    {
        var primary = Named(form, "Exportar carpetas de Outlook…");
        Check(primary?.Current.IsEnabled == true, "entrada a carpetas de Microsoft 365 en el ejecutable publicado");
        // Post the button command to the application's UI loop. The native modal dialog
        // then runs independently of the UI Automation COM call that located its button.
        var b = primary!.Current.NativeWindowHandle;
        if (b != 0) PostMessage((IntPtr)b, 0x00f5, 0, 0); else Invoke(primary);
        IntPtr hwnd = 0;
        Check(Wait(() =>
        {
            hwnd = FindWindow(null, "Exportar carpetas de Outlook");
            return hwnd != 0 && GetWindowThreadProcessId(hwnd, out uint owner) != 0 && owner == appId;
        }), "el ejecutable independiente abre la ventana de conexión por carpetas");
        var dialog = AE.FromHandle(hwnd);
        Check(Named(dialog, "Conectar con Microsoft 365")?.Current.IsEnabled == true
            && Named(dialog, "Exportar carpetas")?.Current.IsEnabled == false, "la exportación espera una conexión y una selección válidas");
        Invoke(Named(dialog, "Copiar solicitud para Informática")!);
        Check(Wait(() => Clipboard.GetText().Contains("Mail.Read") && Clipboard.GetText().Contains("http://localhost")), "el ejecutable copia la configuración necesaria para Informática");
        Invoke(Named(dialog, "Copiar diagnóstico")!);
        Check(Wait(() => Clipboard.GetText().Contains("falta configuración de la organización")), "el diagnóstico de la primera conexión identifica que falta configuración");
        PostMessage(hwnd, 0x0010, 0, 0);
        Check(Wait(() => FindWindow(null, "Exportar carpetas de Outlook") == 0 && primary.Current.IsEnabled), "se puede cerrar el modo de carpetas y volver al principal");
    }

    static void NativeFolders(string destination, IntPtr other)
    {
        var fixture = new FixtureMailbox { Total = 3, PauseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var log = new DiagnosticLog();
        var startup = new TaskCompletionSource<FolderWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Task<MicrosoftMailbox> Connect(M365Settings settings, CancellationToken ct)
                {
                    var client = new HttpClient(fixture);
                    return Task.FromResult(new MicrosoftMailbox(new(client, _ => Task.FromResult("token-de-prueba"), FixtureMailbox.Account), "cuenta-sintetica", client));
                }
                using var window = new FolderWindow(log, destination, Connect, new M365Settings(Guid.NewGuid(), Guid.NewGuid()));
                window.Shown += (_,_) => startup.TrySetResult(window);
                Application.Run(window);
            }
            catch (Exception e) { startup.TrySetException(e); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var form = startup.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        static IEnumerable<Control> Desc(Control c) => c.Controls.Cast<Control>().SelectMany(child => Desc(child).Prepend(child));
        T Ui<T>(Func<T> read) => (T)form.Invoke(read)!;
        Button ButtonByText(string label) => Desc(form).OfType<Button>().Single(x=>x.Text == label);
        var chosen = "Principal / Tramitados";
        try
        {
            form.Invoke(() => ButtonByText("Conectar con Microsoft 365").PerformClick());
            Check(Wait(() => Ui(() => Desc(form).OfType<CheckedListBox>().Single().Items.Count == 3)), "la ventana de carpetas carga ramas y subcarpetas con el servicio de prueba");
            form.Invoke(() =>
            {
                var list = Desc(form).OfType<CheckedListBox>().Single();
                list.SetItemChecked(list.Items.Cast<CloudFolder>().Select(f => f.Path).ToList().IndexOf(chosen), true);
            });
            Check(Wait(() => Ui(() => ButtonByText("Exportar carpetas").Enabled)), "marcar una carpeta habilita la exportación");
            form.Invoke(() => ButtonByText("Exportar carpetas").PerformClick());
            fixture.Reading.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            form.Invoke(() => { form.WindowState = FormWindowState.Minimized; });
            SetForegroundWindow(other);
            Check(Wait(() => GetForegroundWindow() == other, 5), "otra ventana recibe el foco con el lote de carpetas en marcha");
            var pos = Cursor.Position;
            bool held = true;
            fixture.PauseFirst!.SetResult();
            Check(Wait(() =>
            {
                held &= Cursor.Position == pos && GetForegroundWindow() == other;
                return Ui(() => ButtonByText("Abrir resultado").Enabled);
            }), "la ventana minimizada completa el lote de carpetas");
            Check(held && Cursor.Position == pos && GetForegroundWindow() == other, "el lote de carpetas conserva el foco y el ratón hasta terminar");
            Check(Directory.GetFiles(destination, "Correo.pdf", SearchOption.AllDirectories).Length == 3
                && Directory.GetFiles(destination, "Prueba.bin", SearchOption.AllDirectories).Length == 3, "el lote de la interfaz crea tres carpetas con PDF y adjunto");
            var report = log.Report();
            Check(report.Contains("verificados 3/3") && !report.Contains(chosen) && !report.Contains("example.test") && !report.Contains("Asunto de prueba")
                && !report.Contains("token-de-prueba"), "el diagnóstico de carpetas comparte los contadores sin revelar contenido, credenciales ni carpetas");
        }
        finally
        {
            fixture.PauseFirst?.TrySetResult();
            form.BeginInvoke(form.Close);
            thread.Join(TimeSpan.FromSeconds(10));
        }
    }
}
