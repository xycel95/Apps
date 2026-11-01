using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Guarda
{
    public sealed partial class MainForm : Form
    {
        private ModernButton themeToggle, updatesButton;

        private TableLayoutPanel Grid(int columns, int rows, string role = "page")
        {
            return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, Tag = role, BackColor = role == "card" ? Theme.Card : (role == "tint" ? Theme.Tint : Theme.Background) };
        }

        private void Build()
        {
            TableLayoutPanel page = Grid(1, 7);
            page.Padding = new Padding(26, 18, 26, 12); page.AutoScroll = true;
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int h in new[] { 70, 70, 316, 124, 56, 26 }) page.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(page);

            TableLayoutPanel header = Grid(3, 1);
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 405));
            header.Controls.Add(new Logo { Margin = new Padding(0, 0, 0, 12), Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 0);
            header.Controls.Add(Theme.Label("Guarda", 25, "text", true), 1, 0);
            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Tag = "page", Padding = new Padding(0, 9, 0, 0), Margin = Padding.Empty };
            updatesButton = Theme.Button("Actualizaciones"); updatesButton.Size = new Size(146, 38); updatesButton.Margin = new Padding(8, 0, 0, 0); updatesButton.Click += OpenUpdates;
            themeToggle = Theme.Button("Tema claro"); themeToggle.Size = new Size(108, 38); themeToggle.Margin = new Padding(8, 0, 0, 0); themeToggle.Click += (s, e) => { Theme.Light = !Theme.Light; ApplyAppearance(); Save(); };
            Label version = Theme.Label("v" + Updates.CurrentVersion + "   PORTABLE", 8, "muted", true); version.Size = new Size(129, 38); version.Dock = DockStyle.None; version.TextAlign = ContentAlignment.MiddleRight;
            tools.Controls.Add(updatesButton); tools.Controls.Add(themeToggle); tools.Controls.Add(version); header.Controls.Add(tools, 2, 0); page.Controls.Add(header, 0, 0);

            TableLayoutPanel intro = Grid(1, 2); intro.RowStyles.Add(new RowStyle(SizeType.Absolute, 37)); intro.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            intro.Controls.Add(Theme.Label("Copia de seguridad", 22, "text", true), 0, 0);
            intro.Controls.Add(Theme.Label("De una carpeta a otra, con fecha y verificación.", 10, "muted"), 0, 1); page.Controls.Add(intro, 0, 1);

            TableLayoutPanel workspace = Grid(2, 1); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            TableLayoutPanel folders = Grid(1, 3); folders.Margin = new Padding(0, 0, 16, 0);
            folders.RowStyles.Add(new RowStyle(SizeType.Absolute, 112)); folders.RowStyles.Add(new RowStyle(SizeType.Absolute, 112)); folders.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            folders.Controls.Add(FolderCard("01", "Carpeta de origen", "Lo que quieres guardar", out source, out sourceBrowse), 0, 0);
            folders.Controls.Add(FolderCard("02", "Carpeta de destino", "Pendrive, disco u otra carpeta", out destination, out destinationBrowse), 0, 1);
            source.AccessibleName = "Carpeta de origen"; destination.AccessibleName = "Carpeta de destino";
            sourceBrowse.Click += (s, e) => Choose(source, "Selecciona la carpeta que quieres copiar");
            destinationBrowse.Click += (s, e) => Choose(destination, "Selecciona el pendrive, disco o carpeta de destino");
            source.TextChanged += (s, e) => { if (!updatingName && !Busy) { updatingName = true; copyName.Text = BackupEngine.DefaultName(source.Text); updatingName = false; } UpdatePreview(); };
            destination.TextChanged += (s, e) => UpdatePreview();
            RoundCard verification = new RoundCard { Padding = new Padding(16, 11, 16, 10), Margin = new Padding(0, 0, 0, 0) };
            TableLayoutPanel verificationBody = Grid(1, 2, "card"); verificationBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); verificationBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            verify = new CheckBox { Text = "Verificar al terminar", Dock = DockStyle.Fill, Checked = true, Font = Theme.Font(10, true), Cursor = Cursors.Hand, Margin = Padding.Empty, AccessibleName = "Verificar contenido" };
            verificationBody.Controls.Add(verify, 0, 0); verificationBody.Controls.Add(Theme.Label("Tarda algo más: comprueba el contenido de cada archivo.", 8.5f, "muted"), 0, 1);
            verification.Controls.Add(verificationBody); folders.Controls.Add(verification, 0, 2); workspace.Controls.Add(folders, 0, 0);

            RoundCard summary = new RoundCard { Tinted = true, Padding = new Padding(18, 14, 18, 12), Margin = Padding.Empty };
            TableLayoutPanel contents = Grid(1, 9, "tint");
            foreach (int height in new[] { 28, 22, 35, 14, 22, 60, 22, 34, 24 }) contents.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            contents.Controls.Add(Theme.Label("Tu próxima copia", 14, "text", true), 0, 0);
            contents.Controls.Add(Theme.Label("Nombre de la carpeta", 8.5f, "muted"), 0, 1);
            copyName = Input(); copyName.AccessibleName = "Nombre de la copia"; copyName.TextChanged += (s, e) => UpdatePreview(); contents.Controls.Add(copyName, 0, 2);
            contents.Controls.Add(Theme.Label("Se guardará como", 8.5f, "muted"), 0, 4);
            preview = Theme.Label("", 14, "accent", true); preview.TextAlign = ContentAlignment.TopLeft; contents.Controls.Add(preview, 0, 5);
            contents.Controls.Add(Theme.Label("Dentro de", 8.5f, "muted"), 0, 6);
            destinationHint = Theme.Label("Elige un destino", 9, "text"); contents.Controls.Add(destinationHint, 0, 7);
            contents.Controls.Add(Theme.Label("Nueva carpeta en cada copia", 8.5f, "muted"), 0, 8);
            summary.Controls.Add(contents); workspace.Controls.Add(summary, 1, 0); page.Controls.Add(workspace, 0, 2);

            RoundCard progress = new RoundCard { Padding = new Padding(18, 12, 18, 10), Margin = new Padding(0, 14, 0, 4) };
            TableLayoutPanel p = Grid(2, 4, "card"); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            foreach (int height in new[] { 25, 23, 13, 22 }) p.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            status = Theme.Label("Todo preparado", 11, "text", true); percent = Theme.Label("", 10, "accent", true); percent.TextAlign = ContentAlignment.MiddleRight;
            detail = Theme.Label("Elige las carpetas para comenzar.", 9, "muted");
            bar = new ProgressTrack { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            stats = Theme.Label("Una copia completa e independiente de tus archivos.", 8.5f, "muted");
            p.Controls.Add(status, 0, 0); p.Controls.Add(percent, 1, 0); p.Controls.Add(detail, 0, 1); p.SetColumnSpan(detail, 2); p.Controls.Add(bar, 0, 2); p.SetColumnSpan(bar, 2); p.Controls.Add(stats, 0, 3); p.SetColumnSpan(stats, 2); progress.Controls.Add(p); page.Controls.Add(progress, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Tag = "page", Margin = Padding.Empty, Padding = new Padding(0, 7, 0, 0) };
            start = Theme.Button("Crear copia", true); start.Width = 166; start.Margin = new Padding(0, 0, 10, 0); start.Click += StartClick;
            cancel = Theme.Button("Cancelar"); cancel.Width = 105; cancel.Margin = new Padding(0, 0, 10, 0); cancel.Enabled = false; cancel.Click += (s, e) => Cancel();
            openCopy = Theme.Button("Abrir copia"); openCopy.Width = 126; openCopy.Margin = new Padding(0, 0, 10, 0); openCopy.Enabled = false; openCopy.Click += (s, e) => Open(lastResult == null ? "" : lastResult.OutputPath);
            openReport = Theme.Button("Ver informe"); openReport.Width = 126; openReport.Enabled = false; openReport.Margin = Padding.Empty; openReport.Click += (s, e) => Open(lastResult == null ? "" : lastResult.ReportPath);
            actions.Controls.AddRange(new Control[] { start, cancel, openCopy, openReport }); page.Controls.Add(actions, 0, 4);
            page.Controls.Add(Theme.Label("Carpetas · Unidades USB · Discos externos · Carpetas de red", 8, "muted"), 0, 5);
            AcceptButton = start;
        }

        private Control FolderCard(string number, string title, string hint, out TextBox path, out Button browse)
        {
            RoundCard card = new RoundCard { Padding = new Padding(16, 10, 16, 10), Margin = new Padding(0, 0, 0, 10) };
            TableLayoutPanel body = Grid(3, 2, "card"); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            body.Controls.Add(Theme.Label(number, 9, "accent", true), 0, 0); body.Controls.Add(Theme.Label(title, 11, "text", true), 1, 0);
            path = Input(); path.AllowDrop = true; path.Margin = new Padding(0, 5, 10, 0); body.Controls.Add(path, 0, 1); body.SetColumnSpan(path, 2);
            browse = Theme.Button("Elegir…"); browse.Dock = DockStyle.Fill; browse.Margin = Padding.Empty; body.Controls.Add(browse, 2, 1);
            TextBox box = path; box.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            box.DragDrop += (s, e) => { string[] items = e.Data.GetData(DataFormats.FileDrop) as string[]; if (items != null && items.Length == 1 && Directory.Exists(items[0])) box.Text = items[0]; };
            tips.SetToolTip(box, hint + ". Puedes pegar una ruta o arrastrar una carpeta."); card.Controls.Add(body); return card;
        }

        private TextBox Input() { return new TextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Font(10, false), Margin = new Padding(0, 4, 0, 3), MaxLength = 32000 }; }

        private void ApplyAppearance()
        {
            Theme.Apply(this); themeToggle.Text = Theme.Light ? "Modo oscuro" : "Tema claro";
            if (lastResult != null) status.ForeColor = lastResult.Success ? Theme.Accent : Theme.Warning;
            try { if (BackupEngine.Windows && IsHandleCreated) { int dark = Theme.Light ? 0 : 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); } } catch { }
            Invalidate(true);
        }

        private void OpenUpdates(object sender, EventArgs args)
        {
            if (Busy) return;
            Save();
            using (UpdateForm form = new UpdateForm()) if (form.ShowDialog(this) == DialogResult.OK) Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            ApplyAppearance();
        }

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    }
}
