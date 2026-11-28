using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CorreoDiario.EdgeApp;

internal sealed class FolderWindow : Form
{
    readonly DiagnosticLog log;
    readonly Func<M365Settings, CancellationToken, Task<MicrosoftMailbox>> connect;
    readonly bool remember;
    readonly Button configure = ButtonOf("Configuración…"), request = ButtonOf("Copiar solicitud para Informática"),
        login = ButtonOf("Conectar con Microsoft 365"), refresh = ButtonOf("Actualizar carpetas"),
        run = ButtonOf("Exportar carpetas"), cancel = ButtonOf("Cancelar"), open = ButtonOf("Abrir resultado"),
        choose = ButtonOf("Elegir destino…"), copy = ButtonOf("Copiar diagnóstico"), import = ButtonOf("Abrir configuración…");
    readonly TextBox appId = Input("ID de aplicación"), tenant = Input("ID del directorio"), target = Input("Destino en Windows");
    readonly CheckedListBox folders = new() { Dock = DockStyle.Fill, IntegralHeight = false, CheckOnClick = true,
        HorizontalScrollbar = true, AccessibleName = "Carpetas del buzón" };
    readonly Label status = LabelOf(""), count = LabelOf(""), account = LabelOf("");
    readonly TableLayoutPanel setup = Grid();
    MicrosoftMailbox? session;
    CancellationTokenSource? operation;
    string? output;
    bool closing;

    static Button ButtonOf(string text) => new() { Text = text, AutoSize = true, Padding = new(5, 2, 5, 2) };
    static TextBox Input(string name) => new() { Dock = DockStyle.Fill, AccessibleName = name };
    static Label LabelOf(string text) => new() { Text = text, Dock = DockStyle.Fill, AutoSize = true, Margin = new(3, 5, 3, 4) };
    static TableLayoutPanel Grid() => new() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
    static FlowLayoutPanel Line(params Control[] items)
    {
        var line = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
        line.Controls.AddRange(items);
        return line;
    }
    static void Row(TableLayoutPanel page, Control control, bool fill = false)
    {
        int row = page.RowCount++;
        page.RowStyles.Add(new(fill ? SizeType.Percent : SizeType.AutoSize, fill ? 100 : 0));
        page.Controls.Add(control, 0, row);
    }

