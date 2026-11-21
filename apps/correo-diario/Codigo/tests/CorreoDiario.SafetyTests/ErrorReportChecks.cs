using CorreoDiario.Core;

internal static class ErrorReportChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Diagnóstico: identifica un carácter invisible omitido sin aceptar la pérdida", () =>
        {
            var check = Mismatch("ABC DEF", "ABC\uFE0F DEF");
            Require(check.ExpectedCharacters == check.ExtractedCharacters + 1 && check.ExpectedCodePoint == "U+FE0F" &&
                check.ExpectedUnicodeCategory == "NonSpacingMark" && check.DifferenceKind == "missing-code-point" && !check.ExactSequence);
            return Task.CompletedTask;
        });
        yield return ("Diagnóstico: identifica la cifra cambiada con códigos Unicode", () =>
        {
            var check = Mismatch("Texto 12345", "Texto 12346");
            Require(check.ExpectedCodePoint == "U+0036" && check.ExtractedCodePoint == "U+0035" && check.DifferenceKind == "changed-code-point");
            return Task.CompletedTask;
        });
        yield return ("Copiar error: informe completo disponible aunque se borre el registro", async () =>
        {
            using var env = new TestRun();
            env.Renderer.MissingText = true;
            try { await env.Run(false); }
            catch (ExportException ex)
            {
                File.Delete(ex.ReceiptPath);
                var report = ErrorReport.Build(ex, "0.4.5 beta", "Windows de prueba");
                Require(report.Contains("0.4.5 beta") && report.Contains(ex.Message) && report.Contains("PdfFailure") && report.Contains("FirstDifference"));
                Require(report.Contains("\"FailedMessageNumber\": 1") && report.Contains("\"SelectedMessages\": 2") && report.Contains("\"ConfirmedMoves\": 0"));
                Require(report.Contains("ExpectedCodePoint") && !File.Exists(ex.ReceiptPath) && env.Source.Moved.Count == 0);
                return;
            }
            throw new Exception("La prueba debía generar un informe de fallo.");
        });
        yield return ("Copiar error: conserva ambos cotejos sin volcar datos arbitrarios de la excepción", () =>
        {
            var check = Mismatch("Texto 12345", "Texto 12346");
            var inner = new Exception("CUERPO_PRIVADO_NO_COPIAR");
            inner.Data["Authorization"] = "SESION_PRIVADA_NO_COPIAR";
            inner.Data[PdfVerifier.FailureDataKey] = new PdfRenderFailure(check, check);
            var outer = new Exception("Aviso controlado", inner);
            outer.Data["request"] = "PETICION_PRIVADA_NO_COPIAR";
            var report = ErrorReport.Build(outer, "0.4.5 beta", "Windows de prueba");
            Require(report.Contains("Formatted") && report.Contains("Simplified") && report.Contains("U+0036"));
            Require(!report.Contains("NO_COPIAR") && !report.Contains("Authorization") && !report.Contains("StackTrace"));
            return Task.CompletedTask;
        });
    }

    private static PdfTextCheck Mismatch(string actual, string expected)
    {
        try { PdfVerifier.ValidateText(FakeRenderer.ValidPdf(actual), expected, true); }
        catch (InvalidDataException ex) when (ex.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck check) { return check; }
        throw new Exception("Debía rechazar y diagnosticar el cambio de texto.");
    }
    private static void Require(bool ok) { if (!ok) throw new Exception("Informe de error incompleto o inseguro."); }
}
