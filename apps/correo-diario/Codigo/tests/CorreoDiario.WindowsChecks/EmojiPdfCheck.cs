using System.Text;
using System.Text.RegularExpressions;
using CorreoDiario.Core;
using CorreoDiario.Windows;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

internal static class EmojiPdfCheck
{
    public static async Task RunAsync(PdfRenderer renderer)
    {
        const string body = "Solicitud ⚠\ufe0f recibida. Revisión íntegra 12345. Teléfono ☎\ufe0e. FINALDELACOPIA";
        var mail = new MailInfo("emoji-pdf", "v1", "inbox", "Presentación de símbolos", "origen@ejemplo.invalid", "destino@ejemplo.invalid", "",
            new DateTimeOffset(2026, 1, 21, 10, 0, 0, TimeSpan.Zero), false, "<p>" + body + "</p>", []);
        var output = await renderer.RenderVerifiedAsync(mail, new Dictionary<string, byte[]>(), CancellationToken.None);
        var expected = PdfLayoutCheck.Header(mail) + body;
        var check = PdfVerifier.ValidateText(output.Bytes, expected, exactSequence: true);
        if (!check.ExactSequence || check.ExpectedPresentationSelectorsIgnored != 2 || output.Verification.Text.ExpectedPresentationSelectorsIgnored != 2)
            throw new Exception("El cotejo debe reconocer FE0E/FE0F y conservar toda la secuencia restante.");
        using var pdf = PdfDocument.Open(output.Bytes);
        var actual = string.Join("\n", pdf.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
        var before = LegacyWords(expected); var after = LegacyWords(actual);
        var counts = after.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        if (!before.GroupBy(s => s).All(g => counts.GetValueOrDefault(g.Key) >= g.Count()) && string.Concat(before) != string.Concat(after))
            Console.WriteLine("PASS Regresión emoji Windows: el cotejo anterior rechaza el PDF por el selector omitido y el corregido lo acepta íntegro.");
        try { PdfVerifier.ValidateText(output.Bytes, expected.Replace("12345", "12346"), exactSequence: true); }
        catch (InvalidDataException)
        {
            Console.WriteLine("PASS PDF emoji Windows: FE0E/FE0F reconocidos, secuencia completa y recuentos verificados; cifra real cambiada rechazada.");
            return;
        }
        throw new Exception("La corrección de presentación no debe aceptar una cifra diferente.");
    }

    private static string[] LegacyWords(string value) => Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
        .Select(m => m.Value).ToArray();
}
