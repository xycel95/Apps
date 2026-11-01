using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SSHBackup
{
    public static class TestsUpdates
    {
        static int checks;
        static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("OK " + message); }
        static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
        static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }
        [STAThread]
        public static int Main(string[] args)
        {
            Program.RegisterDependencies();
            try
            {
                string root = Path.GetFullPath(args[1]); Directory.CreateDirectory(root);
                if (args[0] == "gui") Gui(root);
                else if (args[0] == "online") Online(root);
                else Unit(root, Path.GetFullPath(args[2]), Path.GetFullPath(args[3]));
                Console.WriteLine("ACTUALIZACIONES: " + checks + " comprobaciones correctas"); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        static void Unit(string root, string package, string old)
        {
            Assert(Updates.ResolveChannel(null) == Updates.OfficialChannel && Updates.ResolveChannel("  ") == Updates.OfficialChannel, "Canal oficial para instalaciones nuevas y ajustes antiguos vacíos");
            Assert(Updates.ResolveChannel(" https://example.invalid/estable.xml ") == "https://example.invalid/estable.xml", "Se conserva un canal personalizado existente");
            string opened = null;
            Updates.OpenLink(Updates.Website, url => opened = url);
            Assert(opened == Updates.Website, "Novedades web se abre como URL sin comprobar archivos locales");
            Updates.OpenLink(Updates.Changelog, url => opened = url);
            Assert(opened == Updates.Changelog, "Notas de GitHub se abre como URL sin comprobar archivos locales");
            foreach (string url in new[] { "http://example.invalid/", "file:///tmp/exe", "https://user:pass@example.invalid/" })
            { bool rejected = false; try { Updates.RequireHttps(url); } catch (ArgumentException) { rejected = true; } Assert(rejected, "URL insegura rechazada: " + url.Split(':')[0]); }
            var release = Updates.ReadPackage(package);
            Assert(release.Version == "2.2.1" && release.DownloadUrl.Contains("/versiones/2.2.1/"), "Paquete oficial 2.2.1 con firma original y URL fija verificado");
            release.Notes += " alterado"; bool invalid = false; try { Updates.Validate(release); } catch (InvalidDataException) { invalid = true; }
            Assert(invalid, "No se aceptan novedades alteradas tras firmar");
            string damaged = Path.Combine(root, "dañado.sshupdate");
            using (var input = ZipFileOpen(package))
            using (var output = new ZipArchive(File.Create(damaged), ZipArchiveMode.Create))
            { foreach (var entry in input.Entries) { var copy = output.CreateEntry(entry.FullName); using (var source = entry.Open()) using (var dest = copy.Open()) { source.CopyTo(dest); if (entry.FullName == "SSHBackup.exe") dest.WriteByte(1); } } }
            invalid = false; try { Updates.ReadPackage(damaged); } catch (InvalidDataException) { invalid = true; }
            Assert(invalid, "Ejecutable alterado rechazado por SHA-256");
            var cancel = new CancellationTokenSource(); cancel.Cancel();
            bool canceled = false; try { Updates.CheckAsync(null, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
            Assert(canceled, "Consulta cancelada antes de iniciar una conexión");
            string target = Path.Combine(root, "SSHBackup.exe"); File.Copy(old, target, true); string original = Updates.FileHash(target);
            bool launched = false; Updates.ApplyPackage(package, target, -1, path => { launched = Updates.FileHash(path) == Updates.ReadPackage(package).Sha256; });
            Assert(launched && Updates.FileHash(target + ".previous") == original, "Sustitución verificada y EXE anterior conservado");
            invalid = false; try { Updates.ApplyPackage(package, target, -1, path => { }); } catch (InvalidOperationException) { invalid = true; }
            Assert(invalid, "No se reinstala una versión igual o anterior");
            File.Copy(old, target, true);
            invalid = false; try { Updates.ApplyPackage(package, target, -1, path => { throw new IOException("Ensayo: el proceso no arranca"); }); } catch (IOException) { invalid = true; }
            Assert(invalid && Updates.FileHash(target) == original, "Restauración del EXE si falla el inicio de la nueva versión");
            invalid = false; try { Updates.RequireStage(root); } catch (IOException) { invalid = true; }
            Assert(invalid, "Instalador rechaza una carpeta no reconocida");
            var progress = new UpdateProgress { Received = 30, Total = 100 };
            Assert(progress.Percent == 30, "Porcentaje de descarga calculado en bytes");
        }
        static ZipArchive ZipFileOpen(string path) { return new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read); }
        sealed class DownloadCounter : IProgress<UpdateProgress>
        {
            public long Bytes;
            public void Report(UpdateProgress value) { Interlocked.Exchange(ref Bytes, value.Received); }
        }
        static void Online(string root)
        {
            var release = Updates.CheckAsync(Updates.OfficialChannel, CancellationToken.None).GetAwaiter().GetResult();
            Assert(release.Version == "2.2.1", "Canal HTTPS público consultado y firma verificada en Windows");
            var counter = new DownloadCounter();
            string package = Updates.DownloadPackageAsync(release, root, CancellationToken.None, counter).GetAwaiter().GetResult();
            Assert(Updates.ReadPackage(package).Sha256 == release.Sha256 && counter.Bytes == new FileInfo(package).Length, "Descarga real desde la app con progreso, firma y SHA-256 correctos");
        }
        static void Gui(string root)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var store = new ConfigStore(Path.Combine(root, "config")); store.Settings.UpdateManifestUrl = "";
            using (var form = new MainForm(store))
            {
                form.Show(); Application.DoEvents(); Call(form, "ShowPage", "tools");
                foreach (Control control in ((System.Collections.Generic.Dictionary<string, Panel>)Field(form, "pages"))["tools"].Controls)
                    if (control is ModernTabs) ((ModernTabs)control).SelectedIndex = 2;
                Application.DoEvents();
                Assert(((TextBox)Field(form, "updateUrl")).Text == Updates.OfficialChannel, "Pantalla lista con canal oficial sin configurar una URL");
                Call(form, "ShowRelease", Updates.ReadPackage(Environment.GetEnvironmentVariable("SSHBACKUP_TEST_PACKAGE")));
                Assert(!((Button)Field(form, "installUpdate")).Enabled && ((Label)Field(form, "updateStatus")).Text.Contains("actualizado"), "Versión instalada identificada sin ofrecer reinstalación");
                Func<CancellationToken, Task> action = token => { throw new OperationCanceledException(); };
                ((Task)Call(form, "UpdateOperation", action)).GetAwaiter().GetResult();
                Assert(((Label)Field(form, "updateStatus")).Text.Contains("cancelada") && ((Button)Field(form, "checkUpdate")).Enabled, "Cancelar conserva el programa y recupera los controles");
                foreach (bool dark in new[] { false, true })
                {
                    ((CheckBox)Field(form, "darkMode")).Checked = dark;
                    form.Size = dark ? form.MinimumSize : new Size(1320, 930); Application.DoEvents(); Thread.Sleep(100); Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.Left, form.Top, 0, 0, bitmap.Size); bitmap.Save(Path.Combine(root, dark ? "actualizaciones-oscuro.png" : "actualizaciones-claro.png")); }
                    Assert(((Button)Field(form, "installUpdate")).Parent.ClientSize.Width > 710, "Acciones visibles en " + (dark ? "oscuro y tamaño mínimo" : "claro"));
                }
                form.Close();
            }
            store.Settings.UpdateManifestUrl = "https://example.invalid/custom.xml";
            using (var form = new MainForm(store)) Assert(((TextBox)Field(form, "updateUrl")).Text == store.Settings.UpdateManifestUrl, "La pantalla conserva el canal personalizado");
        }
    }
}
