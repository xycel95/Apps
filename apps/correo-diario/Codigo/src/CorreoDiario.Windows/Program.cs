namespace CorreoDiario.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            if (Application.OpenForms.OfType<MainForm>().FirstOrDefault() is { } main) main.ShowError(e.Exception);
            else ErrorDialog.ShowReport(null, ErrorDialog.CreateReport(e.Exception));
        };
        Application.Run(new MainForm());
    }
}
