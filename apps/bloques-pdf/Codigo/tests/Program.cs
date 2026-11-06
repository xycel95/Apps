using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using BloquesPDF;
using PdfSharp.Pdf.IO;

var root = Path.GetFullPath(args[0]);
var input = Path.Combine(root, "input");
var output = Path.Combine(root, "output");
Directory.CreateDirectory(output);
int assertions = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); assertions++; }
var files = BatchEngine.Scan(input, false, CancellationToken.None);
Check(files.Count == 103, "Scans every PDF, without a 50-document input cap");
Check(Path.GetFileName(files[1]) == "documento2.pdf", "Natural file order");
Check(BatchEngine.BatchCount(100, 50) == 2, "100 PDF -> 2 blocks");
Check(BatchEngine.BatchCount(103, 50) == 3, "Partial final block");
Check(BatchEngine.BatchCount(3000, 50) == 60, "3000 PDF -> 60 blocks");
Check(BatchEngine.BatchCount(100, 100) == 1, "Custom group size supported");
var hundred = files.Take(100).ToArray();
var options = new BatchOptions { SourceDirectory = input, DestinationDirectory = output };
var result = BatchEngine.Run(hundred, options, null, CancellationToken.None);
Check(result.Status == "COMPLETADO" && result.CompletedDocuments == 100 && result.Outputs.Count == 2, "100 documents merged into 2 blocks of 50");
Check(result.OutputPages == 101, "Every full document is included once, including the multi-page document");
Check(Directory.GetFileSystemEntries(result.OutputDirectory).Length == 2 && Directory.GetFiles(result.OutputDirectory, "*.pdf").Length == 2, "Export contains only the two PDFs, without reports or marker files");
foreach (var pdfPath in result.Outputs)
{
    using var pdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
    Check(pdf.PageCount >= 50 && pdf.PageCount <= 51, "Readable output block without duplicated pages");
}
Console.WriteLine("MERGE100=" + result.OutputDirectory);
var partial = BatchEngine.Run(files, options, null, CancellationToken.None);
Check(partial.Status == "COMPLETADO" && partial.Outputs.Count == 3, "103 documents produces 50,50,3");
using (var last = PdfReader.Open(partial.Outputs[2], PdfDocumentOpenMode.Import)) Check(last.PageCount == 3, "No empty padding or extra cover pages");
Check(partial.OutputDirectory != result.OutputDirectory, "Runs never overwrite each other");
Console.WriteLine("MERGE103=" + partial.OutputDirectory);

var signed = Path.Combine(root, "special", "signed.pdf");
var form = Path.Combine(root, "special", "form.pdf");
options.SourceDirectory = null;
var signedResult = BatchEngine.Run(new[] { signed, form }, options, null, CancellationToken.None);
Check(signedResult.Status == "COMPLETADO" && signedResult.OutputPages == 2, "Signed PDF and visible filled form are each included once");
Console.WriteLine("SIGNED=" + signedResult.Outputs.Single());

var badResult = BatchEngine.Run(new[] { hundred[0], Path.Combine(root, "special", "corrupt.pdf") }, options, null, CancellationToken.None);
Check(badResult.Status == "ERROR EN LA COMPROBACIÓN" && badResult.Outputs.Count == 0 && badResult.Errors.Count == 1, "Corrupt PDF blocks the job, no silently omitted original");
var encrypted = Path.Combine(root, "special", "encrypted.pdf");
var protectedResult = BatchEngine.Run(new[] { encrypted }, options, null, CancellationToken.None);
Check(protectedResult.Status == "ERROR EN LA COMPROBACIÓN", "Protected PDF is reported without bypassing password");

options.KeepSignedOriginals = true;
options.BatchSize = 2;
var originals = BatchEngine.Run(new[] { signed, encrypted, form }, options, null, CancellationToken.None);
Check(originals.Status == "COMPLETADO" && originals.Outputs.Count == 2, "Folder mode accepts encrypted PDFs and groups 2+1");
Check(File.ReadAllBytes(Path.Combine(originals.Outputs[0], "00001_signed.pdf")).SequenceEqual(File.ReadAllBytes(signed)), "Signed PDF is byte-for-byte identical");
Check(File.ReadAllBytes(Path.Combine(originals.Outputs[0], "00002_encrypted.pdf")).SequenceEqual(File.ReadAllBytes(encrypted)), "Encrypted PDF is byte-for-byte identical");

var duplicateDir = Path.Combine(root, "duplicate"); Directory.CreateDirectory(duplicateDir);
string duplicate = Path.Combine(duplicateDir, Path.GetFileName(form)); File.Copy(form, duplicate, true);
var sameNames = BatchEngine.Run(new[] { form, duplicate }, options, null, CancellationToken.None);
Check(sameNames.Status == "COMPLETADO" && Directory.GetFiles(sameNames.Outputs.Single(), "*.pdf").Length == 2, "Distinct files with identical names never overwrite each other");

