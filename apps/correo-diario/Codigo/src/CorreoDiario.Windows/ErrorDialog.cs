using System.Runtime.InteropServices;
using CorreoDiario.Core;

namespace CorreoDiario.Windows;

internal sealed class ErrorDialog : Form
{
    public ErrorDialog(string report)
    {
        Text = "CorreoDiario · Error";
        Size = new(780, 540);
        MinimumSize = new(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        Font = new("Segoe UI", 10F);
        BackColor = Color.FromArgb(17, 24, 39);
        ForeColor = Color.FromArgb(235, 241, 250);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(18), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.Absolute, 36));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 32));
        layout.RowStyles.Add(new(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { Text = "Puedes copiar este aviso para revisarlo.", Dock = DockStyle.Fill }, 0, 0);
        var details = new TextBox
        {
            Text = report, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical, WordWrap = true, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(30, 41, 59), ForeColor = ForeColor, AccessibleName = "Detalles del error"
        };
        layout.Controls.Add(details, 0, 1);
        var feedback = new Label { Dock = DockStyle.Fill, Padding = new(0, 7, 0, 0), ForeColor = Color.FromArgb(179, 205, 235) };
        layout.Controls.Add(feedback, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var close = Button("Cerrar"); close.DialogResult = DialogResult.OK;
        var copy = Button("Copiar error"); copy.BackColor = Color.FromArgb(24, 112, 210);
        copy.Click += (_, _) =>
        {
            if (!TryCopy(report, out var result)) { details.Focus(); details.SelectAll(); }
            feedback.Text = result;
        };
        buttons.Controls.AddRange([close, copy]);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = close; CancelButton = close;
    }

    private static Button Button(string text) => new ThemedButton
    {
        Text = text, AccessibleName = text, AutoSize = true, MinimumSize = new(100, 36),
        Padding = new(12, 4, 12, 4), BackColor = Color.FromArgb(46, 64, 87), ForeColor = Color.White, Cursor = Cursors.Hand
    };

    internal static string CreateReport(Exception error) => ErrorReport.Build(error,
        (typeof(ErrorDialog).Assembly.GetName().Version?.ToString(3) ?? "desconocida") + " beta",
        Environment.OSVersion.VersionString + "; .NET " + Environment.Version);

    internal static void ShowReport(IWin32Window? owner, string report)
    {
        using var dialog = new ErrorDialog(report);
        if (owner is null) { dialog.StartPosition = FormStartPosition.CenterScreen; dialog.ShowDialog(); }
        else dialog.ShowDialog(owner);
    }

    internal static bool TryCopy(string report, out string feedback)
    {
        try
        {
            Clipboard.SetText(report, TextDataFormat.UnicodeText);
            feedback = "Error copiado. Ya puedes pegarlo.";
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or ThreadStateException)
        {
            feedback = "No se ha podido copiar. Selecciona el texto y pulsa Ctrl+C.";
            return false;
        }
    }
}
