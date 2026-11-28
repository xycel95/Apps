using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using OleData = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace CorreoDiario.EdgeApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}

internal sealed partial class MainWindow : Form
{
    sealed record Row(Mail Message)
    {
        public override string ToString() => Message.DisplayDate + "   ·   " + Message.From + "   ·   " + Message.Subject;
    }
    readonly DiagnosticLog diagnostics = new();
    readonly CheckedListBox rows = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Correos recibidos" };
    readonly ComboBox windows = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Ventanas de Outlook en Edge" };
    readonly TextBox target = new() { Dock = DockStyle.Fill, ReadOnly = true, AccessibleName = "Carpeta de destino" };
    readonly Label status = LabelOf("Para empezar, pulsa Abrir Outlook web y marca los correos en esa ventana de Edge.");
    readonly Label counter = LabelOf("");
    readonly Button update = Btn("Buscar actualizaciones");
    readonly Button cloud = Btn("Exportar carpetas de Outlook…");
    readonly Button paste = Btn("Pegar correos"), files = Btn("Añadir archivos…"), detect = Btn("Detectar Edge"),
        countEdge = Btn("Ver selección en Edge"), choose = Btn("Elegir carpeta…"), export = Btn("Exportar archivos añadidos"),
        clear = Btn("Quitar marcados"), select = Btn("Marcar todos"), open = Btn("Abrir resultado"),
        stop = Btn("Cancelar"), copy = Btn("Copiar diagnóstico");
    bool busy, closeRequested;
    CancellationTokenSource? operation;
    string? last;
    int? lastSelectionCount;
    static readonly string Settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XyceL", "CorreoDiarioEdge", "carpetas.json");

