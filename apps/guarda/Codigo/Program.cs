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
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace Guarda
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => MessageBox.Show("Se ha producido un error: " + e.Exception.Message, "Guarda", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
        }
    }

    public sealed class SavedSettings
    {
        public string Source = "", Destination = "", Name = "";
        public bool Verify = true;
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

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(14, 22, 34), Card = Color.FromArgb(23, 35, 50), Input = Color.FromArgb(14, 25, 39);
        public static readonly Color Text = Color.FromArgb(237, 243, 250), Muted = Color.FromArgb(164, 183, 202), Accent = Color.FromArgb(70, 215, 184), Line = Color.FromArgb(47, 65, 84);
        public static readonly Color Warning = Color.FromArgb(255, 195, 112);
        public static Label Label(string text, float size, Color color, bool bold)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        }
        public static Button Button(string text, bool primary)
        {
            Button b = new Button { Text = text, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Card, ForeColor = primary ? Background : Text, Height = 42, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderColor = primary ? Accent : Line;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(104, 231, 206) : Color.FromArgb(37, 55, 74);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(49, 184, 158) : Color.FromArgb(29, 44, 61);
            return b;
        }
    }

    internal sealed class ProgressTrack : Control
    {
        private int value;
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public ProgressTrack() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Height = 9; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Line);
            using (Brush fill = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(fill, 0, 0, Width * value / 100, Height);
        }
    }

    internal sealed class Logo : Control
    {
        public Logo() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Size = new Size(58, 58); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(Width, Height) / 58f;
            e.Graphics.ScaleTransform(s, s);
            using (Brush bg = new SolidBrush(Theme.Card)) e.Graphics.FillEllipse(bg, 0, 0, 58, 58);
            using (Pen p = new Pen(Theme.Accent, 2.5f))
            {
                p.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(p, new[] { new Point(13, 39), new Point(13, 19), new Point(25, 19), new Point(29, 24), new Point(44, 24), new Point(44, 39), new Point(13, 39) });
                e.Graphics.DrawLines(p, new[] { new Point(25, 31), new Point(29, 35), new Point(36, 28) });
            }
        }
    }

    public sealed class MainForm : Form
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
            Text = "Guarda";
            BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 10);
            ClientSize = new Size(900, 710); MinimumSize = new Size(740, 720);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }
            Build();
            SavedSettings saved = Preferences.Load();
            source.Text = saved.Source; destination.Text = saved.Destination;
            copyName.Text = string.IsNullOrWhiteSpace(saved.Name) ? BackupEngine.DefaultName(saved.Source) : saved.Name;
            verify.Checked = saved.Verify;
            UpdatePreview();
            ticker.Tick += (s, e) => UpdateProgress();
            FormClosing += OnClosing;
        }

        private void Build()
        {
            TableLayoutPanel page = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(30, 18, 30, 12), ColumnCount = 1, RowCount = 9, BackColor = Theme.Background };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 78, 102, 102, 102, 60, 118, 54, 24 }) page.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(page);

            TableLayoutPanel header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); header.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Logo logo = new Logo { Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 5, 0, 0) };
            header.Controls.Add(logo, 0, 0); header.SetRowSpan(logo, 2);
            header.Controls.Add(Theme.Label("Guarda", 25, Theme.Text, true), 1, 0);
            header.Controls.Add(Theme.Label("Tus carpetas, a salvo y con fecha.", 10, Theme.Muted, false), 1, 1);
            Label badge = Theme.Label("PORTABLE  1.0.1", 8, Theme.Accent, true); badge.TextAlign = ContentAlignment.MiddleRight;
            header.Controls.Add(badge, 2, 0); page.Controls.Add(header, 0, 0);

            page.Controls.Add(FolderCard("1", "¿Qué carpeta quieres copiar?", "ORIGEN", out source, out sourceBrowse), 0, 1);
            page.Controls.Add(FolderCard("2", "¿Dónde quieres guardar la copia?", "DESTINO", out destination, out destinationBrowse), 0, 2);
            source.AccessibleName = "Carpeta de origen"; destination.AccessibleName = "Carpeta de destino";
            sourceBrowse.Click += (s, e) => Choose(source, "Selecciona la carpeta que quieres copiar");
            destinationBrowse.Click += (s, e) => Choose(destination, "Selecciona el pendrive, disco o carpeta de destino");
            source.TextChanged += (s, e) => { if (!updatingName && !Busy) { updatingName = true; copyName.Text = BackupEngine.DefaultName(source.Text); updatingName = false; } UpdatePreview(); };
            destination.TextChanged += (s, e) => UpdatePreview();

            TableLayoutPanel naming = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0, 2, 0, 0) };
            naming.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176)); naming.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            naming.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); naming.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); naming.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            naming.Controls.Add(Theme.Label("Nombre de la copia", 10, Theme.Text, true), 0, 0);
            copyName = Input(); copyName.AccessibleName = "Nombre de la copia"; naming.Controls.Add(copyName, 1, 0); copyName.TextChanged += (s, e) => UpdatePreview();
            preview = Theme.Label("", 10, Theme.Accent, true); naming.Controls.Add(preview, 0, 1); naming.SetColumnSpan(preview, 2);
            destinationHint = Theme.Label("El nombre empieza siempre por aaaa_mm_dd.", 9, Theme.Muted, false); naming.Controls.Add(destinationHint, 0, 2); naming.SetColumnSpan(destinationHint, 2);
            page.Controls.Add(naming, 0, 3);

            TableLayoutPanel verification = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            verification.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); verification.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            verify = new CheckBox { Text = "Comprobar el contenido después de copiar", Checked = true, Dock = DockStyle.Fill, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Margin = Padding.Empty, AutoSize = true };
            verification.Controls.Add(verify, 0, 0); verification.Controls.Add(Theme.Label("Relee cada archivo del destino y lo compara con el original (SHA-256).", 9, Theme.Muted, false), 0, 1);
            page.Controls.Add(verification, 0, 4);

            TableLayoutPanel progressPanel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(17, 10, 17, 10), ColumnCount = 2, RowCount = 4, Margin = new Padding(0, 0, 0, 10) };
            progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 15)); progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            status = Theme.Label("Lista para crear una copia", 11, Theme.Text, true); percent = Theme.Label("", 10, Theme.Accent, true); percent.TextAlign = ContentAlignment.MiddleRight;
            detail = Theme.Label("Elige el origen y el destino para empezar.", 9, Theme.Muted, false);
            bar = new ProgressTrack { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            stats = Theme.Label("Cada copia conserva sus archivos en una carpeta independiente.", 8.5f, Theme.Muted, false);
            progressPanel.Controls.Add(status, 0, 0); progressPanel.Controls.Add(percent, 1, 0);
            progressPanel.Controls.Add(detail, 0, 1); progressPanel.SetColumnSpan(detail, 2);
            progressPanel.Controls.Add(bar, 0, 2); progressPanel.SetColumnSpan(bar, 2);
            progressPanel.Controls.Add(stats, 0, 3); progressPanel.SetColumnSpan(stats, 2);
            page.Controls.Add(progressPanel, 0, 5);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
            start = Theme.Button("Crear copia", true); start.Width = 172; start.Margin = new Padding(0, 3, 10, 0); start.Click += StartClick;
            cancel = Theme.Button("Cancelar", false); cancel.Width = 108; cancel.Enabled = false; cancel.Margin = new Padding(0, 3, 10, 0); cancel.Click += (s, e) => Cancel();
            openCopy = Theme.Button("Abrir copia", false); openCopy.Width = 128; openCopy.Enabled = false; openCopy.Margin = new Padding(0, 3, 10, 0); openCopy.Click += (s, e) => Open(lastResult == null ? "" : lastResult.OutputPath);
            openReport = Theme.Button("Ver informe", false); openReport.Width = 125; openReport.Enabled = false; openReport.Margin = new Padding(0, 3, 0, 0); openReport.Click += (s, e) => Open(lastResult == null ? "" : lastResult.ReportPath);
            actions.Controls.AddRange(new Control[] { start, cancel, openCopy, openReport }); page.Controls.Add(actions, 0, 6);
            page.Controls.Add(Theme.Label("Carpetas · Pendrives · Discos externos · Carpetas de red", 8.5f, Theme.Muted, false), 0, 7);
            AcceptButton = start;
        }

        private Control FolderCard(string number, string title, string field, out TextBox path, out Button browse)
        {
            TableLayoutPanel card = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 12), BackColor = Theme.Card, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 0, 0, 10) };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 33)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            card.Controls.Add(Theme.Label(number, 15, Theme.Accent, true), 0, 0);
            card.Controls.Add(Theme.Label(title, 11, Theme.Text, true), 1, 0);
            Label kind = Theme.Label(field, 7.5f, Theme.Muted, true); kind.TextAlign = ContentAlignment.MiddleRight; card.Controls.Add(kind, 2, 0);
            path = Input(); path.AllowDrop = true; card.Controls.Add(path, 0, 1); card.SetColumnSpan(path, 2);
            browse = Theme.Button("Elegir…", false); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(12, 0, 0, 0); card.Controls.Add(browse, 2, 1);
            TextBox box = path;
            box.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            box.DragDrop += (s, e) => { string[] items = e.Data.GetData(DataFormats.FileDrop) as string[]; if (items != null && items.Length == 1 && Directory.Exists(items[0])) box.Text = items[0]; };
            tips.SetToolTip(box, "Puedes pegar una ruta o arrastrar una carpeta aquí.");
            return card;
        }

        private TextBox Input()
        {
            return new TextBox { Dock = DockStyle.Fill, BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11), Margin = new Padding(0, 5, 0, 4), MaxLength = 32000 };
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
                preview.Text = "Se creará:  " + BackupEngine.Preview(source.Text, destination.Text, copyName.Text, DateTime.Now);
                tips.SetToolTip(preview, preview.Text);
            }
            catch { preview.Text = "Ejemplo:  " + DateTime.Now.ToString("yyyy_MM_dd") + "_MiCarpeta"; }
        }

        private void Save()
        {
            Preferences.Save(new SavedSettings { Source = source.Text, Destination = destination.Text, Name = copyName.Text, Verify = verify.Checked });
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
            foreach (Control control in new Control[] { source, destination, sourceBrowse, destinationBrowse, copyName, verify, start }) control.Enabled = !busy;
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
