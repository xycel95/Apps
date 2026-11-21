using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CorreoDiario.Core;

public static class PdfVerifier
{
    public static void Validate(byte[] bytes, string? expectedVisibleText = null)
    {
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            if (pdf.NumberOfPages < 1) throw new InvalidDataException("El PDF no contiene páginas.");
            var text = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                if (page.Width <= 0 || page.Height <= 0) throw new InvalidDataException("El PDF contiene una página inválida.");
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            }
            if (expectedVisibleText is not null)
            {
                var actual = Tokens(text.ToString());
                foreach (var token in Tokens(expectedVisibleText))
                    if (!actual.TryGetValue(token.Key, out var count) || count < token.Value)
                        throw new InvalidDataException("El texto del PDF no coincide por completo con el correo mostrado. Se conserva el correo original.");
            }
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se ha podido abrir y comprobar el PDF generado.", ex); }
    }

    private static Dictionary<string, int> Tokens(string value) => Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{N}]+")
        .Select(m => m.Value).GroupBy(s => s, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
}