    static Button Btn(string text) => new() { AutoSize = true, Text = text, FlatStyle = FlatStyle.System, Padding = new(7, 3, 7, 3) };
    static Label LabelOf(string text) => new() { Text = text, AutoSize = true, Dock = DockStyle.Fill, Margin = new(3, 5, 3, 5) };
    static FlowLayoutPanel Line(params Control[] controls)
    {
        var line = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        line.Controls.AddRange(controls);
        return line;
    }
    static TableLayoutPanel Field(Control field, params Button[] buttons)
    {
        var line = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = buttons.Length + 1, RowCount = 1 };
        line.ColumnStyles.Add(new(SizeType.Percent, 100));
        line.Controls.Add(field, 0, 0);
        for (int i = 0; i < buttons.Length; i++)
        {
            line.ColumnStyles.Add(new(SizeType.AutoSize));
            line.Controls.Add(buttons[i], i + 1, 0);
        }
        return line;
    }

    private readonly Func<Action<string>, CancellationToken, Task<EdgeSession>> startWeb;
    public MainWindow() : this(EdgeSession.Open) { }
    internal MainWindow(Func<Action<string>, CancellationToken, Task<EdgeSession>> startWeb)
    {
        this.startWeb = startWeb;
        Text = "CorreoDiario para Edge";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new(925, 735);
        MinimumSize = new(730, 620);
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), ColumnCount = 1 };
        Controls.Add(page);
        void Add(Control control, bool expand = false)
        {
            int i = page.RowCount++;
            page.RowStyles.Add(new(expand ? SizeType.Percent : SizeType.AutoSize, expand ? 100 : 0));
            page.Controls.Add(control, 0, i);
        }
        var title = LabelOf("CorreoDiario");
        title.Font = new Font(Font.FontFamily, 20, FontStyle.Bold);
        Add(title);
        Add(LabelOf("Marca los correos en la ventana de Edge que abre CorreoDiario. Al exportar podrás seguir trabajando."));
        Add(Line(webOpen, webExport, webClose));
        Add(Line(cloud, update));
        Add(LabelOf("También puedes añadir mensajes completos que ya tengas en el ordenador."));
        Add(Line(paste, files));
        Add(rows, true);
        Add(Line(select, clear, counter));
        Add(LabelOf("Comprobar la pestaña de Outlook (opcional)"));
        Add(Field(windows, detect, countEdge));
        Add(LabelOf("Carpeta de destino"));
        Add(Field(target, choose));
        Add(Line(export, stop, open, showPending, copy));
        showPending.Click+=(_,_)=>ShowPending();
        status.ForeColor = Color.FromArgb(21, 61, 91);
        Add(status);
        LoadFolder();
        foreach (Control surface in new Control[] { this, rows, page, title })
        {
            surface.AllowDrop = true;
            surface.DragEnter += (_, e) =>
                e.Effect = !busy && e.Data != null && e.AllowedEffect.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
            surface.DragDrop += async (_, e) =>
            {
                e.Effect = DragDropEffects.None;
                if (busy || !e.AllowedEffect.HasFlag(DragDropEffects.Copy) || e.Data is not OleData ole) return;
                e.Effect = DragDropEffects.Copy;
                await Receive(() => MailTransfer.Read(ole));
            };
        }
        webOpen.Click += async (_, _) => await OpenWeb();
        webExport.Click += async (_, _) => await ExportWeb();
        webClose.Click += async (_, _) => await CloseWeb();
        update.Click += async (_, _) => await UpdateApp();
        cloud.Click += (_, _) => BeginInvoke(() =>
        {
            if (busy || !Enabled) return;
            using var window = new FolderWindow(diagnostics, target.Text);
            window.ShowDialog(this);
        });
        paste.Click += async (_, _) => await Receive(() => Clipboard.GetDataObject() is OleData ole
            ? MailTransfer.Read(ole) : throw new TransferException("portapapeles vacío", "El portapapeles no contiene correos completos."));
        files.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Multiselect = true, Filter = "Correos (*.eml;*.msg)|*.eml;*.msg", Title = "Añadir correos existentes" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await Receive(() => MailTransfer.Files(dialog.FileNames));
        };
        detect.Click += async (_, _) => await Detect();
        countEdge.Click += async (_, _) => await InspectSelection();
        windows.SelectedIndexChanged += (_, _) => { lastSelectionCount = null; RefreshControls(); };
        select.Click += (_, _) => { for (int i = 0; i < rows.Items.Count; i++) rows.SetItemChecked(i, true); };
        clear.Click += (_, _) => { foreach (int i in rows.CheckedIndices.Cast<int>().Reverse().ToArray()) rows.Items.RemoveAt(i); RefreshControls(); };
        rows.ItemCheck += (_, _) => BeginInvoke(RefreshControls);
        choose.Click += (_, _) => ChooseFolder();
        export.Click += async (_, _) => await Export();
        stop.Click += (_, _) => operation?.Cancel();
        open.Click += (_, _) =>
        {
            try { if (last != null) Process.Start(new ProcessStartInfo(last) { UseShellExecute = true }); }
            catch (Exception e) { Error("Abrir resultado", e); }
        };
        copy.Click += (_, _) => CopyReport();
        FormClosing += (_, e) =>
        {
            if (!busy && webSession == null) return;
            e.Cancel = true;
            closeRequested = true;
            if (!busy) { BeginInvoke(async () => await CloseWeb()); return; }
            operation?.Cancel();
            status.Text = "Cerrando al terminar la operación en curso…";
        };
        RefreshControls();
    }

    async Task UpdateApp()
    {
        if (busy) return;
        busy = true;
        operation = new();
        var ct = operation.Token;
        RefreshControls();
        string? staged = null;
        try
        {
            status.Text = "Buscando la versión estable de CorreoDiario…";
            var release = await Updates.Check(ct);
            if (release == null) { status.Text = "Tienes la versión estable más reciente."; return; }
            var question = $"¿Descargar CorreoDiario {release.Version} y reiniciar la aplicación?";
            if (rows.Items.Count > 0) question += " Los correos de la lista que aún no hayas exportado tendrán que añadirse de nuevo.";
            if (MessageBox.Show(this, question, "Actualizar CorreoDiario", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { status.Text = "Actualización aplazada."; return; }
            var target = Environment.ProcessPath!;
            staged = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            status.Text = $"Descargando y verificando CorreoDiario {release.Version}…";
            await Updates.Download(release, staged, ct);
            ct.ThrowIfCancellationRequested();
            using var next = Updates.Install(target, staged);
            closeRequested = true;
        }
        catch (OperationCanceledException) { status.Text = "Actualización interrumpida."; }
        catch (Exception e) { Error("Actualizar CorreoDiario", e); }
        finally
        {
            try { if (staged != null && File.Exists(staged)) File.Delete(staged); }
            catch (IOException e) { diagnostics.Fault("Limpiar descarga", e); }
            catch (UnauthorizedAccessException e) { diagnostics.Fault("Limpiar descarga", e); }
            End();
        }
    }

    void RefreshControls()
    {
        foreach (var control in new Control[]{update, cloud, paste, files, detect, windows, choose, rows}) control.Enabled = !busy;
        webOpen.Enabled = !busy;
        webOpen.Text = webSession == null ? "Abrir Outlook web" : "Mostrar Outlook web";
        webExport.Enabled = !busy && webSession != null && target.Text.Length > 0;
        webClose.Enabled = !busy && webSession != null;
        select.Enabled = !busy && rows.CheckedItems.Count < rows.Items.Count;
        clear.Enabled = !busy && rows.CheckedItems.Count > 0;
        countEdge.Enabled = !busy && windows.SelectedItem is EdgeWindow;
        export.Enabled = !busy && rows.CheckedItems.Count > 0 && target.Text.Length > 0;
        open.Enabled = !busy && last != null;
        showPending.Enabled = !busy && lastPending.Length>0;
        stop.Enabled = operation != null && !operation.IsCancellationRequested;
        counter.Text = $"{rows.CheckedItems.Count} marcados de {rows.Items.Count} correos recibidos";
    }

    void End()
    {
        operation?.Dispose();
        operation = null;
        busy = false;
        RefreshControls();
        if (closeRequested) Close();
    }

    async Task Receive(Func<MailDelivery> take)
    {
        if (busy) return;
        busy = true;
        operation = new();
        var token = operation.Token;
        RefreshControls();
        string phase = "Recibir selección";
        try
        {
            status.Text = "Recibiendo los mensajes…";
            // Capture the offered OLE data during the user's paste/drop on its owning STA.
            var delivery = take();
            diagnostics.Event(phase, $"{delivery.Method} · recibidos {delivery.Inputs.Count}");
            phase = "Validar correos recibidos";
            var incoming = await Task.Run(() => delivery.Inputs.Select(input =>
            {
                token.ThrowIfCancellationRequested();
                return MailReader.Inspect(input, new("", "", "", delivery.Method));
            }).ToArray(), token);
            token.ThrowIfCancellationRequested();
            var known = rows.Items.Cast<Row>().Select(r => r.Message.Hash).ToHashSet();
            int added = 0;
            rows.BeginUpdate();
            try
            {
                foreach (var message in incoming)
                    if (known.Add(message.Hash)) { rows.Items.Add(new Row(message), true); added++; }
            }
            finally { rows.EndUpdate(); }
            var result = $"Recibidos {incoming.Length} · añadidos {added} · repetidos {incoming.Length - added}.";
            diagnostics.Event(phase, result);
            if (lastSelectionCount is int previous && previous != incoming.Length)
            {
                var difference = $"Último contador de Outlook {previous} · recibidos ahora {incoming.Length}.";
                diagnostics.Event("Comprobar cantidad", difference);
                result += " " + difference + " Revisa la lista antes de exportar.";
            }
            lastSelectionCount = null;
            status.Text = result + " Pulsa Exportar archivos añadidos.";
        }
        catch (OperationCanceledException) { diagnostics.Event(phase, "cancelado"); status.Text = "Entrada cancelada."; }
        catch (Exception e) { Error(phase, e); }
        finally { End(); }
    }

    async Task Detect()
    {
        if (busy) return;
        busy = true;
        windows.Items.Clear();
        RefreshControls();
        try
        {
            var found = await Task.Run(EdgeWindow.Discover);
            diagnostics.DetectionResult(found);
            windows.Items.AddRange(found.Windows.ToArray());
            if (found.Windows.Count > 0) windows.SelectedIndex = 0;
            status.Text = found.Windows.Count > 0 ? "Outlook detectado. Para exportar una selección, pulsa Abrir Outlook web y márcala en esa ventana."
                : "Deja la pestaña de Outlook seleccionada en su ventana y vuelve a pulsar Detectar Edge.";
        }
        catch (Exception e) { Error("Detectar Edge", e); }
        finally { End(); }
    }

    async Task InspectSelection()
    {
        if (busy || windows.SelectedItem is not EdgeWindow window) return;
        busy = true;
        RefreshControls();
        try
        {
            var info = await Task.Run(() =>
            {
                window.Verify();
                var visible = window.VisibleSelection();
                var count = window.SelectionCount();
                window.Verify();
                return (visible.Visible, visible.Marked, Total: count);
            });
            lastSelectionCount = info.Total;
            var counts = $"filas visibles {info.Visible} · marcadas visibles {info.Marked} · contador de Outlook {info.Total?.ToString() ?? "sin dato"}";
            diagnostics.Event("Ver selección en Edge", counts);
            status.Text = counts + (webSession==null ? ". Pulsa Abrir Outlook web y marca los correos en esa ventana."
                : ". Si los marcaste en la ventana de CorreoDiario, pulsa Exportar selección web arriba.");
        }
        catch (Exception e) { Error("Ver selección en Edge", e); }
        finally { End(); }
    }

    async Task Export()
    {
        var items = rows.CheckedItems.Cast<Row>().Select(r => r.Message).ToArray();
        if (busy || items.Length == 0) return;
        var folder = target.Text;
        busy = true;
        operation = new();
        var token = operation.Token;
        RefreshControls();
        diagnostics.Event("Exportar", $"en segundo plano · seleccionados {items.Length}");
        var progress = new Progress<string>(s => { if (!IsDisposed && busy) status.Text = s; });
        try
        {
            var result=await Task.Run(()=>Archive.WriteToFolders(items,folder,new MailPrinter(),progress,token,diagnostics.Fault),token);
            last=folder;
            lastPending=result.Pending;
            var resultText=$"Guardados {result.Saved} de {items.Length} correos · incidencias {lastPending.Length}";
            diagnostics.Event("Exportación terminada",resultText);
            status.Text="Lote terminado. "+resultText+". "+(lastPending.Length>0?"Pulsa Ver pendientes.":"Pulsa Abrir resultado.");
            if(result.Saved==items.Length && result.Pending.Length==0)
                foreach (int i in rows.CheckedIndices.Cast<int>().Reverse().ToArray()) rows.Items.RemoveAt(i);
        }
        catch (OperationCanceledException) { diagnostics.Event("Exportar", "cancelado por el usuario"); status.Text = "Exportación cancelada. Los archivos ya terminados están en la carpeta de destino."; }
        catch (Exception e) { Error("Exportar", e); }
        finally { End(); }
    }

    void Error(string phase, Exception e)
    {
        diagnostics.Fault(phase, e);
        status.Text = e is TransferException ? e.Message + " Para un lote de Outlook web, usa Abrir Outlook web." : phase + " · " + e.Message;
    }

    void CopyReport()
    {
        var report = diagnostics.Report();
        try { Clipboard.SetDataObject(report, true, 5, 80); status.Text = "Diagnóstico copiado. Ya puedes pegarlo en tu consulta."; }
        catch (ExternalException)
        {
            using var dialog = new Form { Text = "Copiar diagnóstico", Width = 700, Height = 410, StartPosition = FormStartPosition.CenterParent };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = report };
            dialog.Controls.Add(text);
            dialog.Shown += (_, _) => text.SelectAll();
            dialog.ShowDialog(this);
        }
    }

    void LoadFolder()
    {
        target.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        try
        {
            var saved = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Settings));
            if (saved is { Length: > 0 } && Directory.Exists(saved[0])) target.Text = saved[0];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
    }

    void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Carpeta para los PDF y los adjuntos", UseDescriptionForTitle = true, InitialDirectory = target.Text };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        target.Text = dialog.SelectedPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
            File.WriteAllText(Settings, JsonSerializer.Serialize(new[] { target.Text }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Error("Recordar carpeta", e); }
        RefreshControls();
    }
}
