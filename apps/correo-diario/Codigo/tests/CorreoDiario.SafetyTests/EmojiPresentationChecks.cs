using CorreoDiario.Core;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

internal static class EmojiPresentationChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("PDF: FE0F de presentación emoji omitido no es texto perdido", () =>
        {
            var check = PdfVerifier.ValidateText(FakeRenderer.ValidPdf("Solicitud recibida"), "Solicitud \u26a0\ufe0f recibida", true);
            Require(check.ExactSequence && check.ExpectedPresentationSelectorsIgnored == 1 && check.ExtractedPresentationSelectorsIgnored == 0);
            return Task.CompletedTask;
        });
        yield return ("PDF: FE0E de presentación textual omitido no es texto perdido", () =>
        {
            var check = PdfVerifier.ValidateText(FakeRenderer.ValidPdf("Solicitud recibida"), "Solicitud \u26a0\ufe0e recibida", true);
            Require(check.ExactSequence && check.ExpectedPresentationSelectorsIgnored == 1);
            return Task.CompletedTask;
        });
        yield return ("PDF: mantiene cotejo íntegro con palabras partidas y ambos selectores", () =>
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(595, 842);
            page.AddText("documen", 12, new PdfPoint(40, 760), font);
            page.AddText("tacion recibida 12345", 12, new PdfPoint(40, 736), font);
            var check = PdfVerifier.ValidateText(builder.Build(), "documentacion\ufe0f recibida\ufe0e 12345", true);
            Require(check.ExactSequence && check.ExpectedPresentationSelectorsIgnored == 2);
            return Task.CompletedTask;
        });
        yield return ("PDF: un selector no permite perder una letra real", () => Reject("Texto recibdo", "Texto recibido\ufe0f"));
        yield return ("PDF: un selector no permite perder un acento", () => Reject("Documento reviso", "Documento revisó\ufe0f"));
        yield return ("PDF: un selector no permite cambiar una cifra", () => Reject("Cantidad 12345", "Cantidad 12346\ufe0e"));
        yield return ("PDF: otros selectores y marcas no se ignoran", () => Reject("ABC DEF", "ABC\ufe00 DEF"));
        yield return ("Exportación: avanza tras FE0F y conserva verificación global antes de mover", async () =>
        {
            using var env = new TestRun();
            for (var i = 0; i < env.Source.Items.Count; i++) env.Source.Items[i] = env.Source.Items[i] with { Html = "<p>Contenido completo\ufe0f</p>" };
            env.Source.OnMove = () => Require(env.Source.Reads == 4 && env.Source.Downloads == 4);
            var result = await new ExportEngine(env.Source, new SelectorRenderer()).RunAsync(env.Source.Items, new(env.Directory, true), null, default);
            Require(result.Messages == 2 && result.Moved == 2);
        });
    }

    private static Task Reject(string actual, string expected)
    {
        try { PdfVerifier.ValidateText(FakeRenderer.ValidPdf(actual), expected, true); }
        catch (InvalidDataException) { return Task.CompletedTask; }
        throw new Exception("La excepción de presentación no debe ocultar una diferencia real.");
    }
    private static void Require(bool ok) { if (!ok) throw new Exception("Cotejo de presentación incorrecto."); }
    private sealed class SelectorRenderer : IPdfRenderer
    {
        public Task<byte[]> RenderAsync(MailInfo mail, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct)
        {
            var bytes = FakeRenderer.ValidPdf("Contenido completo");
            PdfVerifier.ValidateText(bytes, mail.Html.Replace("<p>", "").Replace("</p>", ""), true);
            return Task.FromResult(bytes);
        }
    }
}
