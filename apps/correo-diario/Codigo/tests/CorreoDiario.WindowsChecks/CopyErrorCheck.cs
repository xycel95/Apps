using CorreoDiario.Core;
using CorreoDiario.Windows;

internal static class CopyErrorCheck
{
    public static void Run(IWin32Window owner)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Esta comprobación del portapapeles solo se ejecuta en el escritorio aislado de CI.");
        var check = new PdfTextCheck(2, 50, 49, 1, 201, 200, 100, false, false)
        {
            ExpectedCodePoint = "U+FE0F", ExtractedCodePoint = "U+0061",
            ExpectedUnicodeCategory = "NonSpacingMark", ExtractedUnicodeCategory = "LowercaseLetter",
            DifferenceKind = "missing-code-point"
        };
        var error = new InvalidDataException("No coincide un carácter del PDF de prueba.");
        error.Data[PdfVerifier.FailureDataKey] = new PdfRenderFailure(check, check);
        error.Data[ErrorReport.ExportContextDataKey] = new ExportErrorContext(12, 7, 1, 0, 8, "EXPORTANDO", false);
        try { InlineImages.Resolve(["cid:falta@example.invalid"], [new("a", "logo.png", 1, "image/png", true, "<otro@example.invalid>")], new Dictionary<string, byte[]> { ["a"] = [1] }); }
        catch (InvalidDataException ex) { error.Data[InlineImages.FailureDataKey] = ex.Data[InlineImages.FailureDataKey]; }
        var report = ErrorReport.Build(error, "0.4.8 beta", "Windows sintético");
        var identifiers = ErrorReport.BuildImageIdentifiers(error, "0.4.8 beta") ?? throw new Exception("Faltan los identificadores de prueba.");
        using var dialog = new ErrorDialog(report, identifiers);
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new(-30000, -30000);
        Exception? failure = null;
        dialog.Shown += (_, _) =>
        {
            try
            {
                var button = Descendants(dialog).OfType<Button>().Single(b => b.Text == "Copiar error");
                button.PerformClick();
                var copied = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (copied.Replace("\r\n", "\n") != report.Replace("\r\n", "\n") || !copied.Contains("U+FE0F") || !copied.Contains("FirstDifference") || copied.Contains("example.invalid"))
                    throw new Exception("El botón no ha copiado el informe completo al portapapeles de Windows.");
                Console.WriteLine("PASS Copiar error Windows: botón real, portapapeles Unicode, versión, correo en comprobación y ambos cotejos con códigos Unicode sin leer el registro.");
                Descendants(dialog).OfType<Button>().Single(b => b.Text == "Copiar identificadores").PerformClick();
                if (Clipboard.GetText(TextDataFormat.UnicodeText).Replace("\r\n", "\n") != identifiers.Replace("\r\n", "\n"))
                    throw new Exception("La copia explícita no conserva los identificadores completos.");
                button.PerformClick();
                if (Clipboard.GetText(TextDataFormat.UnicodeText).Contains("example.invalid")) throw new Exception("Copiar error no debe incluir los identificadores aunque se haya usado su copia explícita.");
                Console.WriteLine("PASS Identificadores Windows: botón explícito y portapapeles real; Copiar error conserva la exclusión de CID antes y después.");
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        dialog.ShowDialog(owner);
        // This check runs only in the isolated CI desktop, using synthetic data.
        Clipboard.Clear();
        if (failure is not null) throw failure;
        using var plain = new ErrorDialog("Error sin imágenes");
        if (Descendants(plain).OfType<Button>().Any(b => b.Text == "Copiar identificadores")) throw new Exception("No debe ofrecer identificadores inexistentes.");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
