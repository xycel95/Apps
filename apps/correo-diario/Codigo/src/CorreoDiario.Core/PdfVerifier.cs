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
                var extracted = text.ToString();
                var expectedWords = Words(expectedVisibleText);
                var actualWords = Words(extracted);
                var actual = actualWords.GroupBy(s => s, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
                var completeWords = expectedWords.GroupBy(s => s, StringComparer.Ordinal)
                    .All(g => actual.TryGetValue(g.Key, out var count) && count >= g.Count());
                // PDF extraction inserts spaces/newlines inside words at line and
                // page boundaries. Accept that case only when EVERY letter/digit
                // agrees in order and multiplicity across the whole document.
                // No percentage threshold, character bag or missing-word tolerance.
                if (!completeWords && string.Concat(actualWords) != string.Concat(expectedWords))
                    throw new InvalidDataException("El texto del PDF no coincide por completo con el correo mostrado. Se conserva el correo original.");
            }
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se ha podido abrir y comprobar el PDF generado.", ex); }
    }

    private static string[] Words(string value) => Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
        .Select(m => m.Value).ToArray();
}
