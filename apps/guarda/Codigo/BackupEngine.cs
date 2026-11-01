using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Guarda
{
    public sealed class BackupOptions
    {
        public string Source, Destination, Name;
        public bool Verify = true;
        public DateTime Date = DateTime.Now;
        public string ReportsDirectory;
    }

    public sealed class BackupProgress
    {
        public string Stage, File;
        public long Done, Total, CopiedBytes;
        public int FilesDone, FilesTotal;
        public bool Indeterminate;
    }

    public sealed class BackupResult
    {
        public bool Success, Cancelled;
        public string OutputPath = "", ReportPath = "", Message = "";
        public int Files;
        public long Bytes;
        public readonly List<string> Warnings = new List<string>();
    }

    internal sealed class Entry
    {
        public string Relative;
        public long Length;
        public DateTime Modified, Created;
        public bool Directory;
    }

    internal sealed class Plan
    {
        public readonly List<Entry> Files = new List<Entry>();
        public readonly List<Entry> Directories = new List<Entry>();
        public readonly List<string> Links = new List<string>();
        public long Bytes;
    }

    public static class BackupEngine
    {
        public static readonly bool Windows = Path.DirectorySeparatorChar == '\\';
        private static readonly StringComparison PathComparison = Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new IOException("Elige las carpetas de origen y destino.");
            string p = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            string root = Path.GetPathRoot(p);
            while (p.Length > root.Length && (p.EndsWith("/") || p.EndsWith("\\"))) p = p.Substring(0, p.Length - 1);
            if (Windows && p.StartsWith(@"\\.\", StringComparison.Ordinal)) throw new IOException("Selecciona una carpeta normal del Explorador.");
            return p;
        }

        public static string DefaultName(string source)
        {
            try
            {
                string p = Normalize(source);
                string n = new DirectoryInfo(p).Name.TrimEnd(':', '\\', '/');
                return string.IsNullOrWhiteSpace(n) ? "MiCopia" : n;
            }
            catch { return "MiCopia"; }
        }

        public static string ValidateName(string value)
        {
            string n = (value ?? "").Trim();
            if (n.Length == 0) throw new IOException("Escribe un nombre para la copia.");
            if (n.Length > 100) throw new IOException("El nombre de la copia debe tener como máximo 100 caracteres.");
            if (n == "." || n == ".." || n.EndsWith(".") || n.EndsWith(" ") || n.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || n.Any(c => c < 32))
                throw new IOException("El nombre contiene caracteres no válidos. No uses: < > : \" / \\ | ? *");
            return n;
        }

        public static string Preview(string source, string destination, string name, DateTime date)
        {
            string stem = date.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture) + "_" + ValidateName(name);
            return Path.Combine(Normalize(destination), stem);
        }

        public static void ValidatePaths(string source, string destination)
        {
            if (!Directory.Exists(source)) throw new IOException("No se encuentra el origen. Comprueba la ruta y que la unidad esté conectada.");
            if (!Directory.Exists(destination)) throw new IOException("No se encuentra el destino. Conecta la unidad o elige una carpeta que exista.");
            string realSource = Resolve(source), realDestination = Resolve(destination);
            if (SameOrChild(realDestination, realSource))
                throw new IOException("El destino no puede ser el propio origen ni estar dentro de él. Elige otra carpeta para evitar que la copia se copie a sí misma.");
        }

        private static bool SameOrChild(string candidate, string parent)
        {
            return candidate.Equals(parent, PathComparison) || candidate.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison);
        }

        private static string Resolve(string path)
        {
            if (!Windows)
            {
                IntPtr resolved = realpath(path, IntPtr.Zero);
                if (resolved == IntPtr.Zero) throw new IOException("No se puede comprobar la ubicación real de la carpeta: " + path);
                try { return Normalize(Marshal.PtrToStringAnsi(resolved)); }
                finally { free(resolved); }
            }
            using (SafeFileHandle handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("No se puede acceder a la carpeta: " + path, new Win32Exception(Marshal.GetLastWin32Error()));
                StringBuilder buffer = new StringBuilder(32768);
                uint count = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                if (count == 0 || count >= buffer.Capacity) throw new IOException("No se puede resolver la ubicación real de: " + path);
                string finalPath = buffer.ToString();
                if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) finalPath = @"\\" + finalPath.Substring(8);
                else if (finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)) finalPath = finalPath.Substring(4);
                return Normalize(finalPath);
            }
        }

        private static string ResolvePotential(string path)
        {
            string current = Normalize(path);
            Stack<string> tail = new Stack<string>();
            while (!Directory.Exists(current))
            {
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current) throw new IOException("No se puede resolver la carpeta del informe.");
                tail.Push(Path.GetFileName(current)); current = parent;
            }
            current = Resolve(current);
            while (tail.Count > 0) current = Path.Combine(current, tail.Pop());
            return Normalize(current);
        }

        private static Plan Scan(string source, CancellationToken token, Action<BackupProgress> report)
        {
            Plan plan = new Plan();
            Stack<string> pending = new Stack<string>();
            pending.Push(source);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string folder = pending.Pop();
                DirectoryInfo info = new DirectoryInfo(folder);
                if (!info.Exists) throw new IOException("El origen ha cambiado o se ha desconectado: " + folder);
                plan.Directories.Add(new Entry { Relative = folder == source ? "" : folder.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar), Directory = true, Modified = info.LastWriteTimeUtc, Created = info.CreationTimeUtc });
                try
                {
                    foreach (FileSystemInfo item in info.EnumerateFileSystemInfos())
                    {
                        token.ThrowIfCancellationRequested();
                        string relative = item.FullName.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                        if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            plan.Links.Add(relative);
                            continue;
                        }
                        if ((item.Attributes & FileAttributes.Directory) != 0) pending.Push(item.FullName);
                        else
                        {
                            FileInfo file = (FileInfo)item;
                            plan.Files.Add(new Entry { Relative = relative, Length = file.Length, Modified = file.LastWriteTimeUtc, Created = file.CreationTimeUtc });
                            plan.Bytes = checked(plan.Bytes + file.Length);
                        }
                        if ((plan.Files.Count + plan.Directories.Count) % 128 == 0 && report != null)
                            report(new BackupProgress { Stage = "Analizando carpetas", File = relative, Indeterminate = true, FilesTotal = plan.Files.Count, Total = plan.Bytes });
                    }
                }
                catch (UnauthorizedAccessException ex) { throw new IOException("No se puede leer la carpeta: " + folder + ". Comprueba sus permisos.", ex); }
            }
            return plan;
        }

        private static void Preflight(Plan plan, string destination)
        {
            DriveInfo drive;
            try { drive = new DriveInfo(Path.GetPathRoot(destination)); if (!drive.IsReady) return; }
            catch (ArgumentException) { return; }
            catch (IOException) { return; }
            long free;
            string format;
            try { free = drive.AvailableFreeSpace; format = drive.DriveFormat; }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            if (plan.Bytes > free) throw new IOException("No hay espacio suficiente en el destino. Necesitas " + FormatBytes(plan.Bytes) + " y hay " + FormatBytes(free) + " disponibles.");
            if (string.Equals(format, "FAT32", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "FAT", StringComparison.OrdinalIgnoreCase))
            {
                Entry big = plan.Files.FirstOrDefault(f => f.Length > uint.MaxValue);
                if (big != null) throw new IOException("La unidad FAT32 no admite este archivo de más de 4 GB: " + big.Relative + ". Elige otro destino compatible, por ejemplo exFAT o NTFS.");
            }
        }

        private static string FinishFolder(string staging, string destination, string stem, string suffix)
        {
            for (int n = 1; n <= 99999; n++)
            {
                string target = Path.Combine(destination, stem + (n == 1 ? "" : "_" + n.ToString("00", CultureInfo.InvariantCulture)) + suffix);
                if (Directory.Exists(target) || File.Exists(target)) continue;
                try { Directory.Move(staging, target); return target; }
                catch (IOException) { if (Directory.Exists(staging) && (Directory.Exists(target) || File.Exists(target))) continue; throw; }
            }
            throw new IOException("Hay demasiadas copias con este nombre. Cambia el nombre de la copia.");
        }

        public static BackupResult Run(BackupOptions options, Action<BackupProgress> progress, CancellationToken token)
        {
            BackupResult result = new BackupResult();
            string source = "", destination = "", staging = "", stem = "";
            StreamWriter journal = null;
            Action<string> log = s => { if (journal != null) { try { journal.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s); journal.Flush(); } catch { } } };
            try
            {
                source = Normalize(options.Source);
                destination = Normalize(options.Destination);
                ValidatePaths(source, destination);
                stem = options.Date.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture) + "_" + ValidateName(options.Name);
                string reports = options.ReportsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guarda", "Informes");
                try
                {
                    // A report must never become a changing file inside its own source tree.
                    if (SameOrChild(ResolvePotential(reports), Resolve(source))) reports = Path.Combine(destination, "_Informes_Guarda");
                    Directory.CreateDirectory(reports);
                    result.ReportPath = Path.Combine(reports, DateTime.Now.ToString("yyyy_MM_dd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
                    journal = new StreamWriter(new FileStream(result.ReportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
                }
                catch { result.ReportPath = ""; result.Warnings.Add("No se ha podido guardar el informe local."); }
                log("GUARDA 1.1.0 | " + options.Date.ToString("dd/MM/yyyy HH:mm:ss"));
                log("Origen: " + source); log("Destino: " + destination); log("Verificación SHA-256: " + (options.Verify ? "sí" : "no"));
                if (progress != null) progress(new BackupProgress { Stage = "Analizando carpetas", Indeterminate = true });
                Plan plan = Scan(source, token, progress);
                Preflight(plan, destination);
                token.ThrowIfCancellationRequested();
                log("Contenido: " + plan.Files.Count + " archivos, " + plan.Directories.Count + " carpetas, " + FormatBytes(plan.Bytes));
                // A unique staging name prevents reuse of files from an older run.
                staging = Path.Combine(destination, stem + "_EN_CURSO_" + Guid.NewGuid().ToString("N").Substring(0, 12));
                if (Directory.Exists(staging) || File.Exists(staging)) throw new IOException("La carpeta temporal ya existe. Vuelve a intentarlo.");
                Directory.CreateDirectory(staging);
                result.OutputPath = staging;
                foreach (Entry folder in plan.Directories)
                {
                    token.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.Combine(staging, folder.Relative));
                }
                long workDone = 0, workTotal = checked(plan.Bytes * (options.Verify ? 2L : 1L));
                byte[] buffer = new byte[1024 * 1024];
                Action<string, string> publish = (stage, relative) => {
                    if (progress != null) progress(new BackupProgress { Stage = stage, File = relative, Done = workDone, Total = workTotal, CopiedBytes = result.Bytes, FilesDone = result.Files, FilesTotal = plan.Files.Count });
                };
                foreach (Entry entry in plan.Files)
                {
                    token.ThrowIfCancellationRequested();
                    string from = Path.Combine(source, entry.Relative), to = Path.Combine(staging, entry.Relative);
                    log("Copiando: " + entry.Relative);
                    byte[] expected = null;
                    bool created = false;
                    try
                    {
                        using (FileStream input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
                        {
                            FileInfo current = new FileInfo(from);
                            if (input.Length != entry.Length || current.LastWriteTimeUtc != entry.Modified) throw new IOException("El archivo ha cambiado desde el análisis.");
                            using (SHA256 hash = options.Verify ? SHA256.Create() : null)
                            using (FileStream output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.SequentialScan))
                            {
                                created = true;
                                long copied = 0;
                                int read;
                                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    token.ThrowIfCancellationRequested();
                                    output.Write(buffer, 0, read);
                                    if (hash != null) hash.TransformBlock(buffer, 0, read, buffer, 0);
                                    copied += read; workDone += read;
                                    publish("Copiando", entry.Relative);
                                }
                                output.Flush(true);
                                if (copied != entry.Length || File.GetLastWriteTimeUtc(from) != entry.Modified) throw new IOException("El archivo ha cambiado durante la copia.");
                                if (hash != null) { hash.TransformFinalBlock(new byte[0], 0, 0); expected = hash.Hash; }
                            }
                        }
                        if (options.Verify)
                        {
                            publish("Verificando", entry.Relative);
                            using (SHA256 hash = SHA256.Create())
                            using (FileStream saved = new FileStream(to, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
                            {
                                int read;
                                while ((read = saved.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    token.ThrowIfCancellationRequested();
                                    hash.TransformBlock(buffer, 0, read, buffer, 0);
                                    workDone += read; publish("Verificando", entry.Relative);
                                }
                                hash.TransformFinalBlock(new byte[0], 0, 0);
                                if (!expected.SequenceEqual(hash.Hash)) throw new IOException("La comprobación del archivo copiado no coincide con el original.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Only this newly created file may be deleted; never touch the source or an older copy.
                        if (created) { try { File.Delete(to); } catch { } }
                        if (ex is OperationCanceledException) throw;
                        throw new IOException("No se ha podido copiar «" + entry.Relative + "»: " + ex.Message, ex);
                    }
                    try { File.SetCreationTimeUtc(to, entry.Created); File.SetLastWriteTimeUtc(to, entry.Modified); }
                    catch (Exception ex) { result.Warnings.Add("Fecha no conservada en " + entry.Relative + ": " + ex.Message); }
                    result.Files++; result.Bytes += entry.Length;
                    log((options.Verify ? "Verificado: " : "Copiado: ") + entry.Relative);
                    publish("Copiando", entry.Relative);
                }
                token.ThrowIfCancellationRequested();
                publish("Comprobando cambios en el origen", "");
                Plan finalPlan = Scan(source, token, null);
                Dictionary<string, Entry> initial = plan.Files.ToDictionary(f => f.Relative, Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                if (plan.Files.Count != finalPlan.Files.Count || !plan.Directories.Select(d => d.Relative).OrderBy(s => s).SequenceEqual(finalPlan.Directories.Select(d => d.Relative).OrderBy(s => s)) || !plan.Links.OrderBy(s => s).SequenceEqual(finalPlan.Links.OrderBy(s => s)))
                    throw new IOException("El contenido del origen ha cambiado durante la copia. Cierra los programas que lo estén usando y vuelve a intentarlo.");
                foreach (Entry item in finalPlan.Files)
                {
                    token.ThrowIfCancellationRequested();
                    Entry original;
                    if (!initial.TryGetValue(item.Relative, out original) || original.Length != item.Length || original.Modified != item.Modified)
                        throw new IOException("El origen ha cambiado durante la copia: " + item.Relative + ". Vuelve a intentarlo.");
                }
                foreach (Entry folder in plan.Directories.OrderByDescending(d => d.Relative.Length))
                {
                    token.ThrowIfCancellationRequested();
                    try { string target = Path.Combine(staging, folder.Relative); Directory.SetCreationTimeUtc(target, folder.Created); Directory.SetLastWriteTimeUtc(target, folder.Modified); }
                    catch (Exception ex) { result.Warnings.Add("Fecha no conservada en carpeta " + folder.Relative + ": " + ex.Message); }
                }
                foreach (string link in plan.Links) result.Warnings.Add("Enlace o punto de reanálisis no copiado: " + link);
                token.ThrowIfCancellationRequested();
                result.Success = plan.Links.Count == 0;
                result.OutputPath = FinishFolder(staging, destination, stem, result.Success ? "" : "_INCOMPLETA");
                staging = "";
                result.Message = result.Success ? (options.Verify ? "Copia completada y verificada." : "Copia completada.") : "Copia incompleta: hay enlaces o puntos de reanálisis que no se han copiado. Consulta el informe.";
                publish("Finalizando", "");
            }
            catch (OperationCanceledException) { result.Cancelled = true; result.Success = false; result.Message = string.IsNullOrEmpty(staging) ? "Copia cancelada antes de crear la carpeta de copia." : "Copia cancelada. Los archivos ya copiados se conservan en una carpeta marcada como INCOMPLETA."; }
            catch (Exception ex) { result.Success = false; result.Message = FriendlyError(ex); log("ERROR: " + ex); }
            finally
            {
                if (!string.IsNullOrEmpty(staging) && Directory.Exists(staging))
                {
                    try { result.OutputPath = FinishFolder(staging, destination, stem, "_INCOMPLETA"); }
                    catch { result.OutputPath = staging; result.Message += " La carpeta sigue marcada como EN_CURSO; la copia no está terminada."; }
                }
                log("RESULTADO: " + result.Message);
                log("Carpeta: " + result.OutputPath);
                log("Archivos terminados: " + result.Files + " | " + FormatBytes(result.Bytes));
                foreach (string warning in result.Warnings) log("AVISO: " + warning);
                if (journal != null) { try { journal.Dispose(); } catch { } }
            }
            return result;
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "No hay permiso para leer o escribir. Comprueba el destino y que el pendrive no esté protegido contra escritura. " + ex.Message;
            if (ex is PathTooLongException) return "Una ruta es demasiado larga para esta configuración de Windows. Prueba con un destino más corto. " + ex.Message;
            return ex.Message;
        }

        public static string FormatBytes(long value)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = value; int i = 0;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return size.ToString(i == 0 ? "0" : "0.##", CultureInfo.GetCultureInfo("es-ES")) + " " + units[i];
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder buffer, uint length, uint flags);
        [DllImport("libc", SetLastError = true)] private static extern IntPtr realpath(string path, IntPtr buffer);
        [DllImport("libc")] private static extern void free(IntPtr pointer);
    }
}
