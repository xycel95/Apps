using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SSHBackup
{
    public sealed class ServerConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public string Alias { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string User { get; set; } = "";
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Auth { get; set; } = "password";
        public string KeyFile { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        [XmlIgnore] public string Password { get; set; } = "";
        [XmlIgnore] public bool PasswordLoaded { get; set; } = true;
        public ServerConfig Clone() { return (ServerConfig)MemberwiseClone(); }
        [XmlIgnore] public string AddressKey { get { return Host.Trim().Trim('[', ']').ToLowerInvariant() + "|" + Port; } }
        public string BaseDestination(Settings settings) { return string.IsNullOrWhiteSpace(Destination) ? settings.Destination : Destination.Trim(); }
        public string Folder(Settings settings) { return Path.Combine(BaseDestination(settings), Alias); }
    }

    public sealed class KnownHost
    {
        public string Address { get; set; } = "";
        public string Fingerprint { get; set; } = "";
    }

    public sealed class Settings
    {
        public string Version { get; set; } = "1.1";
        public bool DarkMode { get; set; } = false;
        public bool OnlyChanges { get; set; } = false;
        public bool BandwidthEnabled { get; set; } = false;
        public decimal BandwidthValue { get; set; } = 10;
        public string BandwidthUnit { get; set; } = "Mbps";
        public bool ScheduleEnabled { get; set; } = false;
        public string ScheduleTime { get; set; } = "20:00";
        public bool ScheduleWeekdaysOnly { get; set; } = false;
        // -1 preserves the day selection from configurations created by 2.0.0.
        // New selections use Monday=1 ... Sunday=64; all seven days are 127.
        public int ScheduleDaysMask { get; set; } = -1;
        public string LastScheduleDate { get; set; } = "";
        public string ScheduledExecutable { get; set; } = "";
        public string UpdateManifestUrl { get; set; } = "";
        public string Destination { get; set; } = DefaultDestination();
        public int Parallel { get; set; } = 3;
        public int Retries { get; set; } = 1;
        public int TimeoutSeconds { get; set; } = 30;
        public string Exclusions { get; set; } = "";
        public List<ServerConfig> Servers { get; set; } = new List<ServerConfig>();
        public List<KnownHost> KnownHosts { get; set; } = new List<KnownHost>();
        public static string DefaultDestination()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(documents, "Copias_SSH");
        }
        public Settings ExecutionCopy()
        {
            return new Settings { Destination = Destination, Parallel = Parallel, Retries = Retries,
                TimeoutSeconds = TimeoutSeconds, Exclusions = Exclusions, OnlyChanges = OnlyChanges,
                BandwidthEnabled = BandwidthEnabled, BandwidthValue = BandwidthValue, BandwidthUnit = BandwidthUnit,
                ScheduleEnabled = ScheduleEnabled, ScheduleTime = ScheduleTime,
                ScheduleWeekdaysOnly = ScheduleWeekdaysOnly, ScheduleDaysMask = ScheduleDaysMask, LastScheduleDate = LastScheduleDate,
                ScheduledExecutable = ScheduledExecutable, UpdateManifestUrl = UpdateManifestUrl };
        }
    }

    public sealed class HistoryEntry
    {
        public string Operation { get; set; } = "Copia";
        public string RunId { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public string Alias { get; set; } = "";
        public string Host { get; set; } = "";
        public string Source { get; set; } = "";
        public string Status { get; set; } = "";
        public string Archive { get; set; } = "";
        public string LogFile { get; set; } = "";
        public string Error { get; set; } = "";
        public long Bytes { get; set; }
        public int Files { get; set; }
        public int Warnings { get; set; }
        public long DownloadedBytes { get; set; }
        public long ReusedBytes { get; set; }
        public long ResumedBytes { get; set; }
    }

    public sealed class HistoryDocument { public List<HistoryEntry> Entries { get; set; } = new List<HistoryEntry>(); }

    public static class Credentials
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SSHBackup.Credentials.CurrentUser.v1");
        public static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
        }
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
    }

    public static class XmlFiles
    {
        public static T Read<T>(string path)
        {
            using (var input = File.OpenRead(path))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 128L * 1024 * 1024 }))
                return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = XmlWriter.Create(temp, new XmlWriterSettings {
                    Encoding = new UTF8Encoding(false), Indent = true, CheckCharacters = true }))
                    new XmlSerializer(typeof(T)).Serialize(writer, value);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class ConfigStore
    {
        readonly object sync = new object();
        public string DirectoryPath { get; private set; }
        public string ConfigPath { get { return Path.Combine(DirectoryPath, "configuracion.xml"); } }
        public string HistoryPath { get { return Path.Combine(DirectoryPath, "historial.xml"); } }
        public Settings Settings { get; private set; }
        public HistoryDocument History { get; private set; }
        public string RecoveryNotice { get; private set; } = "";
        public ConfigStore(string path = null)
        {
            DirectoryPath = path ?? Environment.GetEnvironmentVariable("SSHBACKUP_DATA_DIR");
            if (string.IsNullOrWhiteSpace(DirectoryPath))
                DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SSHBackup");
            Directory.CreateDirectory(DirectoryPath);
            Settings = LoadOrRecover<Settings>(ConfigPath) ?? new Settings();
            History = LoadOrRecover<HistoryDocument>(HistoryPath) ?? new HistoryDocument();
            Settings.Servers = Settings.Servers ?? new List<ServerConfig>();
            Settings.KnownHosts = Settings.KnownHosts ?? new List<KnownHost>();
            History.Entries = History.Entries ?? new List<HistoryEntry>();
            Settings.Parallel = Math.Max(1, Math.Min(10, Settings.Parallel));
            Settings.Retries = Math.Max(0, Math.Min(3, Settings.Retries));
            Settings.TimeoutSeconds = Math.Max(5, Math.Min(300, Settings.TimeoutSeconds));
            Settings.BandwidthValue = Math.Max(0.1m, Math.Min(100000m, Settings.BandwidthValue));
            if (Settings.BandwidthUnit != "Mbps" && Settings.BandwidthUnit != "MB/s") Settings.BandwidthUnit = "Mbps";
            foreach (var server in Settings.Servers)
            {
                try { server.Password = Credentials.Unprotect(server.EncryptedPassword); }
                catch { server.PasswordLoaded = false; }
            }
        }
        T LoadOrRecover<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return XmlFiles.Read<T>(path); }
            catch (Exception error)
            {
                if (!File.Exists(path + ".bak"))
                    throw new IOException("No se puede leer " + path + ". No se ha sobrescrito el archivo.", error);
                T recovered = XmlFiles.Read<T>(path + ".bak");
                File.Copy(path, path + ".danado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false);
                File.Copy(path + ".bak", path, true);
                RecoveryNotice += "Se ha recuperado " + Path.GetFileName(path) + " desde su copia anterior.\r\n";
                return recovered;
            }
        }
        public void Save()
        {
            lock (sync)
            {
                foreach (var server in Settings.Servers)
                    if (server.PasswordLoaded) server.EncryptedPassword = Credentials.Protect(server.Password);
                XmlFiles.Write(ConfigPath, Settings);
            }
        }
        public string KnownFingerprint(ServerConfig server)
        {
            if (!string.IsNullOrWhiteSpace(server.Fingerprint)) return server.Fingerprint.Trim();
            lock (sync)
            {
                var found = Settings.KnownHosts.FirstOrDefault(h => h.Address == server.AddressKey);
                return found == null ? "" : found.Fingerprint;
            }
        }
        public void RememberHost(ServerConfig server, string fingerprint)
        {
            lock (sync)
            {
                var found = Settings.KnownHosts.FirstOrDefault(h => h.Address == server.AddressKey);
                if (found == null) Settings.KnownHosts.Add(new KnownHost { Address = server.AddressKey, Fingerprint = fingerprint });
                else if (!string.Equals(found.Fingerprint, fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("La identidad SSH ha cambiado. Revisa la huella antes de continuar.");
                Save();
            }
        }
        public void AddHistory(HistoryEntry result)
        {
            lock (sync)
            {
                History.Entries.Insert(0, result);
                XmlFiles.Write(HistoryPath, History);
            }
        }
        public List<HistoryEntry> ReadHistory() { lock (sync) return History.Entries.ToList(); }
    }

    public static class Validation
    {
        public static string AliasKey(string alias) { return (alias ?? "").Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant(); }
        public static string AliasError(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias.Length > 50) return "El alias debe tener entre 1 y 50 caracteres.";
            if (alias != alias.Trim() || alias.EndsWith(".") || alias == "." || alias == ".." ||
                Regex.IsMatch(alias, "[<>:\"/\\\\|?*\\x00-\\x1f]"))
                return "El alias debe ser un nombre de carpeta válido en Windows, sin espacios al principio o al final.";
            if (Regex.IsMatch(alias, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                string.Equals(alias, "Registros", StringComparison.OrdinalIgnoreCase))
                return "Ese alias está reservado. Elige otro nombre.";
            return "";
        }
        public static bool AbsoluteDestination(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (Regex.IsMatch(value, @"^[A-Za-z]:[\\/]") || value.StartsWith(@"\\"))
            {
                if (value.IndexOfAny(new[] { '*', '?', '"', '<', '>', '|' }) >= 0) return false;
                return !value.Any(char.IsControl);
            }
            return Environment.OSVersion.Platform != PlatformID.Win32NT && Path.IsPathRooted(value);
        }
        public static List<string> ServerErrors(ServerConfig server)
        {
            var errors = new List<string>();
            var alias = AliasError(server.Alias);
            if (alias.Length > 0) errors.Add(alias);
            if (string.IsNullOrWhiteSpace(server.Host) || server.Host.Any(char.IsWhiteSpace) ||
                server.Host.IndexOfAny(new[] { '/', '\\', '@', ';', '\0' }) >= 0)
                errors.Add("Indica una IP o un dominio en host, sin protocolo ni usuario.");
            if (server.Port < 1 || server.Port > 65535) errors.Add("El puerto debe estar entre 1 y 65535.");
            if (string.IsNullOrWhiteSpace(server.User)) errors.Add("Falta el usuario SSH.");
            if (string.IsNullOrWhiteSpace(server.Source)) errors.Add("Falta carpeta_origen.");
            if (server.Source != null && server.Source.Any(char.IsControl)) errors.Add("La carpeta de origen contiene caracteres de control.");
            if (!AbsoluteDestination(server.Destination)) errors.Add("carpeta_destino debe ser una ruta absoluta del PC.");
            if (server.Auth == "key" && string.IsNullOrWhiteSpace(server.KeyFile)) errors.Add("Selecciona la clave privada SSH.");
            if (!string.IsNullOrWhiteSpace(server.Fingerprint) &&
                !Regex.IsMatch(server.Fingerprint.Trim(), @"^SHA256:[A-Za-z0-9+/]{43}=?$"))
                errors.Add("La huella debe tener formato SHA256: seguido de la huella SSH.");
            return errors;
        }
        public static string FriendlyError(Exception error, ServerConfig server = null)
        {
            while (error is System.Reflection.TargetInvocationException && error.InnerException != null) error = error.InnerException;
            string result = error.Message;
            string type = error.GetType().Name;
            if (type == "SshAuthenticationException") result = "No se ha podido autenticar. Revisa el usuario y la contraseña o clave SSH.";
            if (type == "SftpPermissionDeniedException") result = "Acceso denegado por el servidor SFTP. Revisa los permisos de lectura de la carpeta de origen.";
            if (type == "SftpPathNotFoundException") result = "No se encuentra la carpeta o un archivo de origen en el servidor.";
            if (type == "SshOperationTimeoutException") result = "Se agotó el tiempo de espera de la conexión u operación SSH.";
            if (error is System.Net.Sockets.SocketException) result = "No se ha podido conectar al servidor. Revisa la dirección, el puerto SSH y la conexión de red. " + error.Message;
            if (error is UnauthorizedAccessException) result = "Acceso denegado. Revisa los permisos de lectura en el servidor y de escritura en el destino. " + result;
            if (error is PathTooLongException) result = "La ruta de destino es demasiado larga. Elige una carpeta base más corta.";
            if (server != null && !string.IsNullOrEmpty(server.Password)) result = result.Replace(server.Password, "[contraseña]");
            return result;
        }
        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes; int index = 0;
            while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
            return value.ToString(index == 0 ? "0" : "0.0", CultureInfo.CurrentCulture) + " " + units[index];
        }
    }
}
