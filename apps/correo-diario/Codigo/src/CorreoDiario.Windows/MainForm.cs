using System.Diagnostics;
using CorreoDiario.Core;

namespace CorreoDiario.Windows;

internal sealed class MainForm : Form
{
    private readonly Color background = Color.FromArgb(17, 24, 39);
    private readonly Color surface = Color.FromArgb(30, 41, 59);
    private readonly ComboBox account = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly TextBox url = new() { Width = 445, PlaceholderText = "Dirección de acceso de Outlook" };
    private readonly Button login = new() { Text = "Abrir Outlook", AutoSize = true };
    private readonly Button loadFolders = new() { Text = "Cargar carpetas", AutoSize = true, Enabled = false };
    private readonly Button logout = new() { Text = "Cerrar sesión", AutoSize = true, Enabled = false };
    private readonly CheckedListBox folders = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly Button list = new() { Text = "Buscar correos", AutoSize = true, Enabled = false };
    private readonly DataGridView messages = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, BackgroundColor = Color.FromArgb(30, 41, 59), BorderStyle = BorderStyle.None };
    private readonly TextBox destination = new() { Dock = DockStyle.Fill, ReadOnly = true, PlaceholderText = "Elige dónde guardar los PDF y los adjuntos" };
    private readonly Button choose = new() { Text = "Elegir carpeta", AutoSize = true };
    private readonly CheckBox delete = new() { Text = "Eliminar del buzón después de verificar la exportación", AutoSize = true, Checked = false };
    private readonly Button export = new() { Text = "Guardar correos y adjuntos", AutoSize = true, Enabled = false };
    private readonly Button cancel = new() { Text = "Cancelar", AutoSize = true, Enabled = false };
    private readonly Button openResult = new() { Text = "Abrir resultado", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Text = "Abre Outlook e inicia sesión para empezar.", AutoSize = false, Dock = DockStyle.Fill };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Height = 8 };
    private OutlookSession? session;
    private IMailSource? source;
    private PdfRenderer? renderer;
    private CancellationTokenSource? operation;
    private string? lastDirectory;

    public MainForm()
    {
        Text = "CorreoDiario · 0.1 beta";
        Size = new(1280, 900);
        MinimumSize = new(1050, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new("Segoe UI", 10F);
        BackColor = background;
        ForeColor = Color.FromArgb(235, 241, 250);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(26, 20, 26, 18), ColumnCount = 1, RowCount = 8 };
        foreach (var height in new[] { 66F, 88F, 48F }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        foreach (var height in new[] { 46F, 47F, 45F, 52F }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        var title = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        title.Controls.Add(new Label { Text = "CorreoDiario", Font = new("Segoe UI Semibold", 25), AutoSize = true });
        title.Controls.Add(new Label { Text = "Tus correos del día, guardados con sus adjuntos.", AutoSize = true, ForeColor = Color.FromArgb(156, 174, 197) });
        layout.Controls.Add(title, 0, 0);
        account.Items.AddRange(["Personal · Outlook / Microsoft 365", "Trabajo · Outlook web", "Outlook del equipo (clásico)"]);
        account.SelectedIndex = 0;
        url.Text = "https://outlook.cloud.microsoft/mail/";
        var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new(0, 10, 0, 0) };
        url.Width = 330;
        connection.Controls.AddRange([account, url, login, loadFolders, logout]);
        connection.Controls.Add(new Label { Text = "En Trabajo podrás indicar la dirección cuando la tengas. La compatibilidad se comprueba al conectar.", AutoSize = true, ForeColor = Color.FromArgb(156, 174, 197), Margin = new(3, 9, 0, 0) });
        layout.Controls.Add(connection, 0, 1);
        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(0, 4, 0, 4) };
        var today = new Button { Text = "Hoy", AutoSize = true };
        var all = new Button { Text = "Marcar todos", AutoSize = true };
        var none = new Button { Text = "Desmarcar todos", AutoSize = true };
        filter.Controls.AddRange([new Label { Text = "Fecha", AutoSize = true, Margin = new(0, 7, 8, 0) }, date, today, list, all, none]);
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
        messages.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Hora", FillWeight = 16, ReadOnly = true });
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
        login.BackColor = Color.FromArgb(37, 82, 128);
        account.SelectedIndexChanged += (_, _) =>
        {
            url.Text = account.SelectedIndex == 0 ? "https://outlook.cloud.microsoft/mail/" : "";
            (source as IDisposable)?.Dispose(); source = null; session?.Dispose(); session = null;
            folders.Items.Clear(); messages.Rows.Clear(); list.Enabled = loadFolders.Enabled = export.Enabled = false;
            delete.Checked = false;
            status.Text = account.SelectedIndex switch { 0 => "Abre tu Outlook personal e inicia sesión.", 1 => "Introduce la dirección de acceso del Outlook del trabajo cuando la tengas.", _ => "Usa las cuentas ya configuradas en el Outlook clásico de este equipo." };
            login.Text = account.SelectedIndex == 2 ? "Conectar Outlook" : "Abrir Outlook";
            logout.Text = account.SelectedIndex == 2 ? "Desconectar" : "Cerrar sesión";
            RefreshButtons();
        };
        login.Click += async (_, _) => await SafeAsync(async ct =>
        {
            if (account.SelectedIndex == 2)
            {
                (source as IDisposable)?.Dispose(); source = new OutlookClassicSource();
                messages.Rows.Clear(); folders.Items.Clear();
                status.Text = "Conectado al Outlook clásico del equipo. Pulsa «Cargar carpetas».";
                return;
            }
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var address) || address.Scheme != "https" || address.UserInfo.Length > 0)
                throw new InvalidOperationException("Introduce una dirección HTTPS válida de Outlook, sin usuario ni contraseña en el enlace.");
            source = null; messages.Rows.Clear(); folders.Items.Clear();
            session?.Dispose();
            session = new(address, account.SelectedIndex == 0 ? "personal" : "trabajo");
            session.Show(this);
            await session.Ready.WaitAsync(ct);
            status.Text = "Inicia sesión en la ventana de Outlook y después pulsa «Cargar carpetas».";
        });
        loadFolders.Click += async (_, _) => await SafeAsync(async ct =>
        {
            if (account.SelectedIndex != 2) source = new OwaSource(session ?? throw new InvalidOperationException("Abre Outlook primero."));
            if (source is null) throw new InvalidOperationException("Conecta a Outlook primero.");
            var result = await source.FoldersAsync(ct);
            folders.Items.Clear(); messages.Rows.Clear();
            foreach (var folder in result) folders.Items.Add(new FolderLabel(folder), result.IndexOfReference(folder) < 2);
            var notice = source is OwaSource web ? web.OtherFoldersNotice : (source as OutlookClassicSource)?.Notice;
            status.Text = notice ?? $"Conexión preparada. {result.Count} carpetas disponibles.";
        });
        logout.Click += async (_, _) => await SafeAsync(async _ =>
        {
            if (session is not null) { await session.ClearSessionAsync(); session.Dispose(); session = null; }
            (source as IDisposable)?.Dispose(); source = null; folders.Items.Clear(); messages.Rows.Clear();
            status.Text = "Sesión cerrada. Puedes abrir Outlook para entrar con otra cuenta.";
        });
        list.Click += async (_, _) => await SafeAsync(async ct =>
        {
            var selected = folders.CheckedItems.Cast<FolderLabel>().Select(x => x.Folder).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Marca al menos una carpeta.");
            var result = await source!.ListAsync(selected, DateOnly.FromDateTime(date.Value), TimeZoneInfo.Local, ct);
            messages.Rows.Clear();
            foreach (var mail in result)
            {
                var row = messages.Rows[messages.Rows.Add(true, TimeZoneInfo.ConvertTime(mail.Timestamp, TimeZoneInfo.Local).ToString("HH:mm"), mail.Sent ? "ENVIADO" : "RECIBIDO", mail.Sent ? mail.To : mail.From, mail.Subject)];
                row.Tag = mail;
            }
            status.Text = $"{result.Count} correos encontrados para el {date.Value:dd/MM/yyyy}. Los adjuntos se descargarán y comprobarán al guardar.";
        });
        today.Click += (_, _) => date.Value = DateTime.Today;
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
            else { session?.Dispose(); renderer?.Dispose(); (source as IDisposable)?.Dispose(); }
        };
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
        account.Enabled = login.Enabled = choose.Enabled = date.Enabled = folders.Enabled = messages.Enabled = delete.Enabled = !busy;
        url.Enabled = !busy && account.SelectedIndex != 2;
        var connected = session is not null && session.Ready.IsCompletedSuccessfully || source is OutlookClassicSource;
        loadFolders.Enabled = !busy && connected;
        logout.Enabled = !busy && connected;
        list.Enabled = !busy && source is not null && folders.Items.Count > 0;
        export.Enabled = !busy && source is not null && messages.Rows.Count > 0 && destination.Text.Length > 0;
        cancel.Enabled = busy;
    }
    private void PaintControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = Color.FromArgb(46, 64, 87); b.ForeColor = ForeColor; b.Padding = new(10, 4, 10, 4); b.Cursor = Cursors.Hand; }
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
