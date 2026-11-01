using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Guarda
{
    internal static class Tests
    {
        private static int passed;
        private static string root;
        private static BackupOptions Options(string suffix)
        {
            string test = Path.Combine(root, suffix); Directory.CreateDirectory(test);
            string source = Path.Combine(test, "Mis documentos"), dest = Path.Combine(test, "USB");
            Directory.CreateDirectory(source); Directory.CreateDirectory(dest);
            return new BackupOptions { Source = source, Destination = dest, Name = "Mis documentos", Date = new DateTime(2026, 11, 1, 23, 59, 0), ReportsDirectory = Path.Combine(test, "logs") };
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Test(string name, Action body) { body(); passed++; Console.WriteLine("OK " + passed.ToString("00") + " · " + name); }
        private static string Hash(string path) { using (SHA256 h = SHA256.Create()) using (FileStream f = File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)); }
        [DllImport("libc", SetLastError = true)] private static extern int symlink(string target, string linkpath);

        public static int Main()
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            root = Path.Combine(Path.GetTempPath(), "guarda-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Test("Copia verificada, Unicode, ocultos, binario, vacíos y fechas", () => {
                    BackupOptions o = Options("normal");
                    Directory.CreateDirectory(Path.Combine(o.Source, "vacía")); Directory.CreateDirectory(Path.Combine(o.Source, "árbol ñ"));
                    File.WriteAllText(Path.Combine(o.Source, "árbol ñ", "Niño_日本語.txt"), "Información de Rafa — prueba ✓");
                    File.WriteAllBytes(Path.Combine(o.Source, "vacío.bin"), new byte[0]);
                    File.WriteAllText(Path.Combine(o.Source, ".oculto"), "oculto");
                    byte[] data = new byte[3 * 1024 * 1024 + 17]; new Random(13).NextBytes(data); File.WriteAllBytes(Path.Combine(o.Source, "datos.bin"), data);
                    DateTime stamp = new DateTime(2022, 2, 3, 4, 5, 6, DateTimeKind.Utc); File.SetLastWriteTimeUtc(Path.Combine(o.Source, "datos.bin"), stamp);
                    string before = Hash(Path.Combine(o.Source, "datos.bin"));
                    BackupProgress last = null;
                    BackupResult r = BackupEngine.Run(o, p => last = p, CancellationToken.None);
                    Check(r.Success, r.Message); Check(r.Files == 4, "recuento"); Check(Path.GetFileName(r.OutputPath) == "2026_11_01_Mis documentos", "fecha/nombre");
                    Check(Hash(Path.Combine(r.OutputPath, "datos.bin")) == before && Hash(Path.Combine(o.Source, "datos.bin")) == before, "bytes");
                    Check(File.GetLastWriteTimeUtc(Path.Combine(r.OutputPath, "datos.bin")) == stamp, "fecha conservada");
                    Check(Directory.Exists(Path.Combine(r.OutputPath, "vacía")), "carpeta vacía");
                    Check(File.ReadAllText(Path.Combine(r.OutputPath, "árbol ñ", "Niño_日本語.txt")).Contains("✓"), "unicode");
                    Check(File.Exists(Path.Combine(r.OutputPath, ".oculto")), "ocultos");
                    Check(File.Exists(r.ReportPath) && File.ReadAllText(r.ReportPath).Contains("Copia completada y verificada"), "informe");
                    Check(last != null && last.Done == last.Total, "progreso");
                });
                Test("Varias copias el mismo día; no se sobrescriben ni archivos que colisionan", () => {
                    BackupOptions o = Options("collision"); File.WriteAllText(Path.Combine(o.Source, "f.txt"), "primera");
                    BackupResult a = BackupEngine.Run(o, null, CancellationToken.None); Check(a.Success, a.Message);
                    File.WriteAllText(Path.Combine(o.Source, "f.txt"), "segunda");
                    File.WriteAllText(Path.Combine(o.Destination, "2026_11_01_Mis documentos_02"), "no tocar");
                    BackupResult b = BackupEngine.Run(o, null, CancellationToken.None); Check(b.Success, b.Message);
                    Check(Path.GetFileName(b.OutputPath).EndsWith("_03"), "sufijo");
                    Check(File.ReadAllText(Path.Combine(a.OutputPath, "f.txt")) == "primera", "copia anterior intacta");
                    Check(File.ReadAllText(Path.Combine(b.OutputPath, "f.txt")) == "segunda", "segunda copia");
                    Check(File.ReadAllText(Path.Combine(o.Destination, "2026_11_01_Mis documentos_02")) == "no tocar", "archivo intacto");
                });
                Test("Bloquea origen=destino y destino dentro del origen", () => {
                    BackupOptions o = Options("recursive");
                    o.Destination = o.Source; BackupResult a = BackupEngine.Run(o, null, CancellationToken.None); Check(!a.Success && a.OutputPath == "", "mismo origen");
                    o.Destination = Path.Combine(o.Source, "copias"); Directory.CreateDirectory(o.Destination);
                    BackupResult b = BackupEngine.Run(o, null, CancellationToken.None); Check(!b.Success && !Directory.EnumerateFileSystemEntries(o.Destination).Any(), "recursión");
                });
                Test("Permite carpetas hermanas con prefijos similares", () => {
                    BackupOptions o = Options("siblings"); o.Destination = o.Source + "_copias"; Directory.CreateDirectory(o.Destination);
                    Check(BackupEngine.Run(o, null, CancellationToken.None).Success, "prefijo parecido");
                });
                Test("Destino padre del origen; copia completa independiente", () => {
                    BackupOptions o = Options("parent"); o.Destination = Path.GetDirectoryName(o.Source);
                    File.WriteAllText(Path.Combine(o.Source, "f"), "a");
                    Check(BackupEngine.Run(o, null, CancellationToken.None).Success, "destino padre");
                });
                Test("No crea nada si la unidad de destino no existe", () => {
                    BackupOptions o = Options("missing"); Directory.Delete(o.Destination);
                    BackupResult r = BackupEngine.Run(o, null, CancellationToken.None); Check(!r.Success && !Directory.Exists(o.Destination), "destino ausente");
                });
                Test("Cancelación antes de empezar; no crea copia", () => {
                    BackupOptions o = Options("pre-cancel"); CancellationTokenSource c = new CancellationTokenSource(); c.Cancel();
                    BackupResult r = BackupEngine.Run(o, null, c.Token); Check(r.Cancelled && !r.Success && !Directory.GetFileSystemEntries(o.Destination).Any(), "cancelación temprana");
                });
                Test("Cancelación durante archivo; carpeta INCOMPLETA y sin archivo a medias", () => {
                    BackupOptions o = Options("mid-cancel"); File.WriteAllBytes(Path.Combine(o.Source, "grande.bin"), new byte[6 * 1024 * 1024]);
                    CancellationTokenSource c = new CancellationTokenSource();
                    BackupResult r = BackupEngine.Run(o, p => { if (p.Stage == "Copiando" && p.Done > 0) c.Cancel(); }, c.Token);
                    Check(r.Cancelled && !r.Success && r.OutputPath.EndsWith("_INCOMPLETA"), "estado cancelado");
                    Check(Directory.Exists(r.OutputPath) && !File.Exists(Path.Combine(r.OutputPath, "grande.bin")), "no archivo parcial");
                    Check(new FileInfo(Path.Combine(o.Source, "grande.bin")).Length == 6 * 1024 * 1024, "origen intacto");
                });
                Test("Detecta contenido modificado en el destino al verificar", () => {
                    BackupOptions o = Options("tamper"); File.WriteAllBytes(Path.Combine(o.Source, "f.bin"), new byte[32]); bool changed = false;
                    BackupResult r = BackupEngine.Run(o, p => {
                        if (p.Stage == "Verificando" && !changed) {
                            changed = true; string staging = Directory.GetDirectories(o.Destination).Single();
                            byte[] data = File.ReadAllBytes(Path.Combine(staging, "f.bin")); data[0] = 255; File.WriteAllBytes(Path.Combine(staging, "f.bin"), data);
                        }
                    }, CancellationToken.None);
                    Check(changed && !r.Success && r.Message.Contains("no coincide"), "hash detecta diferencia");
                    Check(!File.Exists(Path.Combine(r.OutputPath, "f.bin")), "archivo corrupto eliminado de la nueva copia");
                });
                Test("Detecta archivos nuevos en origen durante la copia", () => {
                    BackupOptions o = Options("source-added"); File.WriteAllText(Path.Combine(o.Source, "f"), "a"); bool changed = false;
                    BackupResult r = BackupEngine.Run(o, p => { if (p.Stage == "Verificando" && !changed) { changed = true; File.WriteAllText(Path.Combine(o.Source, "nuevo"), "b"); } }, CancellationToken.None);
                    Check(!r.Success && r.OutputPath.EndsWith("_INCOMPLETA") && r.Message.Contains("ha cambiado"), "origen cambiante");
                });
                Test("Carpeta vacía y verificación desactivada", () => {
                    BackupOptions o = Options("empty"); o.Verify = false;
                    BackupResult r = BackupEngine.Run(o, null, CancellationToken.None); Check(r.Success && r.Files == 0 && Directory.Exists(r.OutputPath), "copia vacía");
                });
                Test("El informe se mueve fuera del origen si su ubicación cae dentro", () => {
                    BackupOptions o = Options("reports-in-source");
                    o.ReportsDirectory = Path.Combine(o.Source, "informes", "nuevos");
                    File.WriteAllText(Path.Combine(o.Source, "f"), "a");
                    BackupResult r = BackupEngine.Run(o, null, CancellationToken.None);
                    Check(r.Success && r.Files == 1, r.Message);
                    Check(!Directory.Exists(Path.Combine(o.Source, "informes")), "no escribe informe en origen");
                    Check(r.ReportPath.StartsWith(Path.Combine(o.Destination, "_Informes_Guarda")) && File.Exists(r.ReportPath), "informe fuera del origen");
                });
                Test("Rechaza nombres que permiten escapar del destino", () => {
                    BackupOptions o = Options("names");
                    foreach (string name in new[] { "", "..", "../Fuera", "A/B", "A\\B", "A:B", "*.txt", "termina.", "nul\0" }) {
                        o.Name = name; BackupResult r = BackupEngine.Run(o, null, CancellationToken.None); Check(!r.Success && r.OutputPath == "", "nombre: " + name);
                    }
                    Check(Directory.GetFileSystemEntries(o.Destination).Length == 0, "sin escritura");
                });
                Test("Fecha gregoriana aaaa_mm_dd con otro calendario del sistema", () => {
                    BackupOptions o = Options("culture"); CultureInfo original = Thread.CurrentThread.CurrentCulture;
                    try {
                        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
                        BackupResult r = BackupEngine.Run(o, null, CancellationToken.None); Check(r.Success && Path.GetFileName(r.OutputPath).StartsWith("2026_11_01_"), "fecha independiente de cultura");
                    } finally { Thread.CurrentThread.CurrentCulture = original; }
                });
                if (!BackupEngine.Windows) {
                    Test("Bloquea destino anidado oculto por enlace", () => {
                        BackupOptions o = Options("alias"); string child = Path.Combine(o.Source, "interno"); Directory.CreateDirectory(child);
                        string alias = Path.Combine(Path.GetDirectoryName(o.Source), "alias"); Check(symlink(child, alias) == 0, "crear enlace");
                        o.Destination = alias; BackupResult r = BackupEngine.Run(o, null, CancellationToken.None); Check(!r.Success && r.OutputPath == "", "alias a hijo");
                    });
                    Test("Enlaces y bucles no se siguen; copia claramente INCOMPLETA", () => {
                        BackupOptions o = Options("links"); File.WriteAllText(Path.Combine(o.Source, "normal"), "dato");
                        Check(symlink(o.Source, Path.Combine(o.Source, "bucle")) == 0, "crear bucle");
                        BackupResult r = BackupEngine.Run(o, null, CancellationToken.None);
                        Check(!r.Success && r.Files == 1 && r.OutputPath.EndsWith("_INCOMPLETA"), "omisión visible");
                        Check(r.Warnings.Any(s => s.Contains("bucle")) && !Directory.Exists(Path.Combine(r.OutputPath, "bucle")), "informe enlace");
                    });
                }
                Console.WriteLine("RESULTADO: " + passed + " pruebas superadas."); return 0;
            }
            catch (Exception ex) {
                Console.Error.WriteLine("FALLO: " + ex);
                foreach (string report in Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories).Where(p => p.Contains("logs"))) Console.Error.WriteLine(File.ReadAllText(report));
                return 1;
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }
    }
}
