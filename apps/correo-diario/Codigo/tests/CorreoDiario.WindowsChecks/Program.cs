using CorreoDiario.Windows;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        var result = 1;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-30000, -30000), Size = new(100, 100) };
        host.Shown += async (_, _) =>
        {
            try
            {
                CopyErrorCheck.Run(host);
                using var renderer = new PdfRenderer();
                await EmojiPdfCheck.RunAsync(renderer);
                await PdfLayoutCheck.RunAsync(renderer);
                await PdfReflowCheck.RunAsync(renderer);
                await BrowserAuthCheck.RunAsync(renderer);
                Console.WriteLine("PASS Flujo Windows con WebView2 real: búsqueda, lectura, PDF cotejados, adjuntos originales y cero movimientos con datos sintéticos.");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return result;
    }
}
