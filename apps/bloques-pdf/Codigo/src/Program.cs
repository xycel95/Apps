using System;
using System.Windows.Forms;

namespace BloquesPDF
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, e) => MessageBox.Show(
                "No se ha podido completar la acción.\r\n\r\n" + BatchEngine.Describe(e.Exception),
                "Bloques PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
        }
    }
}
