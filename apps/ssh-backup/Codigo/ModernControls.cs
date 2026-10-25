using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SSHBackup
{
    // Keep the native Button/CheckBox interaction and accessibility semantics.
    // Only their appearance is drawn here; no additional UI runtime is needed.
    public sealed class ModernButton : Button
    {
        bool hover, pressed;
        public bool Selected { get; set; }
        public string IconKey { get; set; }
        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Ui.Background : Parent.BackColor);
            bool primary = (Tag as string) == Ui.PrimaryButtonTag;
            bool navigation = !string.IsNullOrEmpty(IconKey);
            Color fill = Selected ? Ui.Selection : primary ? Ui.Primary : hover && Enabled ? Ui.Hover : navigation ? Ui.Surface : BackColor;
            if (pressed && Enabled) fill = primary ? Ui.PrimaryPressed : Ui.Selection;
            if (!Enabled) fill = Ui.Header;
            var bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (var path = Ui.Rounded(bounds, 9))
            {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (!navigation && !primary || Focused && ShowFocusCues)
                    using (var pen = new Pen(Focused && ShowFocusCues ? Ui.Blue : Ui.Border)) g.DrawPath(pen, path);
            }
            Color ink = !Enabled ? Ui.Muted : primary ? Color.White : Selected ? Ui.Blue : ForeColor;
            var text = new Rectangle(10, 0, Width - 20, Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine;
            if (navigation)
            {
                Ui.DrawIcon(g, IconKey, new Rectangle(16, (Height - 21) / 2, 21, 21), ink);
                text = new Rectangle(48, 0, Width - 56, Height); flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            }
            TextRenderer.DrawText(g, Text, Font, text, ink, flags);
        }
    }

    public sealed class ModernCheckBox : CheckBox
    {
        public bool Toggle { get; set; }
        public bool DayCard { get; set; }
        bool hover;
        public ModernCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            Size text = TextRenderer.MeasureText(Text, Font);
            return new Size(text.Width + (Toggle ? 51 : 29), Math.Max(text.Height + 6, Toggle ? 28 : 26));
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Parent == null ? Ui.Surface : Parent.BackColor;
            g.Clear(fill);
            int inset = DayCard ? 12 : 1;
            if (DayCard)
            {
                fill = Checked ? Ui.Selection : hover ? Ui.Hover : Ui.Header;
                using (var path = Ui.Rounded(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), 9))
                {
                    using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                    using (var pen = new Pen(Checked ? Ui.Primary : Ui.Border)) g.DrawPath(pen, path);
                }
            }
            Color ink = Enabled ? Checked && DayCard ? Ui.Blue : Ui.Ink : Ui.Muted;
            int iconWidth = Toggle ? 37 : 18, iconHeight = Toggle ? 21 : 18;
            var icon = new Rectangle(inset, (Height - iconHeight) / 2, iconWidth, iconHeight);
            using (var path = Ui.Rounded(icon, Toggle ? 10 : 4))
            {
                using (var brush = new SolidBrush(Checked && Enabled ? Ui.Primary : Toggle ? Ui.Border : fill)) g.FillPath(brush, path);
                if (!Toggle) using (var pen = new Pen(Checked ? Ui.Primary : Ui.Muted, 1.3f)) g.DrawPath(pen, path);
            }
            if (Toggle)
                using (var brush = new SolidBrush(Color.White)) g.FillEllipse(brush, icon.X + (Checked ? 18 : 3), icon.Y + 3, 15, 15);
            else if (Checked)
                using (var pen = new Pen(Color.White, 2)) g.DrawLines(pen, new[] { new Point(icon.X + 4, icon.Y + 9), new Point(icon.X + 8, icon.Y + 13), new Point(icon.X + 14, icon.Y + 5) });
            var text = new Rectangle(icon.Right + 9, 0, Math.Max(1, Width - icon.Right - 13), Height);
            TextRenderer.DrawText(g, Text, Font, text, ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(Ui.Blue)) { pen.DashStyle = DashStyle.Dot; g.DrawRectangle(pen, 3, 3, Math.Max(1, Width - 7), Math.Max(1, Height - 7)); }
        }
    }

    public class ModernPanel : Panel
    {
        public bool Tinted { get; set; }
        public ModernPanel()
        {
            BackColor = Ui.Surface;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Ui.Background : Parent.BackColor);
            using (var path = Ui.Rounded(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12))
            {
                using (var brush = new SolidBrush(Tinted ? Ui.Selection : BackColor)) g.FillPath(brush, path);
                using (var pen = new Pen(Tinted ? Ui.Primary : Ui.Border)) g.DrawPath(pen, path);
            }
        }
    }

    public sealed class ModernTabs : Panel
    {
        readonly List<Control> pages = new List<Control>();
        readonly List<ModernButton> buttons = new List<ModernButton>();
        readonly FlowLayoutPanel strip;
        readonly Panel content;
        int selected;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < 0 || value >= pages.Count) throw new ArgumentOutOfRangeException("value");
                selected = value;
                for (int i = 0; i < pages.Count; i++) { pages[i].Visible = i == value; buttons[i].Selected = i == value; buttons[i].Invalidate(); }
                pages[value].BringToFront();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        public ModernTabs()
        {
            Dock = DockStyle.Fill;
            content = new Panel { Dock = DockStyle.Fill };
            strip = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, WrapContents = false, Padding = new Padding(0, 3, 0, 12) };
            Controls.Add(content); Controls.Add(strip);
        }
        public void AddPage(string title, Control page, int width)
        {
            int index = pages.Count; pages.Add(page); page.Dock = DockStyle.Fill; content.Controls.Add(page);
            var button = (ModernButton)Ui.Button(title, false, width); button.Height = 42; button.AccessibleName = title;
            button.Click += (s, e) => SelectedIndex = index;
            buttons.Add(button); strip.Controls.Add(button); SelectedIndex = 0;
        }
    }

    public static partial class Ui
    {
        public static Color PrimaryPressed { get { return Color.FromArgb(28, 102, 211); } }
        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath(); int d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void DrawIcon(Graphics g, string key, Rectangle r, Color color)
        {
            var state = g.Save(); g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 24f, r.Height / 24f);
            using (var p = new Pen(color, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (key == "servers" || key == "logo")
                    for (int i = 0; i < 3; i++) { g.DrawRectangle(p, 3, 3 + i * 7, 18, 5); g.DrawLine(p, 6, 5 + i * 7, 8, 5 + i * 7); }
                else if (key == "copies") { g.DrawRectangle(p, 7, 6, 13, 15); g.DrawLine(p, 4, 17, 4, 3); g.DrawLine(p, 4, 3, 16, 3); g.DrawLine(p, 10, 11, 17, 11); g.DrawLine(p, 10, 15, 17, 15); }
                else if (key == "history") { g.DrawArc(p, 3, 3, 18, 18, 220, 315); g.DrawLine(p, 3, 3, 3, 8); g.DrawLine(p, 3, 8, 8, 8); g.DrawLine(p, 12, 6, 12, 12); g.DrawLine(p, 12, 12, 16, 14); }
                else if (key == "tools") { g.DrawArc(p, 11, 2, 10, 10, 35, 285); g.DrawLine(p, 12, 10, 3, 19); g.DrawLine(p, 3, 19, 6, 22); g.DrawLine(p, 6, 22, 15, 13); }
                else if (key == "settings") { g.DrawEllipse(p, 4, 4, 16, 16); g.DrawEllipse(p, 9, 9, 6, 6); for (int i = 0; i < 8; i++) { double a = Math.PI * i / 4; g.DrawLine(p, 12 + (float)Math.Cos(a) * 8, 12 + (float)Math.Sin(a) * 8, 12 + (float)Math.Cos(a) * 11, 12 + (float)Math.Sin(a) * 11); } }
                else { g.DrawEllipse(p, 3, 3, 18, 18); g.DrawArc(p, 9, 7, 6, 5, 180, 235); g.DrawLine(p, 12, 12, 12, 14); g.DrawLine(p, 12, 17, 12, 17.5f); }
            }
            g.Restore(state);
        }
        public static Control Input(Control control)
        {
            var frame = new ModernPanel { Padding = new Padding(10, 6, 10, 5), Margin = new Padding(0, 4, 0, 4), Dock = DockStyle.Fill, Size = new Size(200, 34) };
            var text = control as TextBox;
            if (text != null) text.BorderStyle = BorderStyle.None;
            var number = control as NumericUpDown; if (number != null) number.BorderStyle = BorderStyle.None;
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0); frame.Controls.Add(control);
            return frame;
        }
    }
}
