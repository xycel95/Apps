using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BloquesPDF
{
    public sealed class MainForm : Form
    {
        readonly List<string> files = new List<string>();
        readonly NumericUpDown batchSize = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 50, Width = 110, ThousandsSeparator = true };
        readonly NumericUpDown copies = new NumericUpDown { Minimum = 1, Maximum = 2, Value = 2, Width = 80 };
        readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        readonly CheckBox recursive = new CheckBox { Text = "Incluir subcarpetas", AutoSize = true };
        readonly CheckBox dark = new CheckBox { Text = "Modo oscuro", AutoSize = true };
        readonly Label summary = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        readonly Label status = new Label { AutoSize = false, Dock = DockStyle.Fill, AutoEllipsis = true };
        readonly Label hint = new Label { AutoSize = false, Dock = DockStyle.Fill, AutoEllipsis = true };
        readonly ProgressBar bar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        readonly DataGridView documentGrid = Grid();
        readonly DataGridView batchGrid = Grid();
        readonly Button addFiles = Button("Añadir PDF…");
        readonly Button addFolder = Button("Añadir carpeta…");
        readonly Button remove = Button("Quitar seleccionados");
        readonly Button clear = Button("Vaciar lista");
        readonly Button generate = Button("Crear bloques", true);
        readonly Button cancel = Button("Cancelar");
        readonly Button open = Button("Abrir resultado");
        CancellationTokenSource cancellation;
        bool busy;
        string lastOutput;
        string lastDestination = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public MainForm()
        {
            Text = "Bloques PDF · XyceL";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1040, 748);
            MinimumSize = new Size(900, 660);
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(24, 16, 24, 14) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 76, 80, 90, 40 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 66, 52, 62 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Controls.Add(layout);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new Label { Text = "Bloques PDF", Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true }, 0, 0);
            header.Controls.Add(dark, 1, 0);
            var subtitle = new Label { Text = "De muchos documentos a bloques listos para imprimir. Todo en tu ordenador.", AutoSize = true };
            header.Controls.Add(subtitle, 0, 1); header.SetColumnSpan(subtitle, 2);
            layout.Controls.Add(header, 0, 0);

            var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            input.Controls.Add(new Label { Text = "1. Añade los documentos · También puedes arrastrar aquí PDF o carpetas", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
            var inputButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            inputButtons.Controls.AddRange(new Control[] { addFiles, addFolder, remove, clear, recursive });
            recursive.Margin = new Padding(12, 10, 0, 0);
            input.Controls.Add(inputButtons); layout.Controls.Add(input, 0, 1);

            mode.Items.AddRange(new object[] { "Unir en PDF para imprimir", "Carpetas con los PDF firmados originales" });
            mode.SelectedIndex = 0;
            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settings.Controls.Add(new Label { Text = "2. PDF originales por bloque", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            settings.Controls.Add(new Label { Text = "Copias de cada documento", AutoSize = true }, 1, 0);
            settings.Controls.Add(new Label { Text = "Tipo de resultado", AutoSize = true }, 2, 0);
            settings.Controls.Add(batchSize, 0, 1); settings.Controls.Add(copies, 1, 1); settings.Controls.Add(mode, 2, 1);
            var defaultHint = new Label { Text = "50 por defecto · Cantidad editable", AutoSize = true };
            settings.Controls.Add(defaultHint, 0, 2);
            var printHint = new Label { Text = "Repeticiones dentro del PDF", AutoSize = true };
            settings.Controls.Add(printHint, 1, 2);
            settings.Controls.Add(new Label { Text = "Sin límite fijo de documentos añadidos", AutoSize = true }, 2, 2);
            layout.Controls.Add(settings, 0, 2);

            summary.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            layout.Controls.Add(summary, 0, 3);
            documentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "N.º", Width = 58, Name = "N" });
            documentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Documento PDF", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55 });
            documentGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Carpeta", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 70 });
            batchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bloque", Width = 75 });
            batchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PDF", Width = 65 });
            batchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Primer documento", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            batchGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Último documento", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            foreach (DataGridViewColumn column in documentGrid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            foreach (DataGridViewColumn column in batchGrid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var docs = new TabPage("Documentos añadidos") { Padding = new Padding(1) }; docs.Controls.Add(documentGrid);
            var batches = new TabPage("Bloques previstos") { Padding = new Padding(1) }; batches.Controls.Add(batchGrid);
            tabs.TabPages.AddRange(new[] { docs, batches }); layout.Controls.Add(tabs, 0, 4);

            var progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 10, 0, 0) };
            progress.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); progress.RowStyles.Add(new RowStyle(SizeType.Absolute, 13));
            progress.Controls.Add(status, 0, 0); progress.Controls.Add(bar, 0, 1); layout.Controls.Add(progress, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
            actions.Controls.AddRange(new Control[] { generate, cancel, open }); layout.Controls.Add(actions, 0, 6);
            hint.Padding = new Padding(0, 9, 0, 0); layout.Controls.Add(hint, 0, 7);

            addFiles.Click += (s, e) => ChooseFiles();
            addFolder.Click += async (s, e) => await ChooseFolderAsync();
            remove.Click += (s, e) => RemoveSelected();
            clear.Click += (s, e) => { files.Clear(); lastOutput = null; UpdateLists(); };
            batchSize.ValueChanged += (s, e) => UpdateLists(false);
            copies.ValueChanged += (s, e) => UpdateHints();
            mode.SelectedIndexChanged += (s, e) => { copies.Enabled = !busy && mode.SelectedIndex == 0; UpdateHints(); };
            generate.Click += async (s, e) => await GenerateAsync();
            cancel.Click += (s, e) => { cancellation?.Cancel(); cancel.Enabled = false; status.Text = "Cancelando… Se conservarán los bloques que ya estén completos."; };
            open.Click += (s, e) => OpenOutput();
            dark.CheckedChanged += (s, e) => ApplyTheme(this);
            DragEnter += (s, e) => e.Effect = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += async (s, e) => { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) await AddPathsAsync((string[])e.Data.GetData(DataFormats.FileDrop)); };
            FormClosing += (s, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); status.Text = "Cancelando el proceso. Podrás cerrar al terminar."; } };
            ApplyTheme(this); UpdateLists();
            status.Text = "Añade PDF, arrástralos a esta ventana o selecciona una carpeta.";
        }

        static Button Button(string text, bool primary = false)
        {
            return new Button { Text = text, AutoSize = true, Height = 34, MinimumSize = new Size(primary ? 145 : 105, 34), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 2, 10, 2), Tag = primary ? "primary" : null, UseVisualStyleBackColor = false };
        }

        static DataGridView Grid()
        {
            return new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, RowTemplate = { Height = 27 }, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, AllowUserToResizeRows = false, BackgroundColor = Color.White };
        }

        void ChooseFiles()
        {
            using (var dialog = new OpenFileDialog { Title = "Selecciona todos los PDF que quieras añadir", Filter = "Documentos PDF (*.pdf)|*.pdf", Multiselect = true, CheckFileExists = true, RestoreDirectory = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
        }

        async Task ChooseFolderAsync()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Añadir todos los PDF de una carpeta", ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) await AddPathsAsync(new[] { dialog.SelectedPath });
        }

        async Task AddPathsAsync(string[] paths)
        {
            SetBusy(true);
            cancellation = new CancellationTokenSource();
            bool includeSubfolders = recursive.Checked;
            try
            {
                status.Text = "Buscando documentos PDF…";
                var added = await Task.Run(() =>
                {
                    var result = new List<string>();
                    foreach (string path in paths)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (Directory.Exists(path)) result.AddRange(BatchEngine.Scan(path, includeSubfolders, cancellation.Token));
                        else if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)) result.Add(Path.GetFullPath(path));
                    }
                    return result;
                });
                AddFiles(added);
                status.Text = files.Count == 0 ? "No se han encontrado PDF." : "Lista preparada. Elige Crear bloques para seleccionar el destino.";
            }
            catch (OperationCanceledException) { status.Text = "Se ha cancelado la búsqueda. La lista anterior se conserva."; }
            catch (Exception ex) { MessageBox.Show(this, BatchEngine.Describe(ex), "No se ha podido añadir la carpeta", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { cancellation.Dispose(); cancellation = null; SetBusy(false); }
        }

        void AddFiles(IEnumerable<string> incoming)
        {
            var unique = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (string path in incoming)
            {
                string full = Path.GetFullPath(path);
                if (string.Equals(Path.GetExtension(full), ".pdf", StringComparison.OrdinalIgnoreCase) && unique.Add(full)) { files.Add(full); count++; }
            }
            files.Sort(NaturalPathComparer.Instance);
            lastOutput = null;
            UpdateLists();
            status.Text = count.ToString("N0") + " PDF añadidos. Los archivos ya presentes no se añaden dos veces.";
        }

        void RemoveSelected()
        {
            var indices = documentGrid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).OrderByDescending(index => index).ToArray();
            foreach (int index in indices) files.RemoveAt(index);
            UpdateLists();
        }

        void UpdateLists(bool refreshDocuments = true)
        {
            if (refreshDocuments)
            {
                documentGrid.Rows.Clear();
                for (int i = 0; i < files.Count; i++) documentGrid.Rows.Add(i + 1, Path.GetFileName(files[i]), Path.GetDirectoryName(files[i]));
                documentGrid.ClearSelection();
            }
            batchGrid.Rows.Clear();
            int size = (int)batchSize.Value;
            for (int offset = 0; offset < files.Count; offset += size)
            {
                int n = Math.Min(size, files.Count - offset);
                batchGrid.Rows.Add(offset / size + 1, n, Path.GetFileName(files[offset]), Path.GetFileName(files[offset + n - 1]));
            }
            summary.Text = files.Count.ToString("N0") + " documentos · " + BatchEngine.BatchCount(files.Count, size).ToString("N0") + " bloques previstos";
            generate.Enabled = !busy && files.Count > 0;
            remove.Enabled = !busy && files.Count > 0;
            clear.Enabled = !busy && files.Count > 0;
            open.Enabled = !busy && lastOutput != null;
            cancel.Visible = busy;
            UpdateHints();
        }

        void UpdateHints()
        {
            if (mode.SelectedIndex == 1)
                hint.Text = "Los PDF se copian en carpetas por bloques, conservando todas sus firmas digitales. Al imprimir, selecciona 2 copias.";
            else
                hint.Text = "El PDF final incluirá " + copies.Value + " copia(s) seguida(s) de cada documento: imprime con 1 copia.\r\nEs una copia para impresión; valida las firmas digitales en los originales.";
            if (batchSize.Value > 50) hint.Text += " Para tu impresora se recomienda utilizar 50 PDF por bloque.";
        }

        async Task GenerateAsync()
        {
            if (busy || files.Count == 0) return;
            string destination;
            using (var dialog = new FolderBrowserDialog { Description = "3. ¿Dónde quieres guardar los bloques? Se creará una carpeta nueva dentro del destino.", SelectedPath = lastDestination, ShowNewFolderButton = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                destination = dialog.SelectedPath; lastDestination = destination;
            }
            var options = new BatchOptions { DestinationDirectory = destination, BatchSize = (int)batchSize.Value, Copies = (int)copies.Value, KeepSignedOriginals = mode.SelectedIndex == 1 };
            var snapshot = files.ToArray();
            cancellation = new CancellationTokenSource();
            SetBusy(true); bar.Value = 0;
            try
            {
                var progress = new UiProgress(new Progress<ProgressInfo>(p =>
                {
                    bar.Value = Math.Min(100, p.Total == 0 ? 0 : (int)((long)p.Done * 100 / p.Total));
                    status.Text = p.Phase + " · " + p.Done.ToString("N0") + "/" + p.Total.ToString("N0") + " documentos · " + p.CompletedBatches + " bloques guardados\r\n" + p.FileName;
                }));
                var result = await Task.Run(() => BatchEngine.Run(snapshot, options, progress, cancellation.Token));
                lastOutput = result.OutputDirectory;
                if (result.Status == "COMPLETADO")
                {
                    bar.Value = 100;
                    status.Text = "Completado · " + result.CompletedDocuments.ToString("N0") + " documentos en " + result.Outputs.Count.ToString("N0") + " bloques.\r\n" + result.OutputDirectory;
                    MessageBox.Show(this, "Se han guardado " + result.Outputs.Count.ToString("N0") + " bloques.\r\n\r\n" +
                        (options.KeepSignedOriginals ? "Los originales firmados se han copiado sin modificar." : "Cada PDF ya incluye " + options.Copies + " copia(s) de cada documento. Al imprimir el bloque, elige 1 copia.") +
                        "\r\n\r\nDestino: " + result.OutputDirectory, "Bloques creados", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    status.Text = result.Status + " · " + result.CompletedDocuments.ToString("N0") + " de " + result.SourceDocuments.ToString("N0") + " documentos guardados.";
                    string errors = string.Join("\r\n", result.Errors.Take(6));
                    MessageBox.Show(this, "Estado: " + result.Status + ". El trabajo está incompleto.\r\n\r\n" + errors +
                        "\r\n\r\nConsulta LEEME_RESULTADO.txt en la carpeta del resultado. Ningún documento se ha omitido de forma silenciosa.", "Revisa el resultado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) { status.Text = "No se ha completado el trabajo."; MessageBox.Show(this, BatchEngine.Describe(ex), "No se han podido crear los bloques", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { cancellation.Dispose(); cancellation = null; SetBusy(false); }
        }

        void SetBusy(bool value)
        {
            busy = value;
            foreach (Control control in new Control[] { addFiles, addFolder, remove, clear, batchSize, recursive, mode, generate, documentGrid }) control.Enabled = !value;
            copies.Enabled = !value && mode.SelectedIndex == 0;
            cancel.Visible = value; cancel.Enabled = value;
            open.Enabled = !value && lastOutput != null;
            if (!value) UpdateLists(false);
        }

        void OpenOutput()
        {
            if (lastOutput == null || !Directory.Exists(lastOutput)) return;
            try { Process.Start(new ProcessStartInfo { FileName = lastOutput, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, BatchEngine.Describe(ex), "No se ha podido abrir la carpeta", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void ApplyTheme(Control control)
        {
            Color background = dark.Checked ? Color.FromArgb(24, 29, 39) : Color.FromArgb(246, 248, 251);
            Color surface = dark.Checked ? Color.FromArgb(34, 41, 54) : Color.White;
            Color foreground = dark.Checked ? Color.FromArgb(233, 238, 246) : Color.FromArgb(30, 43, 62);
            control.BackColor = background; control.ForeColor = foreground;
            if (control is Button button)
            {
                bool primary = Equals(button.Tag, "primary");
                button.BackColor = primary ? Color.FromArgb(35, 94, 202) : surface;
                button.ForeColor = primary ? Color.White : foreground;
                button.FlatAppearance.BorderColor = dark.Checked ? Color.FromArgb(68, 80, 99) : Color.FromArgb(210, 219, 232);
            }
            if (control is NumericUpDown || control is ComboBox) control.BackColor = surface;
            if (control is DataGridView grid)
            {
                grid.BackgroundColor = surface; grid.GridColor = dark.Checked ? Color.FromArgb(58, 66, 82) : Color.FromArgb(230, 235, 242);
                grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = foreground;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 94, 202); grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.BackColor = dark.Checked ? Color.FromArgb(44, 54, 70) : Color.FromArgb(231, 237, 247);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
                grid.ColumnHeadersHeight = 32;
                return;
            }
            foreach (Control child in control.Controls) ApplyTheme(child);
        }

        sealed class UiProgress : IProgress<ProgressInfo>
        {
            readonly IProgress<ProgressInfo> target;
            readonly Stopwatch clock = Stopwatch.StartNew();
            string phase;
            public UiProgress(IProgress<ProgressInfo> target) { this.target = target; }
            public void Report(ProgressInfo info)
            {
                if (info.Phase == phase && info.Message == null && clock.ElapsedMilliseconds < 80) return;
                phase = info.Phase; clock.Restart(); target.Report(info);
            }
        }
    }
}
