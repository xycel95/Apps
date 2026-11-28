using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CorreoDiario.EdgeApp;

internal sealed class DiagnosticLog
{
    readonly object gate = new();
    readonly Queue<string> events = new();
    string detection = "Todavía no se ha completado la detección de Edge.";
    string? lastError;
    string? lastBatch;
    readonly List<string> samples=new();
    public void BeginWebBatch()
    {
        lock(gate) { lastBatch=null;samples.Clear();lastError=null; }
    }

    public static string ErrorCode(Exception e) => $"{e.GetType().Name} · 0x{e.HResult:X8}";

    // Callers provide fixed stages and counters. Never copy a status label, Exception.Message, UI name or file path.
    public void Event(string stage, string detail)
    {
        lock (gate)
        {
            if(stage is "Estructura del panel" or "Pendientes del lote" && samples.Count<10) samples.Add(stage+" — "+detail);
            if(stage=="Lote de Outlook web") lastBatch=$"Último lote terminado a las {DateTimeOffset.Now:HH:mm:ss} · {detail}";
            Append($"{stage} — {detail}");
        }
    }

    void Append(string entry)
    {
        if (events.Count >= 24) events.Dequeue();
        events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} {entry}");
    }

    public void Fault(string stage, Exception e)
    {
        var method = new StackTrace(e, false).GetFrames()?.Select(frame => frame.GetMethod())
            .FirstOrDefault(m => m?.DeclaringType?.Namespace?.StartsWith("CorreoDiario", StringComparison.Ordinal) == true);
        var reason = e is TransferException t ? " · " + t.Reason : e is WebExportException w ? " · " + w.Code : "";
        var where = method == null ? "" : $" · {method.DeclaringType!.Name}.{method.Name}";
        lock (gate)
        {
            lastError = $"{stage} — {ErrorCode(e)}{reason}{where}";
            Append(lastError);
        }
    }

    public void DetectionResult(Detection result)
    {
        var summary = $"Procesos Edge {result.Processes} · ventanas visibles {result.Probes.Count} · ventanas Outlook {result.Windows.Count}";
        var description = new StringBuilder(summary);
        for (int i = 0; i < result.Probes.Count; i++) description.AppendLine().Append($"Ventana {i + 1} — {result.Probes[i]}");
        lock (gate)
        {
            detection = description.ToString();
            Append("Detectar Edge — " + summary);
        }
    }

    public string Report()
    {
        var version = typeof(DiagnosticLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
        lock (gate)
        {
            var report = new StringBuilder().AppendLine($"CorreoDiario para Edge {version} · Diagnóstico")
                .AppendLine($"Fecha {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
                .AppendLine($"Windows {Environment.OSVersion.Version} · {RuntimeInformation.ProcessArchitecture} · .NET {Environment.Version}")
                .AppendLine().AppendLine(detection).AppendLine();
            if(lastBatch!=null) report.AppendLine(lastBatch);
            foreach(string line in samples) report.AppendLine(line);
            if (lastError != null) report.AppendLine("Último error — " + lastError);
            foreach (var entry in events) report.AppendLine(entry);
            return report.ToString();
        }
    }
}
