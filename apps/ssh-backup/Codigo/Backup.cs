using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace SSHBackup
{
    public enum JobState { Pending, Running, Success, Warning, Error, Canceled }
    public sealed class JobInfo
    {
        public readonly object Sync = new object();
        public ServerConfig Server;
        public JobState State = JobState.Pending;
        public int Stage;
        public int Progress;
        public int TotalFiles, CompletedFiles, Warnings, Attempt;
        public long TotalBytes, CopiedBytes;
        public long DownloadedBytes, ReusedBytes, ResumedBytes;
        public string CurrentFile = "", FinalPath = "", Error = "", Phase = "En cola";
        public DateTime StartedUtc, FinishedUtc;
        public void Change(Action<JobInfo> action) { lock (Sync) action(this); }
        public JobInfo Snapshot() { lock (Sync) return (JobInfo)MemberwiseClone(); }
        public string StateText
        {
            get
            {
                switch (State)
                {
                    case JobState.Success: return "Completado";
                    case JobState.Warning: return "Con avisos";
                    case JobState.Error: return "Error";
                    case JobState.Canceled: return "Detenido";
                    default: return Phase;
                }
            }
        }
    }
    public sealed class LogRecord
    {
        public DateTime Time = DateTime.Now;
        public string Level, Alias, Message;
        public override string ToString() { return Time.ToString("HH:mm:ss") + "  " + Level.PadRight(5) + "  " + (string.IsNullOrEmpty(Alias) ? "" : Alias + " · ") + Message; }
    }
    public sealed class RunJournal
    {
        readonly object sync = new object();
        public readonly ConcurrentQueue<LogRecord> Pending = new ConcurrentQueue<LogRecord>();
        public string LogPath { get; private set; }
        public string CsvPath { get; private set; }
        public string WriteError { get; private set; } = "";
        public RunJournal(string root, string runId)
        {
            string folder = Path.Combine(root, "Registros");
            Directory.CreateDirectory(folder);
            LogPath = Path.Combine(folder, runId + ".log");
            CsvPath = Path.Combine(folder, runId + ".csv");
            File.WriteAllText(LogPath, "SSH Backup " + Program.Version + "\r\nEjecución: " + runId + "\r\n", new UTF8Encoding(false));
            File.WriteAllText(CsvPath, "operacion;alias;host;inicio;fin;resultado;archivos;bytes;avisos;zip;error\r\n", new UTF8Encoding(true));
        }
        public void Write(string level, string alias, string message)
        {
            var entry = new LogRecord { Level = level, Alias = alias, Message = message };
            Pending.Enqueue(entry);
            lock (sync)
            {
                try { File.AppendAllText(LogPath, entry.ToString() + "\r\n", new UTF8Encoding(false)); }
                catch (Exception e)
                {
                    if (WriteError.Length == 0)
                    {
                        WriteError = e.Message;
                        Pending.Enqueue(new LogRecord { Level = "ERROR", Message = "No se puede escribir el registro en disco: " + e.Message });
                    }
                }
            }
        }
        public void Result(HistoryEntry entry)
        {
            string[] cells = { entry.Operation, entry.Alias, entry.Host, entry.StartedUtc.ToLocalTime().ToString("s"),
                entry.FinishedUtc.ToLocalTime().ToString("s"), entry.Status, entry.Files.ToString(CultureInfo.InvariantCulture),
                entry.Bytes.ToString(CultureInfo.InvariantCulture), entry.Warnings.ToString(), entry.Archive, entry.Error };
            lock (sync)
                File.AppendAllText(CsvPath, string.Join(";", cells.Select(ServerImporter.CsvCell)) + "\r\n", new UTF8Encoding(true));
        }
    }
    sealed class SnapshotFile
    {
        public string Remote, Relative, Hash;
        public string Local;
        public long Size;
        public DateTime ModifiedUtc;
    }
    sealed class SnapshotDirectory
    {
        public string Remote, Relative;
        public DateTime ModifiedUtc;
        public HashSet<string> Members;
    }
    sealed class SourceSnapshot
    {
        public string Root;
        public long Bytes;
        public readonly List<SnapshotFile> Files = new List<SnapshotFile>();
        public readonly List<string> Directories = new List<string>();
        public readonly List<SnapshotDirectory> DirectoryStates = new List<SnapshotDirectory>();
        public readonly List<string> Skipped = new List<string>();
    }
    sealed class PreferredKeySource : IPrivateKeySource
    {
        public IReadOnlyCollection<Renci.SshNet.Security.HostAlgorithm> HostKeyAlgorithms { get; private set; }
        public PreferredKeySource(PrivateKeyFile file)
        {
            HostKeyAlgorithms = file.HostKeyAlgorithms.OrderBy(k => k.Name == "rsa-sha2-512" ? 0 :
                k.Name == "rsa-sha2-256" ? 1 : k.Name == "ssh-rsa" ? 9 : 2).ToList();
        }
    }
    sealed class ClientOwner : IDisposable
    {
        public SftpClient Client;
        public PrivateKeyFile Key;
        public void Dispose() { try { if (Client != null) Client.Dispose(); } finally { if (Key != null) Key.Dispose(); } }
    }
    public sealed partial class BackupRunner : IDisposable
    {
        public readonly List<JobInfo> Jobs;
        public readonly RunJournal Journal;
        public readonly string RunId;
        public bool IsPaused { get; private set; }
        public bool CheckOnly { get; private set; }
        public bool IsRunning { get; private set; }
        readonly ConfigStore store;
        readonly Settings settings;
        readonly Func<ServerConfig, string, string, bool> trust;
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        readonly ManualResetEventSlim gate = new ManualResetEventSlim(true);
        readonly object trustSync = new object();
        readonly List<Regex> excludes;
        LimitedSshTransport limitedTransport;
        public BackupRunner(ConfigStore config, IEnumerable<ServerConfig> servers,
            Func<ServerConfig, string, string, bool> trustHost, bool checkOnly = false)
        {
            store = config; settings = config.Settings.ExecutionCopy(); trust = trustHost; CheckOnly = checkOnly;
            RunId = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Jobs = servers.Select(s => new JobInfo { Server = s.Clone() }).ToList();
            Journal = new RunJournal(settings.Destination, RunId);
            excludes = (settings.Exclusions ?? "").Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0)
                .Select(p => new Regex("^" + Regex.Escape(p.Replace('\\', '/')).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                    RegexOptions.CultureInvariant)).ToList();
        }
        public void Pause(bool pause) { IsPaused = pause; if (pause) gate.Reset(); else gate.Set(); }
        public void Cancel() { cancellation.Cancel(); gate.Set(); }
        public async Task RunAsync()
        {
            if (IsRunning) throw new InvalidOperationException("Ya hay una ejecución en marcha.");
            IsRunning = true;
            if (settings.BandwidthEnabled)
                try { limitedTransport = new LimitedSshTransport(Jobs.Select(j => j.Server), BandwidthLimiter.Rate(settings.BandwidthValue, settings.BandwidthUnit)); }
                catch { IsRunning = false; throw; }
            var queue = new ConcurrentQueue<JobInfo>(Jobs);
            Journal.Write("INFO", "", (CheckOnly ? "Comprobación SSH" : "Copias") + " · " + Jobs.Count + " servidores · " + settings.Parallel + " conexiones simultáneas.");
            Journal.Write("INFO", "", settings.BandwidthEnabled ? "Límite total de descarga SSH: " + settings.BandwidthValue + " " + settings.BandwidthUnit + ", compartido entre todas las conexiones." : "Descargas sin límite de velocidad configurado.");
            try
            {
                var tasks = Enumerable.Range(0, Math.Min(settings.Parallel, Math.Max(1, Jobs.Count)))
                    .Select(_ => Task.Run(() => {
                        while (!cancellation.IsCancellationRequested)
                        {
                            try { gate.Wait(cancellation.Token); }
                            catch (OperationCanceledException) { break; }
                            JobInfo job;
                            if (!queue.TryDequeue(out job)) break;
                            Execute(job);
                        }
                    })).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var job in Jobs.Where(j => j.Snapshot().State == JobState.Pending))
                {
                    job.Change(j => { j.State = JobState.Canceled; j.Phase = "Detenido en cola"; j.FinishedUtc = DateTime.UtcNow; });
                    Journal.Write("INFO", job.Server.Alias, "Detenido antes de iniciar la conexión.");
                }
                Journal.Write("INFO", "", "Ejecución terminada · " + Jobs.Count(j => j.Snapshot().State == JobState.Success) + " correctas · " +
                    Jobs.Count(j => j.Snapshot().State == JobState.Warning) + " con avisos · " +
                    Jobs.Count(j => j.Snapshot().State == JobState.Error) + " errores.");
            }
            finally { if (limitedTransport != null) limitedTransport.Dispose(); limitedTransport = null; IsRunning = false; }
        }
        void Execute(JobInfo job)
        {
            job.Change(j => { j.State = JobState.Running; j.StartedUtc = DateTime.UtcNow; });
            try
            {
                for (int attempt = 0; attempt <= settings.Retries; attempt++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    job.Change(j => { j.Attempt = attempt + 1; j.Progress = 0; j.CopiedBytes = 0; j.CompletedFiles = 0; j.Warnings = 0; j.Error = ""; });
                    try
                    {
                        using (var owned = CreateClient(job.Server))
                        {
                            var client = owned.Client;
                            Stage(job, 1, "Conectando por SSH", 1);
                            Journal.Write("INFO", job.Server.Alias, "Conectando a " + job.Server.Host + ":" + job.Server.Port + " · intento " + (attempt + 1) + ".");
                            client.Connect();
                            cancellation.Token.ThrowIfCancellationRequested();
                            Stage(job, 2, "Revisando carpeta", 4);
                            Journal.Write("INFO", job.Server.Alias, "SSH conectado. Origen solicitado: " + job.Server.Source +
                                " · ruta SFTP: " + RemotePaths.Input(job.Server.Source));
                            var remote = new RemoteFolder(client, job.Server.Source);
                            Journal.Write("INFO", job.Server.Alias, "Carpeta remota resuelta: " + remote.Root);
                            if (CheckOnly)
                            {
                                Scan(remote, job);
                                job.Change(j => { j.Stage = 5; j.Progress = 100; j.State = JobState.Success; j.Phase = "Conexión correcta"; });
                                Journal.Write("OK", job.Server.Alias, "SSH y listado de la carpeta y sus subcarpetas comprobados: " +
                                    job.Snapshot().TotalFiles + " archivos disponibles. No se ha descargado contenido.");
                            }
                            else CreateBackup(remote, job);
                            break;
                        }
                    }
                    catch (Exception error)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (attempt >= settings.Retries || !Retryable(error)) throw;
                        Journal.Write("AVISO", job.Server.Alias, Validation.FriendlyError(error, job.Server) + " Se reintentará reutilizando la descarga conservada cuando siga siendo válida.");
                        job.Change(j => { j.Phase = "Esperando reintento"; });
                        if (cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2 * (attempt + 1))))
                            cancellation.Token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                job.Change(j => { j.State = JobState.Canceled; j.Phase = "Detenido"; j.Error = "Operación detenida por el usuario."; });
                Journal.Write("INFO", job.Server.Alias, "Operación detenida. La copia incompleta no se ha publicado como ZIP.");
            }
            catch (Exception error)
            {
                if (cancellation.IsCancellationRequested)
                    job.Change(j => { j.State = JobState.Canceled; j.Phase = "Detenido"; j.Error = "Operación detenida por el usuario."; });
                else
                    job.Change(j => { j.State = JobState.Error; j.Phase = "Error"; j.Error = Validation.FriendlyError(error, j.Server); });
                Journal.Write(job.Snapshot().State == JobState.Canceled ? "INFO" : "ERROR", job.Server.Alias, job.Snapshot().Error);
            }
            finally
            {
                job.Change(j => j.FinishedUtc = DateTime.UtcNow);
                var snapshot = job.Snapshot();
                var result = new HistoryEntry {
                    Operation = CheckOnly ? "Comprobación SSH" : "Copia", RunId = RunId, StartedUtc = snapshot.StartedUtc,
                    FinishedUtc = snapshot.FinishedUtc, Alias = snapshot.Server.Alias, Host = snapshot.Server.Host,
                    Source = snapshot.Server.Source, Archive = snapshot.FinalPath, LogFile = Journal.LogPath,
                    Status = CheckOnly && snapshot.State == JobState.Success ? "Conexión correcta" : snapshot.StateText,
                    Error = snapshot.Error, Bytes = snapshot.CopiedBytes, Files = snapshot.CompletedFiles, Warnings = snapshot.Warnings,
                    DownloadedBytes = snapshot.DownloadedBytes, ReusedBytes = snapshot.ReusedBytes, ResumedBytes = snapshot.ResumedBytes
                };
                try { Journal.Result(result); }
                catch (Exception error) { Journal.Write("ERROR", result.Alias, "No se ha podido guardar el resumen CSV: " + error.Message); }
                try { store.AddHistory(result); }
                catch (Exception error) { Journal.Write("ERROR", result.Alias, "No se ha podido guardar el historial: " + error.Message); }
            }
        }
        ClientOwner CreateClient(ServerConfig server)
        {
            if (!server.PasswordLoaded) throw new InvalidOperationException("No se puede descifrar la contraseña guardada. Edita el acceso de este servidor.");
            var auth = new List<AuthenticationMethod>();
            PrivateKeyFile privateKey = null;
            if (server.Auth == "key")
            {
                if (!File.Exists(server.KeyFile)) throw new FileNotFoundException("No se encuentra la clave privada SSH: " + server.KeyFile);
                privateKey = string.IsNullOrEmpty(server.Password) ? new PrivateKeyFile(server.KeyFile) : new PrivateKeyFile(server.KeyFile, server.Password);
                auth.Add(new PrivateKeyAuthenticationMethod(server.User, new PreferredKeySource(privateKey)));
            }
            else
            {
                auth.Add(new PasswordAuthenticationMethod(server.User, server.Password ?? ""));
                var keyboard = new KeyboardInteractiveAuthenticationMethod(server.User);
                keyboard.AuthenticationPrompt += (sender, args) => {
                    foreach (var prompt in args.Prompts)
                        if (Regex.IsMatch(prompt.Request, "password|contrase|passphrase", RegexOptions.IgnoreCase)) prompt.Response = server.Password ?? "";
                };
                auth.Add(keyboard);
            }
            var connection = limitedTransport == null ?
                new ConnectionInfo(server.Host.Trim('[', ']'), server.Port, server.User, auth.ToArray()) :
                new ConnectionInfo(server.Host.Trim('[', ']'), server.Port, server.User, ProxyTypes.Socks5, "127.0.0.1", limitedTransport.Port, "", "", auth.ToArray());
            connection.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            var client = new SftpClient(connection) {
                OperationTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
                KeepAliveInterval = TimeSpan.FromSeconds(15)
            };
            string trustError = "";
            client.HostKeyReceived += (sender, args) => {
                args.CanTrust = false;
                string fingerprint;
                using (var sha = SHA256.Create()) fingerprint = "SHA256:" + Convert.ToBase64String(sha.ComputeHash(args.HostKey)).TrimEnd('=');
                lock (trustSync)
                {
                    if (cancellation.IsCancellationRequested) return;
                    string expected = store.KnownFingerprint(server).Trim().TrimEnd('=');
                    if (expected.Length > 0)
                    {
                        args.CanTrust = string.Equals(expected, fingerprint, StringComparison.Ordinal);
                        if (!args.CanTrust)
                            trustError = "La huella SSH de " + server.Host + " ha cambiado. Se esperaba " + expected + " y se ha recibido " + fingerprint + ". Revisa la identidad del servidor.";
                    }
                    else if (trust != null && trust(server, args.HostKeyName, fingerprint))
                    {
                        store.RememberHost(server, fingerprint); args.CanTrust = true;
                        Journal.Write("INFO", server.Alias, "Identidad SSH guardada: " + fingerprint);
                    }
                    else trustError = "No se ha aceptado la identidad SSH de este servidor.";
                }
                if (!args.CanTrust && trustError.Length > 0) throw new SshConnectionException(trustError);
            };
            return new ClientOwner { Client = client, Key = privateKey };
        }
        static bool Retryable(Exception error)
        {
            if (error is RemoteSourceException) return Retryable(error.InnerException);
            if (error is SshAuthenticationException || error is SftpPermissionDeniedException || error is SftpPathNotFoundException ||
                error is InvalidDataException || error is UnauthorizedAccessException || error is PathTooLongException) return false;
            if (error.Message.IndexOf("huella SSH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                error.Message.IndexOf("identidad SSH", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return error is SshConnectionException || error is SshOperationTimeoutException || error is IOException ||
                error is System.Net.Sockets.SocketException;
        }
        void Stage(JobInfo job, int number, string phase, int percent)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            job.Change(j => { j.Stage = number; j.Phase = phase; j.Progress = percent; });
        }
        bool Excluded(string relative, string name)
        {
            return excludes.Any(r => r.IsMatch(relative) || r.IsMatch(name));
        }
        static void SafeName(string name)
        {
            if (name.Length == 0 || name == "." || name == ".." || name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0 ||
                name.Any(char.IsControl))
                throw new InvalidDataException("El origen contiene un nombre que no se puede guardar de forma segura en un ZIP para Windows: " + name);
        }
        SourceSnapshot Scan(RemoteFolder remote, JobInfo job)
        {
            var source = new SourceSnapshot { Root = remote.Root };
            var queue = new Queue<Tuple<string, string, int>>();
            queue.Enqueue(Tuple.Create(source.Root, "", 0));
            while (queue.Count > 0)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var current = queue.Dequeue();
                if (current.Item3 > 256) throw new InvalidDataException("La carpeta supera 256 niveles de profundidad.");
                job.Change(j => { j.CurrentFile = current.Item2.Length > 0 ? current.Item2 : source.Root; j.TotalFiles = source.Files.Count; j.TotalBytes = source.Bytes; });
                var directory = new SnapshotDirectory {
                    Remote = current.Item1, Relative = current.Item2,
                    ModifiedUtc = remote.Attributes(current.Item1).LastWriteTimeUtc, Members = new HashSet<string>(StringComparer.Ordinal)
                };
                foreach (var entry in remote.List(current.Item1))
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (entry.Name == "." || entry.Name == "..") continue;
                    SafeName(entry.Name);
                    string relative = current.Item2.Length == 0 ? entry.Name : current.Item2 + "/" + entry.Name;
                    if (Excluded(relative, entry.Name))
                    {
                        source.Skipped.Add(relative + " (excluido por configuración)");
                        continue;
                    }
                    directory.Members.Add(entry.Name);
                    if (entry.IsSymbolicLink || (!entry.IsDirectory && !entry.IsRegularFile))
                    {
                        source.Skipped.Add(relative + " (enlace simbólico o archivo especial)");
                        Journal.Write("AVISO", job.Server.Alias, "Omitido: " + relative + " · enlace simbólico o archivo especial.");
                    }
                    else if (entry.IsDirectory)
                    {
                        source.Directories.Add(relative + "/");
                        queue.Enqueue(Tuple.Create(RemotePaths.Child(current.Item1, entry.Name), relative, current.Item3 + 1));
                    }
                    else
                    {
                        if (source.Files.Count >= 1000000) throw new InvalidDataException("La carpeta supera un millón de archivos por copia.");
                        source.Files.Add(new SnapshotFile { Remote = RemotePaths.Child(current.Item1, entry.Name), Relative = relative, Size = entry.Length, ModifiedUtc = entry.LastWriteTimeUtc });
                        source.Bytes = checked(source.Bytes + entry.Length);
                    }
                }
                source.DirectoryStates.Add(directory);
            }
            job.Change(j => { j.TotalFiles = source.Files.Count; j.TotalBytes = source.Bytes; j.Warnings = source.Skipped.Count(s => !s.Contains("(excluido por configuración)")); });
            Journal.Write("INFO", job.Server.Alias, "Origen revisado: " + source.Files.Count + " archivos · " + Validation.FormatBytes(source.Bytes) +
                " · " + source.Files.Count(f => f.Relative.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) + " ZIP" +
                " · " + source.Directories.Count + " subcarpetas · " + source.Skipped.Count + " elementos omitidos.");
            return source;
        }
        void CreateBackup(RemoteFolder remote, JobInfo job)
        { CreateResumableBackup(remote, job); }
        static FileStream CreatePartial(string path, out bool owned)
        {
            owned = false;
            var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 131072);
            owned = true;
            return file;
        }
        void ValidateSource(RemoteFolder remote, SourceSnapshot source)
        {
            foreach (var item in source.Files)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var attributes = remote.Attributes(item.Remote);
                if (!attributes.IsRegularFile || attributes.Size != item.Size || attributes.LastWriteTimeUtc != item.ModifiedUtc)
                    throw new IOException("El origen cambió antes de terminar la copia: " + item.Relative + ".");
            }
            foreach (var directory in source.DirectoryStates)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var members = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in remote.List(directory.Remote))
                {
                    if (item.Name == "." || item.Name == "..") continue;
                    string relative = directory.Relative.Length == 0 ? item.Name : directory.Relative + "/" + item.Name;
                    if (!Excluded(relative, item.Name)) members.Add(item.Name);
                }
                if (!members.SetEquals(directory.Members))
                {
                    string added = string.Join(", ", members.Except(directory.Members).Take(5));
                    string removed = string.Join(", ", directory.Members.Except(members).Take(5));
                    throw new IOException("Se añadieron o borraron elementos durante la copia de " + directory.Remote + "." +
                        (added.Length == 0 ? "" : " Añadidos: " + added + ".") + (removed.Length == 0 ? "" : " Ausentes: " + removed + "."));
                }
            }
        }
        byte[] Manifest(JobInfo job, SourceSnapshot source)
        {
            using (var memory = new MemoryStream())
            {
                using (var xml = XmlWriter.Create(memory, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                {
                    xml.WriteStartDocument(); xml.WriteStartElement("SSHBackup");
                    xml.WriteAttributeString("version", "1.0"); xml.WriteAttributeString("ejecucion", RunId);
                    xml.WriteElementString("Alias", job.Server.Alias); xml.WriteElementString("Servidor", job.Server.Host);
                    xml.WriteElementString("Origen", source.Root);
                    xml.WriteElementString("Puerto", job.Server.Port.ToString(CultureInfo.InvariantCulture));
                    xml.WriteElementString("Usuario", job.Server.User);
                    xml.WriteElementString("ServerId", job.Server.Id);
                    xml.WriteElementString("FechaUtc", DateTime.UtcNow.ToString("o"));
                    xml.WriteStartElement("Archivos");
                    foreach (var item in source.Files)
                    {
                        xml.WriteStartElement("Archivo"); xml.WriteAttributeString("ruta", item.Relative);
                        xml.WriteAttributeString("bytes", item.Size.ToString(CultureInfo.InvariantCulture));
                        xml.WriteAttributeString("modificadoUtc", item.ModifiedUtc.ToString("o"));
                        xml.WriteAttributeString("sha256Base64", item.Hash); xml.WriteEndElement();
                    }
                    xml.WriteEndElement(); xml.WriteStartElement("Omitidos");
                    foreach (string skipped in source.Skipped) xml.WriteElementString("Elemento", skipped);
                    xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument();
                }
                return memory.ToArray();
            }
        }
        void VerifyArchive(string path, SourceSnapshot source, string manifestName, byte[] manifest, JobInfo job)
        {
            using (var stream = File.OpenRead(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8))
            {
                if (zip.Entries.Count != source.Files.Count + source.Directories.Count + 1)
                    throw new InvalidDataException("La estructura del ZIP no coincide con el origen.");
                var files = source.Files.ToDictionary(f => f.Relative, StringComparer.Ordinal);
                long verified = 0; var buffer = new byte[131072];
                foreach (var entry in zip.Entries)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (entry.FullName.EndsWith("/")) continue;
                    SnapshotFile expected;
                    if (entry.FullName == manifestName)
                    {
                        using (var input = entry.Open())
                        using (var memory = new MemoryStream())
                        { input.CopyTo(memory); if (!memory.ToArray().SequenceEqual(manifest)) throw new InvalidDataException("El manifiesto del ZIP no es válido."); }
                        continue;
                    }
                    if (!files.TryGetValue(entry.FullName, out expected) || entry.Length != expected.Size)
                        throw new InvalidDataException("Contenido inesperado en el ZIP: " + entry.FullName + ".");
                    job.Change(j => j.CurrentFile = entry.FullName);
                    using (var hash = SHA256.Create())
                    using (var input = entry.Open())
                    {
                        int read; long size = 0;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellation.Token.ThrowIfCancellationRequested();
                            hash.TransformBlock(buffer, 0, read, null, 0); verified += read; size += read;
                            job.Change(j => j.Progress = source.Bytes == 0 ? 98 : 83 + (int)Math.Min(15, verified * 15.0 / source.Bytes));
                        }
                        hash.TransformFinalBlock(new byte[0], 0, 0);
                        if (size != expected.Size || !string.Equals(Convert.ToBase64String(hash.Hash), expected.Hash, StringComparison.Ordinal))
                            throw new InvalidDataException("La verificación SHA-256 falló en " + entry.FullName + ".");
                    }
                }
            }
        }
        public void Dispose() { cancellation.Dispose(); gate.Dispose(); }
    }
}
