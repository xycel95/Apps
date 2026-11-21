using CorreoDiario.Core;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

internal static class PdfTextChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("PDF: una palabra partida entre líneas conserva todo el contenido", () =>
        {
            PdfVerifier.Validate(Pdf(["documenta", "cion recibida 12345"]), "documentacion recibida 12345");
            return Task.CompletedTask;
        });
        yield return ("PDF: una palabra partida entre páginas conserva todo el contenido", () =>
        {
            PdfVerifier.Validate(Pdf(["certifi"], ["cacion completa"]), "certificacion completa");
            return Task.CompletedTask;
        });
        yield return ("PDF: ligaduras y cifras Unicode equivalentes se normalizan", () =>
        {
            PdfVerifier.Validate(Pdf(["Oficina solicitud 12345"]), "Oﬁcina\u00a0solicitud １２３４５");
            return Task.CompletedTask;
        });
        yield return ("PDF: sigue rechazando una palabra repetida que falta", () =>
            Reject(Pdf(["documen", "tacion recibida"]), "documentacion recibida documentacion"));
        yield return ("PDF: sigue rechazando una cifra cambiada entre líneas", () =>
            Reject(Pdf(["Numero 123", "45"]), "Numero 12346"));
        yield return ("PDF: el cotejo de saltos de línea exige el mismo orden", () =>
            Reject(Pdf(["recibida documen", "tacion"]), "documentacion recibida"));
        yield return ("PDF: una palabra truncada no se trata como salto de línea", () =>
            Reject(Pdf(["Expediente documen"]), "Expediente documentacion"));
        yield return ("PDF: una discrepancia de texto impide todos los movimientos", async () =>
        {
            using var env = new TestRun();
            env.Renderer.MissingText = true;
            try { await env.Run(true); }
            catch (ExportException)
            {
                if (env.Source.Moved.Count != 0) throw new Exception("No debe mover con un PDF incompleto.");
                return;
            }
            throw new Exception("El PDF incompleto debía detener la exportación.");
        });
    }

    private static Task Reject(byte[] bytes, string expected)
    {
        try { PdfVerifier.Validate(bytes, expected); }
        catch (InvalidDataException) { return Task.CompletedTask; }
        throw new Exception("Debía rechazar el PDF que pierde o cambia contenido.");
    }

    private static byte[] Pdf(params string[][] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(595, 842);
            for (var i = 0; i < lines.Length; i++) page.AddText(lines[i], 12, new PdfPoint(40, 760 - 24 * i), font);
        }
        return builder.Build();
    }
}
