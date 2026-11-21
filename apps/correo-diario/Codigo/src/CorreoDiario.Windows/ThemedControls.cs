using System.Windows.Forms.VisualStyles;

namespace CorreoDiario.Windows;

internal sealed class ThemedButton : Button
{
    private bool hovered, pressed;
    public ThemedButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var background = Enabled ? BackColor : Color.FromArgb(34, 46, 64);
        if (Enabled && pressed) background = ControlPaint.Dark(background, .12F);
        else if (Enabled && hovered) background = ControlPaint.Light(background, .08F);
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, ClientRectangle);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            Enabled ? ForeColor : Color.FromArgb(160, 176, 196),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; base.OnMouseDown(e); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}

internal sealed class ThemedCheckBox : CheckBox
{
    public ThemedCheckBox() { FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Enabled) return;
        var width = CheckBoxRenderer.GetGlyphSize(e.Graphics, CheckBoxState.UncheckedNormal).Width;
        var text = new Rectangle(width + 5, 0, Math.Max(0, Width - width - 5), Height);
        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, text);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Color.FromArgb(160, 176, 196),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
