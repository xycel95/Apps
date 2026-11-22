using CorreoDiario.Core;
using CorreoDiario.Windows;

internal static class PendingSelectionCheck
{
    public static void Run(IWin32Window owner)
    {
        using var window = new MainForm { StartPosition = FormStartPosition.Manual, Location = new(-30000, -30000) };
        window.Show(owner);
        var controls = Descendants(window).ToArray();
        var grid = controls.OfType<DataGridView>().Single();
        var delete = controls.OfType<CheckBox>().Single();
        foreach (var id in new[] { "guardado", "pendiente2", "pendiente1" })
        {
            var row = grid.Rows[grid.Rows.Add(true, DateTime.Today, "RECIBIDO", "a@example.invalid", "Prueba")];
            row.Tag = new MailInfo(id, "v1", "inbox", "Prueba", "a@example.invalid", "b@example.invalid", "", DateTimeOffset.Now, false, "", []);
        }
        delete.Checked = true;
        window.SelectPending(["pendiente1", "pendiente2"]);
        if (delete.Checked || grid.Rows[0].Cells[0].Value is not false || grid.Rows[1].Cells[0].Value is not true || grid.Rows[2].Cells[0].Value is not true)
            throw new Exception("La selección debe dejar marcados solo los pendientes y desmarcar el borrado.");
        try { window.SelectPending(["guardado", "otra-cuenta"]); throw new Exception("No debe aplicar una coincidencia parcial."); }
        catch (InvalidDataException) { }
        if (grid.Rows[0].Cells[0].Value is not false || grid.Rows[1].Cells[0].Value is not true || grid.Rows[2].Cells[0].Value is not true)
            throw new Exception("Un listado incompleto no debe cambiar ninguna casilla.");
        window.Size = window.MinimumSize;
        window.PerformLayout();
        foreach (var name in new[] { "Copiar último error", "Ver error", "Marcar pendientes" })
        {
            var button = controls.OfType<Button>().Single(b => b.Text == name);
            if (button.Enabled || button.Bottom > button.Parent!.ClientSize.Height || button.Right > button.Parent.ClientSize.Width)
                throw new Exception("El control debe estar visible y desactivado antes de conectar o generar un error: " + name);
        }
        Console.WriteLine("PASS Pendientes Windows: solo los IDs pendientes marcados, borrado desmarcado, coincidencia completa previa y controles visibles al tamaño mínimo.");
        window.Close();
    }
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
