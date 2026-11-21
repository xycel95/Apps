using System.Diagnostics;
using CorreoDiario.Core;

namespace CorreoDiario.Windows;

internal sealed class MainForm : Form
{
    private readonly Color background = Color.FromArgb(17, 24, 39);
    private readonly Color surface = Color.FromArgb(30, 41, 59);
    private readonly ComboBox account = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly ComboBox tabs = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 730 };
    private readonly Button findTabs = new ThemedButton() { Text = "Buscar pestañas", AutoSize = true };
    private readonly Button connectTab = new ThemedButton() { Text = "Usar esta pestaña", AutoSize = true, Enabled = false };
    private readonly Button disconnect = new ThemedButton() { Text = "Desconectar", AutoSize = true, Enabled = false };
    private readonly Button help = new ThemedButton() { Text = "Cómo conectar", AutoSize = true };
    private readonly CheckedListBox folders = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly DateTimePicker dateFrom = new() { Format = DateTimePickerFormat.Short, Width = 130, AccessibleName = "Desde" };
    private readonly DateTimePicker dateThrough = new() { Format = DateTimePickerFormat.Short, Width = 130, AccessibleName = "Hasta" };
    private readonly Button list = new ThemedButton() { Text = "Buscar correos", AutoSize = true, Enabled = false };
    private readonly DataGridView messages = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, BackgroundColor = Color.FromArgb(30, 41, 59), BorderStyle = BorderStyle.None };
    private readonly TextBox destination = new() { Dock = DockStyle.Fill, ReadOnly = true, PlaceholderText = "Elige dónde guardar los PDF y los adjuntos" };
    private readonly Button choose = new ThemedButton() { Text = "Elegir carpeta", AutoSize = true };
    private readonly CheckBox delete = new ThemedCheckBox() { Text = "Eliminar del buzón después de verificar la exportación", AutoSize = true, Checked = false };
    private readonly Button export = new ThemedButton() { Text = "Guardar correos y adjuntos", AutoSize = true, Enabled = false };
    private readonly Button cancel = new ThemedButton() { Text = "Cancelar", AutoSize = true, Enabled = false };
    private readonly Button openResult = new ThemedButton() { Text = "Abrir resultado", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Text = "Abre tu correo en el navegador y pulsa «Buscar pestañas».", AutoSize = false, Dock = DockStyle.Fill };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Height = 8 };
    private BrowserSocket? browser;
    private BrowserTabSession? selectedSession;
    private IMailSource? source;
    private PdfRenderer? renderer;
    private CancellationTokenSource? operation;
    private string? lastDirectory;

    public MainForm()
    {
        Text = "CorreoDiario · 0.4.1 beta";
        Size = new(1280, 900);
        MinimumSize = new(1050, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new("Segoe UI", 10F);
        BackColor = background;
        ForeColor = Color.FromArgb(235, 241, 250);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(26, 20, 26, 18), ColumnCount = 1, RowCount = 8 };
        foreach (var height in new[] { 82F, 124F, 48F }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        foreach (var height in new[] { 46F, 47F, 45F, 52F }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        var title = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        title.RowStyles.Add(new(SizeType.Absolute, 48)); title.RowStyles.Add(new(SizeType.Absolute, 24));
        title.Controls.Add(new Label { Text = "CorreoDiario", Font = new("Segoe UI Semibold", 25), AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty });
        title.Controls.Add(new Label { Text = "Guarda tu correo usando la pestaña que ya tienes abierta.", AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = Color.FromArgb(156, 174, 197) });
        layout.Controls.Add(title, 0, 0);
        account.Items.AddRange(["Microsoft Edge · ya abierto", "Google Chrome · ya abierto", "Outlook del equipo (clásico)"]);
        account.SelectedIndex = 0;
        dateFrom.Value = dateThrough.Value = DateTime.Today;
        var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new(0, 10, 0, 0) };
        connection.Controls.AddRange([account, findTabs, disconnect, help]);
        connection.SetFlowBreak(help, true);
        connection.Controls.AddRange([tabs, connectTab]);
        connection.SetFlowBreak(connectTab, true);
        connection.Controls.Add(new Label { Text = "La primera vez, pulsa «Cómo conectar». El navegador te pedirá permitir la conexión; después elige tu pestaña de Outlook.", AutoSize = true, ForeColor = Color.FromArgb(156, 174, 197), Margin = new(3, 5, 0, 0) });
        layout.Controls.Add(connection, 0, 1);
        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(0, 4, 0, 4) };
        var today = new ThemedButton { Text = "Hoy", AutoSize = true };
        var all = new ThemedButton { Text = "Marcar todos", AutoSize = true };
        var none = new ThemedButton { Text = "Desmarcar todos", AutoSize = true };
        filter.Controls.AddRange([new Label { Text = "Desde", AutoSize = true, Margin = new(0, 7, 8, 0) }, dateFrom,
            new Label { Text = "Hasta", AutoSize = true, Margin = new(8, 7, 8, 0) }, dateThrough, today, list, all, none]);
        layout.Controls.Add(filter, 0, 2);
        var split = new SplitContainer { Size = new(1180, 430), Dock = DockStyle.Fill, SplitterDistance = 245, FixedPanel = FixedPanel.Panel1, BackColor = background };
        var folderLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new(12), BackColor = surface };
        folderLayout.RowStyles.Add(new(SizeType.Absolute, 31)); folderLayout.RowStyles.Add(new(SizeType.Percent, 100));
        folderLayout.Controls.Add(new Label { Text = "CARPETAS", Font = new("Segoe UI Semibold", 10), AutoSize = true }, 0, 0);
        folderLayout.Controls.Add(folders, 0, 1);
        split.Panel1.Controls.Add(folderLayout);
        split.Panel2.Controls.Add(messages);
        layout.Controls.Add(split, 0, 3);
        messages.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", FillWeight = 10, ReadOnly = false });
        messages.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Fecha y hora", FillWeight = 31, ReadOnly = true,
            ValueType = typeof(DateTime), DefaultCellStyle = new() { Format = "dd/MM/yyyy HH:mm" } });
        messages.Columns.Add(new DataGridViewTextBoxColumn { Name = "Direction", HeaderText = "Tipo", FillWeight = 23, ReadOnly = true });
        messages.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = "De / Para", FillWeight = 45, ReadOnly = true });
        messages.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "Asunto", FillWeight = 80, ReadOnly = true });
        messages.EnableHeadersVisualStyles = false;
        messages.ColumnHeadersDefaultCellStyle = new() { BackColor = surface, ForeColor = ForeColor, Font = new("Segoe UI Semibold", 10), Padding = new(4) };
        messages.DefaultCellStyle = new() { BackColor = surface, ForeColor = ForeColor, SelectionBackColor = Color.FromArgb(35, 67, 103), SelectionForeColor = Color.White, Padding = new(4) };
        messages.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 35, 51);
        messages.GridColor = Color.FromArgb(43, 57, 77);
        messages.RowTemplate.Height = 34;
        var outputRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new(0, 9, 0, 0) };
        outputRow.ColumnStyles.Add(new(SizeType.Percent, 100)); outputRow.ColumnStyles.Add(new(SizeType.AutoSize));
        outputRow.Controls.Add(destination, 0, 0); outputRow.Controls.Add(choose, 1, 0);
        layout.Controls.Add(outputRow, 0, 4);
        var deletionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(0, 8, 0, 0) };
        deletionRow.Controls.Add(delete);
        layout.Controls.Add(deletionRow, 0, 5);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.AddRange([export, cancel, openResult]);
        layout.Controls.Add(actions, 0, 6);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        footer.RowStyles.Add(new(SizeType.Percent, 100)); footer.RowStyles.Add(new(SizeType.Absolute, 8));
        footer.Controls.Add(status, 0, 0); footer.Controls.Add(progress, 0, 1);
        layout.Controls.Add(footer, 0, 7);
        Controls.Add(layout);
        PaintControls(this);
        export.BackColor = Color.FromArgb(24, 112, 210);
        findTabs.BackColor = Color.FromArgb(37, 82, 128);
        account.SelectedIndexChanged += (_, _) =>
        {
            Disconnect();
            delete.Checked = false;
            status.Text = account.SelectedIndex == 2 ? "Usa las cuentas ya configuradas en el Outlook clásico de este equipo." : "Abre tu correo en el navegador y pulsa «Buscar pestañas».";
            findTabs.Text = account.SelectedIndex == 2 ? "Conectar Outlook" : "Buscar pestañas";
            RefreshButtons();
        };
        findTabs.Click += async (_, _) => await SafeAsync(async ct =>
        {
            Disconnect();
            if (account.SelectedIndex == 2)
            {
                source = new OutlookClassicSource();
                await LoadFoldersAsync(ct);
                return;
            }
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var profile = account.SelectedIndex == 0 ? Path.Combine(local, "Microsoft", "Edge", "User Data") : Path.Combine(local, "Google", "Chrome", "User Data");
            var descriptor = Path.Combine(profile, "DevToolsActivePort");
            if (!File.Exists(descriptor)) throw new InvalidOperationException("El navegador todavía no permite la conexión. Abre tu correo en Edge o Chrome y pulsa «Cómo conectar» para activar su opción de conexión.");
            var endpoint = BrowserEndpoint.Parse(await File.ReadAllTextAsync(descriptor, ct));
            browser = new BrowserSocket();
            try
            {
                status.Text = "Confirma «Permitir» en el aviso de tu navegador. Puedes cancelar desde aquí.";
                await browser.ConnectAsync(endpoint, ct);
                var found = await BrowserTabSession.FindAsync(browser, ct);
                foreach (var tab in found) tabs.Items.Add(tab);
                if (found.Count == 1) tabs.SelectedIndex = 0;
                status.Text = found.Count == 0 ? "No se ha encontrado una pestaña HTTPS de Outlook. Abre tu correo y vuelve a buscar." : $"{found.Count} pestañas de Outlook encontradas. Elige una y pulsa «Usar esta pestaña».";
            }
            catch { browser.Dispose(); browser = null; throw; }
        });
        connectTab.Click += async (_, _) => await SafeAsync(async ct =>
        {
            if (browser is null || tabs.SelectedItem is not BrowserTab selected) throw new InvalidOperationException("Selecciona primero una pestaña de Outlook.");
            selectedSession ??= await BrowserTabSession.AttachAsync(browser, selected, ct);
            source = new OwaSource(new OwaPageTransport(selectedSession));
            status.Text = "Leyendo las carpetas. Si tarda, pulsa Actualizar en tu pestaña de Outlook para renovar la conexión.";
            await LoadFoldersAsync(ct);
        });
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (source is not OutlookClassicSource) { source = null; folders.Items.Clear(); messages.Rows.Clear(); }
            selectedSession?.Dispose(); selectedSession = null;
            delete.Checked = false; RefreshButtons();
        };
        disconnect.Click += (_, _) => { Disconnect(); status.Text = "Desconectado. Tu correo continúa abierto en el navegador."; RefreshButtons(); };
        help.Click += (_, _) => BrowserHelp.Show(this, account.SelectedIndex == 1);
        list.Click += async (_, _) => await SafeAsync(async ct =>
        {
            var selected = folders.CheckedItems.Cast<FolderLabel>().Select(x => x.Folder).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Marca al menos una carpeta.");
            messages.Rows.Clear();
            var dates = new MailDateRange(DateOnly.FromDateTime(dateFrom.Value), DateOnly.FromDateTime(dateThrough.Value));
            var result = await source!.ListAsync(selected, dates, TimeZoneInfo.Local, ct);
            foreach (var mail in result)
            {
                var row = messages.Rows[messages.Rows.Add(true, TimeZoneInfo.ConvertTime(mail.Timestamp, TimeZoneInfo.Local).DateTime, mail.Sent ? "ENVIADO" : "RECIBIDO", mail.Sent ? mail.To : mail.From, mail.Subject)];
                row.Tag = mail;
            }
            status.Text = $"{result.Count} correos encontrados del {dates.From:dd/MM/yyyy} al {dates.Through:dd/MM/yyyy}, ambos incluidos. Los adjuntos se comprobarán al guardar.";
        });
        today.Click += (_, _) => { dateFrom.Value = DateTime.Today; dateThrough.Value = DateTime.Today; };
        void DatesChanged(object? sender, EventArgs e)
        {
            if (operation is not null) return;
            messages.Rows.Clear(); delete.Checked = false;
            status.Text = "Fechas cambiadas. Pulsa «Buscar correos» para cargar el intervalo.";
            RefreshButtons();
        }
        dateFrom.ValueChanged += DatesChanged;
        dateThrough.ValueChanged += DatesChanged;
        all.Click += (_, _) => { if (operation is null) foreach (DataGridViewRow row in messages.Rows) row.Cells[0].Value = true; };
        none.Click += (_, _) => { if (operation is null) foreach (DataGridViewRow row in messages.Rows) row.Cells[0].Value = false; };
        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Carpeta donde guardar los correos y sus adjuntos", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) destination.Text = dialog.SelectedPath;
            RefreshButtons();
        };
        export.Click += async (_, _) => await SafeAsync(async ct =>
        {
            messages.EndEdit();
            var selected = messages.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true).Select(r => (MailInfo)r.Tag!).ToList();
            renderer ??= new PdfRenderer();
            var engine = new ExportEngine(source!, renderer);
            var options = new ExportOptions(destination.Text, delete.Checked);
            var result = await engine.RunAsync(selected, options, new Progress<ExportProgress>(p =>
            {
                status.Text = p.Text;
                progress.Maximum = Math.Max(1, p.Total); progress.Value = Math.Clamp(p.Completed, 0, progress.Maximum);
            }), ct);
            lastDirectory = result.Directory;
            status.Text = $"Guardados y verificados: {result.Messages} correos y {result.Attachments} adjuntos. Movidos a Eliminados: {result.Moved}.";
            if (result.Moved > 0) messages.Rows.Clear();
            delete.Checked = false;
            openResult.Enabled = true;
            MessageBox.Show(this, status.Text, "Exportación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        cancel.Click += (_, _) => operation?.Cancel();
        openResult.Click += (_, _) => { if (lastDirectory is not null) Process.Start(new ProcessStartInfo(lastDirectory) { UseShellExecute = true }); };
        FormClosing += (_, e) =>
        {
            if (operation is not null) { e.Cancel = true; operation.Cancel(); status.Text = "Deteniendo la operación. Espera antes de cerrar."; }
            else { Disconnect(); renderer?.Dispose(); }
        };
    }

    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        folders.Items.Clear(); messages.Rows.Clear();
        var result = await source!.FoldersAsync(ct);
        foreach (var folder in result) folders.Items.Add(new FolderLabel(folder), result.IndexOfReference(folder) < 2);
        var notice = source is OwaSource web ? web.OtherFoldersNotice : (source as OutlookClassicSource)?.Notice;
        status.Text = notice ?? $"Conexión preparada. {result.Count} carpetas disponibles.";
    }
    private void Disconnect()
    {
        (source as IDisposable)?.Dispose(); source = null;
        selectedSession?.Dispose(); selectedSession = null;
        browser?.Dispose(); browser = null;
        tabs.Items.Clear(); folders.Items.Clear(); messages.Rows.Clear();
        delete.Checked = false;
    }

    private async Task SafeAsync(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        operation = new();
        RefreshButtons();
        try { await action(operation.Token); }
        catch (OperationCanceledException) { status.Text = "Operación cancelada."; }
        catch (Exception ex)
        {
            if (ex is ExportException exportError) { lastDirectory = exportError.Directory; openResult.Enabled = true; }
            status.Text = "La operación se ha detenido. Revisa el aviso antes de repetirla.";
            MessageBox.Show(this, ex.Message, "CorreoDiario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { operation.Dispose(); operation = null; delete.Checked = false; RefreshButtons(); }
    }
    private void RefreshButtons()
    {
        var busy = operation is not null;
        account.Enabled = findTabs.Enabled = choose.Enabled = dateFrom.Enabled = dateThrough.Enabled = folders.Enabled = messages.Enabled = delete.Enabled = !busy;
        tabs.Enabled = !busy && account.SelectedIndex != 2;
        help.Enabled = !busy && account.SelectedIndex != 2;
        connectTab.Enabled = !busy && browser is not null && tabs.SelectedItem is BrowserTab;
        disconnect.Enabled = !busy && (browser is not null || source is OutlookClassicSource);
        list.Enabled = !busy && source is not null && folders.Items.Count > 0;
        export.Enabled = !busy && source is not null && messages.Rows.Count > 0 && destination.Text.Length > 0;
        cancel.Enabled = busy;
    }
    private void PaintControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = Color.FromArgb(46, 64, 87); b.ForeColor = ForeColor; b.Padding = new(10, 4, 10, 4); b.Cursor = Cursors.Hand; }
            else if (c is CheckBox) { c.BackColor = background; c.ForeColor = ForeColor; }
            else if (c is TextBox or ComboBox or CheckedListBox) { c.BackColor = surface; c.ForeColor = ForeColor; }
            if (c is not DataGridView) PaintControls(c);
        }
    }
    private sealed record FolderLabel(MailFolder Folder) { public override string ToString() => Folder.Name; }
}

internal static class ListExtensions
{
    public static int IndexOfReference<T>(this IReadOnlyList<T> list, T value) { for (var i = 0; i < list.Count; i++) if (Equals(list[i], value)) return i; return -1; }
}
