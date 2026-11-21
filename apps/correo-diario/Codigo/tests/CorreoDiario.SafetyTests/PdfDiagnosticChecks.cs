using System.Text.Json;
using CorreoDiario.Core;

internal static class PdfDiagnosticChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("PDF: diagnóstico numérico sin copiar texto del correo", () =>
        {
            try { PdfVerifier.ValidateText(FakeRenderer.ValidPdf("Texto 12345"), "Texto 12346", true); }
            catch (InvalidDataException ex) when (ex.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck check)
            {
                Require(check.ExpectedCharacters == check.ExtractedCharacters && !check.ExactSequence && !check.CompleteWords && check.MissingWords == 1 && check.FirstDifference > 0);
                var json = JsonSerializer.Serialize(check);
                Require(!json.Contains("12345") && !json.Contains("12346") && !json.Contains("Texto"));
                return Task.CompletedTask;
            }
            throw new Exception("Debía diagnosticar la discrepancia.");
        });
        yield return ("PDF: composición simplificada exige toda la secuencia, incluso con palabras completas", () =>
        {
            var bytes = FakeRenderer.ValidPdf("completo Contenido");
            PdfVerifier.Validate(bytes, "Contenido completo");
            try { PdfVerifier.ValidateText(bytes, "Contenido completo", true); }
            catch (InvalidDataException) { return Task.CompletedTask; }
            throw new Exception("La composición simplificada exige el orden exacto.");
        });
        yield return ("PDF: modo y comprobación alternativa quedan en el registro", async () =>
        {
            using var env = new TestRun();
            var renderer = new CheckedRenderer();
            var result = await new ExportEngine(env.Source, renderer).RunAsync(env.Source.Items, new(env.Directory), null, default);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(result.ReceiptPath));
            Require(result.SimplifiedPdfs == 2 && result.Moved == 0 && env.Source.Moved.Count == 0);
            Require(json.RootElement.GetProperty("Messages").EnumerateArray().All(m =>
                m.GetProperty("Pdf").GetProperty("Mode").GetString() == "simplified" && m.GetProperty("Pdf").GetProperty("Text").GetProperty("ExactSequence").GetBoolean()));
        });
        yield return ("PDF: fallo de la segunda composición conserva diagnóstico y cero movimientos", async () =>
        {
            using var env = new TestRun();
            try { await new ExportEngine(env.Source, new CheckedRenderer { FailSecond = true }).RunAsync(env.Source.Items, new(env.Directory, true), null, default); }
            catch (ExportException ex)
            {
                using var json = JsonDocument.Parse(await File.ReadAllTextAsync(ex.ReceiptPath));
                Require(env.Source.Moved.Count == 0 && json.RootElement.GetProperty("Status").GetString() == "ERROR");
                Require(json.RootElement.GetProperty("Messages").GetArrayLength() == 1 && json.RootElement.GetProperty("FailedMessageNumber").GetInt32() == 2);
                Require(!json.RootElement.GetProperty("PdfFailure").GetProperty("Simplified").GetProperty("ExactSequence").GetBoolean());
                return;
            }
            throw new Exception("No debe continuar con una segunda composición incompleta.");
        });
        yield return ("PDF: el modo simplificado no adelanta movimientos a la verificación global", async () =>
        {
            using var env = new TestRun();
            env.Source.OnMove = () => Require(env.Source.Reads == 4 && env.Source.Downloads == 4);
            var result = await new ExportEngine(env.Source, new CheckedRenderer()).RunAsync(env.Source.Items, new(env.Directory, true), null, default);
            Require(result.SimplifiedPdfs == 2 && result.Moved == 2);
        });
    }

    private static void Require(bool ok) { if (!ok) throw new Exception("Fallo de comprobación del PDF o registro."); }

    private sealed class CheckedRenderer : IVerifiedPdfRenderer
    {
        public bool FailSecond;
        private int renders;
        public async Task<byte[]> RenderAsync(MailInfo mail, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct) =>
            (await RenderVerifiedAsync(mail, attachments, ct)).Bytes;
        public Task<PdfRenderOutput> RenderVerifiedAsync(MailInfo mail, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
        {
            renders++;
            var bytes = FakeRenderer.ValidPdf("Contenido completo");
            if (FailSecond && renders == 2)
            {
                try { PdfVerifier.ValidateText(bytes, "Contenido completo omitido", true); }
                catch (InvalidDataException ex) when (ex.Data[PdfVerifier.MismatchDataKey] is PdfTextCheck check)
                {
                    ex.Data[PdfVerifier.FailureDataKey] = new PdfRenderFailure(check, check);
                    throw;
                }
            }
            return Task.FromResult(new PdfRenderOutput(bytes, new("simplified", PdfVerifier.ValidateText(bytes, "Contenido completo", true))));
        }
    }
}
