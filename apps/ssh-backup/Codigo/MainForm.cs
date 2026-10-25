using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SSHBackup
{
    public sealed class FlatProgress : Control
    {
        int value;
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public FlatProgress() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Height = 7; BackColor = Ui.ProgressTrack; }
        protected override void OnPaint(PaintEventArgs e)
        { e.Graphics.Clear(BackColor); using (var brush = new SolidBrush(Ui.Blue)) e.Graphics.FillRectangle(brush, 0, 0, (int)(Width * value / 100.0), Height); }
    }

    public sealed partial class MainForm : ThemedForm
    {
        readonly ConfigStore store;
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        readonly List<Control> editingControls = new List<Control>();
        readonly DataGridView jobsGrid = Ui.Grid(), serverGrid = Ui.Grid(), historyGrid = Ui.Grid();
        readonly RichTextBox detail = new RichTextBox(), logBox = new RichTextBox();
        readonly List<LogRecord> visibleLogs = new List<LogRecord>();
        readonly FlatProgress overallProgress = new FlatProgress(), selectedProgress = new FlatProgress();
        readonly Label counts = Ui.Label(""), runStatus = Ui.Label(""), destinationLabel = Ui.Label(""), historyDetail = Ui.Label("");
        readonly CheckBox errorsOnly = new ModernCheckBox { Text = "Solo errores y avisos", AutoSize = true, ForeColor = Ui.Muted };
        readonly CheckBox darkMode = new ModernCheckBox { Toggle = true, Name = "darkMode", Text = "Modo oscuro", Dock = DockStyle.Bottom, Height = 45 };
        readonly Button start = Ui.Button("Iniciar copia", true, 145), pause = Ui.Button("Pausar cola", false, 125),
            stop = Ui.Button("Detener", false, 105), retry = Ui.Button("Reintentar fallidos", false, 160);
        readonly Button exportServers = Ui.Button("Exportar listado", false, 145);
        readonly TextBox settingsDestination = Ui.Text(), settingsExclusions = Ui.Text();
        readonly NumericUpDown settingParallel = new NumericUpDown(), settingRetries = new NumericUpDown(), settingTimeout = new NumericUpDown();
        readonly CheckBox settingBandwidthEnabled = new ModernCheckBox { Toggle = true, Text = "Limitar la descarga total de la aplicación", AutoSize = true };
        readonly NumericUpDown settingBandwidthValue = new NumericUpDown { Minimum = 0.1m, Maximum = 100000, DecimalPlaces = 2, Increment = 0.5m, Width = 150 };
        readonly ComboBox settingBandwidthUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        readonly Label bandwidthEquivalent = Ui.Label("", 9, Ui.Muted);
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer { Interval = 250 };
        BackupRunner runner;
        System.Threading.Tasks.Task currentTask;
        bool loadingServers, closing, closingAfterRun, changingTheme;
        string selectedPage = "copies";

        public MainForm(ConfigStore config)
        {
            store = config;
            Ui.SetDarkMode(store.Settings.DarkMode);
            Text = "SSH Backup " + Program.Version; Font = new Font("Segoe UI", 10);
            BackColor = Ui.Background; ForeColor = Ui.Ink;
            StartPosition = FormStartPosition.CenterScreen; Size = new Size(1320, 930); MinimumSize = new Size(1120, 790);
            AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true; KeyPreview = true;
            Icon = Program.AppIcon;
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Background };
            foreach (string name in new[] { "copies", "servers", "history", "settings", "tools" })
            {
                var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), Visible = false };
                pages.Add(name, panel); body.Controls.Add(panel);
            }
            var nav = new Panel { Name = "sidebar", Dock = DockStyle.Left, Width = 184, Padding = new Padding(12), BackColor = Ui.Surface };
            nav.Paint += (s, e) => { using (var pen = new Pen(Ui.Border)) e.Graphics.DrawLine(pen, nav.Width - 1, 0, nav.Width - 1, nav.Height); };
            var brand = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Ui.Surface };
            var brandIcon = new Panel { Dock = DockStyle.Left, Width = 38, BackColor = Ui.Surface };
            brandIcon.Paint += (s, e) => Ui.DrawIcon(e.Graphics, "logo", new Rectangle(3, 17, 27, 27), Ui.Blue);
            var nameLabel = Ui.Label("SSH Backup", 13); nameLabel.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            nameLabel.Dock = DockStyle.Fill; brand.Controls.Add(nameLabel); brand.Controls.Add(brandIcon);
            var navItems = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 354, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (var item in new[] { new[] { "copies", "Copias" }, new[] { "servers", "Servidores" }, new[] { "history", "Historial" }, new[] { "settings", "Ajustes" }, new[] { "tools", "Herramientas" } })
            {
                string key = item[0]; var button = (ModernButton)Ui.Button(item[1], false, 160);
                button.IconKey = key; button.Height = 48; button.Margin = new Padding(0, 0, 0, 8);
                button.FlatAppearance.BorderSize = 0;
                button.Click += (s, e) => ShowPage(key); navigation[key] = button; navItems.Controls.Add(button);
            }
            var help = (ModernButton)Ui.Button("Ayuda", false, 160); help.IconKey = "help"; help.Height = 48;
            help.Click += (s, e) => new HelpDialog(Program.ResourceText("GUIA.txt")).ShowDialog(this); navItems.Controls.Add(help);
            darkMode.Checked = store.Settings.DarkMode;
            darkMode.CheckedChanged += (s, e) => ChangeTheme();
            var version = Ui.Label("Versión " + Program.Version, 9, Ui.Muted); version.Dock = DockStyle.Bottom; version.Height = 30;
            nav.Controls.Add(navItems); nav.Controls.Add(brand); nav.Controls.Add(darkMode); nav.Controls.Add(version);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(18, 0, 18, 0) };
            runStatus.ForeColor = Ui.Muted; runStatus.Font = new Font("Segoe UI", 9); footer.Controls.Add(runStatus);
            Controls.Add(body); Controls.Add(nav); Controls.Add(footer);
            BuildCopies(); BuildServers(); BuildHistory(); BuildSettings(); BuildTools();
            ShowPage("copies"); RefreshServers(); RefreshHistory(); FillSettings();
            RefreshJobs(); RefreshButtons();
            Ui.ApplyTheme(this); RefreshNavigation();
            Load += (s, e) => RefreshNavigation();
            refreshTimer.Tick += (s, e) => { DrainLogs(); RefreshJobs(); RefreshButtons(); CheckSchedule(); }; refreshTimer.Start();
            FormClosing += OnClosing;
            KeyDown += (s, e) => {
                if (e.Control && e.KeyCode == Keys.O) { ImportServers(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.N) { EditServer(null); e.Handled = true; }
                if (e.KeyCode == Keys.F5 && !Busy) { StartCopies(false, false); e.Handled = true; }
            };
            Shown += (s, e) => {
                if (store.RecoveryNotice.Length > 0) MessageBox.Show(this, store.RecoveryNotice, "Configuración recuperada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }
        bool Busy { get { return currentTask != null && !currentTask.IsCompleted; } }
        void ShowPage(string key)
        {
            selectedPage = key;
            foreach (var page in pages) page.Value.Visible = page.Key == key;
            pages[key].BringToFront();
            RefreshNavigation();
            if (key == "history") RefreshHistory();
            if (key == "settings" && !Busy) FillSettings();
        }
        void RefreshNavigation()
        {
            foreach (var button in navigation)
            { ((ModernButton)button.Value).Selected = button.Key == selectedPage; button.Value.BackColor = Ui.Surface;
              button.Value.ForeColor = button.Key == selectedPage ? Ui.Blue : Ui.Muted; button.Value.Invalidate(); }
        }
        void ChangeTheme()
        {
            if (changingTheme || darkMode.Checked == store.Settings.DarkMode) return;
            bool previous = store.Settings.DarkMode;
            store.Settings.DarkMode = darkMode.Checked;
            if (!TrySave())
            {
                store.Settings.DarkMode = previous;
                changingTheme = true; darkMode.Checked = previous; changingTheme = false;
                return;
            }
            Ui.SetDarkMode(store.Settings.DarkMode);
            Ui.ApplyTheme(this); RefreshNavigation(); RedrawLog();
        }
        void BuildCopies()
        {
            var page = pages["copies"];
            var heading = new Panel { Dock = DockStyle.Top, Height = 93 };
            var title = Ui.Label("Copias de seguridad", 22); title.Dock = DockStyle.Top; title.Height = 39;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 43, WrapContents = false };
            actions.Controls.AddRange(new Control[] { start, pause, stop, retry, onlyChanges });
            start.Click += (s, e) => StartCopies(false, false);
            pause.Click += (s, e) => { if (runner != null) { runner.Pause(!runner.IsPaused); pause.Text = runner.IsPaused ? "Continuar cola" : "Pausar cola"; } };
            stop.Click += (s, e) => { if (runner != null) { runner.Cancel(); stop.Enabled = false; runStatus.Text = "Deteniendo conexiones. La descarga se conservará para reanudar…"; } };
            retry.Click += (s, e) => StartCopies(false, true);
            heading.Controls.Add(actions); heading.Controls.Add(title);
            var destination = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(12, 7, 5, 7), BackColor = Ui.Surface };
            var browse = Ui.Button("Elegir destino", false, 145); browse.Dock = DockStyle.Right; editingControls.Add(browse);
            destinationLabel.AutoEllipsis = true; destination.Controls.Add(destinationLabel); destination.Controls.Add(browse);
            browse.Click += (s, e) => ChooseGlobalDestination();
            var progressPanel = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(1, 4, 1, 10) };
            counts.Dock = DockStyle.Fill; counts.Font = new Font("Segoe UI", 10);
            overallProgress.Dock = DockStyle.Bottom; overallProgress.Height = 7;
            progressPanel.Controls.Add(counts); progressPanel.Controls.Add(overallProgress);
            Ui.Column(jobsGrid, "alias", "Alias", 110, 100); Ui.Column(jobsGrid, "host", "Servidor", 115, 100);
            Ui.Column(jobsGrid, "state", "Estado", 120, 110); Ui.Column(jobsGrid, "progress", "Avance", 60, 65);
            Ui.Column(jobsGrid, "files", "Archivos", 65, 65);
            jobsGrid.SelectionChanged += (s, e) => RefreshDetail();
            detail.Dock = DockStyle.Fill; detail.ReadOnly = true; detail.BorderStyle = BorderStyle.None;
            detail.BackColor = Ui.Surface; detail.ForeColor = Ui.Ink; detail.Font = new Font("Segoe UI", 10);
            detail.DetectUrls = false; detail.WordWrap = true;
            var detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 7, 14, 14), BackColor = Ui.Surface };
            selectedProgress.Dock = DockStyle.Bottom; selectedProgress.Height = 7;
            detailPanel.Controls.Add(detail); detailPanel.Controls.Add(selectedProgress);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Size = new Size(1230, 430),
                SplitterWidth = 12, Panel1MinSize = 560, Panel2MinSize = 300, SplitterDistance = 800, BackColor = Ui.Background };
            split.Panel1.Controls.Add(Ui.Section("Cola de servidores", jobsGrid)); split.Panel2.Controls.Add(Ui.Section("Visor de actividad", detailPanel));
            logBox.Dock = DockStyle.Fill; logBox.ReadOnly = true; logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Ui.LogBackground; logBox.Tag = Ui.LogTag; logBox.ForeColor = Ui.Ink; logBox.Font = new Font("Consolas", 9);
            logBox.DetectUrls = false; logBox.WordWrap = true;
            var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 7), BackColor = Ui.Surface };
            var filter = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 31, Padding = new Padding(2, 3, 0, 0), BackColor = Ui.Surface };
            errorsOnly.CheckedChanged += (s, e) => RedrawLog(); filter.Controls.Add(errorsOnly);
            var openLog = new LinkLabel { Text = "Abrir registro completo", AutoSize = true, LinkColor = Ui.Blue, Margin = new Padding(20, 3, 0, 0) };
            openLog.LinkClicked += (s, e) => { if (runner != null) OpenPath(runner.Journal.LogPath); };
            filter.Controls.Add(openLog); logPanel.Controls.Add(logBox); logPanel.Controls.Add(filter);
            var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(1230, 650),
                SplitterWidth = 12, Panel1MinSize = 220, Panel2MinSize = 145, SplitterDistance = 440, BackColor = Ui.Background };
            outer.Panel1.Controls.Add(split); outer.Panel2.Controls.Add(Ui.Section("Registro de la ejecución", logPanel));
            page.Controls.Add(outer); page.Controls.Add(progressPanel); page.Controls.Add(destination); page.Controls.Add(heading);
        }
        void BuildServers()
        {
            var page = pages["servers"];
            var heading = Ui.Label("Servidores", 22); heading.Dock = DockStyle.Top; heading.Height = 49;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 0, 0, 12), WrapContents = true };
            var add = Ui.Button("Añadir", true, 100); var edit = Ui.Button("Editar", false, 95);
            var remove = Ui.Button("Eliminar", false, 100); var import = Ui.Button("Importar listado", false, 145);
            var template = Ui.Button("Plantilla Excel / CSV", false, 175); var access = Ui.Button("Aplicar acceso", false, 145);
            var check = Ui.Button("Comprobar SSH", false, 145);
            add.Click += (s, e) => EditServer(null); edit.Click += (s, e) => EditSelectedServer();
            remove.Click += (s, e) => DeleteServers(); import.Click += (s, e) => ImportServers();
            exportServers.Click += (s, e) => ExportServers();
            template.Click += (s, e) => SaveTemplate(); access.Click += (s, e) => SetAccess();
            check.Click += (s, e) => StartCopies(true, false);
            actions.Controls.AddRange(new Control[] { add, edit, remove, import, exportServers, template, access, check });
            editingControls.AddRange(new Control[] { add, edit, remove, import, access, check, serverGrid });
            var enabled = new DataGridViewCheckBoxColumn { Name = "enabled", HeaderText = "Copiar", FillWeight = 50, MinimumWidth = 50,
                ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable };
            serverGrid.Columns.Add(enabled);
            Ui.Column(serverGrid, "alias", "Alias", 110, 100); Ui.Column(serverGrid, "host", "Servidor / puerto", 125, 125);
            Ui.Column(serverGrid, "user", "Usuario", 70, 70); Ui.Column(serverGrid, "source", "Carpeta de origen", 135, 130);
            Ui.Column(serverGrid, "destination", "Carpeta de las copias", 175, 155); Ui.Column(serverGrid, "auth", "Acceso", 90, 95);
            serverGrid.CurrentCellDirtyStateChanged += (s, e) => { if (serverGrid.IsCurrentCellDirty) serverGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            serverGrid.CellValueChanged += (s, e) => {
                if (loadingServers || e.RowIndex < 0 || e.ColumnIndex != 0 || Busy) return;
                var server = serverGrid.Rows[e.RowIndex].Tag as ServerConfig;
                if (server != null) { server.Enabled = Convert.ToBoolean(serverGrid.Rows[e.RowIndex].Cells[0].Value); TrySave(); RefreshButtons(); }
            };
            serverGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex > 0) EditSelectedServer(); };
            var note = Ui.Label("Marca «Copiar» para incluir un servidor en el lote. Selecciona filas para editar su acceso o comprobar la conexión.", 9, Ui.Muted);
            note.Dock = DockStyle.Bottom; note.Height = 39;
            page.Controls.Add(Ui.Section("Alias y destino de cada servidor", serverGrid)); page.Controls.Add(note);
            page.Controls.Add(actions); page.Controls.Add(heading);
        }
        void BuildHistory()
        {
            var page = pages["history"];
            var heading = Ui.Label("Historial de operaciones", 22); heading.Dock = DockStyle.Top; heading.Height = 49;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 49, WrapContents = false };
            var folder = Ui.Button("Abrir carpeta del ZIP", false, 180); var archive = Ui.Button("Abrir ZIP", false, 115);
            var log = Ui.Button("Abrir registro", false, 145); var refresh = Ui.Button("Actualizar", false, 110);
            folder.Click += (s, e) => { var h = SelectedHistory(); if (h != null && !string.IsNullOrEmpty(h.Archive)) OpenPath(System.IO.Path.GetDirectoryName(h.Archive)); };
            archive.Click += (s, e) => { var h = SelectedHistory(); if (h != null && !string.IsNullOrEmpty(h.Archive)) OpenPath(h.Archive); };
            log.Click += (s, e) => { var h = SelectedHistory(); if (h != null) OpenPath(h.LogFile); };
            refresh.Click += (s, e) => RefreshHistory();
            actions.Controls.AddRange(new Control[] { folder, archive, log, refresh });
            Ui.Column(historyGrid, "time", "Fecha y hora", 115, 135); Ui.Column(historyGrid, "type", "Operación", 100, 100);
            Ui.Column(historyGrid, "alias", "Alias", 110, 100); Ui.Column(historyGrid, "result", "Resultado", 110, 115);
            Ui.Column(historyGrid, "files", "Archivos", 55, 60); Ui.Column(historyGrid, "size", "Datos", 70, 75);
            Ui.Column(historyGrid, "duration", "Tiempo", 60, 70); Ui.Column(historyGrid, "path", "ZIP guardado", 180, 160);
            historyGrid.SelectionChanged += (s, e) => {
                var h = SelectedHistory();
                historyDetail.Text = h == null ? "" : (h.Archive.Length > 0 ? h.Archive + "\r\n" : "") +
                    (h.Error.Length > 0 ? h.Error : h.Operation + " · " + h.Status + " · " + h.Warnings + " avisos");
            };
            historyDetail.Dock = DockStyle.Bottom; historyDetail.Height = 78; historyDetail.ForeColor = Ui.Muted;
            page.Controls.Add(Ui.Section("Resultados guardados", historyGrid)); page.Controls.Add(historyDetail);
            page.Controls.Add(actions); page.Controls.Add(heading);
        }
        void BuildSettings()
        {
            var page = pages["settings"];
            var title = Ui.Label("Ajustes de copia", 22); title.Dock = DockStyle.Top; title.Height = 53;
            var table = Ui.FormTable(); table.Padding = new Padding(18); table.ColumnStyles[0].Width = 230;
            settingParallel.Minimum = 1; settingParallel.Maximum = 10;
            settingRetries.Minimum = 0; settingRetries.Maximum = 3;
            settingTimeout.Minimum = 5; settingTimeout.Maximum = 300;
            settingsExclusions.Multiline = true; settingsExclusions.ScrollBars = ScrollBars.Vertical;
            Ui.Field(table, "Destino general en el PC", Ui.Browse(settingsDestination, true), 46);
            Ui.Field(table, "Conexiones simultáneas", settingParallel, 45);
            Ui.Field(table, "Reintentos por servidor", settingRetries, 45);
            Ui.Field(table, "Espera de conexión / operación (s)", settingTimeout, 45);
            settingBandwidthUnit.Items.AddRange(new object[] { "Mbps", "MB/s" }); settingBandwidthUnit.SelectedIndex = 0;
            var bandwidth = new FlowLayoutPanel { WrapContents = false };
            var speedInput = Ui.Input(settingBandwidthValue); speedInput.Dock = DockStyle.None; speedInput.Size = new Size(150, 35);
            var unitInput = Ui.Input(settingBandwidthUnit); unitInput.Dock = DockStyle.None; unitInput.Size = new Size(110, 35);
            bandwidthEquivalent.Dock = DockStyle.None; bandwidthEquivalent.Size = new Size(140, 35);
            bandwidth.Controls.AddRange(new Control[] { speedInput, unitInput, bandwidthEquivalent });
            Action refreshBandwidth = () => {
                settingBandwidthValue.Enabled = settingBandwidthUnit.Enabled = settingBandwidthEnabled.Checked;
                bandwidthEquivalent.Text = "  ≈ " + (BandwidthLimiter.Rate(settingBandwidthValue.Value, settingBandwidthUnit.Text) / 1000000m).ToString("0.###") + " MB/s";
            };
            settingBandwidthEnabled.CheckedChanged += (s, e) => refreshBandwidth();
            settingBandwidthValue.ValueChanged += (s, e) => refreshBandwidth(); settingBandwidthUnit.SelectedIndexChanged += (s, e) => refreshBandwidth();
            Ui.Field(table, "Límite de red (opcional)", settingBandwidthEnabled, 42);
            Ui.Field(table, "Velocidad máxima total", bandwidth, 46);
            Ui.Field(table, "", Ui.Label("Compartido entre todas las conexiones SSH. 8 Mbps = 1 MB/s.\r\nSe aplica al guardar para las siguientes copias, incluidas las programadas. Puede haber pequeñas ráfagas por los búferes de red.", 9, Ui.Muted), 65);
            Ui.Field(table, "Excluir archivos o carpetas", settingsExclusions, 128);
            Ui.Field(table, "", Ui.Label("Un patrón por línea. Ejemplos: *.tmp o cache/*.\r\nLas exclusiones son relativas a la carpeta de origen y quedan anotadas en el ZIP.", 9, Ui.Muted), 63);
            Ui.Field(table, "Configuración y credenciales", Ui.Label("Se guardan para tu usuario de Windows. Las contraseñas se cifran con la protección de Windows.", 9, Ui.Muted), 53);
            Ui.Field(table, "Verificación", Ui.Label("Cada archivo se comprueba con SHA-256. El ZIP completo se vuelve a leer antes de dar la copia por terminada.", 9, Ui.Muted), 53);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(0, 10, 0, 0) };
            var save = Ui.Button("Guardar ajustes", true, 155); save.Click += (s, e) => SaveSettings();
            var config = Ui.Button("Abrir configuración", false, 175); config.Click += (s, e) => OpenPath(store.DirectoryPath);
            var licenses = Ui.Button("Licencias", false, 120); licenses.Click += (s, e) => new HelpDialog(Program.ResourceText("LICENCIAS.txt"), "Licencias de componentes").ShowDialog(this);
            buttons.Controls.AddRange(new Control[] { save, config, licenses });
            editingControls.AddRange(new Control[] { table, save });
            page.Controls.Add(Ui.ScrollForm(table)); page.Controls.Add(buttons); page.Controls.Add(title);
        }
        void RefreshServers()
        {
            string selected = serverGrid.CurrentRow == null ? "" : (serverGrid.CurrentRow.Tag as ServerConfig)?.Id;
            loadingServers = true; serverGrid.Rows.Clear();
            foreach (var server in store.Settings.Servers)
            {
                string access = server.Auth == "key" ? "Clave SSH" : !server.PasswordLoaded ? "Revisar contraseña" :
                    string.IsNullOrEmpty(server.Password) ? "Sin contraseña" : "Contraseña guardada";
                int index = serverGrid.Rows.Add(server.Enabled, server.Alias, server.Host + ":" + server.Port, server.User,
                    server.Source, server.Folder(store.Settings), access);
                serverGrid.Rows[index].Tag = server;
                if (server.Id == selected) serverGrid.CurrentCell = serverGrid.Rows[index].Cells[1];
            }
            loadingServers = false;
            destinationLabel.Text = "Destino general: " + store.Settings.Destination + "     ·     " + store.Settings.Parallel + " conexiones simultáneas" +
                (store.Settings.BandwidthEnabled ? " · límite total " + store.Settings.BandwidthValue + " " + store.Settings.BandwidthUnit : " · sin límite de velocidad");
        }
        void RefreshJobs()
        {
            if (runner == null)
            {
                counts.Text = store.Settings.Servers.Count == 0 ? "Añade servidores o importa un Excel / CSV para empezar." :
                    store.Settings.Servers.Count(s => s.Enabled) + " servidores marcados para copiar.";
                runStatus.Text = "Listo · las copias se guardarán por alias y fecha";
                detail.Text = "Selecciona un servidor de la cola para ver su actividad.\r\n\r\n1. Conectar por SSH\r\n2. Revisar carpeta de origen\r\n3. Descargar y crear ZIP\r\n4. Verificar integridad\r\n5. Guardar copia y resultado";
                return;
            }
            var snapshots = runner.Jobs.Select(j => j.Snapshot()).ToList();
            if (jobsGrid.Rows.Count != snapshots.Count)
            {
                jobsGrid.Rows.Clear();
                foreach (var job in runner.Jobs)
                {
                    int index = jobsGrid.Rows.Add(); jobsGrid.Rows[index].Tag = job;
                }
            }
            for (int index = 0; index < snapshots.Count; index++)
            {
                var job = snapshots[index]; var cells = jobsGrid.Rows[index].Cells;
                cells["alias"].Value = job.Server.Alias; cells["host"].Value = job.Server.Host;
                cells["state"].Value = job.StateText; cells["progress"].Value = job.Progress + "%";
                cells["files"].Value = job.CompletedFiles + " / " + job.TotalFiles;
                jobsGrid.Rows[index].DefaultCellStyle.ForeColor = job.State == JobState.Error ? Ui.Red :
                    job.State == JobState.Success ? Ui.Green : Ui.Ink;
            }
            int success = snapshots.Count(j => j.State == JobState.Success), warnings = snapshots.Count(j => j.State == JobState.Warning),
                errors = snapshots.Count(j => j.State == JobState.Error), active = snapshots.Count(j => j.State == JobState.Running),
                pending = snapshots.Count(j => j.State == JobState.Pending), canceled = snapshots.Count(j => j.State == JobState.Canceled);
            counts.Text = success + (success == 1 ? " correcta     " : " correctas     ") + active + " en curso     " + errors + (errors == 1 ? " error     " : " errores     ") +
                warnings + " con avisos     " + pending + " en cola" + (canceled > 0 ? "     " + canceled + " detenidas" : "");
            overallProgress.Value = snapshots.Count == 0 ? 0 : (int)snapshots.Average(j =>
                j.State == JobState.Error || j.State == JobState.Canceled ? 100 : j.Progress);
            if (Busy) runStatus.Text = runner.IsPaused ? "Cola pausada · las copias en curso siguen hasta terminar" :
                "Ejecución " + runner.RunId + " · " + (runner.CheckOnly ? "comprobando acceso SSH" : "copias en marcha");
            RefreshDetail();
        }
        void RefreshDetail()
        {
            var job = jobsGrid.CurrentRow == null ? null : jobsGrid.CurrentRow.Tag as JobInfo;
            if (job == null) return;
            var s = job.Snapshot();
            var text = s.Server.Alias + "\r\n" + s.Server.User + "@" + s.Server.Host + ":" + s.Server.Port +
                "\r\n\r\nORIGEN\r\n" + s.Server.Source + "\r\n\r\nDESTINO\r\n" +
                (s.FinalPath.Length > 0 ? s.FinalPath : s.Server.Folder(store.Settings)) +
                "\r\n\r\n" + s.StateText + " · " + s.Progress + "%\r\n" + s.CompletedFiles + " / " + s.TotalFiles + " archivos · " +
                Validation.FormatBytes(s.CopiedBytes) + " / " + Validation.FormatBytes(s.TotalBytes) + "\r\n";
            text += "Descargado por SSH: " + Validation.FormatBytes(s.DownloadedBytes) +
                "\r\nSin cambios reutilizados: " + Validation.FormatBytes(s.ReusedBytes) +
                "\r\nRecuperado de descarga anterior: " + Validation.FormatBytes(s.ResumedBytes) + "\r\n";
            if (s.Attempt > 0) text += "Intento " + s.Attempt + "\r\n";
            if (s.CurrentFile.Length > 0) text += "\r\nArchivo / carpeta actual:\r\n" + s.CurrentFile + "\r\n";
            string[] steps = runner != null && runner.CheckOnly ? new[] { "Conectar por SSH", "Revisar carpeta" } :
                new[] { "Conectar por SSH", "Revisar carpeta", "Descargar y crear ZIP", "Verificar integridad", "Guardar resultado" };
            text += "\r\n";
            for (int index = 0; index < steps.Length; index++)
                text += (s.Stage > index + 1 || s.Stage == 5 ? "✓ " : s.Stage == index + 1 ? "› " : "  ") + steps[index] + "\r\n";
            if (s.Warnings > 0) text += "\r\nAvisos: " + s.Warnings + ". Consulta el registro y el manifiesto del ZIP.\r\n";
            if (s.Error.Length > 0) text += "\r\n" + s.Error;
            if (detail.Text != text) detail.Text = text;
            selectedProgress.Value = s.Progress;
        }
        void RefreshHistory()
        {
            historyGrid.Rows.Clear();
            foreach (var h in store.ReadHistory().Take(5000))
            {
                var seconds = Math.Max(0, (h.FinishedUtc - h.StartedUtc).TotalSeconds);
                int row = historyGrid.Rows.Add(h.StartedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), h.Operation, h.Alias, h.Status,
                    h.Files, Validation.FormatBytes(h.Bytes), TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"), h.Archive);
                historyGrid.Rows[row].Tag = h;
                if (h.Status == "Error") historyGrid.Rows[row].DefaultCellStyle.ForeColor = Ui.Red;
            }
        }
        HistoryEntry SelectedHistory() { return historyGrid.CurrentRow == null ? null : historyGrid.CurrentRow.Tag as HistoryEntry; }
        void RefreshButtons()
        {
            bool busy = Busy;
            start.Enabled = !busy && store.Settings.Servers.Any(s => s.Enabled);
            pause.Enabled = busy; stop.Enabled = busy;
            retry.Enabled = !busy && runner != null && runner.Jobs.Any(j => j.Snapshot().State == JobState.Error || j.Snapshot().State == JobState.Canceled);
            foreach (var control in editingControls) control.Enabled = !busy;
            exportServers.Enabled = store.Settings.Servers.Count > 0;
        }
    }
}
