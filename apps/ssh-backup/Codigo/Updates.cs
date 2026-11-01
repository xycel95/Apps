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
    public sealed class UpdateProgress
    {
        public long Received, Total;
        public int Percent { get { return Total > 0 ? (int)Math.Min(100, Received * 100 / Total) : 0; } }
    }
    public sealed class UpdateInstallPlan
    {
        public string Nonce, Target, TargetHash, Package, HelperHash;
        public int ParentId;
        public long ParentStarted;
    }
    public static class Updates
    {
        public const string OfficialChannel = "https://raw.githubusercontent.com/xycel95/Apps/ssh-backup/apps/ssh-backup/actualizaciones/stable.xml";
        public const string Website = "https://xycel95.es/aplicaciones/ssh-backup/";
        public const string Changelog = "https://github.com/xycel95/Apps/blob/ssh-backup/apps/ssh-backup/notas-de-parche/README.md";
        public static string ResolveChannel(string value) { return string.IsNullOrWhiteSpace(value) ? OfficialChannel : value.Trim(); }
        public static string CacheRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SSHBackup", "Actualizaciones"); } }
        public const string PublicKey = "<RSAKeyValue><Modulus>olhkOgYH6HrmNcrDBfojoUqRun7aNW+sDg0d7AfH9k5ExOPITaJ7cnjPhbww0GKYFBDmXLaG/fh2wGX5EZ63iKDywE9nd4D5mEodROCUXmontJnCKpswfDyj8RtBJdKsnU2lK2Nxgmvz4fobSwMILeQLb+fSXxbDetKDYLPvDvUpO4fi6C5d/+Sc0sCkH1eNVCZq2lbhA7F3ZglGTC5FvsKYP3nK451gBNvudHdXiaxLhcKaWAgCahRy6mywn0fINFIIdEH/JVqGtHsnjIulw0y9Uyr20kZ+mNEGICtiqRQVh4ss9sDAUtPnRAP081pjISWFMBASoZmQndArtJA9nrB/5pnN8FqvMNjRhkiV2LK4XanRPBvWmWtNzM7Hk7CZCwVig367HsdZQgOkIUfY+B9bIKEO+RnyE5goOhSTrjU8FoAuQ9+nzlxmuBHA3c2hDNWxusZXImzq9I6NDIRjtZcvYhly85OUbRVT9IO1K/cdecrStmC5Mm6P0bBhK2zL</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        public static byte[] SignedData(UpdateRelease release)
        {
            return Encoding.UTF8.GetBytes(release.Version + "\n" + release.Sha256 + "\n" + release.DownloadUrl + "\n" + release.Notes.Replace("\r\n", "\n").Replace("\r", "\n"));
        }
        public static void Validate(UpdateRelease release)
        {
            Version version;
            if (release == null || !Version.TryParse(release.Version, out version) || version.Build < 0 || release.Notes == null || release.Notes.Length > 65536 || release.Sha256 == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(release.Sha256, "^[a-f0-9]{64}$")) throw new InvalidDataException("La información de actualización no es válida.");
            if (!string.IsNullOrEmpty(release.DownloadUrl)) RequireHttps(release.DownloadUrl);
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
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
        static async Task Download(string url, string path, long limit, CancellationToken token, IProgress<UpdateProgress> progress = null)
        {
            Uri current = RequireHttps(url);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            for (int redirects = 0; redirects < 6; redirects++)
            {
                token.ThrowIfCancellationRequested();
                var request = (HttpWebRequest)WebRequest.Create(current); request.AllowAutoRedirect = false;
                request.Timeout = 30000; request.ReadWriteTimeout = 30000; request.UserAgent = "SSHBackup/" + Program.Version;
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                try {
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
                            if (progress != null) progress.Report(new UpdateProgress { Received = total, Total = response.ContentLength });
                        }
                        token.ThrowIfCancellationRequested();
                        if (response.ContentLength >= 0 && total != response.ContentLength) throw new IOException("La descarga está incompleta. Vuelve a intentarlo.");
                    }
                    return;
                }
                } catch (WebException) { token.ThrowIfCancellationRequested(); throw; }
            }
            throw new IOException("Demasiadas redirecciones del canal de actualizaciones.");
        }
        public static async Task<UpdateRelease> CheckAsync(string url, CancellationToken token)
        {
            string path = Path.GetTempFileName();
            try { await Download(ResolveChannel(url), path, 262144, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); var release = XmlFiles.Read<UpdateRelease>(path); Validate(release); return release; }
            finally { File.Delete(path); }
        }
        public static async Task<string> DownloadPackageAsync(UpdateRelease release, string directory, CancellationToken token, IProgress<UpdateProgress> progress = null)
        {
            Validate(release); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "SSHBackup-" + Guid.NewGuid().ToString("N") + ".sshupdate");
            try
            {
                await Download(release.DownloadUrl, path, 128L * 1024 * 1024, token, progress).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
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
            string probe = Path.Combine(Path.GetDirectoryName(target), ".sshbackup-permiso-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            string stage = Path.Combine(CacheRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            string saved = Path.Combine(stage, "paquete.sshupdate"), helper = Path.Combine(stage, "actualizar.exe");
            File.Copy(package, saved, false);
            var copied = ReadPackage(saved);
            if (copied.Version != release.Version || copied.Sha256 != release.Sha256) throw new InvalidDataException("El paquete ha cambiado antes de instalar.");
            File.Copy(target, helper, false);
            byte[] bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            using (var parent = Process.GetCurrentProcess())
            {
                var plan = new UpdateInstallPlan { Nonce = BitConverter.ToString(bytes).Replace("-", ""), Target = Path.GetFullPath(target), TargetHash = FileHash(target), Package = saved, HelperHash = FileHash(helper), ParentId = parent.Id, ParentStarted = parent.StartTime.ToUniversalTime().Ticks };
                string path = Path.Combine(stage, "install.xml");
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false))) new XmlSerializer(typeof(UpdateInstallPlan)).Serialize(writer, plan);
                using (var child = Process.Start(new ProcessStartInfo(helper, "--install-update " + Quote(path) + " " + Quote(plan.Nonce)) { UseShellExecute = false, WorkingDirectory = stage }))
                {
                    var watch = Stopwatch.StartNew();
                    while (watch.ElapsedMilliseconds < 10000)
                    {
                        string ready = Path.Combine(stage, "ready.txt"), error = Path.Combine(stage, "error.txt");
                        if (File.Exists(ready) && File.ReadAllText(ready) == plan.Nonce) { File.WriteAllText(Path.Combine(stage, "approve.txt"), plan.Nonce); return; }
                        if (File.Exists(error)) throw new IOException(File.ReadAllText(error));
                        if (child.HasExited) throw new IOException("El instalador no ha podido iniciarse. SSH Backup sigue abierto.");
                        Thread.Sleep(100);
                    }
                    throw new IOException("El instalador no ha respondido. SSH Backup sigue abierto y no se ha sustituido.");
                }
            }
        }
        public static void RequireStage(string stage)
        {
            Guid id;
            string full = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar);
            if (!Guid.TryParseExact(Path.GetFileName(full), "N", out id) || !string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(CacheRoot), StringComparison.OrdinalIgnoreCase)) throw new IOException("Carpeta de actualización no reconocida.");
            if ((new DirectoryInfo(full).Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("La carpeta de actualización no puede ser un enlace.");
        }
        public static int RunInstaller(string path, string nonce)
        {
            string stage = Path.GetDirectoryName(Path.GetFullPath(path)); bool parentStopped = false, validStage = false; UpdateInstallPlan plan = null;
            try
            {
                RequireStage(stage); validStage = true;
                if (Path.GetFileName(path) != "install.xml" || new FileInfo(path).Length > 262144) throw new IOException("Plan de instalación no válido.");
                plan = XmlFiles.Read<UpdateInstallPlan>(path);
                if (plan.Nonce != nonce || nonce.Length != 64 || plan.Package != Path.Combine(stage, "paquete.sshupdate") || FileHash(Assembly.GetExecutingAssembly().Location) != plan.HelperHash || plan.HelperHash != plan.TargetHash)
                    throw new IOException("No se ha podido autenticar el instalador.");
                if (Path.GetFullPath(Assembly.GetExecutingAssembly().Location) != Path.Combine(stage, "actualizar.exe")) throw new IOException("Instalador fuera de su carpeta.");
                var release = ReadPackage(plan.Package);
                if (!Path.IsPathRooted(plan.Target) || !plan.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || FileHash(plan.Target) != plan.TargetHash || AssemblyName.GetAssemblyName(plan.Target).Version >= Version.Parse(release.Version)) throw new IOException("El programa instalado ha cambiado o no necesita esta actualización.");
                using (var parent = Process.GetProcessById(plan.ParentId))
                {
                    if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStarted || !string.Equals(Path.GetFullPath(parent.MainModule.FileName), plan.Target, StringComparison.OrdinalIgnoreCase)) throw new IOException("La aplicación de origen no coincide.");
                    File.WriteAllText(Path.Combine(stage, "ready.txt"), nonce);
                    var approval = Stopwatch.StartNew();
                    while (!File.Exists(Path.Combine(stage, "approve.txt")) && approval.ElapsedMilliseconds < 12000) Thread.Sleep(100);
                    if (!File.Exists(Path.Combine(stage, "approve.txt")) || File.ReadAllText(Path.Combine(stage, "approve.txt")) != nonce) throw new IOException("No se ha confirmado el reinicio. No se ha sustituido el programa.");
                    if (!parent.WaitForExit(60000)) throw new IOException("SSH Backup sigue abierto. No se ha sustituido el programa.");
                    parentStopped = true;
                }
                if (FileHash(plan.Target) != plan.TargetHash) throw new IOException("El programa ha cambiado antes de instalar.");
                ApplyPackage(plan.Package, plan.Target, -1);
                File.WriteAllText(Path.Combine(stage, "resultado.txt"), "Actualización completada: " + release.Version); return 0;
            }
            catch (Exception ex)
            {
                try { if (validStage) File.WriteAllText(Path.Combine(stage, "error.txt"), ex.Message); } catch { }
                if (parentStopped)
                {
                    try { if (plan != null && File.Exists(plan.Target)) Process.Start(new ProcessStartInfo(plan.Target) { UseShellExecute = true }); } catch { }
                    System.Windows.Forms.MessageBox.Show("No se ha podido terminar la actualización. Se ha conservado la versión anterior.\n\n" + ex.Message, "SSH Backup", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                return 1;
            }
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
