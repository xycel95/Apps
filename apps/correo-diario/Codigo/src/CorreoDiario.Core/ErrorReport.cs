using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace CorreoDiario.Core;

public static class ErrorReport
{
    public const string ExportContextDataKey = "CorreoDiario.ExportErrorContext";

    public static string Build(Exception error, string version, string platform)
    {
        var text = new StringBuilder();
        text.AppendLine("CorreoDiario · " + version);
        text.AppendLine("Fecha: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        text.AppendLine("Sistema: " + platform);
        text.AppendLine("Tipo: " + error.GetType().FullName);
        text.AppendLine($"Código: 0x{error.HResult:X8}");
        text.AppendLine();
        text.AppendLine(error.Message);
        var context = Find<ExportErrorContext>(error, ExportContextDataKey);
        var failure = Find<PdfRenderFailure>(error, PdfVerifier.FailureDataKey);
        if (failure is null && Find<PdfTextCheck>(error, PdfVerifier.MismatchDataKey) is { } check) failure = new(check, null);
        if (context is not null || failure is not null)
        {
            text.AppendLine();
            text.AppendLine("Diagnóstico:");
            // This is plain text for the dialog/clipboard, never HTML or script.
            text.AppendLine(JsonSerializer.Serialize(new { Export = context, PdfFailure = failure }, new JsonSerializerOptions
                { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        // Never dump Exception.Data, stack traces, request headers or arbitrary
        // inner exception messages: they may contain private session information.
        return text.ToString();
    }

    private static T? Find<T>(Exception error, string key) where T : class
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current.Data[key] is T value) return value;
        return null;
    }
}
