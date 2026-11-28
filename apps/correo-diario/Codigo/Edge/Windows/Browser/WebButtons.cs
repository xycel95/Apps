namespace CorreoDiario.EdgeApp;

internal sealed partial class MainWindow
{
    private readonly Button webOpen = Btn("Abrir Outlook web");
    private readonly Button webExport = Btn("Exportar selección web");
    private readonly Button webClose = Btn("Cerrar ventana web");
    private EdgeSession? webSession;
    private OutlookSelection? webReader;

    private async Task ReleaseWeb()
    {
        var session = webSession;
        webSession = null;
        webReader = null;
        if (session != null) await session.DisposeAsync();
    }

    private async Task OpenWeb()
    {
        if (busy) return;
        busy = true;
        operation = new();
        var ct = operation.Token;
        RefreshControls();
        var progress = new Progress<string>(value => { if (busy && !IsDisposed) status.Text = value; });
        try
        {
            if (webSession != null)
            {
                try { await webSession.Show(ct); status.Text = "Marca tus correos en esa ventana y pulsa Exportar selección web."; return; }
                catch (Exception e) when (!ct.IsCancellationRequested && (webSession.Invalid || e is System.Net.Http.HttpRequestException)) { await ReleaseWeb(); }
            }
            status.Text = "Preparando la ventana de exportación de Edge…";
            webSession = await Task.Run(() => startWeb(((IProgress<string>)progress).Report,ct),ct);
            webReader = new OutlookSelection(webSession);
            diagnostics.Event("Abrir Outlook web","ventana de exportación preparada");
            status.Text = "En la nueva ventana de Edge, abre tu buzón y marca los correos. Después pulsa Exportar selección web.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { status.Text = "Apertura de Edge cancelada."; }
        catch (Exception e) { Error("Abrir Outlook web",e); }
        finally { End(); }
    }

    private async Task CloseWeb()
    {
        if (busy) return;
        busy = true;
        RefreshControls();
        try { await ReleaseWeb(); status.Text = "Ventana de exportación cerrada."; }
        catch (Exception e) { Error("Cerrar ventana web",e); }
        finally { End(); }
    }

    private async Task ExportWeb()
    {
        if (busy || webSession == null || webReader == null) return;
        busy = true;
        operation = new();
        var ct = operation.Token;
        var folder = target.Text;
        var reader = webReader;
        var progress = new Progress<string>(value => { if (busy && !IsDisposed) status.Text = value; });
        RefreshControls();
        try
        {
            diagnostics.Event("Exportar selección web","inicio");
            status.Text = "Leyendo la selección de tu ventana de Outlook…";
            var result = await Task.Run(() => reader.Export(folder,progress,diagnostics,ct),ct);
            last = result.Folder;
            var counts = $"Verificados {result.Saved} de {result.Total} correos · {result.Attachments} adjuntos.";
            status.Text = (result.Cancelled ? "Exportación cancelada. " : "") + counts + " " +
                (result.Problem ?? (result.Saved==result.Total ? "Pulsa Abrir resultado." : "Consulta Pendientes.txt."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { status.Text = "Exportación interrumpida."; }
        catch (Exception e) { Error("Exportar selección web",e); }
        finally
        {
            if (webSession.Invalid || ct.IsCancellationRequested)
            {
                try { await ReleaseWeb(); status.Text += " Pulsa Abrir Outlook web para iniciar otra sesión."; }
                catch (Exception e) { diagnostics.Fault("Cerrar ventana web",e); }
            }
            End();
        }
    }
}