options.KeepSignedOriginals = false; options.BatchSize = 50;
using var cancelled = new CancellationTokenSource();
var before = BatchEngine.Run(hundred, options, new ImmediateProgress(p => { if (p.Phase == "Comprobando PDF") cancelled.Cancel(); }), cancelled.Token);
Check(before.Status == "CANCELADO" && before.Outputs.Count == 0 && Directory.GetFiles(before.OutputDirectory, "*.en_curso").Length == 0, "Cancellation leaves no partial PDF");
using var afterOne = new CancellationTokenSource();
var cancelAfterBlock = BatchEngine.Run(hundred, options, new ImmediateProgress(p => { if (p.Phase == "Bloque guardado") afterOne.Cancel(); }), afterOne.Token);
Check(cancelAfterBlock.Status == "CANCELADO" && cancelAfterBlock.CompletedDocuments == 50 && cancelAfterBlock.Outputs.Count == 1, "Cancellation preserves completed blocks only");

var mutatePath = Path.Combine(root, "special", "mutate.pdf"); File.Copy(form, mutatePath, true);
var mutation = BatchEngine.Run(new[] { mutatePath }, options, new ImmediateProgress(p => { if (p.Phase.StartsWith("Creando bloque")) File.AppendAllText(mutatePath, "\nchanged"); }), CancellationToken.None);
Check(mutation.Status == "ERROR" && mutation.Outputs.Count == 0 && Directory.GetFiles(mutation.OutputDirectory, "*.en_curso").Length == 0, "Document changes after validation stop the job");

var nested = Path.Combine(root, "nested"); Directory.CreateDirectory(nested);
File.Copy(form, Path.Combine(nested, "one.PDF"), true);
string oldOutput = Path.Combine(nested, "BloquesPDF_previous"); Directory.CreateDirectory(oldOutput);
File.WriteAllText(Path.Combine(oldOutput, BatchEngine.JobMarker), "Generated");
File.Copy(form, Path.Combine(oldOutput, "old.pdf"), true);
Check(BatchEngine.Scan(nested, true, CancellationToken.None).Count == 1, "Recursive scans ignore generated job folders");

var stressInput = Path.Combine(root, "stress3000"); Directory.CreateDirectory(stressInput);
for (int i = 1; i <= 3000; i++) File.Copy(files[1], Path.Combine(stressInput, "pdf" + i + ".pdf"), true);
var stressFiles = BatchEngine.Scan(stressInput, false, CancellationToken.None);
Check(stressFiles.Count == 3000, "Folder selection accepts 3000 PDFs");
options.BatchSize = 50; options.KeepSignedOriginals = false;
var stress = BatchEngine.Run(stressFiles, options, null, CancellationToken.None);
Check(stress.Status == "COMPLETADO" && stress.CompletedDocuments == 3000 && stress.Outputs.Count == 60 && stress.OutputPages == 3000, "3000 single-page documents -> 60 PDFs, with each document included once");
Check(Directory.GetFileSystemEntries(stress.OutputDirectory).Length == 60 && Directory.GetFiles(stress.OutputDirectory, "*.pdf").Length == 60, "Stress export contains exactly the 60 PDFs and nothing else");
Check(Directory.GetFiles(originals.OutputDirectory, "*", SearchOption.AllDirectories).All(path => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)), "Folder mode also exports only PDFs");
Check(Directory.GetFileSystemEntries(badResult.OutputDirectory).Length == 0 && Directory.GetFileSystemEntries(protectedResult.OutputDirectory).Length == 0, "Failed validation creates no reports, markers or partial files");
Check(Directory.GetFileSystemEntries(before.OutputDirectory).Length == 0 && Directory.GetFileSystemEntries(cancelAfterBlock.OutputDirectory).Length == 1, "Cancellation leaves only completed PDFs, without extra files");

string newScanRoot = Path.Combine(root, "scan-clean-output"); Directory.CreateDirectory(newScanRoot);
string scanSource = Path.Combine(newScanRoot, "original.pdf"); File.Copy(form, scanSource, true);
var cleanScan = BatchEngine.Run(new[] { scanSource }, new BatchOptions { DestinationDirectory = newScanRoot }, null, CancellationToken.None);
Check(cleanScan.Status == "COMPLETADO" && !File.Exists(Path.Combine(cleanScan.OutputDirectory, BatchEngine.JobMarker)), "New output folders require no marker file");
Check(BatchEngine.Scan(newScanRoot, true, CancellationToken.None).SequenceEqual(new[] { scanSource }), "Recursive scan excludes clean generated output folders without reimporting their blocks");
Console.WriteLine("PASS: " + assertions + " assertions");

sealed class ImmediateProgress : IProgress<ProgressInfo>
{
    readonly Action<ProgressInfo> action;
    public ImmediateProgress(Action<ProgressInfo> action) { this.action = action; }
    public void Report(ProgressInfo info) { action(info); }
}
