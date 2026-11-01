using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Guarda
{
    internal static class Theme
    {
        public static bool Light;
        public static Color Background { get { return Light ? Color.FromArgb(243, 246, 250) : Color.FromArgb(14, 19, 29); } }
        public static Color Card { get { return Light ? Color.White : Color.FromArgb(24, 32, 45); } }
        public static Color Input { get { return Light ? Color.FromArgb(245, 248, 251) : Color.FromArgb(16, 24, 36); } }
        public static Color Text { get { return Light ? Color.FromArgb(25, 39, 56) : Color.FromArgb(236, 242, 250); } }
        public static Color Muted { get { return Light ? Color.FromArgb(91, 111, 132) : Color.FromArgb(152, 172, 195); } }
        public static Color Accent { get { return Light ? Color.FromArgb(0, 119, 99) : Color.FromArgb(88, 221, 191); } }
        public static Color Primary { get { return Color.FromArgb(82, 218, 186); } }
        public static Color Line { get { return Light ? Color.FromArgb(222, 230, 238) : Color.FromArgb(43, 56, 73); } }
        public static Color Tint { get { return Light ? Color.FromArgb(229, 245, 240) : Color.FromArgb(22, 48, 48); } }
        public static Color Warning { get { return Light ? Color.FromArgb(158, 80, 13) : Color.FromArgb(255, 194, 112); } }
        public static Font Font(float size, bool bold) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }
        public static Label Label(string text, float size = 10, string role = "text", bool bold = false)
        {
            return new Label { Text = text, Font = Font(size, bold), Tag = role, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty, BackColor = Color.Transparent };
        }
        public static ModernButton Button(string text, bool primary = false) { return new ModernButton { Text = text, Primary = primary, Height = 42, Font = Font(10, true), Cursor = Cursors.Hand }; }
        public static Color Role(string role)
        {
            switch (role) { case "muted": return Muted; case "accent": return Accent; case "warning": return Warning; case "page": return Background; case "input": return Input; case "tint": return Tint; default: return Text; }
        }
        public static void Apply(Control root)
        {
            string role = root.Tag as string;
            if (root is Form) { root.BackColor = Background; root.ForeColor = Text; }
            else if (root is ModernButton || root is RoundCard || root is Logo || root is ProgressTrack) root.Invalidate();
            else if (root is TextBox) { root.BackColor = Input; root.ForeColor = Text; }
            else if (root is Label) root.ForeColor = Role(role);
            else if (root is CheckBox) { root.ForeColor = Text; root.BackColor = Color.Transparent; }
            else { root.BackColor = role == "card" ? Card : (role == "tint" ? Tint : Background); root.ForeColor = Text; }
            foreach (Control child in root.Controls) Apply(child);
        }
        public static GraphicsPath Rounded(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(rectangle.X, rectangle.Y, d, d, 180, 90); path.AddArc(rectangle.Right - d, rectangle.Y, d, d, 270, 90);
            path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90); path.AddArc(rectangle.X, rectangle.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }

    internal sealed class RoundCard : Panel
    {
        public bool Tinted;
        public RoundCard() { DoubleBuffered = true; Padding = new Padding(18); Dock = DockStyle.Fill; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 14))
            using (Brush fill = new SolidBrush(Tinted ? Theme.Tint : Theme.Card))
            using (Pen pen = new Pen(Tinted ? Theme.Accent : Theme.Line)) { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape); }
        }
    }

    internal sealed class ModernButton : Button
    {
        public bool Primary;
        private bool hover;
        public ModernButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = Primary ? Theme.Primary : Theme.Card;
            if (hover && Enabled) bg = Primary ? Color.FromArgb(111, 234, 204) : Theme.Input;
            if (!Enabled) bg = Theme.Input;
            using (GraphicsPath shape = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 9))
            using (Brush brush = new SolidBrush(bg))
            using (Pen line = new Pen(Primary && Enabled ? Theme.Primary : Theme.Line)) { e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(line, shape); }
            Color text = !Enabled ? Theme.Muted : (Primary ? Color.FromArgb(13, 43, 37) : Theme.Text);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), text, bg);
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
        public Logo() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Size = new Size(48, 48); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(Width, Height) / 58f; e.Graphics.ScaleTransform(s, s);
            using (GraphicsPath shape = Theme.Rounded(new RectangleF(0, 0, 57, 57), 15))
            using (Brush bg = new SolidBrush(Theme.Tint)) e.Graphics.FillPath(bg, shape);
            using (Pen p = new Pen(Theme.Accent, 2.5f)) { p.LineJoin = LineJoin.Round;
                e.Graphics.DrawLines(p, new[] { new Point(13, 39), new Point(13, 19), new Point(25, 19), new Point(29, 24), new Point(44, 24), new Point(44, 39), new Point(13, 39) });
                e.Graphics.DrawLines(p, new[] { new Point(25, 31), new Point(29, 35), new Point(36, 28) }); }
        }
    }
}
