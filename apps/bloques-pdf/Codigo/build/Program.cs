using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ILRepacking;

string input = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output));
var closure = new List<string>();
var pending = new Queue<string>();
var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
pending.Enqueue(Path.Combine(input, "BloquesPDF.exe"));
while (pending.Count != 0)
{
    string file = pending.Dequeue();
    if (!visited.Add(file)) continue;
    closure.Add(file);
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    foreach (var handle in md.AssemblyReferences)
    {
        var reference = md.GetAssemblyReference(handle);
        string candidate = Path.Combine(input, md.GetString(reference.Name) + ".dll");
        if (File.Exists(candidate)) pending.Enqueue(candidate);
    }
}
var options = new RepackOptions {
    InputAssemblies = closure.ToArray(),
    OutputFile = output,
    SearchDirectories = new[] { input, Path.GetFullPath(args[2]) },
    TargetKind = ILRepack.Kind.WinExe,
    Internalize = true,
    DebugInfo = false,
    Parallel = false,
    Log = false,
    UnionMerge = false,
    CopyAttributes = false,
};
new ILRepack(options).Repack();
Console.WriteLine("Created: " + output);
