using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using BloquesPDF;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

internal static class NativeTests
{
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            string input = Path.Combine(root, "input");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(input); Directory.CreateDirectory(output);
            for (int i = 1; i <= 103; i++)
                using (var pdf = new PdfDocument()) { pdf.AddPage(); pdf.Save(Path.Combine(input, "documento" + i + ".pdf")); }
            var files = BatchEngine.Scan(input, false, CancellationToken.None);
            Check(files.Count == 103, "Folder scan");
            Check(Path.GetFileName(files[1]) == "documento2.pdf", "Natural order");
            var options = new BatchOptions { DestinationDirectory = output };
            var first = BatchEngine.Run(files.Take(100).ToArray(), options, null, CancellationToken.None);
            Check(first.Status == "COMPLETADO" && first.Outputs.Count == 2 && first.OutputPages == 100, "100 PDF -> 2 blocks, no repeats");
            Check(Directory.GetFileSystemEntries(first.OutputDirectory).Length == 2, "Only PDF exported");
            Check(Regex.IsMatch(Path.GetFileName(first.OutputDirectory), @"^BloquesPDF_[0-9]{4}\.[0-9]{2}\.[0-9]{2}_[0-9]{2}\.[0-9]{2}\.[0-9]{2}(?: \([0-9]+\))?$"), "Readable folder name");
            foreach (string path in first.Outputs)
                using (var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import)) Check(pdf.PageCount == 50, "50 pages per block");
            var partial = BatchEngine.Run(files, options, null, CancellationToken.None);
            Check(partial.Status == "COMPLETADO" && partial.Outputs.Count == 3 && partial.OutputPages == 103, "Partial block");
            Check(first.OutputDirectory != partial.OutputDirectory, "Previous exports preserved");
            using (var pdf = PdfReader.Open(partial.Outputs[2], PdfDocumentOpenMode.Import)) Check(pdf.PageCount == 3, "Last block: 3 pages");
            Check(BatchEngine.Scan(output, true, CancellationToken.None).Count == 0, "Generated folders excluded");
            options.KeepSignedOriginals = true; options.BatchSize = 2;
            var copied = BatchEngine.Run(files.Take(3).ToArray(), options, null, CancellationToken.None);
            Check(copied.Status == "COMPLETADO" && copied.Outputs.Count == 2, "Original file mode");
            Check(Directory.GetFiles(copied.OutputDirectory, "*", SearchOption.AllDirectories).All(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase)), "Folder mode only exports PDFs");
            Check(File.ReadAllBytes(Path.Combine(copied.Outputs[0], "00001_documento1.pdf")).SequenceEqual(File.ReadAllBytes(files[0])), "Original bytes preserved");
            using (var form = new MainForm())
            {
                form.Show(); Application.DoEvents();
                Check(form.IsHandleCreated && form.Visible, "Windows Forms window opens");
                Check(!HasCopiesLabel(form), "Removed copies control");
                form.Close(); Application.DoEvents();
            }
            Console.WriteLine("NATIVE_CHECKS_PASSED=" + checks);
            Console.WriteLine("WINDOWS_GUI_VERIFIED=true");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
    static bool HasCopiesLabel(Control parent)
    {
        foreach (Control c in parent.Controls)
            if (c.Text == "Copias de cada documento" || HasCopiesLabel(c)) return true;
        return false;
    }
}
