using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Guarda
{
    internal static class GuiSmoke
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (MainForm form = new MainForm())
            {
                form.Show(); Application.DoEvents();
                TextBox source = All(form).OfType<TextBox>().Single(c => c.AccessibleName == "Carpeta de origen");
                TextBox destination = All(form).OfType<TextBox>().Single(c => c.AccessibleName == "Carpeta de destino");
                source.Text = @"C:\Users\Rafa\Documents\Mis documentos"; destination.Text = @"E:\Copias de seguridad";
                TextBox name = All(form).OfType<TextBox>().Single(c => c.AccessibleName == "Nombre de la copia"); name.Text = "Mis documentos";
                Pump(250);
                Directory.CreateDirectory(args[0]);
                Capture(form, Path.Combine(args[0], "ventana.png"));
                Console.WriteLine("GUI abierta: " + form.ClientSize + "; controles: " + All(form).Count());
                string testRoot = Path.Combine(Path.GetTempPath(), "guarda-gui-" + Guid.NewGuid().ToString("N"));
                try {
                    string from = Path.Combine(testRoot, "Origen"), to = Path.Combine(testRoot, "Destino");
                    Directory.CreateDirectory(from); Directory.CreateDirectory(to);
                    File.WriteAllText(Path.Combine(from, "prueba.txt"), "Copia desde el botón de la aplicación.");
                    source.Text = from; destination.Text = to;
                    All(form).OfType<Button>().Single(b => b.Text == "Crear copia").PerformClick();
                    Stopwatch timeout = Stopwatch.StartNew();
                    while (form.Busy && timeout.ElapsedMilliseconds < 15000) Pump(20);
                    if (form.Busy) throw new Exception("La ventana no finalizó la copia.");
                    string result = Directory.GetDirectories(to).Single();
                    if (result.Contains("INCOMPLETA") || File.ReadAllText(Path.Combine(result, "prueba.txt")) != File.ReadAllText(Path.Combine(from, "prueba.txt"))) throw new Exception("Copia desde la ventana incorrecta.");
                    if (!All(form).OfType<Button>().Single(b => b.Text == "Abrir copia").Enabled) throw new Exception("Abrir copia no se habilita.");
                    Pump(150); Capture(form, Path.Combine(args[0], "ventana-completada.png"));
                    form.Size = form.MinimumSize; Pump(100); Capture(form, Path.Combine(args[0], "ventana-minima.png"));
                    Console.WriteLine("GUI: copia real desde el botón completada, controles recuperados y tamaño mínimo revisado.");
                } finally { try { Directory.Delete(testRoot, true); } catch { } }
                form.Close();
            }
        }
        private static void Pump(int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            do { Application.DoEvents(); Thread.Sleep(10); } while (watch.ElapsedMilliseconds < milliseconds);
        }
        private static void Capture(Form form, string path)
        {
            using (Bitmap image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            using (Graphics g = Graphics.FromImage(image)) {
                g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize); image.Save(path);
            }
        }
        private static System.Collections.Generic.IEnumerable<Control> All(Control c)
        {
            foreach (Control child in c.Controls) { yield return child; foreach (Control sub in All(child)) yield return sub; }
        }
    }
}
