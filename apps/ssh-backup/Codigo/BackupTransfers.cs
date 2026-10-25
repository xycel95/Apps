using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SSHBackup
{
    public sealed class DownloadCheckpoint
    {
        public string Relative { get; set; } = "";
        public long Size { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public string Hash { get; set; } = "";
    }
    public sealed partial class BackupRunner
    {
        static string CacheKey(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        string HashLocal(string path)
        {
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(path))
            {
                byte[] buffer = new byte[131072]; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                { cancellation.Token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, count, null, 0); }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return Convert.ToBase64String(hash.Hash);
            }
        }
        BackupCatalog PreviousBackup(JobInfo job, SourceSnapshot source)
        {
            if (!settings.OnlyChanges) return null;
            foreach (var history in store.ReadHistory().Where(h => h.Operation == "Copia" && h.Alias == job.Server.Alias &&
                h.Host == job.Server.Host && !string.IsNullOrWhiteSpace(h.Archive)).Take(100))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!File.Exists(history.Archive)) continue;
                try
                {
                    var catalog = ArchiveCatalog.Read(history.Archive, cancellation.Token);
                    // Legacy copies have no account/port anchor: the first 2.0 run establishes a safe baseline.
                    if (catalog.Host == job.Server.Host && catalog.Source == source.Root && catalog.Port == job.Server.Port &&
                        catalog.User == job.Server.User && catalog.ServerId == job.Server.Id) return catalog;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is System.Xml.XmlException || e is InvalidOperationException)
                { Journal.Write("AVISO", job.Server.Alias, "No se reutilizará la copia anterior: " + e.Message); }
            }
            return null;
        }
        void ProgressBytes(JobInfo job, SourceSnapshot source, long bytes, bool downloaded)
        {
            job.Change(j => {
                j.CopiedBytes += bytes; if (downloaded) j.DownloadedBytes += bytes;
                j.Progress = source.Bytes == 0 ? 65 : 8 + (int)Math.Min(57, j.CopiedBytes * 57.0 / source.Bytes);
            });
        }
        void ValidateItem(RemoteFolder remote, SnapshotFile item)
        {
            var attributes = remote.Attributes(item.Remote);
            if (!attributes.IsRegularFile || attributes.Size != item.Size || attributes.LastWriteTimeUtc != item.ModifiedUtc)
                throw new IOException("El archivo ha cambiado durante la copia: " + item.Relative + ". Se volverá a descargar en el siguiente intento.");
        }
        void DownloadItem(RemoteFolder remote, SnapshotFile item, string cache, SourceSnapshot source, JobInfo job,
            BackupCatalog previous, ZipArchive previousZip)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            ValidateItem(remote, item);
            string name = CacheKey(item.Relative);
            string data = Path.Combine(cache, name + ".part"), meta = Path.Combine(cache, name + ".xml");
            item.Local = data;
            DownloadCheckpoint checkpoint = null;
            try { if (File.Exists(meta)) checkpoint = XmlFiles.Read<DownloadCheckpoint>(meta); } catch (Exception e) when (e is IOException || e is InvalidOperationException || e is System.Xml.XmlException) { }
            if (checkpoint == null || checkpoint.Relative != item.Relative || checkpoint.Size != item.Size || checkpoint.ModifiedUtc != item.ModifiedUtc ||
                (File.Exists(data) && new FileInfo(data).Length > item.Size))
            {
                if (File.Exists(data)) File.Delete(data);
                checkpoint = new DownloadCheckpoint { Relative = item.Relative, Size = item.Size, ModifiedUtc = item.ModifiedUtc };
                XmlFiles.Write(meta, checkpoint);
            }
            if (File.Exists(data) && new FileInfo(data).Length == item.Size && checkpoint.Hash.Length > 0)
            {
                if (HashLocal(data) == checkpoint.Hash)
                {
                    item.Hash = checkpoint.Hash; job.Change(j => j.ResumedBytes += item.Size);
                    ProgressBytes(job, source, item.Size, false);
                    Journal.Write("INFO", job.Server.Alias, "Descarga anterior recuperada y verificada: " + item.Relative);
                    ValidateItem(remote, item); return;
                }
                File.Delete(data); checkpoint.Hash = ""; XmlFiles.Write(meta, checkpoint);
                Journal.Write("AVISO", job.Server.Alias, "El archivo conservado no superó la verificación. Se descargará de nuevo: " + item.Relative);
            }
            CatalogFile old;
            if (!File.Exists(data) && previous != null && previousZip != null && previous.Files.TryGetValue(item.Relative, out old) &&
                old.Size == item.Size && old.ModifiedUtc == item.ModifiedUtc && !string.IsNullOrEmpty(old.Hash))
            {
                var entry = previousZip.GetEntry(item.Relative);
                if (entry != null && entry.Length == item.Size)
                {
                    try
                    {
                        using (var input = entry.Open())
                        using (var output = new FileStream(data, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[131072]; int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            { cancellation.Token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
                        }
                        if (new FileInfo(data).Length != item.Size || HashLocal(data) != old.Hash)
                            throw new InvalidDataException("La copia anterior no supera SHA-256: " + item.Relative);
                        checkpoint.Hash = old.Hash; XmlFiles.Write(meta, checkpoint); item.Hash = old.Hash;
                        job.Change(j => j.ReusedBytes += item.Size); ProgressBytes(job, source, item.Size, false);
                        ValidateItem(remote, item);
                        Journal.Write("INFO", job.Server.Alias, "Sin cambios, reutilizado y verificado: " + item.Relative); return;
                    }
                    catch (IOException e)
                    {
                        if (File.Exists(data)) File.Delete(data);
                        checkpoint.Hash = ""; XmlFiles.Write(meta, checkpoint);
                        Journal.Write("AVISO", job.Server.Alias, "Se descargará el original: " + e.Message);
                    }
                }
            }
            long offset = File.Exists(data) ? new FileInfo(data).Length : 0;
            if (offset > 0)
            {
                job.Change(j => j.ResumedBytes += offset); ProgressBytes(job, source, offset, false);
                Journal.Write("INFO", job.Server.Alias, "Reanudando " + item.Relative + " desde " + Validation.FormatBytes(offset) + ".");
            }
            // Clear a completion marker before appending, even after an unclean shutdown.
            checkpoint.Hash = ""; XmlFiles.Write(meta, checkpoint);
            using (var input = remote.OpenRead(item.Remote))
            using (var output = new FileStream(data, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 131072))
            {
                input.Seek(offset, SeekOrigin.Begin); output.Position = offset;
                var buffer = new byte[131072]; int read; long total = offset;
                while ((read = remote.Read(input, buffer, item.Remote)) > 0)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (total + read > item.Size) throw new IOException("El origen creció durante la descarga: " + item.Relative);
                    output.Write(buffer, 0, read); total += read; ProgressBytes(job, source, read, true);
                }
                output.Flush(true);
                if (total != item.Size) throw new IOException("Descarga interrumpida o tamaño inesperado: " + item.Relative);
            }
            try { ValidateItem(remote, item); }
            catch { File.Delete(data); File.Delete(meta); throw; }
            item.Hash = HashLocal(data); checkpoint.Hash = item.Hash; XmlFiles.Write(meta, checkpoint);
        }
        void CreateResumableBackup(RemoteFolder remote, JobInfo job)
        {
            SourceSnapshot source = Scan(remote, job);
            string folder = job.Server.Folder(settings); Directory.CreateDirectory(folder);
            string identity = job.Server.Id + "|" + job.Server.AddressKey + "|" + job.Server.User + "|" +
                store.KnownFingerprint(job.Server) + "|" + source.Root + "|" + settings.Exclusions;
            string cache = Path.Combine(folder, ".sshbackup-resume", CacheKey(identity));
            Directory.CreateDirectory(cache);
            string final = Path.Combine(folder, RunId + ".zip"), partial = final + ".partial";
            bool completed = false;
            using (var cacheLock = new FileStream(Path.Combine(cache, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    job.Change(j => { j.DownloadedBytes = 0; j.ReusedBytes = 0; j.ResumedBytes = 0; });
                    Stage(job, 3, "Descargando / reanudando", 8);
                    var previous = PreviousBackup(job, source);
                    if (settings.OnlyChanges) Journal.Write("INFO", job.Server.Alias, previous == null ?
                        "Solo novedades activado: no hay una base compatible; se hará una descarga completa." : "Solo novedades activado: se reutilizará " + previous.Path);
                    using (var previousStream = previous == null ? null : File.OpenRead(previous.Path))
                    using (var previousZip = previousStream == null ? null : new ZipArchive(previousStream, ZipArchiveMode.Read))
                    {
                        foreach (var item in source.Files)
                        {
                            cancellation.Token.ThrowIfCancellationRequested(); job.Change(j => j.CurrentFile = item.Relative);
                            DownloadItem(remote, item, cache, source, job, previous, previousZip);
                            job.Change(j => j.CompletedFiles++);
                        }
                    }
                    Stage(job, 3, "Comprobando cambios en el origen", 66); ValidateSource(remote, source);
                    string manifestName = ".ssh-backup-" + Guid.NewGuid().ToString("N") + ".xml";
                    byte[] manifest = Manifest(job, source);
                    bool owned = false;
                    using (var file = CreatePartial(partial, out owned))
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Create, false, Encoding.UTF8))
                    {
                        foreach (var directory in source.Directories) zip.CreateEntry(directory, CompressionLevel.NoCompression);
                        int packed = 0;
                        foreach (var item in source.Files)
                        {
                            cancellation.Token.ThrowIfCancellationRequested(); job.Change(j => { j.Phase = "Creando ZIP completo"; j.CurrentFile = item.Relative; });
                            var entry = zip.CreateEntry(item.Relative, CompressionLevel.Optimal);
                            if (item.ModifiedUtc.Year >= 1980 && item.ModifiedUtc.Year <= 2107)
                                entry.LastWriteTime = new DateTimeOffset(DateTime.SpecifyKind(item.ModifiedUtc, DateTimeKind.Utc));
                            using (var input = File.OpenRead(item.Local))
                            using (var output = entry.Open())
                            {
                                var buffer = new byte[131072]; int read;
                                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                                { cancellation.Token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
                            }
                            packed++; job.Change(j => j.Progress = 67 + (int)(14.0 * packed / Math.Max(1, source.Files.Count)));
                        }
                        using (var output = zip.CreateEntry(manifestName, CompressionLevel.Optimal).Open()) output.Write(manifest, 0, manifest.Length);
                    }
                    Stage(job, 4, "Verificando ZIP completo", 83); VerifyArchive(partial, source, manifestName, manifest, job);
                    cancellation.Token.ThrowIfCancellationRequested(); File.Move(partial, final); completed = true;
                    job.Change(j => { j.FinalPath = final; j.Progress = 100; j.Stage = 5; j.CurrentFile = "";
                        j.State = j.Warnings > 0 ? JobState.Warning : JobState.Success; j.Phase = "ZIP completo guardado y verificado"; });
                    var result = job.Snapshot();
                    Journal.Write("OK", job.Server.Alias, "ZIP verificado: " + final + " · descargados: " + Validation.FormatBytes(result.DownloadedBytes) +
                        " · reutilizados: " + Validation.FormatBytes(result.ReusedBytes) + " · recuperados: " + Validation.FormatBytes(result.ResumedBytes));
                }
                finally
                {
                    try { if (File.Exists(partial)) File.Delete(partial); }
                    catch (IOException e) { Journal.Write("AVISO", job.Server.Alias, "No se pudo retirar el ZIP incompleto: " + e.Message); }
                    if (!completed) Journal.Write("INFO", job.Server.Alias, "Descarga conservada en el PC para reanudar en el siguiente intento.");
                }
            }
            if (completed)
                try { Directory.Delete(cache, true); }
                catch (IOException e) { Journal.Write("AVISO", job.Server.Alias, "Copia completada; no se pudo limpiar la caché: " + e.Message); }
        }
    }
}
