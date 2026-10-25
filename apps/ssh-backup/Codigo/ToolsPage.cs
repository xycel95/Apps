using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SSHBackup
{
    public sealed partial class MainForm
    {
        readonly CheckBox onlyChanges = new ModernCheckBox { Text = "Copiar solo novedades", AutoSize = true, Margin = new Padding(12, 5, 0, 0) };
        readonly CheckBox scheduleEnabled = new ModernCheckBox { Toggle = true, Text = "Activar programación", AutoSize = true };
        readonly CheckBox[] scheduleDays = new CheckBox[7];
        readonly Button scheduleAllDays = Ui.Button("Todos los días", false, 150), scheduleNoDays = Ui.Button("Ninguno", false, 115);
        readonly Label scheduleSummary = Ui.Label("", 11, Ui.Blue);
        readonly TextBox scheduleTime = Ui.Text("20:00");
        readonly Label scheduleStatus = Ui.Label("", 10, Ui.Muted), catalogStatus = Ui.Label("Carga el catálogo para buscar o comparar las copias guardadas.", 10, Ui.Muted);
        readonly ComboBox compareBefore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, compareAfter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox copyQuery = Ui.Text(), updateUrl = Ui.Text();
        readonly DataGridView catalogGrid = Ui.Grid();
        readonly RichTextBox updateNotes = new RichTextBox { ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false };
        readonly Button installUpdate = Ui.Button("Instalar actualización", true, 200);
        CancellationTokenSource toolsCancellation;
        readonly List<BackupCatalog> catalogs = new List<BackupCatalog>();
        readonly List<string> addedCatalogs = new List<string>();
        EventWaitHandle scheduleSignal;
        bool catalogBusy, scheduleChecking, fillingOptions;
        string pendingPackage;
        UpdateRelease pendingRelease;

        void BuildTools()
        {
            var tabs = new ModernTabs { Name = "toolsTabs" };
            var explore = new Panel(); var scheduling = new Panel(); var updates = new Panel();
            tabs.AddPage("Buscar y comparar", explore, 185); tabs.AddPage("Programación", scheduling, 160); tabs.AddPage("Actualizaciones", updates, 170);
            var heading = new Panel { Dock = DockStyle.Top, Height = 98 };
            var title = Ui.Label("Buscar y comparar", 25); title.Dock = DockStyle.Top; title.Height = 48;
            var subtitle = Ui.Label("Encuentra archivos y revisa los cambios entre tus copias.", 10, Ui.Muted); subtitle.Dock = DockStyle.Top; subtitle.Height = 32;
            heading.Controls.Add(subtitle); heading.Controls.Add(title);
            tabs.SelectedIndexChanged += (s, e) => {
                title.Text = new[] { "Buscar y comparar", "Programación", "Actualizaciones" }[tabs.SelectedIndex];
                subtitle.Text = new[] { "Encuentra archivos y revisa los cambios entre tus copias.", "Elige cuándo hacer tus copias de seguridad.", "Mantén SSH Backup al día con paquetes firmados." }[tabs.SelectedIndex];
            };
            pages["tools"].Controls.Add(tabs); pages["tools"].Controls.Add(heading);
            BuildExplorer(explore); BuildScheduling(scheduling); BuildUpdates(updates);
            onlyChanges.Checked = store.Settings.OnlyChanges;
            onlyChanges.CheckedChanged += (s, e) => {
                if (fillingOptions || Busy) return;
                bool old = store.Settings.OnlyChanges; store.Settings.OnlyChanges = onlyChanges.Checked;
                if (!TrySave()) { store.Settings.OnlyChanges = old; fillingOptions = true; onlyChanges.Checked = old; fillingOptions = false; }
            };
            editingControls.Add(onlyChanges);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                scheduleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Scheduling.SignalName);
            FillSchedule(); updateUrl.Text = store.Settings.UpdateManifestUrl;
        }
        void BuildExplorer(Control page)
        {
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false };
            Button load = Ui.Button("Actualizar catálogo", true, 180), add = Ui.Button("Añadir ZIP…", false, 145), cancel = Ui.Button("Cancelar búsqueda", false, 180);
            load.Click += async (s, e) => await LoadCatalogs();
            add.Click += async (s, e) => {
                using (var file = new OpenFileDialog { Title = "Añadir copias anteriores al catálogo", Filter = "Copias SSH Backup|*.zip", Multiselect = true })
                    if (file.ShowDialog(this) == DialogResult.OK) { addedCatalogs.AddRange(file.FileNames); await LoadCatalogs(); }
            };
            cancel.Click += (s, e) => { if (toolsCancellation != null) toolsCancellation.Cancel(); };
            header.Controls.AddRange(new Control[] { load, add, cancel });
            var selectors = new TableLayoutPanel { Dock = DockStyle.Top, Height = 104, ColumnCount = 3, RowCount = 2 };
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            selectors.Controls.Add(Ui.Label("Copia anterior", 9, Ui.Muted), 0, 0); selectors.Controls.Add(Ui.Input(compareBefore), 1, 0);
            selectors.Controls.Add(Ui.Label("Copia posterior", 9, Ui.Muted), 0, 1); selectors.Controls.Add(Ui.Input(compareAfter), 1, 1);
            compareBefore.Dock = compareAfter.Dock = DockStyle.Fill;
            var compare = Ui.Button("Comparar", false, 150); selectors.Controls.Add(compare, 2, 1);
            compare.Click += async (s, e) => await CompareCatalogs();
            var search = new TableLayoutPanel { Dock = DockStyle.Top, Height = 55, ColumnCount = 3, RowCount = 1 };
            search.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); search.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            search.Controls.Add(Ui.Label("Nombre o ruta", 9, Ui.Muted), 0, 0); copyQuery.Dock = DockStyle.Fill; search.Controls.Add(Ui.Input(copyQuery), 1, 0);
            var find = Ui.Button("Buscar en todas", true, 150); search.Controls.Add(find, 2, 0); find.Click += async (s, e) => await SearchCatalogs();
            copyQuery.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchCatalogs(); } };
            Ui.Column(catalogGrid, "kind", "Resultado", 90, 105); Ui.Column(catalogGrid, "alias", "Servidor", 100, 105);
            Ui.Column(catalogGrid, "date", "Fecha de la copia", 115, 145); Ui.Column(catalogGrid, "relative", "Archivo / carpeta", 225, 180);
            Ui.Column(catalogGrid, "bytes", "Tamaño / cambio", 90, 140);
            catalogGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenCatalogResult(); };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 77 };
            var open = Ui.Button("Abrir ZIP seleccionado", false, 215); var openActions = new Panel { Dock = DockStyle.Right, Width = 222, Padding = new Padding(0, 15, 0, 15) };
            open.Dock = DockStyle.Fill; openActions.Controls.Add(open); open.Click += (s, e) => OpenCatalogResult();
            catalogStatus.Dock = DockStyle.Fill; catalogStatus.AutoEllipsis = true; footer.Controls.Add(catalogStatus); footer.Controls.Add(openActions);
            page.Controls.Add(catalogGrid); page.Controls.Add(footer); page.Controls.Add(search); page.Controls.Add(selectors); page.Controls.Add(header);
        }
        async Task CatalogOperation(Func<CancellationToken, Task> action)
        {
            if (catalogBusy) return;
            catalogBusy = true; toolsCancellation = new CancellationTokenSource();
            try { await action(toolsCancellation.Token); }
            catch (OperationCanceledException) { if (!IsDisposed) catalogStatus.Text = "Operación cancelada."; }
            catch (Exception e) { if (!IsDisposed) { catalogStatus.Text = e.Message; Ui.Error(this, e.Message); } }
            finally { catalogBusy = false; toolsCancellation.Dispose(); toolsCancellation = null; }
        }
        async Task LoadCatalogs()
        {
            await CatalogOperation(async token => {
                var paths = ArchiveCatalog.Discover(store).Concat(addedCatalogs).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                catalogStatus.Text = "Leyendo " + paths.Count + " copias…";
                var failures = new List<string>();
                var found = await Task.Run(() => {
                    var result = new List<BackupCatalog>();
                    foreach (var path in paths)
                    {
                        token.ThrowIfCancellationRequested();
                        try { result.Add(ArchiveCatalog.Read(path, token)); }
                        catch (Exception e) when (e is IOException || e is InvalidDataException || e is System.Xml.XmlException || e is InvalidOperationException || e is UnauthorizedAccessException)
                        { failures.Add(Path.GetFileName(path) + ": " + e.Message); }
                    }
                    return result.OrderByDescending(c => c.DateUtc).ToList();
                }, token);
                if (IsDisposed) return;
                catalogs.Clear(); catalogs.AddRange(found); compareBefore.Items.Clear(); compareAfter.Items.Clear();
                compareBefore.Items.AddRange(found.Cast<object>().ToArray()); compareAfter.Items.AddRange(found.Cast<object>().ToArray());
                if (found.Count > 0) { compareAfter.SelectedIndex = 0; compareBefore.SelectedIndex = found.Count > 1 ? 1 : 0; }
                catalogStatus.Text = found.Count + " copias disponibles · " + found.Sum(c => (long)c.Files.Count) + " archivos catalogados." +
                    (failures.Count == 0 ? "" : "\r\n" + failures.Count + " copias no se pudieron leer. " + failures[0]);
            });
        }
        async Task SearchCatalogs()
        {
            if (catalogs.Count == 0 && !catalogBusy) await LoadCatalogs();
            string query = copyQuery.Text.Trim(); var copies = catalogs.ToList();
            await CatalogOperation(async token => {
                var hits = await Task.Run(() => ArchiveCatalog.Search(copies, query, token), token);
                if (IsDisposed) return;
                catalogGrid.Rows.Clear();
                foreach (var hit in hits)
                {
                    int row = catalogGrid.Rows.Add("Encontrado", hit.Backup.Alias, hit.Backup.DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), hit.File.Relative, Validation.FormatBytes(hit.File.Size));
                    catalogGrid.Rows[row].Tag = hit.Backup.Path;
                }
                catalogStatus.Text = hits.Count + " coincidencias en " + copies.Count + " copias. Busca nombres y rutas; no el texto interior ni los ZIP anidados.";
            });
        }
        async Task CompareCatalogs()
        {
            var before = compareBefore.SelectedItem as BackupCatalog; var after = compareAfter.SelectedItem as BackupCatalog;
            if (before == null || after == null) { Ui.Error(this, "Carga el catálogo y elige dos copias."); return; }
            if (before.Path == after.Path) { Ui.Error(this, "Selecciona dos copias diferentes."); return; }
            if (before.Host != after.Host || before.Source != after.Source || before.Alias != after.Alias ||
                (before.Port != 0 && after.Port != 0 && (before.Port != after.Port || before.User != after.User)))
            { Ui.Error(this, "Selecciona dos copias del mismo servidor y carpeta de origen."); return; }
            if (before.DateUtc > after.DateUtc) { var swap = before; before = after; after = swap; }
            await CatalogOperation(async token => {
                var differences = await Task.Run(() => ArchiveCatalog.Compare(before, after, token), token);
                if (IsDisposed) return;
                if (differences.Count > 20000) throw new InvalidOperationException("Hay más de 20.000 cambios. Compara copias más próximas.");
                catalogGrid.Rows.Clear();
                foreach (var diff in differences)
                {
                    int row = catalogGrid.Rows.Add(diff.Change, after.Alias, after.DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), diff.Relative,
                        Validation.FormatBytes(diff.BeforeBytes) + " → " + Validation.FormatBytes(diff.AfterBytes));
                    catalogGrid.Rows[row].Tag = diff.Change.Contains("eliminad") || diff.Change == "Eliminado" ? before.Path : after.Path;
                }
                catalogStatus.Text = differences.Count + " cambios · " + before.DateUtc.ToLocalTime().ToString("g") + " → " + after.DateUtc.ToLocalTime().ToString("g") + ". Comparación por SHA-256.";
            });
        }
        void OpenCatalogResult()
        { var row = catalogGrid.CurrentRow; if (row != null) OpenPath(row.Tag as string); }
        void BuildScheduling(Control page)
        {
            var card = new ModernPanel { Dock = DockStyle.Fill, Padding = new Padding(18), Name = "scheduleCard" };
            var heading = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Ui.Surface };
            var title = Ui.Label("Copias programadas", 16); title.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            title.Dock = DockStyle.Top; title.Height = 34;
            var description = Ui.Label("Configura los días y la hora de tus copias.", 10, Ui.Muted); description.Dock = DockStyle.Top; description.Height = 30;
            var headingText = new Panel { Dock = DockStyle.Fill }; headingText.Controls.Add(description); headingText.Controls.Add(title);
            var activate = new Panel { Dock = DockStyle.Right, Width = 225, Padding = new Padding(6, 12, 0, 0) };
            activate.Controls.Add(scheduleEnabled); heading.Controls.Add(headingText); heading.Controls.Add(activate);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Surface };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, Height = 368, BackColor = Ui.Surface, Margin = new Padding(0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Action<Control, int> row = (control, height) => {
                int index = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                control.Dock = DockStyle.Fill; control.Margin = new Padding(0); table.Controls.Add(control, 0, index);
            };
            var dayHeading = Ui.Label("Días de la semana", 11); dayHeading.Font = new Font("Segoe UI", 11, FontStyle.Bold); row(dayHeading, 28);
            row(Ui.Label("Marca los días que quieras.", 9.5f, Ui.Muted), 22);
            var dayRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Margin = new Padding(0), BackColor = Ui.Surface };
            for (int index = 0; index < scheduleDays.Length; index++)
            {
                dayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
                var day = new ModernCheckBox { DayCard = true, Name = "scheduleDay" + index, Text = Scheduling.DayLabels[index],
                    AccessibleName = Scheduling.DayLabels[index], Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f),
                    Margin = new Padding(0, 0, index == 6 ? 0 : 7, 0) };
                day.CheckedChanged += (s, e) => RefreshScheduleSummary(); scheduleDays[index] = day; dayRow.Controls.Add(day, index, 0);
            }
            row(dayRow, 56);
            var shortcuts = new FlowLayoutPanel { WrapContents = false, Padding = new Padding(0, 10, 0, 0), BackColor = Ui.Surface };
            shortcuts.Controls.AddRange(new Control[] { scheduleAllDays, scheduleNoDays });
            scheduleAllDays.Click += (s, e) => SetScheduleDays(127);
            scheduleNoDays.Click += (s, e) => SetScheduleDays(0);
            row(shortcuts, 50);
            var timeRow = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, BackColor = Ui.Surface, Padding = new Padding(0, 3, 0, 8) };
            timeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            timeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); timeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); timeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            timeRow.Controls.Add(Ui.Label("Hora del ordenador", 10), 0, 0); timeRow.Controls.Add(Ui.Input(scheduleTime), 1, 0); row(timeRow, 56);
            var summary = new ModernPanel { Tinted = true, Padding = new Padding(18, 5, 18, 5), BackColor = Ui.Selection };
            scheduleSummary.BackColor = Ui.Selection; scheduleSummary.Font = new Font("Segoe UI", 11, FontStyle.Bold); summary.Controls.Add(scheduleSummary); row(summary, 52);
            var info = Ui.Label("El PC debe estar encendido y tu sesión de Windows iniciada.\r\nGuarda los accesos, comprueba SSH y mantén el programa en una ubicación fija.", 9.5f, Ui.Muted);
            info.Padding = new Padding(0, 8, 0, 4); row(info, 64);
            scheduleStatus.AutoEllipsis = true; row(scheduleStatus, 40);
            scroll.Controls.Add(table);
            var actions = new Panel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(0, 10, 0, 0), BackColor = Ui.Surface };
            var save = Ui.Button("Guardar programación", true, 225); save.Name = "saveSchedule"; save.Dock = DockStyle.Right; save.Click += (s, e) => SaveSchedule(); actions.Controls.Add(save);
            card.Controls.Add(scroll); card.Controls.Add(actions); card.Controls.Add(heading);
            scheduleTime.TextChanged += (s, e) => RefreshScheduleSummary();
            editingControls.Add(card); page.Controls.Add(card);
        }
        int PickedScheduleDays()
        { int mask = 0; for (int i = 0; i < scheduleDays.Length; i++) if (scheduleDays[i] != null && scheduleDays[i].Checked) mask |= 1 << i; return mask; }
        void SetScheduleDays(int mask)
        { for (int i = 0; i < scheduleDays.Length; i++) if (scheduleDays[i] != null) scheduleDays[i].Checked = (mask & (1 << i)) != 0; RefreshScheduleSummary(); }
        void RefreshScheduleSummary()
        {
            int days = PickedScheduleDays();
            if (days == 0) { scheduleSummary.Text = "Selecciona uno o varios días de la semana."; return; }
            try { scheduleSummary.Text = "Cada " + Scheduling.DaysText(days) + " a las " + Scheduling.Time(scheduleTime.Text.Trim()).ToString(@"hh\:mm") + ".";
                if (days == 127) scheduleSummary.Text = "Todos los días a las " + Scheduling.Time(scheduleTime.Text.Trim()).ToString(@"hh\:mm") + ".";
            }
            catch (ArgumentException) { scheduleSummary.Text = "Introduce una hora válida en formato HH:mm."; }
        }
        void FillSchedule()
        {
            scheduleEnabled.Checked = store.Settings.ScheduleEnabled; SetScheduleDays(Scheduling.SelectedDays(store.Settings));
            TimeSpan time; try { time = Scheduling.Time(store.Settings.ScheduleTime); } catch { time = new TimeSpan(20, 0, 0); }
            scheduleTime.Text = time.ToString(@"hh\:mm");
            scheduleStatus.Text = !store.Settings.ScheduleEnabled ? "Desactivada." : "Activa a las " + store.Settings.ScheduleTime +
                " · " + Scheduling.DaysText(Scheduling.SelectedDays(store.Settings)) + ".\r\n" + store.Settings.ScheduledExecutable;
            RefreshScheduleSummary();
        }
        void SaveSchedule()
        {
            if (Busy) return;
            string time;
            try { time = Scheduling.Time(scheduleTime.Text.Trim()).ToString(@"hh\:mm"); }
            catch (ArgumentException error) { Ui.Error(this, error.Message); return; }
            int mask = PickedScheduleDays();
            if (scheduleEnabled.Checked && mask == 0) { Ui.Error(this, "Selecciona al menos un día de la semana."); return; }
            if (scheduleEnabled.Checked)
            {
                var servers = store.Settings.Servers.Where(s => s.Enabled).ToList();
                if (servers.Count == 0) { Ui.Error(this, "Marca al menos un servidor para copiar."); return; }
                if (servers.Any(s => !s.PasswordLoaded || (s.Auth != "key" && string.IsNullOrEmpty(s.Password)) ||
                    (s.Auth == "key" && !File.Exists(s.KeyFile)) || string.IsNullOrEmpty(store.KnownFingerprint(s))))
                { Ui.Error(this, "Antes de programar, guarda los accesos y utiliza «Comprobar SSH» en todos los servidores marcados."); return; }
            }
            var old = store.Settings.ExecutionCopy();
            store.Settings.ScheduleEnabled = scheduleEnabled.Checked; store.Settings.ScheduleTime = time;
            store.Settings.ScheduleDaysMask = mask; store.Settings.ScheduleWeekdaysOnly = mask == 31;
            try
            {
                Scheduling.Apply(store.Settings, store.DirectoryPath, old.ScheduleEnabled);
                store.Save(); FillSchedule(); scheduleStatus.Text += "\r\nProgramación guardada en Windows.";
            }
            catch (Exception error)
            {
                store.Settings.ScheduleEnabled = old.ScheduleEnabled; store.Settings.ScheduleTime = old.ScheduleTime;
                store.Settings.ScheduleWeekdaysOnly = old.ScheduleWeekdaysOnly; store.Settings.ScheduledExecutable = old.ScheduledExecutable;
                store.Settings.ScheduleDaysMask = old.ScheduleDaysMask;
                try { Scheduling.Apply(store.Settings, store.DirectoryPath, scheduleEnabled.Checked); store.Save(); } catch { }
                FillSchedule(); Ui.Error(this, error.Message);
            }
        }
        void CheckSchedule()
        {
            if (scheduleChecking || Busy || closing || !store.Settings.ScheduleEnabled) return;
            if (scheduleSignal != null) scheduleSignal.WaitOne(0);
            try
            {
                if (!Scheduling.Due(store.Settings, DateTime.Now)) return;
                scheduleChecking = true;
                store.Settings.LastScheduleDate = DateTime.Now.ToString("yyyy-MM-dd");
                store.Save(); StartCopies(false, false, true);
            }
            catch (Exception error) { runStatus.Text = "No se pudo iniciar la copia programada: " + error.Message; }
            finally { scheduleChecking = false; }
        }
        void BuildUpdates(Control page)
        {
            var table = Ui.FormTable(); table.Dock = DockStyle.Top; table.Height = 220;
            Ui.Field(table, "Versión instalada", Ui.Label("SSH Backup " + Program.Version, 15), 45);
            Ui.Field(table, "Canal web (opcional)", updateUrl, 42);
            Ui.Field(table, "Distribución privada", Ui.Label("Puedes instalar un paquete .sshupdate recibido por correo o descargado de Drive.\r\nPara consultar versiones online, configura la URL HTTPS de un manifiesto firmado.", 10, Ui.Muted), 70);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, WrapContents = false };
            Button check = Ui.Button("Comprobar novedades", false, 210), load = Ui.Button("Abrir paquete…", false, 170), cancel = Ui.Button("Cancelar", false, 110);
            check.Click += async (s, e) => await CheckUpdates();
            load.Click += async (s, e) => await LoadUpdatePackage();
            cancel.Click += (s, e) => { if (toolsCancellation != null) toolsCancellation.Cancel(); };
            installUpdate.Enabled = false; installUpdate.Click += async (s, e) => await InstallUpdate();
            actions.Controls.AddRange(new Control[] { check, load, installUpdate, cancel });
            updateNotes.Dock = DockStyle.Fill; updateNotes.Font = new Font("Segoe UI", 11);
            updateNotes.Text = "Actualizador preparado.\r\n\r\nLos paquetes deben llevar una firma válida de SSH Backup. Se conservan los ajustes, accesos e historial y una copia del EXE anterior (.previous).\r\n\r\nNo hay un canal público configurado: la distribución actual es privada por correo/Drive.";
            page.Controls.Add(updateNotes); page.Controls.Add(actions); page.Controls.Add(table);
        }
        async Task CheckUpdates()
        {
            await CatalogOperation(async token => {
                string url = updateUrl.Text.Trim();
                if (url.Length == 0) throw new InvalidOperationException("No hay un canal web configurado. Abre un paquete .sshupdate recibido por correo o configura una URL HTTPS de versiones.");
                Updates.RequireHttps(url); store.Settings.UpdateManifestUrl = url; store.Save();
                var release = await Updates.CheckAsync(url, token);
                if (IsDisposed) return;
                pendingPackage = null; ShowRelease(release);
            });
        }
        async Task LoadUpdatePackage()
        {
            using (var file = new OpenFileDialog { Title = "Abrir actualización de SSH Backup", Filter = "Paquete firmado SSH Backup|*.sshupdate" })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                string path = file.FileName;
                await CatalogOperation(async token => {
                    var release = await Task.Run(() => Updates.ReadPackage(path), token);
                    if (IsDisposed) return;
                    pendingPackage = path; ShowRelease(release);
                });
            }
        }
        void ShowRelease(UpdateRelease release)
        {
            pendingRelease = release;
            bool newer = Version.Parse(release.Version) > Version.Parse(Program.Version);
            installUpdate.Enabled = newer;
            updateNotes.Text = "SSH Backup " + release.Version + "\r\n" + (newer ? "Nueva versión disponible. Firma verificada." : "Ya tienes esta versión o una posterior.") + "\r\n\r\n" + release.Notes;
        }
        async Task InstallUpdate()
        {
            if (Busy) { Ui.Error(this, "Espera a que terminen las copias antes de actualizar."); return; }
            var release = pendingRelease;
            if (release == null) return;
            if (MessageBox.Show(this, "Se cerrará SSH Backup, se guardará el EXE anterior y se abrirá la nueva versión.\r\nLos servidores y credenciales se conservan. ¿Continuar?", "Instalar actualización", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            await CatalogOperation(async token => {
                string path = pendingPackage;
                if (path == null) path = await Updates.DownloadPackageAsync(release, Path.Combine(store.DirectoryPath, "Actualizaciones"), token);
                if (IsDisposed) return;
                if (Busy) throw new InvalidOperationException("Hay una copia activa; vuelve a instalar al terminar.");
                store.Save(); Updates.LaunchInstall(path, store.DirectoryPath); Close();
            });
        }
    }
}
