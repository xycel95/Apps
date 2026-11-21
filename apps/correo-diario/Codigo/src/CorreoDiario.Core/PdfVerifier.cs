using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CorreoDiario.Core;

public static class PdfVerifier
{
    public const string MismatchDataKey = "CorreoDiario.PdfTextMismatch";
    public const string FailureDataKey = "CorreoDiario.PdfRenderFailure";

    public static void Validate(byte[] bytes, string? expectedVisibleText = null) => Inspect(bytes, expectedVisibleText, false);
    public static PdfTextCheck ValidateText(byte[] bytes, string expectedVisibleText, bool exactSequence = false) =>
        Inspect(bytes, expectedVisibleText, exactSequence)!;

    private static PdfTextCheck? Inspect(byte[] bytes, string? expectedVisibleText, bool exactSequence)
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
                var missingWords = expectedWords.GroupBy(s => s, StringComparer.Ordinal)
                    .Sum(g => Math.Max(0, g.Count() - actual.GetValueOrDefault(g.Key)));
                var expectedSequence = string.Concat(expectedWords);
                var actualSequence = string.Concat(actualWords);
                var difference = 0;
                while (difference < Math.Min(expectedSequence.Length, actualSequence.Length) && expectedSequence[difference] == actualSequence[difference]) difference++;
                var same = expectedSequence == actualSequence;
                var check = new PdfTextCheck(pdf.NumberOfPages, expectedWords.Length, actualWords.Length, missingWords,
                    expectedSequence.Length, actualSequence.Length, same ? -1 : difference, missingWords == 0, same)
                {
                    ExpectedCodePoint = same ? null : CodePoint(expectedSequence, difference),
                    ExtractedCodePoint = same ? null : CodePoint(actualSequence, difference),
                    ExpectedUnicodeCategory = same ? null : Category(expectedSequence, difference),
                    ExtractedUnicodeCategory = same ? null : Category(actualSequence, difference),
                    DifferenceKind = same ? "identical" : DifferenceKind(expectedSequence, actualSequence, difference)
                };
                // PDF extraction inserts spaces/newlines inside words at line and
                // page boundaries. Accept that case only when EVERY letter/digit
                // agrees in order and multiplicity across the whole document.
                // No percentage threshold, character bag or missing-word tolerance.
                if (exactSequence ? !same : missingWords != 0 && !same)
                {
                    var error = new InvalidDataException($"El texto del PDF no coincide por completo con el correo mostrado. Se conserva el correo original. Letras y cifras: esperadas {check.ExpectedCharacters}, leídas {check.ExtractedCharacters}.");
                    error.Data[MismatchDataKey] = check;
                    throw error;
                }
                return check;
            }
            return null;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("No se ha podido abrir y comprobar el PDF generado.", ex); }
    }

    private static string[] Words(string value) => Regex.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
        .Select(m => m.Value).ToArray();

    private static string CodePoint(string text, int index) => index >= text.Length ? "END" :
        Rune.TryGetRuneAt(text, index, out var rune) ? $"U+{rune.Value:X4}" : $"U+{(int)text[index]:X4}";
    private static string Category(string text, int index) => index >= text.Length ? "END" :
        Rune.TryGetRuneAt(text, index, out var rune) ? Rune.GetUnicodeCategory(rune).ToString() : char.GetUnicodeCategory(text[index]).ToString();
    private static int RuneLength(string text, int index) => index >= text.Length ? 0 :
        Rune.TryGetRuneAt(text, index, out var rune) ? rune.Utf16SequenceLength : 1;
    private static string DifferenceKind(string expected, string actual, int index)
    {
        var e = RuneLength(expected, index); var a = RuneLength(actual, index);
        if (e > 0 && expected.AsSpan(index + e).SequenceEqual(actual.AsSpan(index))) return "missing-code-point";
        if (a > 0 && expected.AsSpan(index).SequenceEqual(actual.AsSpan(index + a))) return "extra-code-point";
        if (e > 0 && a > 0 && expected.AsSpan(index + e).SequenceEqual(actual.AsSpan(index + a))) return "changed-code-point";
        return "sequence-difference";
    }
}
