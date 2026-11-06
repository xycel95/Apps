using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BloquesPDF
{
    public sealed class BatchOptions
    {
        public string SourceDirectory { get; set; }
        public string DestinationDirectory { get; set; }
        public int BatchSize { get; set; } = 50;
        public bool KeepSignedOriginals { get; set; }
    }

    public sealed class ProgressInfo
    {
        public string Phase { get; set; }
        public string FileName { get; set; }
        public int Done { get; set; }
        public int Total { get; set; }
        public int CompletedBatches { get; set; }
        public string Message { get; set; }
    }

    public sealed class BatchResult
    {
        public string OutputDirectory { get; set; }
        public string Status { get; set; }
        public int SourceDocuments { get; set; }
        public int CompletedDocuments { get; set; }
        public int OutputPages { get; set; }
        public List<string> Outputs { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
    }

    internal sealed class SourceInfo
    {
        internal string Path;
        internal string RelativePath;
        internal string Hash;
        internal int Pages;
    }

    public sealed class NaturalPathComparer : IComparer<string>
    {
        public static readonly NaturalPathComparer Instance = new NaturalPathComparer();
        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            int a = 0, b = 0;
            while (a < x.Length && b < y.Length)
            {
                if (x[a] >= '0' && x[a] <= '9' && y[b] >= '0' && y[b] <= '9')
                {
                    int xa = a, yb = b;
                    while (a < x.Length && x[a] >= '0' && x[a] <= '9') a++;
                    while (b < y.Length && y[b] >= '0' && y[b] <= '9') b++;
                    int xs = xa, ys = yb;
                    while (xs < a - 1 && x[xs] == '0') xs++;
                    while (ys < b - 1 && y[ys] == '0') ys++;
                    int c = (a - xs).CompareTo(b - ys);
                    if (c != 0) return c;
                    c = string.Compare(x, xs, y, ys, a - xs, StringComparison.Ordinal);
                    if (c != 0) return c;
                    c = (a - xa).CompareTo(b - yb);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToUpperInvariant(x[a]).CompareTo(char.ToUpperInvariant(y[b]));
                    if (c != 0) return c;
                    a++; b++;
                }
            }
            int result = (x.Length - a).CompareTo(y.Length - b);
            return result != 0 ? result : string.Compare(x, y, StringComparison.Ordinal);
        }
    }

    public static class BatchEngine
    {
        public const string JobMarker = "BloquesPDF.trabajo";

        public static List<string> Scan(string directory, bool recursive, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new IOException("La carpeta de origen no existe o no está accesible.");
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(directory));
            while (pending.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                string current = pending.Pop();
                if (File.Exists(Path.Combine(current, JobMarker)) || Regex.IsMatch(
                    Path.GetFileName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    @"^BloquesPDF_[0-9]{8}_[0-9]{6}_[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)) continue;
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
                        files.Add(file);
                }
                if (!recursive) continue;
                foreach (string sub in Directory.EnumerateDirectories(current))
                {
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) pending.Push(sub);
                }
            }
            files.Sort(NaturalPathComparer.Instance);
            return files;
        }

        public static int BatchCount(int documents, int batchSize)
        {
            if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), "Indica al menos un PDF por bloque.");
            return documents / batchSize + (documents % batchSize == 0 ? 0 : 1);
        }

        public static BatchResult Run(IReadOnlyList<string> files, BatchOptions options,
            IProgress<ProgressInfo> progress, CancellationToken cancellation)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            BatchCount(files.Count, options.BatchSize);
            if (files.Count == 0) throw new IOException("No hay documentos PDF para agrupar.");
            if (!Directory.Exists(options.DestinationDirectory)) throw new IOException("Selecciona una carpeta de destino existente.");
            string root = string.IsNullOrWhiteSpace(options.SourceDirectory) ? null :
                Path.GetFullPath(options.SourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string output = Path.Combine(Path.GetFullPath(options.DestinationDirectory),
                "BloquesPDF_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(output);
            var result = new BatchResult { OutputDirectory = output, Status = "EN CURSO", SourceDocuments = files.Count };
            var plan = new List<SourceInfo>();
            string temporary = null;
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string file = Path.GetFullPath(files[i]);
                    string relative = root != null && file.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? file.Substring(root.Length) : Path.GetFileName(file);
                    Report(progress, "Comprobando PDF", relative, i, files.Count, 0, null);
                    try
                    {
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            string hash = Hash(stream);
                            stream.Position = 0;
                            int pages;
                            if (options.KeepSignedOriginals)
                            {
                                // Copy mode preserves every byte, including encrypted PDF and signatures.
                                // A PDF header must be present but no decryption or alteration is attempted.
                                byte[] header = new byte[(int)Math.Min(1024, stream.Length)];
                                int read = 0;
                                while (read < header.Length)
                                {
                                    int n = stream.Read(header, read, header.Length - read);
                                    if (n == 0) break;
                                    read += n;
                                }
                                if (!Encoding.ASCII.GetString(header).Contains("%PDF-")) throw new IOException("No se reconoce como PDF.");
                                pages = 0;
                            }
                            else
                            {
                                using (var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import)) pages = pdf.PageCount;
                                if (pages == 0) throw new IOException("El PDF no contiene páginas.");
                            }
                            plan.Add(new SourceInfo { Path = file, RelativePath = relative, Hash = hash, Pages = pages });
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        result.Errors.Add(file + ": " + Describe(ex));
                    }
                }
                if (result.Errors.Count > 0)
                {
                    result.Status = "ERROR EN LA COMPROBACIÓN";
                    return result;
                }
                cancellation.ThrowIfCancellationRequested();
                for (int offset = 0; offset < plan.Count; offset += options.BatchSize)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int batch = offset / options.BatchSize + 1;
                    int count = Math.Min(options.BatchSize, plan.Count - offset);
                    string label = "BLOQUE " + batch.ToString(CultureInfo.InvariantCulture);
                    if (options.KeepSignedOriginals)
                    {
                        string folder = Path.Combine(output, label);
                        temporary = folder + ".en_curso";
                        Directory.CreateDirectory(temporary);
                        for (int j = 0; j < count; j++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            SourceInfo info = plan[offset + j];
                            Report(progress, "Copiando originales", info.RelativePath, offset + j, plan.Count, result.Outputs.Count, null);
                            string destination = Path.Combine(temporary, (offset + j + 1).ToString("D5") + "_" + Path.GetFileName(info.Path));
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            using (var input = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                CheckHash(input, info);
                                using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) input.CopyTo(target);
                            }
                        }
                        cancellation.ThrowIfCancellationRequested();
                        Directory.Move(temporary, folder);
                        temporary = null;
                        result.Outputs.Add(folder);
                    }
                    else
                    {
                        string name = label + ".pdf";
                        string final = Path.Combine(output, name);
                        temporary = final + ".en_curso";
                        var opened = new List<IDisposable>();
                        try
                        {
                            using (var merged = new PdfDocument())
                            {
                                merged.Info.Title = label + " - " + count + " documentos";
                                merged.Info.Subject = "Copia para impresión. Para validar las firmas digitales consulte los archivos originales.";
                                merged.Info.Creator = "Bloques PDF 1.0.2 - XyceL";
                                for (int j = 0; j < count; j++)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    SourceInfo info = plan[offset + j];
                                    Report(progress, "Creando bloque " + batch, info.RelativePath, offset + j, plan.Count, result.Outputs.Count, null);
                                    var stream = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    opened.Add(stream);
                                    CheckHash(stream, info);
                                    var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                                    opened.Add(pdf);
                                    for (int page = 0; page < pdf.PageCount; page++)
                                    {
                                        cancellation.ThrowIfCancellationRequested();
                                        var added = merged.AddPage(pdf.Pages[page]);
                                        if (page == 0) merged.Outlines.Add(Path.GetFileName(info.Path), added, false);
                                    }
                                }
                                cancellation.ThrowIfCancellationRequested();
                                int expectedPages = merged.PageCount;
                                merged.Save(temporary);
                                cancellation.ThrowIfCancellationRequested();
                                using (var check = PdfReader.Open(temporary, PdfDocumentOpenMode.Import))
                                {
                                    if (check.PageCount != expectedPages) throw new IOException("La comprobación del bloque generado ha fallado.");
                                }
                                File.Move(temporary, final);
                                temporary = null;
                                result.OutputPages += expectedPages;
                                result.Outputs.Add(final);
                            }
                        }
                        finally
                        {
                            for (int k = opened.Count - 1; k >= 0; k--) opened[k].Dispose();
                        }
                    }
                    result.CompletedDocuments += count;
                    Report(progress, "Bloque guardado", label, result.CompletedDocuments, plan.Count, result.Outputs.Count, label);
                    // Release imported document graphs between batches, not after every page.
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
                result.Status = "COMPLETADO";
            }
            catch (OperationCanceledException)
            {
                result.Status = "CANCELADO";
            }
            catch (Exception ex)
            {
                result.Status = "ERROR";
                result.Errors.Add(Describe(ex));
            }
            finally
            {
                if (temporary != null)
                {
                    try { if (File.Exists(temporary)) File.Delete(temporary); else if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
                    catch (Exception ex) { result.Errors.Add("No se pudo eliminar el bloque incompleto: " + Describe(ex)); }
                }
            }
            return result;
        }

        static void CheckHash(Stream input, SourceInfo info)
        {
            if (!string.Equals(Hash(input), info.Hash, StringComparison.Ordinal))
                throw new IOException("El documento ha cambiado después de comprobarlo: " + info.RelativePath + ". Vuelve a analizar la carpeta.");
            input.Position = 0;
        }

        static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void Report(IProgress<ProgressInfo> progress, string phase, string name, int done, int total, int batches, string message)
        {
            progress?.Report(new ProgressInfo { Phase = phase, FileName = name, Done = done, Total = total, CompletedBatches = batches, Message = message });
        }

        public static string Describe(Exception exception)
        {
            if (exception is UnauthorizedAccessException) return "Acceso denegado. Comprueba los permisos de la carpeta o del archivo.";
            string message = exception.Message;
            if (message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("encrypted", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PDF protegido: no se puede unir sin la autorización o contraseña correspondiente. Puedes conservarlo en el modo de carpetas.";
            return string.IsNullOrWhiteSpace(message) ? exception.GetType().Name : message;
        }
    }
}
