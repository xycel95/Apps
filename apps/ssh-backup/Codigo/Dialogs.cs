using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SSHBackup
{
    public static partial class Ui
    {
        public static Button Button(string text, bool primary = false, int width = 130)
        {
            var button = new ModernButton { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular),
                BackColor = primary ? Primary : Surface, ForeColor = primary ? Color.White : Ink,
                Tag = primary ? PrimaryButtonTag : null,
                Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = primary ? Primary : Border;
            return button;
        }
        public static Label Label(string text, float size = 10, Color? color = null)
        {
            return new Label { Text = text, AutoSize = false, ForeColor = color ?? Ink,
                Font = new Font("Segoe UI", size, size >= 18 ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        }
        public static TextBox Text(string value = "", bool password = false)
        {
            return new TextBox { Text = value ?? "", Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = password, Margin = new Padding(0, 5, 0, 5) };
        }
        public static DataGridView Grid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Surface, BorderStyle = BorderStyle.None,
                AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Border,
                EnableHeadersVisualStyles = false, ColumnHeadersHeight = 42, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowTemplate = { Height = 42 }, Font = new Font("Segoe UI", 9.5F), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Header, ForeColor = Muted,
                Font = new Font("Segoe UI", 9F), SelectionBackColor = Header, Padding = new Padding(7, 0, 7, 0) };
            grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.DefaultCellStyle.ForeColor = Ink;
            return grid;
        }
        public static void Column(DataGridView grid, string key, string title, float weight = 100, int minimum = 65)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = key, HeaderText = title, FillWeight = weight,
                MinimumWidth = minimum, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        }
        public static Panel Section(string title, Control content)
        {
            var panel = new ModernPanel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(8), Margin = new Padding(0) };
            var heading = Label(title, 10);
            heading.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            heading.Dock = DockStyle.Top; heading.Height = 42; heading.Padding = new Padding(8, 0, 0, 0);
            panel.Controls.Add(content); panel.Controls.Add(heading);
            return panel;
        }
        public static TableLayoutPanel FormTable()
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2, Padding = new Padding(22),
                BackColor = Surface };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }
        public static void Field(TableLayoutPanel table, string label, Control control, int height = 42)
        {
            int row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(Label(label, 9, Muted), 0, row);
            Control field = control is TextBox || control is NumericUpDown || control is ComboBox ? Input(control) : control;
            field.Dock = DockStyle.Fill; table.Controls.Add(field, 1, row);
        }
        public static Control ScrollForm(TableLayoutPanel table)
        {
            int height = table.Padding.Vertical;
            foreach (RowStyle row in table.RowStyles) if (row.SizeType == SizeType.Absolute) height += (int)row.Height;
            table.Dock = DockStyle.Top; table.AutoScroll = false; table.Height = height;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Surface };
            scroll.Controls.Add(table); return scroll;
        }
        public static Control Browse(TextBox text, bool folder)
        {
            var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            layout.Controls.Add(Input(text), 0, 0);
            var button = Button("…", false, 40); button.Dock = DockStyle.Fill; button.Margin = new Padding(7, 3, 0, 3);
            button.Click += (s, e) => {
                if (folder)
                {
                    using (var dialog = new FolderBrowserDialog { Description = "Selecciona la carpeta de destino", SelectedPath = text.Text })
                        if (dialog.ShowDialog(layout.FindForm()) == DialogResult.OK) text.Text = dialog.SelectedPath;
                }
                else
                {
                    using (var dialog = new OpenFileDialog { Title = "Selecciona la clave privada SSH", Filter = "Todos los archivos|*.*" })
                        if (dialog.ShowDialog(layout.FindForm()) == DialogResult.OK) text.Text = dialog.FileName;
                }
            };
            layout.Controls.Add(button, 1, 0);
            return layout;
        }
        public static FlowLayoutPanel Actions(params Control[] controls)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 64,
                Padding = new Padding(16, 13, 16, 0), BackColor = Background, WrapContents = false };
            panel.Controls.AddRange(controls); return panel;
        }
        public static void Error(IWin32Window owner, string text) { MessageBox.Show(owner, text, "SSH Backup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    public sealed class ServerDialog : ThemedForm
    {
        public ServerConfig Result { get; private set; }
        readonly ServerConfig original;
        readonly List<ServerConfig> others;
        readonly Settings settings;
        readonly TextBox alias, host, user, password, source, destination, key, fingerprint;
        readonly NumericUpDown port;
        readonly ComboBox auth;
        readonly Label preview;
        public ServerDialog(ServerConfig server, Settings current)
        {
            original = server; others = current.Servers; settings = current;
            Text = server == null ? "Añadir servidor" : "Editar " + server.Alias;
            Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(730, 690); Size = new Size(790, 750); MaximizeBox = false; MinimizeBox = false;
            var config = server ?? new ServerConfig();
            alias = Ui.Text(config.Alias); host = Ui.Text(config.Host); user = Ui.Text(config.User);
            password = Ui.Text(config.PasswordLoaded ? config.Password : "", true); source = Ui.Text(config.Source);
            destination = Ui.Text(config.Destination); key = Ui.Text(config.KeyFile); fingerprint = Ui.Text(config.Fingerprint);
            port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, config.Port)), Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5) };
            auth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 5) };
            auth.Items.AddRange(new object[] { "Contraseña SSH", "Clave privada SSH" }); auth.SelectedIndex = config.Auth == "key" ? 1 : 0;
            var table = Ui.FormTable();
            Ui.Field(table, "Alias único", alias); Ui.Field(table, "IP o dominio", host); Ui.Field(table, "Puerto SSH", port);
            Ui.Field(table, "Usuario", user); Ui.Field(table, "Tipo de acceso", auth);
            var secretPanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            secretPanel.RowCount = 1; secretPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            secretPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); secretPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            secretPanel.Controls.Add(Ui.Input(password), 0, 0);
            var reveal = new ModernCheckBox { Text = "Mostrar", Dock = DockStyle.Fill, Margin = new Padding(9, 0, 0, 0), Font = new Font("Segoe UI", 9) };
            reveal.CheckedChanged += (s, e) => password.UseSystemPasswordChar = !reveal.Checked;
            secretPanel.Controls.Add(reveal, 1, 0);
            Ui.Field(table, "Contraseña / frase de clave", secretPanel);
            var keyControl = Ui.Browse(key, false); Ui.Field(table, "Archivo de clave privada", keyControl);
            Ui.Field(table, "Carpeta de origen", source); Ui.Field(table, "Destino base en el PC", Ui.Browse(destination, true));
            Ui.Field(table, "Huella SSH (opcional)", fingerprint);
            preview = Ui.Label("", 9, Ui.Blue); preview.AutoEllipsis = true;
            Ui.Field(table, "Carpeta de las copias", preview, 52);
            Ui.Field(table, "", Ui.Label("Destino vacío: usar el general. La huella SHA256 se verificará al conectar.\r\nLa contraseña se guarda protegida para tu usuario de Windows.", 9, Ui.Muted), 53);
            Action update = () => {
                keyControl.Enabled = auth.SelectedIndex == 1;
                preview.Text = Path.Combine(string.IsNullOrWhiteSpace(destination.Text) ? settings.Destination : destination.Text.Trim(), alias.Text.Trim());
            };
            alias.TextChanged += (s, e) => { try { update(); } catch { preview.Text = "Revisa la ruta y el alias."; } };
            destination.TextChanged += (s, e) => { try { update(); } catch { preview.Text = "Revisa la ruta y el alias."; } };
            auth.SelectedIndexChanged += (s, e) => update(); update();
            var save = Ui.Button("Guardar", true); var cancel = Ui.Button("Cancelar"); cancel.DialogResult = DialogResult.Cancel;
            save.Click += (s, e) => SaveResult();
            Controls.Add(table); Controls.Add(Ui.Actions(save, cancel)); AcceptButton = save; CancelButton = cancel;
        }
        void SaveResult()
        {
            var result = original == null ? new ServerConfig() : original.Clone();
            result.Alias = alias.Text.Trim(); result.Host = host.Text.Trim().Trim('[', ']'); result.Port = (int)port.Value;
            result.User = user.Text.Trim(); result.Password = password.Text; result.PasswordLoaded = true;
            result.Source = source.Text.Trim(); result.Destination = destination.Text.Trim();
            result.Auth = auth.SelectedIndex == 1 ? "key" : "password"; result.KeyFile = key.Text.Trim(); result.Fingerprint = fingerprint.Text.Trim();
            var errors = Validation.ServerErrors(result);
            if (others.Any(o => o.Id != result.Id && Validation.AliasKey(o.Alias) == Validation.AliasKey(result.Alias)))
                errors.Add("Ese alias ya pertenece a otro servidor.");
            if (errors.Count > 0) { Ui.Error(this, string.Join("\r\n", errors)); return; }
            Result = result; DialogResult = DialogResult.OK; Close();
        }
    }

    public sealed class ImportDialog : ThemedForm
    {
        readonly List<ImportedRow> rows;
        readonly List<ServerConfig> existing;
        readonly DataGridView grid = Ui.Grid();
        readonly CheckBox update;
        readonly Label summary;
        readonly Button accept;
        public List<ServerConfig> Result { get; private set; }
        public ImportDialog(string path, List<ServerConfig> servers)
        {
            rows = ServerImporter.Read(path); existing = servers;
            Text = "Importar servidores — " + Path.GetFileName(path); Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent; Size = new Size(1180, 630); MinimumSize = new Size(950, 520);
            Ui.Column(grid, "row", "Fila", 35, 42); Ui.Column(grid, "alias", "Alias", 95, 90);
            Ui.Column(grid, "host", "Servidor", 100, 90); Ui.Column(grid, "user", "Usuario", 65, 70);
            Ui.Column(grid, "source", "Origen", 110, 105); Ui.Column(grid, "destination", "Destino base", 110, 100);
            Ui.Column(grid, "access", "Acceso", 75, 70); Ui.Column(grid, "result", "Revisión", 140, 140);
            var top = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(18, 8, 18, 6), BackColor = Ui.Surface };
            summary = Ui.Label("", 10); summary.Dock = DockStyle.Top; summary.Height = 33;
            update = new ModernCheckBox { Text = "Actualizar alias existentes. Contraseña vacía: conservarla si coincide el tipo de acceso.",
                Dock = DockStyle.Bottom, Height = 30, Font = new Font("Segoe UI", 9) };
            top.Controls.Add(summary); top.Controls.Add(update);
            update.CheckedChanged += (s, e) => RefreshRows();
            accept = Ui.Button("Importar", true, 190); var cancel = Ui.Button("Cancelar"); cancel.DialogResult = DialogResult.Cancel;
            accept.Click += (s, e) => { Result = ServerImporter.Apply(rows, existing, update.Checked); DialogResult = DialogResult.OK; Close(); };
            Controls.Add(grid); Controls.Add(top); Controls.Add(Ui.Actions(accept, cancel)); CancelButton = cancel;
            RefreshRows();
        }
        void RefreshRows()
        {
            ServerImporter.ValidateRows(rows, existing, update.Checked);
            grid.Rows.Clear();
            foreach (var row in rows)
            {
                var s = row.Server;
                int index = grid.Rows.Add(row.RowNumber, s.Alias, s.Host + ":" + s.Port, s.User, s.Source,
                    string.IsNullOrWhiteSpace(s.Destination) ? "Destino general" : s.Destination,
                    s.Auth == "key" ? "Clave SSH" : string.IsNullOrEmpty(s.Password) ? "Sin contraseña" : "Contraseña incluida",
                    row.Errors.Count == 0 ? "Correcto" : string.Join(" ", row.Errors));
                if (row.Errors.Count > 0) grid.Rows[index].DefaultCellStyle.ForeColor = Ui.Red;
            }
            int errors = rows.Count(r => r.Errors.Count > 0);
            summary.Text = rows.Count + " servidores · " + errors + " filas con errores. Las contraseñas no se muestran en esta vista.";
            summary.ForeColor = errors == 0 ? Ui.Ink : Ui.Red;
            accept.Enabled = errors == 0; accept.Text = "Importar " + rows.Count + " servidores";
        }
    }

    public sealed class AccessDialog : ThemedForm
    {
        public string UserName, Secret, KeyPath;
        public bool UseKey;
        public AccessDialog(int count)
        {
            Text = "Aplicar acceso a " + count + " servidores"; Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent; Size = new Size(710, 390); MinimumSize = Size; MaximizeBox = false; MinimizeBox = false;
            var table = Ui.FormTable();
            var user = Ui.Text(); var secret = Ui.Text("", true); var key = Ui.Text();
            var auth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            auth.Items.AddRange(new object[] { "Contraseña SSH", "Clave privada SSH" }); auth.SelectedIndex = 0;
            Ui.Field(table, "Usuario (opcional)", user); Ui.Field(table, "Tipo de acceso", auth);
            Ui.Field(table, "Contraseña / frase", secret); var keyControl = Ui.Browse(key, false); Ui.Field(table, "Archivo de clave SSH", keyControl);
            Ui.Field(table, "", Ui.Label("Usuario vacío: conservar el actual. En acceso por clave, la frase puede quedar vacía.", 9, Ui.Muted), 48);
            auth.SelectedIndexChanged += (s, e) => keyControl.Enabled = auth.SelectedIndex == 1; keyControl.Enabled = false;
            var apply = Ui.Button("Aplicar acceso", true, 150); var cancel = Ui.Button("Cancelar"); cancel.DialogResult = DialogResult.Cancel;
            apply.Click += (s, e) => {
                if (auth.SelectedIndex == 1 && !File.Exists(key.Text)) { Ui.Error(this, "Selecciona un archivo de clave privada que exista."); return; }
                if (auth.SelectedIndex == 0 && secret.Text.Length == 0) { Ui.Error(this, "Introduce la contraseña que quieres aplicar."); return; }
                UserName = user.Text.Trim(); Secret = secret.Text; KeyPath = key.Text.Trim(); UseKey = auth.SelectedIndex == 1;
                DialogResult = DialogResult.OK; Close();
            };
            Controls.Add(table); Controls.Add(Ui.Actions(apply, cancel)); AcceptButton = apply; CancelButton = cancel;
        }
    }

    public sealed class HostTrustDialog : ThemedForm
    {
        public HostTrustDialog(ServerConfig server, string algorithm, string fingerprint)
        {
            Text = "Identidad SSH de " + server.Alias; Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent; Size = new Size(750, 385); MinimumSize = Size; MaximizeBox = false; MinimizeBox = false;
            var table = Ui.FormTable();
            Ui.Field(table, "Servidor", Ui.Label(server.Host + ":" + server.Port + " (" + server.Alias + ")"));
            Ui.Field(table, "Tipo de clave", Ui.Label(algorithm));
            var value = Ui.Text(fingerprint); value.ReadOnly = true; Ui.Field(table, "Huella SHA256", value, 52);
            Ui.Field(table, "", Ui.Label("Es la primera conexión a este servidor. Compara la huella con la del servidor o con una fuente que conozcas. Si la aceptas, quedará guardada para las siguientes conexiones.", 10, Ui.Muted), 100);
            var accept = Ui.Button("Confiar y guardar", true, 165); accept.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Cancelar"); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(table); Controls.Add(Ui.Actions(accept, cancel)); CancelButton = cancel;
        }
    }

    public sealed class ExportDialog : ThemedForm
    {
        readonly ComboBox format = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly CheckBox passwords = new ModernCheckBox { Text = "Incluir contraseñas", AutoSize = true };
        public bool IncludePasswords { get { return passwords.Checked; } }
        public string Extension { get { return format.SelectedIndex == 1 ? ".csv" : ".xlsx"; } }
        public ExportDialog(int count)
        {
            Text = "Exportar listado de servidores"; Font = new Font("Segoe UI", 10);
            StartPosition = FormStartPosition.CenterParent; Size = new Size(760, 440);
            MinimumSize = Size; MaximizeBox = false; MinimizeBox = false;
            var table = Ui.FormTable(); table.ColumnStyles[0].Width = 155;
            format.Items.AddRange(new object[] { "Excel (.xlsx)", "CSV UTF-8 (.csv)" }); format.SelectedIndex = 0;
            Ui.Field(table, "Listado completo", Ui.Label(count + " servidores, incluidos los que no están marcados para copiar."), 48);
            Ui.Field(table, "Formato del archivo", format, 45);
            Ui.Field(table, "Acceso", passwords, 42);
            Ui.Field(table, "", Ui.Label("Si incluyes las contraseñas, quedarán visibles en el archivo exportado.", 9, Ui.Muted), 45);
            Ui.Field(table, "Al cambiar de equipo", Ui.Label("Se exporta el destino efectivo de cada servidor. Revisa las rutas en el nuevo PC. Los archivos de clave privada se trasladan por separado.", 9, Ui.Muted), 76);
            var save = Ui.Button("Guardar listado…", true, 165); save.DialogResult = DialogResult.OK;
            var cancel = Ui.Button("Cancelar"); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(table); Controls.Add(Ui.Actions(save, cancel)); AcceptButton = save; CancelButton = cancel;
        }
    }

    public sealed class HelpDialog : ThemedForm
    {
        public HelpDialog(string text, string title = "Cómo usar SSH Backup")
        {
            Text = title; Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 650);
            var body = new TextBox { Multiline = true, ReadOnly = true, Text = text.Replace("\n", "\r\n").Replace("\r\r", "\r"),
                Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Ui.Surface, ForeColor = Ui.Ink, Font = new Font("Segoe UI", 10) };
            var panel = new Panel { Padding = new Padding(22), Dock = DockStyle.Fill, BackColor = Ui.Surface };
            panel.Controls.Add(body); var close = Ui.Button("Cerrar"); close.DialogResult = DialogResult.OK;
            Controls.Add(panel); Controls.Add(Ui.Actions(close)); CancelButton = close;
        }
    }
}
