using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

[assembly: AssemblyTitle("Guarda")]
[assembly: AssemblyDescription("Copias de carpetas a unidades USB, discos y otras carpetas con fecha y verificación.")]
[assembly: AssemblyCompany("Rafa")]
[assembly: AssemblyProduct("Guarda")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace Guarda
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            if (args.Length == 3 && args[0] == "--guarda-apply-update") return Updates.RunInstaller(args[1], args[2]);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => MessageBox.Show("Se ha producido un error: " + e.Exception.Message, "Guarda", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
            return 0;
        }
    }

    public sealed class SavedSettings
    {
        public string Source = "", Destination = "", Name = "";
        public bool Verify = true;
        public bool LightTheme = false;
    }

    internal static class Preferences
    {
        private static string FileName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guarda", "Preferencias.xml"); } }
        public static SavedSettings Load()
        {
            try
            {
                string path = FileName;
                if (!File.Exists(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopiaFacil", "Preferencias.xml");
                using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    return (SavedSettings)new XmlSerializer(typeof(SavedSettings)).Deserialize(reader);
            }
            catch { return new SavedSettings(); }
        }
        public static void Save(SavedSettings settings)
        {
            string temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName));
                using (StreamWriter writer = new StreamWriter(temporary)) new XmlSerializer(typeof(SavedSettings)).Serialize(writer, settings);
                if (File.Exists(FileName)) File.Replace(temporary, FileName, null); else File.Move(temporary, FileName);
            }
            catch { }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
    }

    public sealed partial class MainForm : Form
    {
        private TextBox source, destination, copyName;
        private Button sourceBrowse, destinationBrowse, start, cancel, openCopy, openReport;
        private CheckBox verify;
        private Label preview, status, detail, stats, percent, destinationHint;
        private ProgressTrack bar;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400 };
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 120 };
        private readonly Stopwatch elapsed = new Stopwatch();
        private volatile BackupProgress latest;
        private CancellationTokenSource cancellation;
        private BackupResult lastResult;
        private bool updatingName, closeWhenFinished;
        public bool Busy { get { return cancellation != null; } }

        public MainForm()
        {
            SavedSettings saved = Preferences.Load();
            Theme.Light = saved.LightTheme;
            Text = "Guarda";
            BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10);
            ClientSize = new Size(1040, 710); MinimumSize = new Size(860, 720);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
            Build();
            source.Text = saved.Source; destination.Text = saved.Destination;
            copyName.Text = string.IsNullOrWhiteSpace(saved.Name) ? BackupEngine.DefaultName(saved.Source) : saved.Name;
            verify.Checked = saved.Verify;
            UpdatePreview();
            ApplyAppearance();
            ticker.Tick += (s, e) => UpdateProgress();
            FormClosing += OnClosing;
        }

        private void Choose(TextBox box, string description)
        {
            using (FolderBrowserDialog picker = new FolderBrowserDialog { Description = description, ShowNewFolderButton = true })
            {
                if (Directory.Exists(box.Text)) picker.SelectedPath = box.Text;
                if (picker.ShowDialog(this) == DialogResult.OK) box.Text = picker.SelectedPath;
            }
        }

        private void UpdatePreview()
        {
            if (preview == null || source == null || destination == null || copyName == null) return;
            try
            {
                string full = BackupEngine.Preview(source.Text, destination.Text, copyName.Text, DateTime.Now);
                preview.Text = Path.GetFileName(full);
                destinationHint.Text = Path.GetDirectoryName(full);
                tips.SetToolTip(preview, full);
                tips.SetToolTip(destinationHint, full);
            }
            catch { preview.Text = DateTime.Now.ToString("yyyy_MM_dd", System.Globalization.CultureInfo.InvariantCulture) + "_MiCarpeta"; destinationHint.Text = "Elige una carpeta de destino"; }
        }

        private void Save()
        {
            Preferences.Save(new SavedSettings { Source = source.Text, Destination = destination.Text, Name = copyName.Text, Verify = verify.Checked, LightTheme = Theme.Light });
        }

        private async void StartClick(object sender, EventArgs e)
        {
            if (Busy) return;
            BackupOptions options;
            try
            {
                string from = BackupEngine.Normalize(source.Text), to = BackupEngine.Normalize(destination.Text);
                BackupEngine.ValidatePaths(from, to);
                options = new BackupOptions { Source = from, Destination = to, Name = BackupEngine.ValidateName(copyName.Text), Verify = verify.Checked, Date = DateTime.Now };
            }
            catch (Exception ex) { status.Text = "Revisa las carpetas"; status.ForeColor = Theme.Warning; detail.Text = ex.Message; tips.SetToolTip(detail, ex.Message); return; }
            Save();
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            latest = null; lastResult = null; bar.Value = 0; percent.Text = "";
            status.Text = "Preparando copia"; status.ForeColor = Theme.Text; detail.Text = "Analizando el contenido del origen…";
            SetBusy(true); elapsed.Restart(); ticker.Start();
            try
            {
                lastResult = await Task.Run(() => BackupEngine.Run(options, p => latest = p, token));
                UpdateProgress(); ticker.Stop(); elapsed.Stop();
                BackupResult result = lastResult;
                status.ForeColor = result.Success ? Theme.Accent : Theme.Warning;
                status.Text = result.Success ? (result.Warnings.Count == 0 ? "Copia terminada" : "Copia terminada con avisos") : (result.Cancelled ? "Copia cancelada" : "Copia incompleta");
                detail.Text = result.Success ? result.OutputPath : result.Message;
                tips.SetToolTip(detail, detail.Text);
                bar.Value = result.Success ? 100 : bar.Value;
                percent.Text = result.Success ? "100 %" : "";
                stats.Text = result.Files + " archivos · " + BackupEngine.FormatBytes(result.Bytes) + " · " + elapsed.Elapsed.ToString(@"hh\:mm\:ss") + (result.Success && options.Verify ? " · Contenido verificado" : "") + (result.Warnings.Count > 0 ? " · " + result.Warnings.Count + " avisos en el informe" : "");
            }
            catch (Exception ex) { status.Text = "No se ha podido terminar"; status.ForeColor = Theme.Warning; detail.Text = ex.Message; tips.SetToolTip(detail, ex.Message); }
            finally
            {
                ticker.Stop(); elapsed.Stop(); cancellation.Dispose(); cancellation = null; SetBusy(false);
                if (closeWhenFinished) Close();
            }
        }

        private void SetBusy(bool busy)
        {
            foreach (Control control in new Control[] { source, destination, sourceBrowse, destinationBrowse, copyName, verify, start, themeToggle, updatesButton }) control.Enabled = !busy;
            cancel.Enabled = busy;
            openCopy.Enabled = !busy && lastResult != null && !string.IsNullOrEmpty(lastResult.OutputPath);
            openReport.Enabled = !busy && lastResult != null && !string.IsNullOrEmpty(lastResult.ReportPath);
            start.Text = busy ? "Copiando…" : "Crear copia";
        }

        private void UpdateProgress()
        {
            BackupProgress p = latest;
            if (p == null) return;
            status.Text = cancellation != null && cancellation.IsCancellationRequested ? "Cancelando…" : p.Stage;
            detail.Text = string.IsNullOrEmpty(p.File) ? "Preparando el contenido de la copia…" : p.File;
            tips.SetToolTip(detail, detail.Text);
            if (!p.Indeterminate)
            {
                int value = p.Total == 0 ? 0 : (int)Math.Min(99, (double)p.Done * 100 / p.Total);
                bar.Value = value; percent.Text = value + " %";
                stats.Text = p.FilesDone + " / " + p.FilesTotal + " archivos · " + BackupEngine.FormatBytes(p.CopiedBytes) + " copiados · " + elapsed.Elapsed.ToString(@"hh\:mm\:ss");
            }
            else stats.Text = p.FilesTotal + " archivos encontrados · " + BackupEngine.FormatBytes(p.Total) + " · " + elapsed.Elapsed.ToString(@"hh\:mm\:ss");
        }

        public void Cancel()
        {
            if (cancellation == null) return;
            cancellation.Cancel(); cancel.Enabled = false; status.Text = "Cancelando…";
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (Busy) { closeWhenFinished = true; Cancel(); e.Cancel = true; }
            else Save();
        }

        private void Open(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || (!Directory.Exists(path) && !File.Exists(path))) throw new IOException("No se encuentra la copia o el informe. Comprueba que la unidad siga conectada.");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { detail.Text = ex.Message; tips.SetToolTip(detail, ex.Message); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { ticker.Dispose(); tips.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
