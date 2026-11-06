using System;
using System.IO;
using System.Linq;
using System.Threading;
using BloquesPDF;

string root = Path.GetFullPath(args[0]);
var files = BatchEngine.Scan(Path.Combine(root, "input"), false, CancellationToken.None).Take(100).ToArray();
string dest = Path.Combine(root, "artifact-output"); Directory.CreateDirectory(dest);
var result = BatchEngine.Run(files, new BatchOptions { DestinationDirectory = dest }, null, CancellationToken.None);
if (result.Status != "COMPLETADO" || result.Outputs.Count != 2 || result.CompletedDocuments != 100 || result.OutputPages != 202)
    throw new Exception(result.Status + ": " + string.Join("; ", result.Errors));
Console.WriteLine("EXACT COMPILED EXE: 100 documents -> 2 blocks, 202 pages, complete-document copies.");
Console.WriteLine("Referenced assemblies: " + string.Join(", ", typeof(BatchEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name)));
