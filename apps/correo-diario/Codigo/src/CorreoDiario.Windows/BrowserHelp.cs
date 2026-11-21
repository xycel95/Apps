using System.Diagnostics;

namespace CorreoDiario.Windows;

internal static class BrowserHelp
{
    public static void Show(IWin32Window owner, bool chrome)
    {
        var name = chrome ? "Google Chrome" : "Microsoft Edge";
        var address = chrome ? "chrome://inspect/#remote-debugging" : "edge://inspect/#remote-debugging";
        using var dialog = new Form { Text = "Conectar con tu navegador abierto", Size = new(740, 475),
            MinimumSize = new(640, 430), StartPosition = FormStartPosition.CenterParent, Font = new("Segoe UI", 10F),
            BackColor = Color.FromArgb(17, 24, 39), ForeColor = Color.White };
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new(24) };
        body.RowStyles.Add(new(SizeType.Percent, 100)); body.RowStyles.Add(new(SizeType.Absolute, 36)); body.RowStyles.Add(new(SizeType.Absolute, 55));
        body.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
            $"Usa el Outlook que ya tienes abierto en {name}.\n\n" +
            $"1. Abre esta dirección en {name}:\n{address}\n\n" +
            "2. En «Remote debugging / Depuración remota», marca «Allow remote debugging for this browser instance» (permitir la conexión para esta instancia).\n\n" +
            "3. Vuelve a CorreoDiario y pulsa «Buscar pestañas». Acepta «Permitir» en el aviso del navegador y elige tu pestaña de Outlook.\n\n" +
            "El permiso técnico del navegador da acceso a su sesión. CorreoDiario realiza sus operaciones sobre la pestaña que selecciones. Al terminar puedes desconectar y desmarcar la opción en el navegador." }, 0, 0);
        var url = new TextBox { Text = address, ReadOnly = true, Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
        body.Controls.Add(url, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(0, 8, 0, 0) };
        var open = new Button { Text = "Abrir esa opción", AutoSize = true };
        var copy = new Button { Text = "Copiar dirección", AutoSize = true };
        var close = new Button { Text = "Entendido", AutoSize = true, DialogResult = DialogResult.OK };
        foreach (var button in new[] { open, copy, close })
        {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(37, 82, 128); button.ForeColor = Color.White;
            button.Padding = new(8, 3, 8, 3);
        }
        open.Click += (_, _) =>
        {
            try
            {
                var start = new ProcessStartInfo(chrome ? "chrome.exe" : "msedge.exe") { UseShellExecute = true };
                start.ArgumentList.Add("--new-tab"); start.ArgumentList.Add(address);
                Process.Start(start);
            }
            catch { MessageBox.Show(dialog, "Copia la dirección y pégala en la barra del navegador que ya tienes abierto.", "Abrir opción del navegador"); }
        };
        copy.Click += (_, _) => Clipboard.SetText(address);
        buttons.Controls.AddRange([open, copy, close]);
        body.Controls.Add(buttons, 0, 2); dialog.Controls.Add(body); dialog.AcceptButton = close;
        dialog.ShowDialog(owner);
    }
}
