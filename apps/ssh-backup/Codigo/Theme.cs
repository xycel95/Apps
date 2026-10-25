using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SSHBackup
{
    // All application-owned windows share the user's saved appearance.
    public class ThemedForm : Form
    {
        protected override void OnLoad(EventArgs e)
        {
            Ui.ApplyTheme(this);
            base.OnLoad(e);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyWindowFrame(this);
        }
    }

    public static partial class Ui
    {
        sealed class Palette
        {
            public Color Background, Surface, Log, Header, Selection, Ink, Muted, Blue, Border,
                Green, Red, Warning, Progress, Primary, Hover;
        }
        static readonly Palette light = new Palette {
            Background = Color.FromArgb(244, 247, 252), Surface = Color.White,
            Log = Color.FromArgb(248, 250, 253), Header = Color.FromArgb(247, 249, 252),
            Selection = Color.FromArgb(231, 239, 255), Ink = Color.FromArgb(25, 37, 58),
            Muted = Color.FromArgb(84, 101, 125), Blue = Color.FromArgb(34, 93, 221),
            Border = Color.FromArgb(218, 226, 237), Green = Color.FromArgb(22, 117, 81),
            Red = Color.FromArgb(181, 39, 65), Warning = Color.FromArgb(132, 91, 19),
            Progress = Color.FromArgb(228, 233, 242), Primary = Color.FromArgb(34, 93, 221),
            Hover = Color.FromArgb(237, 242, 251)
        };
        static readonly Palette dark = new Palette {
            Background = Color.FromArgb(13, 23, 39), Surface = Color.FromArgb(23, 36, 55),
            Log = Color.FromArgb(17, 29, 46), Header = Color.FromArgb(28, 43, 64),
            Selection = Color.FromArgb(29, 57, 94), Ink = Color.FromArgb(235, 241, 252),
            Muted = Color.FromArgb(170, 187, 211), Blue = Color.FromArgb(123, 183, 255),
            Border = Color.FromArgb(49, 68, 93), Green = Color.FromArgb(115, 216, 168),
            Red = Color.FromArgb(255, 141, 163), Warning = Color.FromArgb(244, 195, 107),
            Progress = Color.FromArgb(45, 60, 82), Primary = Color.FromArgb(48, 128, 247),
            Hover = Color.FromArgb(35, 51, 74)
        };
        static Palette palette = light;
        internal const string PrimaryButtonTag = "SSHBackup.PrimaryButton";
        internal const string LogTag = "SSHBackup.Log";
        public static bool DarkMode { get { return palette == dark; } }
        public static void SetDarkMode(bool enabled) { palette = enabled ? dark : light; }
        public static Color Background { get { return palette.Background; } }
        public static Color Surface { get { return palette.Surface; } }
        public static Color LogBackground { get { return palette.Log; } }
        public static Color Header { get { return palette.Header; } }
        public static Color Selection { get { return palette.Selection; } }
        public static Color Ink { get { return palette.Ink; } }
        public static Color Muted { get { return palette.Muted; } }
        public static Color Blue { get { return palette.Blue; } }
        public static Color Border { get { return palette.Border; } }
        public static Color Green { get { return palette.Green; } }
        public static Color Red { get { return palette.Red; } }
        public static Color Warning { get { return palette.Warning; } }
        public static Color ProgressTrack { get { return palette.Progress; } }
        public static Color Primary { get { return palette.Primary; } }
        public static Color Hover { get { return palette.Hover; } }

        static bool Matches(Color color, Color a, Color b)
        { return color.ToArgb() == a.ToArgb() || color.ToArgb() == b.ToArgb(); }
        static Color Foreground(Color color)
        {
            if (color.IsEmpty) return color;
            if (Matches(color, light.Muted, dark.Muted)) return Muted;
            if (Matches(color, light.Blue, dark.Blue)) return Blue;
            if (Matches(color, light.Red, dark.Red)) return Red;
            if (Matches(color, light.Green, dark.Green)) return Green;
            if (Matches(color, light.Warning, dark.Warning)) return Warning;
            return Ink;
        }
        static Color BackgroundFor(Control control)
        {
            Color color = control.BackColor;
            if (color == Color.Transparent) return color;
            if (Matches(color, light.Background, dark.Background)) return Background;
            if (Matches(color, light.Surface, dark.Surface)) return Surface;
            if (Matches(color, light.Log, dark.Log)) return LogBackground;
            if (Matches(color, light.Header, dark.Header)) return Header;
            if (Matches(color, light.Selection, dark.Selection)) return Selection;
            if (Matches(color, light.Progress, dark.Progress)) return ProgressTrack;
            return control.Parent == null ? Background : control.Parent.BackColor;
        }
        public static void ApplyTheme(Control root)
        {
            root.SuspendLayout();
            try { ApplyControl(root); }
            finally { root.ResumeLayout(true); root.Invalidate(true); }
        }
        static void ApplyControl(Control control)
        {
            if (control.IsDisposed) return;
            var grid = control as DataGridView;
            var button = control as Button;
            var combo = control as ComboBox;
            if (control is Form)
            {
                control.BackColor = Background; control.ForeColor = Ink;
                ApplyWindowFrame((Form)control);
            }
            else if (grid != null)
            {
                grid.BackgroundColor = Surface; grid.BackColor = Surface; grid.ForeColor = Ink;
                grid.GridColor = Border; grid.EnableHeadersVisualStyles = false;
                grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = Ink;
                grid.DefaultCellStyle.SelectionBackColor = Selection; grid.DefaultCellStyle.SelectionForeColor = Ink;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Header; grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Header; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
                grid.RowsDefaultCellStyle.SelectionBackColor = Selection; grid.RowsDefaultCellStyle.SelectionForeColor = Ink;
                foreach (DataGridViewRow row in grid.Rows)
                    if (!row.DefaultCellStyle.ForeColor.IsEmpty)
                        row.DefaultCellStyle.ForeColor = Foreground(row.DefaultCellStyle.ForeColor);
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var check = column as DataGridViewCheckBoxColumn;
                    if (check != null) check.FlatStyle = FlatStyle.Flat;
                }
            }
            else if (button != null)
            {
                bool primary = (button.Tag as string) == PrimaryButtonTag;
                button.BackColor = primary ? Primary : Surface; button.ForeColor = primary ? Color.White : Foreground(button.ForeColor);
                button.UseVisualStyleBackColor = false;
                button.FlatAppearance.BorderColor = primary ? Primary : Border;
                button.FlatAppearance.MouseOverBackColor = primary ? Primary : Hover;
                button.FlatAppearance.MouseDownBackColor = primary ? Primary : Selection;
            }
            else if (combo != null)
            {
                combo.BackColor = Surface; combo.ForeColor = Ink; combo.FlatStyle = FlatStyle.Flat;
                if (combo.DrawMode != DrawMode.OwnerDrawFixed)
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += (s, e) => {
                        bool selected = (e.State & DrawItemState.Selected) != 0;
                        using (var background = new SolidBrush(selected ? Selection : Surface))
                            e.Graphics.FillRectangle(background, e.Bounds);
                        string text = e.Index < 0 ? combo.Text : combo.GetItemText(combo.Items[e.Index]);
                        TextRenderer.DrawText(e.Graphics, text, e.Font ?? combo.Font, e.Bounds,
                            combo.Enabled ? Ink : Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        e.DrawFocusRectangle();
                    };
                }
            }
            else if (control is TextBoxBase || control is NumericUpDown)
            {
                control.BackColor = (control.Tag as string) == LogTag ? LogBackground : Surface;
                control.ForeColor = Ink;
            }
            else if (control is TabPage)
            {
                ((TabPage)control).UseVisualStyleBackColor = false;
                control.BackColor = Background; control.ForeColor = Ink;
            }
            else if (control is TabControl)
            { control.BackColor = Background; control.ForeColor = Ink; }
            else if (control is DateTimePicker)
            { control.BackColor = Surface; control.ForeColor = Ink; }
            else if (control is FlatProgress)
            { control.BackColor = ProgressTrack; }
            else
            {
                control.BackColor = BackgroundFor(control);
                control.ForeColor = Foreground(control.ForeColor);
                var check = control as CheckBox;
                if (check != null) { check.FlatStyle = FlatStyle.Flat; check.UseVisualStyleBackColor = false; }
                var link = control as LinkLabel;
                if (link != null)
                { link.LinkColor = Blue; link.ActiveLinkColor = Blue; link.VisitedLinkColor = Blue; link.DisabledLinkColor = Muted; }
            }
            // Grid scroll bars and numeric text boxes inherit their parent's new colors.
            foreach (Control child in control.Controls) ApplyControl(child);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        public static void ApplyWindowFrame(Form window)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || !window.IsHandleCreated) return;
            try
            {
                int enabled = DarkMode ? 1 : 0;
                DwmSetWindowAttribute(window.Handle, 20, ref enabled, sizeof(int));
                // Windows 11 supports explicit caption/text colors. Earlier systems ignore these attributes.
                int caption = DarkMode ? ColorTranslator.ToWin32(Background) : -1;
                int text = DarkMode ? ColorTranslator.ToWin32(Ink) : -1;
                DwmSetWindowAttribute(window.Handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(window.Handle, 36, ref text, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