    internal FolderWindow(DiagnosticLog log, string destination,
        Func<M365Settings, CancellationToken, Task<MicrosoftMailbox>>? connector = null, M365Settings? initial = null)
    {
        this.log = log;
        connect = connector ?? MicrosoftMailbox.Connect;
        remember = connector == null;
        Text = "Exportar carpetas de Outlook";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Size = new(820, 680);
        MinimumSize = new(650, 580);
        var page = Grid();
        page.Padding = new(15);
        Controls.Add(page);
        var title = LabelOf("Carpetas de Outlook");
        title.Font = new Font(Font.FontFamily, 17, FontStyle.Bold);
        Row(page, title);
        Row(page, LabelOf("Conecta tu cuenta de trabajo, marca las carpetas y pulsa Exportar carpetas. Después puedes minimizar la ventana y seguir trabajando."));
        Row(page, Line(login, configure, request));
        account.Visible = false;
        Row(page, account);
        Row(setup, LabelOf("ID de aplicación (cliente) que facilita Informática"));
        Row(setup, appId);
        Row(setup, LabelOf("ID del directorio de la organización (opcional para registros que admiten varias organizaciones)"));
        Row(setup, tenant);
        Row(setup, Line(import));
        Row(page, setup);
        Row(page, LabelOf("Carpetas del buzón · Marca también las subcarpetas que quieras incluir."));
        Row(page, folders, true);
        Row(page, Line(refresh, count));
        Row(page, LabelOf("Carpeta de destino en Windows"));
        target.ReadOnly = true;
        var location = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        location.ColumnStyles.Add(new(SizeType.Percent, 100)); location.ColumnStyles.Add(new(SizeType.AutoSize));
        location.Controls.Add(target, 0, 0); location.Controls.Add(choose, 1, 0);
        Row(page, location);
        Row(page, Line(run, cancel, open, copy));
        status.ForeColor = Color.FromArgb(21, 61, 91);
        Row(page, status);
        target.Text = destination;
        status.Text = "Conecta con la cuenta cuyo buzón quieres exportar.";
        try
        {
            var settings = initial ?? (remember ? M365Settings.Load() : null);
            log.Event("Abrir carpetas de Microsoft 365", settings == null ? "falta configuración de la organización" : "configuración disponible");
            if (settings != null) Apply(settings);
            else status.Text = "Primera conexión: si aún no tienes el ID de aplicación, copia la solicitud para Informática.";
        }
        catch (Exception e) { Fault("Leer configuración de Microsoft 365", e); }
        setup.Visible = appId.Text.Length == 0;
        login.Click += async (_, _) => await Login();
        refresh.Click += async (_, _) => await Operate("Cargar carpetas de Microsoft 365", LoadFolders);
        configure.Click += (_, _) => setup.Visible = !setup.Visible;
        request.Click += (_, _) => Copy(MicrosoftMailbox.ITRequest, "Solicitud copiada. Puedes enviarla a Informática.");
        import.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Configuración Microsoft 365 (*.json)|*.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { Apply(M365Settings.Read(dialog.FileName)); status.Text = "Configuración preparada. Pulsa Conectar con Microsoft 365."; }
            catch (Exception e) { Fault("Importar configuración de Microsoft 365", e); }
        };
        folders.ItemCheck += (_, _) => BeginInvoke(ControlsState);
        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Guardar los PDF y adjuntos de Outlook", UseDescriptionForTitle = true, InitialDirectory = target.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK) { target.Text = dialog.SelectedPath; Persist(); ControlsState(); }
        };
        run.Click += async (_, _) => await Export();
        cancel.Click += (_, _) => operation?.Cancel();
        copy.Click += (_, _) => Copy(log.Report(), "Diagnóstico copiado. Ya puedes pegarlo en tu consulta.");
        open.Click += (_, _) =>
        {
            try { if (output != null) Process.Start(new ProcessStartInfo(output) { UseShellExecute = true }); }
            catch (Exception e) { Fault("Abrir resultado", e); }
        };
        FormClosing += (_, e) => { if (operation != null) { e.Cancel = true; closing = true; operation.Cancel(); } };
        FormClosed += (_, _) => { session?.Dispose(); session = null; };
        ControlsState();
    }

    void Apply(M365Settings settings) { appId.Text = settings.ClientId.ToString(); tenant.Text = settings.TenantId?.ToString() ?? ""; }
    void ControlsState()
    {
        bool idle = operation == null;
        foreach (Control c in new Control[] { login, configure, setup, choose }) c.Enabled = idle;
        refresh.Enabled = folders.Enabled = idle && session != null;
        run.Enabled = idle && session != null && folders.CheckedItems.Count > 0 && target.Text.Length > 0;
        cancel.Enabled = !idle && !operation!.IsCancellationRequested;
        open.Enabled = idle && output != null;
        count.Text = $"{folders.CheckedItems.Count} marcadas de {folders.Items.Count}";
    }

    async Task Operate(string phase, Func<CancellationToken, Task> action)
    {
        if (operation != null) return;
        operation = new();
        ControlsState();
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { log.Event(phase, "cancelado"); status.Text = "Operación cancelada."; }
        catch (Exception e) { Fault(phase, e); }
        finally { operation.Dispose(); operation = null; ControlsState(); if (closing) Close(); }
    }
    void Fault(string phase, Exception e)
    {
        log.Fault(phase, e);
        status.Text = e switch
        {
            MailboxException or FormatException => e.Message,
            UnauthorizedAccessException => "Windows ha denegado el acceso al destino. Elige una carpeta con permiso de escritura.",
            OperationCanceledException or System.Net.Http.HttpRequestException => "No se pudo completar la conexión. Comprueba la red y vuelve a intentarlo.",
            IOException => "No se ha podido acceder a los archivos. Comprueba el destino y si hay otro lote en curso.",
            _ => phase + " · " + DiagnosticLog.ErrorCode(e) + ". Puedes copiar el diagnóstico."
        };
    }

    async Task Login()
    {
        if (!Guid.TryParse(appId.Text, out var client) || client == Guid.Empty)
        { log.Event("Conectar con Microsoft 365", "ID de aplicación ausente o no válido"); setup.Visible = true; status.Text = "Introduce el ID de aplicación que facilita Informática. El botón Copiar solicitud prepara el texto."; return; }
        Guid? directory = null;
        if (!string.IsNullOrWhiteSpace(tenant.Text))
        {
            if (!Guid.TryParse(tenant.Text, out var tid) || tid == Guid.Empty)
            { log.Event("Conectar con Microsoft 365", "ID de directorio no válido"); setup.Visible = true; status.Text = "El ID del directorio debe ser el identificador que facilita Informática."; return; }
            directory = tid;
        }
        var settings = new M365Settings(client, directory);
        await Operate("Conectar con Microsoft 365", async ct =>
        {
            session?.Dispose(); session = null; folders.Items.Clear(); output = null; account.Visible = false;
            status.Text = "Completa la identificación en el navegador de tu organización.";
            session = await connect(settings, ct);
            account.Text = "Buzón conectado · " + session.DisplayAccount;
            account.Visible = session.DisplayAccount.Length > 0;
            log.Event("Conectar con Microsoft 365", "cuenta conectada");
            if (remember) try { settings.Save(); } catch (Exception e) { log.Fault("Recordar configuración de Microsoft 365", e); }
            setup.Visible = false;
            try { await LoadFolders(ct); }
            catch (Exception e) when (!ct.IsCancellationRequested) { Fault("Cargar carpetas de Microsoft 365", e); }
        });
    }

    sealed record Preferences(string Destination, string[] Ids);
    string PreferencePath => Path.Combine(M365Settings.DirectoryPath, "Carpetas365-" + session!.AccountKey + ".json");
    bool firstList = true;
    async Task LoadFolders(CancellationToken ct)
    {
        var previous = folders.CheckedItems.Cast<CloudFolder>().Select(f => f.Id).ToHashSet();
        bool loadSaved = firstList || folders.Items.Count == 0;
        folders.Items.Clear();
        status.Text = "Leyendo las carpetas del buzón…";
        var all = await session!.Graph.Folders(ct);
        if (loadSaved && remember && File.Exists(PreferencePath))
        {
            try
            {
                var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencePath));
                if (p != null) { previous = (p.Ids ?? []).ToHashSet(); if (Directory.Exists(p.Destination)) target.Text = p.Destination; }
            }
            catch (Exception e) { log.Fault("Leer preferencias de carpetas", e); }
        }
        firstList = false;
        folders.BeginUpdate();
        try { foreach (var entry in all) folders.Items.Add(entry, previous.Contains(entry.Id)); }
        finally { folders.EndUpdate(); }
        log.Event("Cargar carpetas de Microsoft 365", $"carpetas {all.Count}");
        status.Text = all.Count == 0 ? "Microsoft 365 devolvió un buzón sin carpetas visibles." : "Marca las carpetas que quieras y pulsa Exportar carpetas.";
    }

    void Persist()
    {
        if (!remember || session == null) return;
        try
        {
            Directory.CreateDirectory(M365Settings.DirectoryPath);
            var value = new Preferences(target.Text, folders.CheckedItems.Cast<CloudFolder>().Select(f => f.Id).ToArray());
            File.WriteAllText(PreferencePath, JsonSerializer.Serialize(value));
        }
        catch (Exception e) { log.Fault("Recordar carpetas seleccionadas", e); }
    }
    async Task Export()
    {
        var selected = folders.CheckedItems.Cast<CloudFolder>().ToArray();
        if (selected.Length == 0 || session == null) return;
        Persist();
        var active = session;
        string destination = target.Text;
        await Operate("Exportar carpetas de Microsoft 365", async ct =>
        {
            var progress = new Progress<string>(s => { if (!IsDisposed && operation != null) status.Text = s; });
            log.Event("Exportar carpetas de Microsoft 365", $"carpetas elegidas {selected.Length}");
            var result = await Task.Run(() => FolderArchive.WriteAsync(active.Graph, active.AccountKey, selected, destination, progress, ct, log.Fault,flatDestination:true), ct);
            output = result.Directory;
            var summary = $"verificados {result.Saved + result.Reused}/{result.Listed} · nuevos o actualizados {result.Saved} · ya guardados {result.Reused} · revisar {result.Review} · pendientes {result.Pending} · carpetas incompletas {result.IncompleteFolders} · errores de hilo {result.ThreadErrors}";
            log.Event("Resultado de carpetas", (result.Cancelled ? "cancelado · " : "") + summary);
            status.Text = (result.Cancelled ? "Cancelado · " : result.Complete ? "Terminado · " : "Hay incidencias · ") + summary + ".";
        });
    }

    void Copy(string value, string message)
    {
        try { Clipboard.SetDataObject(value, true, 5, 80); status.Text = message; }
        catch (ExternalException)
        {
            using var dialog = new Form { Text = "Texto para copiar", Size = new(700, 420), StartPosition = FormStartPosition.CenterParent };
            var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = value };
            dialog.Controls.Add(box); dialog.Shown += (_, _) => box.SelectAll(); dialog.ShowDialog(this);
        }
    }
}
