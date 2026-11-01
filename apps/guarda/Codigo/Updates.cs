using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace Guarda
{
    public sealed class SignedUpdate
    {
        public string Version, Url, Sha256, Notes;
        public long Size;
        public byte[] Manifest, Signature;
    }

    public sealed class InstallPlan
    {
        public string Nonce, Target, TargetHash, Payload, HelperHash, Manifest, Signature;
        public int ParentId;
        public long ParentStarted;
    }

    public static class Updates
    {
        public const string Channel = "https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/actualizaciones/stable.xml";
        public const string DownloadPrefix = "https://raw.githubusercontent.com/xycel95/Apps/guarda/apps/guarda/descargas/versiones/";
        public const long MaximumExe = 64L * 1024 * 1024;
        public static string CacheRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guarda", "Actualizaciones"); } }
        public static string CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }

        public static Version ParseVersion(string text)
        {
            Version v;
            if (!Version.TryParse(text, out v) || v.Major < 0 || v.Minor < 0 || v.Build < 0) throw new InvalidDataException("La versión anunciada no es válida.");
            return new Version(v.Major, v.Minor, v.Build, Math.Max(0, v.Revision));
        }

        public static SignedUpdate ReadSigned(byte[] manifest, byte[] signature)
        {
            if (manifest == null || manifest.Length == 0 || manifest.Length > 65536 || signature == null || signature.Length != 384)
                throw new InvalidDataException("La información de actualización no es válida.");
            using (Stream key = Assembly.GetExecutingAssembly().GetManifestResourceStream("Guarda.UpdatePublicKey"))
            using (StreamReader reader = new StreamReader(key))
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false; rsa.FromXmlString(reader.ReadToEnd());
                if (!rsa.VerifyData(manifest, CryptoConfig.MapNameToOID("SHA256"), signature)) throw new InvalidDataException("No se reconoce la firma de la actualización. No se instalará.");
            }
            XmlDocument document = new XmlDocument { XmlResolver = null };
            using (MemoryStream input = new MemoryStream(manifest))
            using (XmlReader reader = XmlReader.Create(input, SafeXml())) document.Load(reader);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "GuardaUpdate" || root.GetAttribute("schema") != "1" || root.GetAttribute("app") != "guarda")
                throw new InvalidDataException("El anuncio no corresponde a Guarda.");
            Func<string, string> field = name => {
                XmlNodeList nodes = root.SelectNodes(name);
                if (nodes.Count != 1) throw new InvalidDataException("Campo de actualización no válido: " + name);
                return nodes[0].InnerText;
            };
            SignedUpdate update = new SignedUpdate { Version = field("Version"), Url = field("Url"), Sha256 = field("Sha256"), Notes = field("Notes"), Manifest = manifest, Signature = signature };
            ParseVersion(update.Version);
            if (!long.TryParse(field("Size"), NumberStyles.None, CultureInfo.InvariantCulture, out update.Size) || update.Size < 1024 || update.Size > MaximumExe || !System.Text.RegularExpressions.Regex.IsMatch(update.Sha256, "^[a-f0-9]{64}$"))
                throw new InvalidDataException("La huella o el tamaño anunciado no es válido.");
            Uri uri;
            string expected = DownloadPrefix + update.Version + "/Guarda.exe";
            if (!Uri.TryCreate(update.Url, UriKind.Absolute, out uri) || update.Url != expected || uri.UserInfo.Length > 0 || uri.Scheme != "https")
                throw new InvalidDataException("La descarga no pertenece al canal oficial de Guarda.");
            return update;
        }

        public static XmlReaderSettings SafeXml() { return new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 }; }

        public static string FileHash(string path)
        {
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        public static byte[] FetchBytes(string url, int limit, CancellationToken token)
        {
            using (MemoryStream memory = new MemoryStream()) { Fetch(url, memory, limit, token, null); return memory.ToArray(); }
        }

        private static void Fetch(string url, Stream output, long limit, CancellationToken token, Action<long> progress)
        {
            Uri uri = new Uri(url);
            if (uri.Scheme != "https" || uri.Host != "raw.githubusercontent.com" || uri.UserInfo.Length != 0) throw new InvalidDataException("La conexión de actualización no es válida.");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Timeout = 15000; request.ReadWriteTimeout = 15000; request.AllowAutoRedirect = false;
            request.UserAgent = "Guarda/" + CurrentVersion;
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            try
            {
                using (token.Register(() => request.Abort()))
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > limit) throw new IOException("El servidor no ha devuelto una actualización válida.");
                    using (Stream input = response.GetResponseStream())
                    {
                        byte[] buffer = new byte[65536]; int count; long total = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested(); total += count;
                            if (total > limit) throw new IOException("La descarga supera el tamaño anunciado.");
                            output.Write(buffer, 0, count); if (progress != null) progress(total);
                        }
                    }
                }
            }
            catch (WebException) { token.ThrowIfCancellationRequested(); throw; }
            token.ThrowIfCancellationRequested();
        }

        public static SignedUpdate Check(CancellationToken token, Func<string, int, CancellationToken, byte[]> fetch = null)
        {
            if (fetch == null) fetch = FetchBytes;
            byte[] bytes = fetch(Channel, 65536, token);
            byte[] signatureText = fetch(Channel + ".sig", 4096, token);
            if (signatureText.Length > 4096) throw new InvalidDataException("Firma demasiado grande.");
            return ReadSigned(bytes, Convert.FromBase64String(Encoding.ASCII.GetString(signatureText).Trim()));
        }

        public static void VerifyPayload(SignedUpdate release, string payload)
        {
            SignedUpdate checkedRelease = ReadSigned(release.Manifest, release.Signature);
            FileInfo file = new FileInfo(payload);
            if (!file.Exists || file.Length != checkedRelease.Size || FileHash(payload) != checkedRelease.Sha256) throw new InvalidDataException("La descarga está incompleta o no supera la comprobación de integridad.");
            using (FileStream input = File.OpenRead(payload)) if (input.ReadByte() != 'M' || input.ReadByte() != 'Z') throw new InvalidDataException("La descarga no es un programa de Windows.");
            AssemblyName assembly = AssemblyName.GetAssemblyName(payload);
            if (assembly.Name != "Guarda" || ParseVersion(assembly.Version.ToString()) != ParseVersion(checkedRelease.Version)) throw new InvalidDataException("El ejecutable no corresponde a la versión de Guarda anunciada.");
        }

        public static string Download(SignedUpdate release, CancellationToken token, Action<long> progress)
        {
            release = ReadSigned(release.Manifest, release.Signature);
            string stage = Path.Combine(CacheRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            string payload = Path.Combine(stage, "Guarda.Nueva.exe");
            try
            {
                using (FileStream output = new FileStream(payload, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { Fetch(release.Url, output, release.Size, token, progress); output.Flush(true); }
                token.ThrowIfCancellationRequested(); VerifyPayload(release, payload); return payload;
            }
            catch { try { File.Delete(payload); } catch { } throw; }
        }

        public static string Quote(string value)
        {
            if (value.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0) throw new InvalidDataException("La ruta de instalación no es válida.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }

        public static string PrepareInstall(SignedUpdate release, string payload)
        {
            VerifyPayload(release, payload);
            if (ParseVersion(release.Version) <= ParseVersion(CurrentVersion)) throw new IOException("Esta versión ya está instalada o es anterior.");
            string stage = Path.GetDirectoryName(payload), target = Assembly.GetExecutingAssembly().Location;
            RequireStage(stage);
            if (Path.GetFileName(payload) != "Guarda.Nueva.exe") throw new IOException("La carpeta de descarga no es válida.");
            // Probe the destination before closing the application, without changing its executable.
            string probe = Path.Combine(Path.GetDirectoryName(target), ".guarda-permiso-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            string helper = Path.Combine(stage, "Guarda.Instalador.exe"); File.Copy(target, helper, false);
            byte[] nonce = new byte[32]; using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(nonce);
            using (Process parent = Process.GetCurrentProcess())
            {
                InstallPlan plan = new InstallPlan { Nonce = BitConverter.ToString(nonce).Replace("-", ""), Target = Path.GetFullPath(target), TargetHash = FileHash(target), Payload = Path.GetFullPath(payload), HelperHash = FileHash(helper), ParentId = parent.Id, ParentStarted = parent.StartTime.ToUniversalTime().Ticks, Manifest = Convert.ToBase64String(release.Manifest), Signature = Convert.ToBase64String(release.Signature) };
                string path = Path.Combine(stage, "install.xml");
                using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false))) new XmlSerializer(typeof(InstallPlan)).Serialize(writer, plan);
                Process child = Process.Start(new ProcessStartInfo(helper, "--guarda-apply-update " + Quote(path) + " " + Quote(plan.Nonce)) { UseShellExecute = false, WorkingDirectory = stage });
                using (child)
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    while (watch.ElapsedMilliseconds < 10000)
                    {
                        string ready = Path.Combine(stage, "ready.txt"), error = Path.Combine(stage, "error.txt");
                        if (File.Exists(ready) && File.ReadAllText(ready) == plan.Nonce) { File.WriteAllText(Path.Combine(stage, "approve.txt"), plan.Nonce); return stage; }
                        if (File.Exists(error)) throw new IOException(File.ReadAllText(error));
                        if (child.HasExited) throw new IOException("El instalador no ha podido iniciarse. Guarda sigue abierto.");
                        Thread.Sleep(100);
                    }
                    throw new IOException("El instalador no ha respondido. Guarda sigue abierto y no se ha sustituido.");
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

        public static string InstallFiles(string target, string payload, string expectedHash, Action<string> launch)
        {
            string sibling = target + ".guarda-nueva-" + Guid.NewGuid().ToString("N"), backup = target + ".anterior-" + Guid.NewGuid().ToString("N") + ".bak";
            bool replaced = false;
            try
            {
                using (FileStream input = new FileStream(payload, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(sibling, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
                if (FileHash(sibling) != expectedHash) throw new InvalidDataException("El archivo preparado no supera SHA-256.");
                try { File.Replace(sibling, target, backup, true); }
                catch (IOException) { FallbackSwap(sibling, target, backup); }
                catch (PlatformNotSupportedException) { FallbackSwap(sibling, target, backup); }
                replaced = true;
                try { launch(target); }
                catch { File.Copy(backup, target, true); replaced = false; throw; }
                return backup;
            }
            finally { if (File.Exists(sibling)) File.Delete(sibling); if (!replaced && !File.Exists(target) && File.Exists(backup)) File.Move(backup, target); }
        }

        private static void FallbackSwap(string sibling, string target, string backup)
        {
            if (!File.Exists(sibling) || !File.Exists(target) || File.Exists(backup)) throw new IOException("No se ha podido sustituir el programa. Se conserva la versión instalada.");
            File.Move(target, backup);
            try { File.Move(sibling, target); }
            catch { File.Move(backup, target); throw; }
        }

        public static int RunInstaller(string path, string nonce)
        {
            string stage = Path.GetDirectoryName(Path.GetFullPath(path)); bool parentStopped = false, validStage = false; InstallPlan plan = null;
            try
            {
                RequireStage(stage);
                validStage = true;
                if (Path.GetFileName(path) != "install.xml" || new FileInfo(path).Length > 262144) throw new IOException("Plan de instalación no válido.");
                using (XmlReader reader = XmlReader.Create(path, SafeXml())) plan = (InstallPlan)new XmlSerializer(typeof(InstallPlan)).Deserialize(reader);
                if (plan.Nonce != nonce || nonce.Length != 64 || plan.Payload != Path.Combine(stage, "Guarda.Nueva.exe") || FileHash(Assembly.GetExecutingAssembly().Location) != plan.HelperHash || plan.HelperHash != plan.TargetHash)
                    throw new IOException("No se ha podido autenticar el instalador.");
                if (Path.GetFullPath(Assembly.GetExecutingAssembly().Location) != Path.Combine(stage, "Guarda.Instalador.exe")) throw new IOException("Instalador fuera de su carpeta.");
                SignedUpdate release = ReadSigned(Convert.FromBase64String(plan.Manifest), Convert.FromBase64String(plan.Signature)); VerifyPayload(release, plan.Payload);
                if (!Path.IsPathRooted(plan.Target) || !plan.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || FileHash(plan.Target) != plan.TargetHash || ParseVersion(AssemblyName.GetAssemblyName(plan.Target).Version.ToString()) >= ParseVersion(release.Version)) throw new IOException("El programa instalado ha cambiado o no necesita esta actualización.");
                using (Process parent = Process.GetProcessById(plan.ParentId))
                {
                    if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStarted || !string.Equals(Path.GetFullPath(parent.MainModule.FileName), plan.Target, StringComparison.OrdinalIgnoreCase)) throw new IOException("La aplicación de origen no coincide.");
                    File.WriteAllText(Path.Combine(stage, "ready.txt"), nonce);
                    Stopwatch approval = Stopwatch.StartNew();
                    while (!File.Exists(Path.Combine(stage, "approve.txt")) && approval.ElapsedMilliseconds < 12000) Thread.Sleep(100);
                    if (!File.Exists(Path.Combine(stage, "approve.txt")) || File.ReadAllText(Path.Combine(stage, "approve.txt")) != nonce) throw new IOException("La aplicación no ha confirmado el reinicio. No se ha sustituido el programa.");
                    if (!parent.WaitForExit(45000)) throw new IOException("Guarda sigue abierto. No se ha sustituido el programa.");
                    parentStopped = true;
                }
                if (FileHash(plan.Target) != plan.TargetHash) throw new IOException("El programa ha cambiado antes de instalar la actualización.");
                VerifyPayload(release, plan.Payload);
                InstallFiles(plan.Target, plan.Payload, release.Sha256, target => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }));
                File.WriteAllText(Path.Combine(stage, "resultado.txt"), "Actualización completada: " + release.Version); return 0;
            }
            catch (Exception ex)
            {
                try { if (validStage) File.WriteAllText(Path.Combine(stage, "error.txt"), ex.Message); } catch { }
                if (parentStopped) {
                    try { if (plan != null && File.Exists(plan.Target)) Process.Start(new ProcessStartInfo(plan.Target) { UseShellExecute = true }); } catch { }
                    System.Windows.Forms.MessageBox.Show("No se ha podido terminar la actualización. Se ha conservado la versión anterior.\n\n" + ex.Message, "Guarda", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                return 1;
            }
        }
    }
}
