using System;
using System.IO;
using System.Linq;
using System.Threading;
using BloquesPDF;

string root = Path.GetFullPath(args[0]);
var files = BatchEngine.Scan(Path.Combine(root, "input"), false, CancellationToken.None).Take(100).ToArray();
string dest = Path.Combine(root, "artifact-output"); Directory.CreateDirectory(dest);
var result = BatchEngine.Run(files, new BatchOptions { DestinationDirectory = dest }, null, CancellationToken.None);
if (result.Status != "COMPLETADO" || result.Outputs.Count != 2 || result.CompletedDocuments != 100 || result.OutputPages != 101)
    throw new Exception(result.Status + ": " + string.Join("; ", result.Errors));
if (Directory.GetFileSystemEntries(result.OutputDirectory).Length != 2 || Directory.GetFiles(result.OutputDirectory, "*.pdf").Length != 2)
    throw new Exception("Export must contain only its two PDF blocks.");
var singlePageFiles = BatchEngine.Scan(Path.Combine(root, "input"), false, CancellationToken.None).Skip(1).Take(100).ToArray();
var singlePageResult = BatchEngine.Run(singlePageFiles, new BatchOptions { DestinationDirectory = dest }, null, CancellationToken.None);
if (singlePageResult.Status != "COMPLETADO" || singlePageResult.OutputPages != 100 || singlePageResult.Outputs.Count != 2 || Directory.GetFileSystemEntries(singlePageResult.OutputDirectory).Length != 2)
    throw new Exception("100 single-page PDFs must produce only two 50-page PDF blocks.");
Console.WriteLine("EXACT COMPILED EXE: 100 single-page documents -> 2 PDF blocks, 100 pages total, no extra files.");
Console.WriteLine("SINGLE_PAGE_OUTPUT=" + singlePageResult.OutputDirectory);
Console.WriteLine("MULTIPAGE_OUTPUT=" + result.OutputDirectory);
Console.WriteLine("Referenced assemblies: " + string.Join(", ", typeof(BatchEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name)));
