using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;

namespace SSHBackup
{
    public sealed class CatalogFile
    {
        public string Relative { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public string Hash { get; set; }
    }
    public sealed class BackupCatalog
    {
        public string Path, Alias, Host, Source, User = "", ServerId = "";
        public int Port;
        public DateTime DateUtc;
        public Dictionary<string, CatalogFile> Files = new Dictionary<string, CatalogFile>(StringComparer.Ordinal);
        public HashSet<string> Directories = new HashSet<string>(StringComparer.Ordinal);
        public override string ToString() { return Alias + " · " + DateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + " · " + System.IO.Path.GetFileName(Path); }
    }
    public sealed class CatalogDifference
    {
        public string Change, Relative;
        public long BeforeBytes, AfterBytes;
    }
    public sealed class CatalogHit
    {
        public BackupCatalog Backup;
        public CatalogFile File;
    }
    public static class ArchiveCatalog
    {
        public static BackupCatalog Read(string path, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            using (var input = File.OpenRead(path))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                var manifests = archive.Entries.Where(e => !e.FullName.Contains("/") && e.FullName.StartsWith(".ssh-backup-", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList();
                foreach (var entry in manifests)
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.Length > 128L * 1024 * 1024) throw new InvalidDataException("El manifiesto supera el límite de lectura: " + path);
                    var xml = new XmlDocument { XmlResolver = null };
                    using (var stream = entry.Open())
                    using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = 128L * 1024 * 1024 })) xml.Load(reader);
                    if (xml.DocumentElement == null || xml.DocumentElement.Name != "SSHBackup") continue;
                    Func<string, string> text = n => xml.DocumentElement[n] == null ? "" : xml.DocumentElement[n].InnerText;
                    var result = new BackupCatalog { Path = System.IO.Path.GetFullPath(path), Alias = text("Alias"), Host = text("Servidor"), Source = text("Origen"),
                        User = text("Usuario"), ServerId = text("ServerId"), DateUtc = File.GetLastWriteTimeUtc(path) };
                    DateTime date;
                    if (DateTime.TryParse(text("FechaUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date)) result.DateUtc = date.ToUniversalTime();
                    int port; if (int.TryParse(text("Puerto"), out port)) result.Port = port;
                    foreach (XmlElement element in xml.SelectNodes("/SSHBackup/Archivos/Archivo"))
                    {
                        token.ThrowIfCancellationRequested();
                        var file = new CatalogFile { Relative = element.GetAttribute("ruta"), Hash = element.GetAttribute("sha256Base64") };
                        long size;
                        if (!long.TryParse(element.GetAttribute("bytes"), NumberStyles.None, CultureInfo.InvariantCulture, out size) || size < 0 ||
                            !DateTime.TryParse(element.GetAttribute("modificadoUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date))
                            throw new InvalidDataException("Datos de manifiesto no válidos: " + path);
                        if (string.IsNullOrEmpty(file.Relative) || result.Files.ContainsKey(file.Relative)) throw new InvalidDataException("Rutas duplicadas en el manifiesto.");
                        file.Size = size; file.ModifiedUtc = date.ToUniversalTime();
                        result.Files.Add(file.Relative, file);
                    }
                    var entries = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var content in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!entries.Add(content.FullName)) throw new InvalidDataException("El ZIP contiene rutas duplicadas.");
                        if (content.FullName.EndsWith("/", StringComparison.Ordinal)) result.Directories.Add(content.FullName);
                        else if (content.FullName != entry.FullName)
                        {
                            CatalogFile file;
                            if (!result.Files.TryGetValue(content.FullName, out file) || file.Size != content.Length)
                                throw new InvalidDataException("El manifiesto no corresponde al ZIP: " + path);
                        }
                    }
                    if (entries.Count != result.Files.Count + result.Directories.Count + 1) throw new InvalidDataException("Faltan archivos del manifiesto en el ZIP.");
                    return result;
                }
                throw new InvalidDataException("No es una copia de SSH Backup: " + System.IO.Path.GetFileName(path));
            }
        }
        public static List<string> Discover(ConfigStore store)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var history in store.ReadHistory()) if (!string.IsNullOrEmpty(history.Archive) && File.Exists(history.Archive)) paths.Add(System.IO.Path.GetFullPath(history.Archive));
            foreach (var server in store.Settings.Servers)
            {
                string folder = server.Folder(store.Settings);
                if (!Directory.Exists(folder)) continue;
                try { foreach (var path in Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)) paths.Add(System.IO.Path.GetFullPath(path)); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return paths.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        }
        public static List<CatalogDifference> Compare(BackupCatalog before, BackupCatalog after, CancellationToken token = default(CancellationToken))
        {
            var result = new List<CatalogDifference>();
            foreach (var path in before.Files.Keys.Union(after.Files.Keys, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested(); CatalogFile a, b;
                before.Files.TryGetValue(path, out a); after.Files.TryGetValue(path, out b);
                string change = a == null ? "Añadido" : b == null ? "Eliminado" :
                    a.Hash.Length > 0 && b.Hash.Length > 0 ? (a.Hash == b.Hash ? "" : "Modificado") :
                    a.Size == b.Size && a.ModifiedUtc == b.ModifiedUtc ? "" : "Modificado";
                if (change.Length > 0) result.Add(new CatalogDifference { Change = change, Relative = path, BeforeBytes = a == null ? 0 : a.Size, AfterBytes = b == null ? 0 : b.Size });
            }
            foreach (var path in before.Directories.Union(after.Directories, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (before.Directories.Contains(path) != after.Directories.Contains(path))
                    result.Add(new CatalogDifference { Relative = path, Change = after.Directories.Contains(path) ? "Carpeta añadida" : "Carpeta eliminada" });
            }
            return result;
        }
        public static List<CatalogHit> Search(IEnumerable<BackupCatalog> copies, string query, CancellationToken token = default(CancellationToken), int limit = 20000)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Escribe un nombre o una parte de la ruta para buscar.");
            var result = new List<CatalogHit>();
            foreach (var copy in copies)
                foreach (var file in copy.Files.Values)
                {
                    token.ThrowIfCancellationRequested();
                    if (file.Relative.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (result.Count >= limit) throw new InvalidOperationException("Hay más de " + limit + " resultados. Usa una búsqueda más concreta.");
                    result.Add(new CatalogHit { Backup = copy, File = file });
                }
            return result;
        }
    }
}
