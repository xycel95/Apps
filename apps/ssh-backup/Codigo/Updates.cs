using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace SSHBackup
{
    public sealed class UpdateRelease
    {
        public string Version { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Signature { get; set; } = "";
    }
    public static class Updates
    {
        public const string PublicKey = "<RSAKeyValue><Modulus>olhkOgYH6HrmNcrDBfojoUqRun7aNW+sDg0d7AfH9k5ExOPITaJ7cnjPhbww0GKYFBDmXLaG/fh2wGX5EZ63iKDywE9nd4D5mEodROCUXmontJnCKpswfDyj8RtBJdKsnU2lK2Nxgmvz4fobSwMILeQLb+fSXxbDetKDYLPvDvUpO4fi6C5d/+Sc0sCkH1eNVCZq2lbhA7F3ZglGTC5FvsKYP3nK451gBNvudHdXiaxLhcKaWAgCahRy6mywn0fINFIIdEH/JVqGtHsnjIulw0y9Uyr20kZ+mNEGICtiqRQVh4ss9sDAUtPnRAP081pjISWFMBASoZmQndArtJA9nrB/5pnN8FqvMNjRhkiV2LK4XanRPBvWmWtNzM7Hk7CZCwVig367HsdZQgOkIUfY+B9bIKEO+RnyE5goOhSTrjU8FoAuQ9+nzlxmuBHA3c2hDNWxusZXImzq9I6NDIRjtZcvYhly85OUbRVT9IO1K/cdecrStmC5Mm6P0bBhK2zL</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        public static byte[] SignedData(UpdateRelease release)
        {
            return Encoding.UTF8.GetBytes(release.Version + "\n" + release.Sha256 + "\n" + release.DownloadUrl + "\n" + release.Notes.Replace("\r\n", "\n").Replace("\r", "\n"));
        }
        public static void Validate(UpdateRelease release)
        {
            Version version;
            if (release == null || !Version.TryParse(release.Version, out version) || release.Sha256 == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(release.Sha256, "^[a-f0-9]{64}$")) throw new InvalidDataException("La información de actualización no es válida.");
            if (!string.IsNullOrEmpty(release.DownloadUrl)) RequireHttps(release.DownloadUrl);
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKey);
                    if (!rsa.VerifyData(SignedData(release), CryptoConfig.MapNameToOID("SHA256"), Convert.FromBase64String(release.Signature)))
                        throw new InvalidDataException("La firma de la actualización no es válida.");
                }
            }
            catch (FormatException e) { throw new InvalidDataException("Firma no válida.", e); }
        }
        public static string FileHash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        public static UpdateRelease ReadPackage(string path)
        {
            using (var file = File.OpenRead(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                var release = zip.GetEntry("release.xml"); var executable = zip.GetEntry("SSHBackup.exe");
                if (release == null || executable == null || zip.Entries.Count != 2 || release.Length > 262144 || executable.Length > 128L * 1024 * 1024)
                    throw new InvalidDataException("El paquete debe contener release.xml y SSHBackup.exe.");
                UpdateRelease result;
                using (var input = release.Open())
                using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 }))
                    result = (UpdateRelease)new XmlSerializer(typeof(UpdateRelease)).Deserialize(reader);
                Validate(result);
                string hash;
                using (var input = executable.Open())
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (hash != result.Sha256) throw new InvalidDataException("El ejecutable del paquete ha cambiado o está dañado.");
                return result;
            }
        }
        public static Uri RequireHttps(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0)
                throw new ArgumentException("El canal y las descargas de actualización deben usar HTTPS.");
            return uri;
        }
        static async Task Download(string url, string path, long limit, CancellationToken token)
        {
            Uri current = RequireHttps(url);
            for (int redirects = 0; redirects < 6; redirects++)
            {
                var request = (HttpWebRequest)WebRequest.Create(current); request.AllowAutoRedirect = false;
                request.Timeout = 30000; request.ReadWriteTimeout = 30000; request.UserAgent = "SSHBackup/" + Program.Version;
                using (token.Register(() => request.Abort()))
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                {
                    int status = (int)response.StatusCode;
                    if (status >= 300 && status < 400)
                    { current = RequireHttps(new Uri(current, response.Headers["Location"]).AbsoluteUri); continue; }
                    if (status != 200 || response.ContentLength > limit) throw new IOException("Respuesta o tamaño de actualización no válido.");
                    using (var input = response.GetResponseStream())
                    using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[65536]; int count; long total = 0;
                        while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                        {
                            total += count; if (total > limit) throw new IOException("El archivo de actualización supera el límite permitido.");
                            await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                        }
                    }
                    return;
                }
            }
            throw new IOException("Demasiadas redirecciones del canal de actualizaciones.");
        }
        public static async Task<UpdateRelease> CheckAsync(string url, CancellationToken token)
        {
            string path = Path.GetTempFileName();
            try { await Download(url, path, 262144, token).ConfigureAwait(false); var release = XmlFiles.Read<UpdateRelease>(path); Validate(release); return release; }
            finally { File.Delete(path); }
        }
        public static async Task<string> DownloadPackageAsync(UpdateRelease release, string directory, CancellationToken token)
        {
            Validate(release); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "SSHBackup-" + Guid.NewGuid().ToString("N") + ".sshupdate");
            try
            {
                await Download(release.DownloadUrl, path, 128L * 1024 * 1024, token).ConfigureAwait(false);
                var verified = ReadPackage(path);
                if (verified.Version != release.Version || verified.Sha256 != release.Sha256) throw new InvalidDataException("La descarga no coincide con la versión anunciada.");
                return path;
            }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }
        public static string Quote(string arg)
        {
            if (arg.IndexOf('"') >= 0 || arg.IndexOf('\0') >= 0 || arg.IndexOf('\r') >= 0 || arg.IndexOf('\n') >= 0) throw new ArgumentException("Ruta de actualización no válida.");
            return "\"" + arg.TrimEnd('\\') + "\"";
        }
        public static void LaunchInstall(string package, string directory)
        {
            var release = ReadPackage(package);
            if (Version.Parse(release.Version) <= Version.Parse(Program.Version)) throw new InvalidOperationException("El paquete no es más reciente que la versión instalada.");
            string target = Assembly.GetExecutingAssembly().Location;
            string root = Path.Combine(directory, "Actualizaciones", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string saved = Path.Combine(root, "paquete.sshupdate"), helper = Path.Combine(root, "actualizar.exe");
            File.Copy(package, saved, false);
            using (var file = File.OpenRead(saved))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            using (var source = zip.GetEntry("SSHBackup.exe").Open())
            using (var output = File.Create(helper)) source.CopyTo(output);
            if (FileHash(helper) != release.Sha256) throw new InvalidDataException("No se pudo verificar el instalador.");
            Process.Start(new ProcessStartInfo { FileName = helper, UseShellExecute = true,
                Arguments = "--apply-update " + Quote(saved) + " " + Quote(target) + " " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) });
        }
        public static void ApplyPackage(string package, string target, int parentPid, Action<string> startProcess = null)
        {
            var release = ReadPackage(package);
            if (!Path.IsPathRooted(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) throw new IOException("Destino de actualización no válido.");
            if (Version.Parse(release.Version) <= AssemblyName.GetAssemblyName(target).Version) throw new InvalidOperationException("No se instalará una versión anterior o igual.");
            try { using (var parent = Process.GetProcessById(parentPid)) if (!parent.WaitForExit(60000)) throw new IOException("La aplicación sigue abierta. No se ha sustituido."); }
            catch (ArgumentException) { }
            string staged = target + ".new-" + Guid.NewGuid().ToString("N"), backup = target + ".previous";
            try
            {
                using (var file = File.OpenRead(package))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                using (var input = zip.GetEntry("SSHBackup.exe").Open())
                using (var output = File.Create(staged)) input.CopyTo(output);
                if (FileHash(staged) != release.Sha256) throw new InvalidDataException("La actualización no supera SHA-256.");
                File.Replace(staged, target, backup);
                try { if (startProcess != null) startProcess(target); else Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); }
                catch { File.Copy(backup, target, true); throw; }
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
    }
}
