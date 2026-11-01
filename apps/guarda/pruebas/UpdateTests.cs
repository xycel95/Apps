using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;

namespace Guarda
{
    internal static class UpdateTests
    {
        private static int count;
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Case(string title, Action action) { action(); count++; Console.WriteLine("OK " + count.ToString("00") + " · " + title); }
        private static void Reject(Action action) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, "Debía rechazarse."); }
        public static int Main(string[] args)
        {
            string root = Path.Combine(Path.GetTempPath(), "guarda-updater-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                byte[] manifest = File.ReadAllBytes(args[0]), signature = Convert.FromBase64String(File.ReadAllText(args[0] + ".sig"));
                string payload = args[1]; SignedUpdate release = null;
                Case("Canal oficial firmado con la clave integrada", () => { release = Updates.ReadSigned(manifest, signature); Check(Updates.ParseVersion(release.Version) == Updates.ParseVersion(System.Reflection.AssemblyName.GetAssemblyName(payload).Version.ToString()), "versión"); });
                Case("Versiones: actualización y comparación con cuatro componentes", () => { Check(Updates.ParseVersion(release.Version) > Updates.ParseVersion("1.0.1"), "nueva"); Check(Updates.ParseVersion("1.1.0") == Updates.ParseVersion("1.1.0.0"), "igual"); });
                Case("Manifiesto alterado se rechaza", () => { byte[] changed = (byte[])manifest.Clone(); changed[25] ^= 1; Reject(() => Updates.ReadSigned(changed, signature)); });
                Case("Firma alterada o truncada se rechaza", () => { byte[] changed = (byte[])signature.Clone(); changed[20] ^= 1; Reject(() => Updates.ReadSigned(manifest, changed)); Reject(() => Updates.ReadSigned(manifest, new byte[12])); });
                Case("Tamaño máximo de metadatos y versiones inválidas", () => { Reject(() => Updates.ReadSigned(new byte[65537], signature)); Reject(() => Updates.ParseVersion("otra")); Reject(() => Updates.ParseVersion("1.2")); });
                Case("Consulta del canal mediante transporte controlado", () => {
                    SignedUpdate item = Updates.Check(CancellationToken.None, (url, limit, token) => url.EndsWith(".sig") ? Encoding.ASCII.GetBytes(Convert.ToBase64String(signature)) : manifest);
                    Check(item.Sha256 == release.Sha256, "mismo anuncio");
                });
                Case("Ejecutable descargado: firma, longitud, SHA-256 y versión", () => { Updates.VerifyPayload(release, payload); });
                Case("Descarga truncada no puede instalarse", () => { string f = Path.Combine(root, "truncado.exe"); File.WriteAllBytes(f, File.ReadAllBytes(payload).Take(1024).ToArray()); Reject(() => Updates.VerifyPayload(release, f)); });
                Case("Descarga alterada del mismo tamaño no puede instalarse", () => { string f = Path.Combine(root, "alterado.exe"); byte[] bytes = File.ReadAllBytes(payload); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(f, bytes); Reject(() => Updates.VerifyPayload(release, f)); });
                Case("Sustitución con conservación de la versión anterior", () => {
                    string target = Path.Combine(root, "Guarda.exe"); File.WriteAllText(target, "version anterior"); bool launched = false;
                    string backup = Updates.InstallFiles(target, payload, release.Sha256, p => launched = true);
                    Check(launched && Updates.FileHash(target) == release.Sha256 && File.ReadAllText(backup) == "version anterior", "sustitución");
                });
                Case("Si el reinicio falla, se restaura el ejecutable anterior", () => {
                    string target = Path.Combine(root, "Guarda-Rollback.exe"); File.WriteAllText(target, "anterior intacto");
                    Reject(() => Updates.InstallFiles(target, payload, release.Sha256, p => { throw new IOException("Error simulado al abrir"); }));
                    Check(File.ReadAllText(target) == "anterior intacto", "rollback");
                });
                Case("Un hash erróneo no toca el programa instalado", () => {
                    string target = Path.Combine(root, "Guarda-Error.exe"); File.WriteAllText(target, "no modificar");
                    Reject(() => Updates.InstallFiles(target, payload, new string('0', 64), p => { })); Check(File.ReadAllText(target) == "no modificar", "original intacto");
                });
                Case("No admite rutas o argumentos de instalación externos", () => { Reject(() => Updates.RequireStage(root)); Reject(() => Updates.Quote("ruta\nmaliciosa")); Reject(() => Updates.Quote("ruta\"maliciosa")); });
                Console.WriteLine("RESULTADO: " + count + " pruebas del actualizador superadas."); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { try { Directory.Delete(root, true); } catch { } }
        }
    }
}
