using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SSHBackup
{
    public sealed partial class MainForm
    {
        bool TrySave()
        {
            try { store.Save(); return true; }
            catch (Exception error) { Ui.Error(this, "No se ha podido guardar la configuración:\r\n" + error.Message); return false; }
        }
        List<ServerConfig> SelectedServers()
        {
            return serverGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as ServerConfig)
                .Where(s => s != null).OrderBy(s => store.Settings.Servers.IndexOf(s)).ToList();
        }
        void EditServer(ServerConfig server)
        {
            if (Busy) return;
            using (var dialog = new ServerDialog(server, store.Settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var previous = store.Settings.Servers;
                var next = previous.Select(s => s.Clone()).ToList();
                int index = server == null ? -1 : next.FindIndex(s => s.Id == server.Id);
                if (index < 0) next.Add(dialog.Result); else next[index] = dialog.Result;
                store.Settings.Servers = next;
                if (!TrySave()) store.Settings.Servers = previous;
                RefreshServers(); RefreshJobs(); RefreshButtons();
            }
        }
        void EditSelectedServer()
        {
            var selected = SelectedServers();
            if (selected.Count == 0) { MessageBox.Show(this, "Selecciona un servidor."); return; }
            EditServer(selected[0]);
        }
        void DeleteServers()
        {
            if (Busy) return;
            var selected = SelectedServers();
            if (selected.Count == 0) return;
            if (MessageBox.Show(this, "¿Quitar " + selected.Count + " servidores de la configuración?\r\nLas copias ya guardadas seguirán en el disco.",
                "Eliminar servidores", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var previous = store.Settings.Servers;
            var ids = new HashSet<string>(selected.Select(s => s.Id));
            store.Settings.Servers = previous.Where(s => !ids.Contains(s.Id)).ToList();
            if (!TrySave()) store.Settings.Servers = previous;
            RefreshServers(); RefreshJobs(); RefreshButtons();
        }
        void ImportServers()
        {
            if (Busy) return;
            using (var file = new OpenFileDialog { Title = "Importar servidores",
                Filter = "Excel y CSV|*.xlsx;*.csv;*.tsv|Excel (.xlsx)|*.xlsx|CSV|*.csv;*.tsv" })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var dialog = new ImportDialog(file.FileName, store.Settings.Servers))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK) return;
                        var previous = store.Settings.Servers;
                        store.Settings.Servers = dialog.Result;
                        if (!TrySave()) store.Settings.Servers = previous;
                        RefreshServers(); RefreshJobs(); RefreshButtons(); ShowPage("servers");
                    }
                }
                catch (Exception error) { Ui.Error(this, "No se ha podido importar el archivo:\r\n" + error.Message); }
            }
        }
        void ExportServers()
        {
            if (store.Settings.Servers.Count == 0) { MessageBox.Show(this, "No hay servidores para exportar."); return; }
            using (var options = new ExportDialog(store.Settings.Servers.Count))
            {
                if (options.ShowDialog(this) != DialogResult.OK) return;
                string extension = options.Extension;
                using (var file = new SaveFileDialog { Title = "Guardar listado de servidores",
                    Filter = extension == ".xlsx" ? "Excel (.xlsx)|*.xlsx" : "CSV UTF-8 (.csv)|*.csv",
                    FileName = "Servidores_SSH_" + DateTime.Now.ToString("yyyy-MM-dd") + extension,
                    DefaultExt = extension.TrimStart('.'), AddExtension = true, OverwritePrompt = true })
                {
                    if (file.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        int count = ServerExporter.Write(file.FileName, store.Settings.Servers, store.Settings.ExecutionCopy(),
                            server => store.KnownFingerprint(server), options.IncludePasswords);
                        MessageBox.Show(this, count + " servidores exportados.\r\n\r\n" + file.FileName +
                            "\r\n\r\nUtiliza «Importar listado» en el otro equipo con SSH Backup 1.2.0 o posterior.",
                            "Listado exportado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception error) { Ui.Error(this, "No se ha podido exportar el listado:\r\n" + error.Message); }
                }
            }
        }
        void SaveTemplate()
        {
            using (var file = new SaveFileDialog { Title = "Guardar plantilla de servidores",
                Filter = "Excel (.xlsx)|*.xlsx|CSV UTF-8 (.csv)|*.csv", FileName = "Plantilla_Servidores_SSH.xlsx", AddExtension = true })
            {
                if (file.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                        File.WriteAllBytes(file.FileName, Program.ResourceBytes("Plantilla_Servidores_SSH.xlsx"));
                    else ServerImporter.WriteTemplateCsv(file.FileName);
                    MessageBox.Show(this, "Plantilla guardada. Incluye alias, host, puerto, usuario, contraseña, origen y destino.",
                        "SSH Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception error) { Ui.Error(this, error.Message); }
            }
        }
        void SetAccess()
        {
            if (Busy) return;
            var selected = SelectedServers();
            if (selected.Count == 0) { MessageBox.Show(this, "Selecciona una o varias filas de servidores."); return; }
            ApplyAccess(selected);
        }
        bool ApplyAccess(List<ServerConfig> selected)
        {
            using (var dialog = new AccessDialog(selected.Count))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                var previous = selected.Select(s => s.Clone()).ToList();
                foreach (var server in selected)
                {
                    if (dialog.UserName.Length > 0) server.User = dialog.UserName;
                    server.Auth = dialog.UseKey ? "key" : "password"; server.KeyFile = dialog.UseKey ? dialog.KeyPath : "";
                    server.Password = dialog.Secret; server.PasswordLoaded = true;
                }
                if (!TrySave())
                {
                    foreach (var old in previous)
                    {
                        int index = store.Settings.Servers.FindIndex(s => s.Id == old.Id);
                        if (index >= 0) store.Settings.Servers[index] = old;
                    }
                    RefreshServers(); return false;
                }
                RefreshServers(); return true;
            }
        }
        void FillSettings()
        {
            settingsDestination.Text = store.Settings.Destination;
            settingParallel.Value = store.Settings.Parallel; settingRetries.Value = store.Settings.Retries;
            settingTimeout.Value = store.Settings.TimeoutSeconds; settingsExclusions.Text = store.Settings.Exclusions;
            settingBandwidthValue.Value = store.Settings.BandwidthValue; settingBandwidthUnit.SelectedItem = store.Settings.BandwidthUnit;
            settingBandwidthEnabled.Checked = store.Settings.BandwidthEnabled;
            settingBandwidthValue.Enabled = settingBandwidthUnit.Enabled = settingBandwidthEnabled.Checked;
        }
        void SaveSettings()
        {
            if (Busy) return;
            string destination = settingsDestination.Text.Trim();
            if (destination.Length == 0 || !Validation.AbsoluteDestination(destination))
            { Ui.Error(this, "Selecciona una carpeta de destino absoluta del PC."); return; }
            var old = store.Settings.ExecutionCopy();
            store.Settings.Destination = destination; store.Settings.Parallel = (int)settingParallel.Value;
            store.Settings.Retries = (int)settingRetries.Value; store.Settings.TimeoutSeconds = (int)settingTimeout.Value;
            store.Settings.Exclusions = settingsExclusions.Text;
            store.Settings.BandwidthEnabled = settingBandwidthEnabled.Checked; store.Settings.BandwidthValue = settingBandwidthValue.Value;
            store.Settings.BandwidthUnit = settingBandwidthUnit.Text;
            if (!TrySave())
            {
                store.Settings.Destination = old.Destination; store.Settings.Parallel = old.Parallel;
                store.Settings.Retries = old.Retries; store.Settings.TimeoutSeconds = old.TimeoutSeconds; store.Settings.Exclusions = old.Exclusions;
                store.Settings.BandwidthEnabled = old.BandwidthEnabled; store.Settings.BandwidthValue = old.BandwidthValue; store.Settings.BandwidthUnit = old.BandwidthUnit;
                return;
            }
            RefreshServers(); runStatus.Text = "Ajustes guardados.";
        }
        void ChooseGlobalDestination()
        {
            if (Busy) return;
            using (var dialog = new FolderBrowserDialog { Description = "Carpeta base para las copias SSH", SelectedPath = store.Settings.Destination })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string old = store.Settings.Destination; store.Settings.Destination = dialog.SelectedPath;
                if (!TrySave()) store.Settings.Destination = old;
                RefreshServers(); FillSettings();
            }
        }
        async void StartCopies(bool checkOnly, bool failedOnly, bool scheduled = false)
        {
            if (Busy) return;
            var targets = checkOnly ? SelectedServers() : store.Settings.Servers.Where(s => s.Enabled).ToList();
            if (checkOnly && targets.Count == 0) targets = store.Settings.Servers.Where(s => s.Enabled).ToList();
            if (failedOnly)
            {
                if (runner == null) return;
                checkOnly = runner.CheckOnly;
                var ids = new HashSet<string>(runner.Jobs.Where(j => j.Snapshot().State == JobState.Error || j.Snapshot().State == JobState.Canceled).Select(j => j.Server.Id));
                targets = store.Settings.Servers.Where(s => ids.Contains(s.Id)).ToList();
            }
            if (targets.Count == 0) { if (scheduled) { runStatus.Text = "Programación: no hay servidores marcados."; return; } MessageBox.Show(this, "Añade servidores y marca los que quieras copiar."); ShowPage("servers"); return; }
            var errors = targets.SelectMany(s => Validation.ServerErrors(s).Select(e => s.Alias + ": " + e)).ToList();
            if (targets.GroupBy(s => Validation.AliasKey(s.Alias)).Any(g => g.Count() > 1)) errors.Add("Hay alias duplicados en la configuración.");
            if (!Validation.AbsoluteDestination(store.Settings.Destination) || string.IsNullOrWhiteSpace(store.Settings.Destination))
                errors.Add("Selecciona un destino general válido en Ajustes.");
            if (errors.Count > 0) { if (scheduled) { SaveScheduleError(string.Join("; ", errors)); return; } Ui.Error(this, string.Join("\r\n", errors.Take(20))); return; }
            var missing = targets.Where(s => s.Auth != "key" && (!s.PasswordLoaded || string.IsNullOrEmpty(s.Password))).ToList();
            if (scheduled && (missing.Count > 0 || targets.Any(s => !s.PasswordLoaded || string.IsNullOrEmpty(store.KnownFingerprint(s)))))
            { SaveScheduleError("Faltan credenciales o huellas SSH. Usa Comprobar SSH antes de programar."); return; }
            if (missing.Count > 0 && !ApplyAccess(missing)) return;
            try
            {
                if (!TrySave()) return;
                if (runner != null) { DrainLogs(); runner.Dispose(); }
                runner = new BackupRunner(store, targets, scheduled ? (Func<ServerConfig, string, string, bool>)((s, a, f) => false) : ConfirmHost, checkOnly);
                jobsGrid.Rows.Clear(); visibleLogs.Clear(); logBox.Clear(); pause.Text = "Pausar cola";
                ShowPage("copies");
                currentTask = runner.RunAsync();
                RefreshJobs(); RefreshButtons();
                await currentTask;
                DrainLogs(); RefreshJobs(); RefreshHistory();
                runStatus.Text = "Ejecución terminada · registro: " + runner.Journal.LogPath;
                if (!string.IsNullOrEmpty(runner.Journal.WriteError)) Ui.Error(this, "Algunas líneas del registro no se pudieron guardar:\r\n" + runner.Journal.WriteError);
            }
            catch (Exception error) { Ui.Error(this, "No se ha podido completar la ejecución:\r\n" + error.Message); }
            finally
            {
                RefreshButtons();
                if (closingAfterRun) { closing = true; Close(); }
            }
        }
        bool ConfirmHost(ServerConfig server, string algorithm, string fingerprint)
        {
            if (closing || IsDisposed) return false;
            if (InvokeRequired)
            {
                try { return (bool)Invoke(new Func<bool>(() => ConfirmHost(server, algorithm, fingerprint))); }
                catch (InvalidOperationException) { return false; }
            }
            using (var dialog = new HostTrustDialog(server, algorithm, fingerprint))
                return dialog.ShowDialog(this) == DialogResult.OK;
        }
        void DrainLogs()
        {
            if (runner == null) return;
            LogRecord entry; bool changed = false;
            while (runner.Journal.Pending.TryDequeue(out entry))
            { visibleLogs.Add(entry); changed = true; }
            if (!changed) return;
            if (visibleLogs.Count > 5000) visibleLogs.RemoveRange(0, visibleLogs.Count - 5000);
            RedrawLog();
        }
        void RedrawLog()
        {
            var entries = visibleLogs.Where(l => !errorsOnly.Checked || l.Level == "ERROR" || l.Level == "AVISO").TakeLastCompat(500).ToList();
            logBox.SuspendLayout(); logBox.Clear();
            foreach (var entry in entries)
            {
                logBox.SelectionStart = logBox.TextLength;
                logBox.SelectionColor = entry.Level == "ERROR" ? Ui.Red : entry.Level == "OK" ? Ui.Green :
                    entry.Level == "AVISO" ? Ui.Warning : Ui.Ink;
                logBox.AppendText(entry + Environment.NewLine);
            }
            logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret(); logBox.ResumeLayout();
        }
        void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) { Ui.Error(this, "La ruta ya no está disponible:\r\n" + path); return; }
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception error) { Ui.Error(this, error.Message); }
        }
        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (closing) return;
            if (Busy)
            {
                e.Cancel = true;
                if (MessageBox.Show(this, "Hay operaciones en marcha. ¿Detenerlas y cerrar cuando terminen de limpiarse?",
                    "Cerrar SSH Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                { closingAfterRun = true; runner.Cancel(); runStatus.Text = "Deteniendo las operaciones antes de cerrar…"; }
                return;
            }
            if (toolsCancellation != null) toolsCancellation.Cancel();
            if (scheduleSignal != null) scheduleSignal.Dispose();
            refreshTimer.Stop(); refreshTimer.Dispose(); if (runner != null) runner.Dispose(); closing = true;
        }
        void SaveScheduleError(string message)
        {
            runStatus.Text = "Copia programada no iniciada: " + message;
            try { File.AppendAllText(Path.Combine(store.DirectoryPath, "programacion.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch (IOException) { }
        }
    }
    static class EnumerableCompat
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
        { var queue = new Queue<T>(); foreach (var value in source) { queue.Enqueue(value); if (queue.Count > count) queue.Dequeue(); } return queue; }
    }
}
