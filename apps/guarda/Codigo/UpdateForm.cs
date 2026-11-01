using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Guarda
{
    public sealed class UpdateForm : Form
    {
        private Label state, info, installed;
        private TextBox notes;
        private ModernButton check, install, close;
        private ProgressTrack progress;
        private SignedUpdate release;
        private CancellationTokenSource cancellation;
        private long received;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 120 };
        private bool closeAfter;
        public UpdateForm()
        {
            Text = "Actualizaciones de Guarda"; ClientSize = new Size(590, 425); MinimumSize = Size; MaximumSize = new Size(850, 650); StartPosition = FormStartPosition.CenterParent; Font = Theme.Font(10, false); AutoScaleMode = AutoScaleMode.Dpi;
            TableLayoutPanel page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 7, Tag = "page" };
            foreach (int h in new[] { 42, 28, 40, 46, 120, 19, 55 }) page.RowStyles.Add(new RowStyle(SizeType.Absolute, h)); Controls.Add(page);
            page.Controls.Add(Theme.Label("Siempre al día", 22, "text", true), 0, 0);
            installed = Theme.Label("Versión instalada: " + Updates.CurrentVersion, 9, "muted"); page.Controls.Add(installed, 0, 1);
            state = Theme.Label("Buscar una nueva versión", 12, "text", true); page.Controls.Add(state, 0, 2);
            info = Theme.Label("La consulta se realiza al pulsar Buscar. Tus preferencias se conservan al actualizar.", 9, "muted"); page.Controls.Add(info, 0, 3);
            notes = new TextBox { Multiline = true, ReadOnly = true, TabStop = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, ScrollBars = ScrollBars.Vertical, Font = Theme.Font(10, false), Text = "Aquí aparecerán las novedades de la última versión.", Margin = new Padding(0, 6, 0, 8) }; page.Controls.Add(notes, 0, 4);
            progress = new ProgressTrack { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 6) }; page.Controls.Add(progress, 0, 5);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Tag = "page", Padding = new Padding(0, 9, 0, 0) };
            check = Theme.Button("Buscar"); check.Width = 100; check.Click += CheckClick;
            install = Theme.Button("Actualizar y reiniciar", true); install.Width = 220; install.Enabled = false; install.Click += InstallClick;
            close = Theme.Button("Cerrar"); close.Width = 100; close.Click += (s, e) => Close();
            actions.Controls.Add(check); actions.Controls.Add(install); actions.Controls.Add(close); page.Controls.Add(actions, 0, 6);
            timer.Tick += (s, e) => { if (release != null && release.Size > 0) { long done = Interlocked.Read(ref received); progress.Value = (int)Math.Min(99, done * 100 / release.Size); info.Text = BackupEngine.FormatBytes(done) + " / " + BackupEngine.FormatBytes(release.Size); } };
            FormClosing += (s, e) => { if (cancellation != null) { closeAfter = true; cancellation.Cancel(); e.Cancel = true; } };
            Theme.Apply(this);
            AcceptButton = check;
            Shown += (s, e) => { notes.SelectionLength = 0; check.Focus(); };
        }

        private void Busy(bool value)
        {
            check.Enabled = !value; install.Enabled = !value && release != null && Updates.ParseVersion(release.Version) > Updates.ParseVersion(Updates.CurrentVersion); close.Text = value ? "Cancelar" : "Cerrar";
        }

        private async void CheckClick(object sender, EventArgs e)
        {
            if (cancellation != null) return;
            cancellation = new CancellationTokenSource(); CancellationToken token = cancellation.Token;
            release = null; progress.Value = 0; Busy(true); state.Text = "Buscando actualizaciones…"; state.ForeColor = Theme.Text; info.Text = "Consultando el canal oficial de Guarda.";
            try
            {
                release = await Task.Run(() => Updates.Check(token));
                bool newer = Updates.ParseVersion(release.Version) > Updates.ParseVersion(Updates.CurrentVersion);
                state.Text = newer ? "Nueva versión disponible: " + release.Version : "Ya tienes la última versión";
                state.ForeColor = Theme.Accent; notes.Text = release.Notes.Replace("\n", Environment.NewLine); progress.Value = 100;
                info.Text = newer ? "Lista para descargar. Guarda se cerrará y volverá a abrirse al instalar." : "Guarda " + Updates.CurrentVersion + " está al día.";
            }
            catch (OperationCanceledException) { state.Text = "Búsqueda cancelada"; info.Text = "Puedes volver a intentarlo cuando quieras."; }
            catch (Exception ex) { state.Text = "No se ha podido buscar"; state.ForeColor = Theme.Warning; info.Text = ex is WebException ? "Comprueba tu conexión y vuelve a intentarlo. La copia de carpetas sigue disponible sin Internet." : ex.Message; }
            finally { cancellation.Dispose(); cancellation = null; Busy(false); if (closeAfter) Close(); }
        }

        private async void InstallClick(object sender, EventArgs e)
        {
            if (cancellation != null || release == null) return;
            cancellation = new CancellationTokenSource(); CancellationToken token = cancellation.Token; received = 0;
            Busy(true); state.Text = "Descargando Guarda " + release.Version; state.ForeColor = Theme.Text; timer.Start();
            bool ready = false;
            try
            {
                string payload = await Task.Run(() => Updates.Download(release, token, count => Interlocked.Exchange(ref received, count)));
                timer.Stop(); progress.Value = 100; state.Text = "Preparando la instalación…"; info.Text = "Se conservarán tus carpetas seleccionadas y el tema.";
                token.ThrowIfCancellationRequested();
                // The installer is prepared before the parent window is allowed to close.
                close.Enabled = false;
                await Task.Run(() => Updates.PrepareInstall(release, payload));
                ready = true;
            }
            catch (OperationCanceledException) { state.Text = "Actualización cancelada"; info.Text = "Tu versión instalada permanece disponible."; }
            catch (Exception ex) { state.Text = "No se ha podido actualizar"; state.ForeColor = Theme.Warning; info.Text = ex.Message; }
            finally { timer.Stop(); cancellation.Dispose(); cancellation = null; close.Enabled = true; Busy(false); }
            if (ready) { DialogResult = DialogResult.OK; Close(); } else if (closeAfter) Close();
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
