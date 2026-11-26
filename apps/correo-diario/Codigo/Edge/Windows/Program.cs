using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;

namespace CorreoDiario.EdgeApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Form
{
    readonly ComboBox windows = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
    };
    readonly CheckedListBox rows = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        HorizontalScrollbar = true,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
    };
    readonly CheckedListBox files = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        HorizontalScrollbar = true,
        IntegralHeight = false,
    };
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly CheckBox search = new()
    {
        Text = "Buscar también el origen y las respuestas en Outlook",
        Checked = true,
        AutoSize = true,
    };
    readonly Label status = new()
    {
        AutoSize = true,
        MaximumSize = new(1200, 0),
        ForeColor = Color.FromArgb(27, 68, 100),
        Margin = new(0, 8, 0, 2),
    };
    readonly Label counter = new() { AutoSize = true, Margin = new(7, 9, 0, 0) };
    readonly TextBox target = new() { Dock = DockStyle.Fill, ReadOnly = true };
    readonly TextBox incoming = new() { Dock = DockStyle.Fill, ReadOnly = true };
    readonly Button export = Button("Exportar selección"),
        cancel = Button("Cancelar"),
        open = Button("Abrir resultado");
    readonly Button copyDiagnostics = Button("Copiar diagnóstico");
    readonly DiagnosticLog diagnostics = new();
    readonly System.Windows.Forms.Timer copyFeedback = new() { Interval = 2200 };
    readonly ProgressBar bar = new()
    {
        Width = 105,
        Height = 13,
        Style = ProgressBarStyle.Marquee,
        Visible = false,
        Margin = new(10, 11, 0, 0),
    };
    readonly List<Control> lockable = [];
    EdgeWindow? connected;
    int? edgeCount;
    string? last;
    CancellationTokenSource? operation;
    bool refreshing;
    static readonly string settings = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XyceL",
        "CorreoDiarioEdge",
        "carpetas.json"
    );

    static Button Button(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new(8, 3, 8, 3),
            FlatStyle = FlatStyle.System,
            Margin = new(0, 0, 6, 0),
        };

    static Label LabelOf(string t) =>
        new()
        {
            Text = t,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new(0, 3, 8, 3),
        };

    static FlowLayoutPanel Line() =>
        new()
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = Padding.Empty,
            Margin = new(0, 3, 0, 3),
        };

    static TableLayoutPanel Stack() =>
        new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

    static void Add(TableLayoutPanel table, Control item, bool fill = false)
    {
        int r = table.RowCount++;
        table.RowStyles.Add(
            new RowStyle(fill ? SizeType.Percent : SizeType.AutoSize, fill ? 100 : 0)
        );
        table.Controls.Add(item, 0, r);
    }

    static TableLayoutPanel Horizontal(Control center, params Control[] side)
    {
        var t = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1 + side.Length,
            Margin = new(0, 4, 0, 4),
        };
        t.ColumnStyles.Add(new(SizeType.Percent, 100));
        t.Controls.Add(center, 0, 0);
        for (int i = 0; i < side.Length; i++)
        {
            t.ColumnStyles.Add(new(SizeType.AutoSize));
            t.Controls.Add(side[i], i + 1, 0);
        }
        return t;
    }

    public MainWindow()
    {
        Text = "CorreoDiario para Edge";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new(935, 715);
        MinimumSize = new(710, 570);
        StartPosition = FormStartPosition.CenterScreen;
        var page = Stack();
        page.Padding = new(20, 14, 20, 12);
        Controls.Add(page);
        Add(
            page,
            new Label
            {
                Text = "CorreoDiario",
                AutoSize = true,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 47, 73),
                Margin = new(0, 0, 0, 5),
            }
        );
        Add(page, tabs, true);
        lockable.Add(tabs);
        var live = new TabPage("Desde Edge") { Padding = new(12) };
        var disk = new TabPage("Archivos originales") { Padding = new(12) };
        tabs.Controls.Add(live);
        tabs.Controls.Add(disk);
        var lc = Stack();
        live.Controls.Add(lc);
        Add(lc, LabelOf("Deja la bandeja de Outlook visible en Edge y selecciona su ventana."));
        windows.Name = "OutlookWindows";
        windows.AccessibleName = "Ventanas de Outlook en Edge";
        status.Name = "OperationStatus";
        var detect = Button("Detectar Edge");
        var reload = Button("Leer selección");
        Add(lc, Horizontal(windows, detect, reload));
        var pick = Line();
        var all = Button("Marcar lista");
        var none = Button("Desmarcar");
        pick.Controls.AddRange([all, none, counter]);
        Add(lc, pick);
        Add(lc, rows, true);
        Add(lc, search);
        var incomingButton = Button("Cambiar…");
        Add(lc, LabelOf("Carpeta donde Edge descarga los mensajes originales"));
        Add(lc, Horizontal(incoming, incomingButton));
        var fc = Stack();
        disk.Controls.Add(fc);
        Add(
            fc,
            LabelOf(
                "Selecciona archivos EML o MSG descargados desde Outlook. Se agrupan por las cabeceras del hilo."
            )
        );
        var actions = Line();
        var choose = Button("Añadir archivos…");
        var clear = Button("Quitar marcados");
        actions.Controls.AddRange([choose, clear]);
        Add(fc, actions);
        Add(fc, files, true);
        Add(page, LabelOf("Carpeta de destino"));
        var destination = Button("Elegir carpeta…");
        lockable.Add(destination);
        Add(page, Horizontal(target, destination));
        var bottom = Line();
        copyDiagnostics.Name = "CopyDiagnostics";
        copyDiagnostics.AccessibleDescription = "Copia un informe técnico para compartir cuando ocurra un error.";
        copyDiagnostics.Click += (_, _) => CopyDiagnostics();
        copyFeedback.Tick += (_, _) => { copyFeedback.Stop(); copyDiagnostics.Text = "Copiar diagnóstico"; };
        FormClosed += (_, _) => copyFeedback.Dispose();
        bottom.Controls.AddRange([export, open, cancel, copyDiagnostics, bar]);
        Add(page, bottom);
        Add(page, status);
        cancel.Enabled = false;
        open.Enabled = false;
        status.Text = "Abre la bandeja en Edge o añade originales descargados.";
        LoadFolders();
        all.Click += (_, _) =>
        {
            for (int i = 0; i < rows.Items.Count; i++)
                rows.SetItemChecked(i, true);
        };
        none.Click += (_, _) =>
        {
            for (int i = 0; i < rows.Items.Count; i++)
                rows.SetItemChecked(i, false);
        };
        rows.ItemCheck += (_, _) => BeginInvoke(new Action(Counts));
        tabs.SelectedIndexChanged += (_, _) => Counts();
        destination.Click += (_, _) => PickFolder(target, "Carpeta de destino");
        incomingButton.Click += (_, _) => PickFolder(incoming, "Carpeta de descargas de Edge");
        detect.Click += async (_, _) => await Discover();
        reload.Click += async (_, _) => await RefreshRows();
        windows.SelectedIndexChanged += async (_, _) =>
        {
            if (!refreshing)
                await RefreshRows();
        };
        choose.Click += (_, _) =>
        {
            using var d = new OpenFileDialog
            {
                Filter = "Mensajes de correo (*.eml;*.msg)|*.eml;*.msg",
                Multiselect = true,
                Title = "Añadir mensajes originales",
            };
            if (d.ShowDialog(this) == DialogResult.OK)
                foreach (var file in d.FileNames)
                    if (!files.Items.Contains(file))
                        files.Items.Add(file, true);
        };
        clear.Click += (_, _) =>
        {
            foreach (var f in files.CheckedItems.Cast<object>().ToArray())
                files.Items.Remove(f);
        };
        export.Click += async (_, _) => await Export();
        cancel.Click += (_, _) => operation?.Cancel();
        open.Click += (_, _) =>
        {
            if (last != null && Directory.Exists(last))
                Process.Start(
                    new ProcessStartInfo("explorer.exe")
                    {
                        ArgumentList = { last },
                        UseShellExecute = false,
                    }
                );
        };
        FormClosing += (_, e) =>
        {
            if (operation != null)
            {
                operation.Cancel();
                e.Cancel = true;
                status.Text = "Deteniendo la operación…";
            }
        };
        Shown += async (_, _) => await Discover();
    }

    void CopyDiagnostics()
    {
        var report = diagnostics.Report();
        try
        {
            Clipboard.SetDataObject(report, true, 5, 80);
            copyDiagnostics.Text = "Diagnóstico copiado";
            copyFeedback.Stop();
            copyFeedback.Start();
        }
        catch (ExternalException)
        {
            using var dialog = new Form { Text = "Diagnóstico · selecciona el texto y pulsa Ctrl+C", Width = 680,
                Height = 380, StartPosition = FormStartPosition.CenterParent };
            var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, Text = report };
            dialog.Controls.Add(text);
            dialog.Shown += (_, _) => { text.Focus(); text.SelectAll(); };
            dialog.ShowDialog(this);
        }
    }

    void Counts() =>
        counter.Text = $"{rows.CheckedItems.Count} marcados · {rows.Items.Count} en la lista";

    static string Downloads()
    {
        const string key =
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
        var value =
            Registry.GetValue(key, "{374DE290-123F-4565-9164-39C4925E467B}", null) as string;
        return value == null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            )
            : Environment.ExpandEnvironmentVariables(value);
    }

    void LoadFolders()
    {
        target.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        incoming.Text = Downloads();
        try
        {
            var a = JsonSerializer.Deserialize<string[]>(File.ReadAllText(settings));
            if (a?.Length == 2)
            {
                if (Directory.Exists(a[0]))
                    target.Text = a[0];
                if (Directory.Exists(a[1]))
                    incoming.Text = a[1];
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { }
    }

    void PickFolder(TextBox box, string title)
    {
        using var d = new FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            InitialDirectory = box.Text,
        };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;
        box.Text = d.SelectedPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
            File.WriteAllText(
                settings,
                JsonSerializer.Serialize(new[] { target.Text, incoming.Text })
            );
        }
        catch (IOException) { }
    }

    void Busy(bool on)
    {
        foreach (var c in lockable)
            c.Enabled = !on;
        export.Enabled = !on;
        cancel.Enabled = on && operation != null;
        bar.Visible = on;
        open.Enabled = !on && last != null;
    }

    async Task Discover()
    {
        if (refreshing || operation != null)
            return;
        refreshing = true;
        Busy(true);
        try
        {
            windows.Items.Clear();
            rows.Items.Clear();
            connected = null;
            edgeCount = null;
            status.Text = "Buscando la pestaña visible de Outlook en las ventanas de Edge…";
            var detection = await Task.Run(EdgeWindow.Discover);
            diagnostics.DetectionResult(detection);
            var list = detection.Windows;
            if (list.Count > 0)
            {
                windows.Items.AddRange(list.ToArray());
                windows.SelectedIndex = 0;
            }
            else
                status.Text = (detection.Processes == 0 ? "No se ha encontrado Microsoft Edge abierto."
                    : detection.Probes.Count == 0 ? "Edge está abierto, pero no se encontró una ventana visible."
                    : detection.Probes.All(p => p.Minimized) ? "Restaura la ventana de Edge y vuelve a detectar."
                    : "No se ha podido confirmar la pestaña de Outlook. Deja su pestaña seleccionada en Edge.")
                    + " Pulsa Copiar diagnóstico para ver el detalle.";
        }
        catch (Exception e)
        {
            diagnostics.Fault("Detectar Edge", e);
            status.Text = "No se pudo consultar Edge. " + e.Message;
        }
        finally
        {
            refreshing = false;
            Busy(false);
        }
        if (windows.SelectedItem != null)
            await RefreshRows();
        else
            Counts();
    }

    async Task RefreshRows()
    {
        if (refreshing || operation != null || windows.SelectedItem is not EdgeWindow w)
            return;
        refreshing = true;
        Busy(true);
        status.Text = "Leyendo la lista de Outlook…";
        rows.Items.Clear();
        connected = null;
        edgeCount = null;
        diagnostics.Event("Leer selección", "inicio");
        try
        {
            var view = await Task.Run(() =>
            {
                w.Verify();
                var visible = w.Rows();
                var count = w.SelectionCount();
                var scan =
                    count > visible.Count(r => r.Selected)
                        ? w.Scan(true, count, CancellationToken.None)
                        : (Rows: visible, Notice: (string?)null);
                return (scan.Rows, count, scan.Notice);
            });
            edgeCount = view.Item2;
            connected = w;
            diagnostics.Event("Leer selección", $"filas {view.Item1.Count} · marcadas {view.Item1.Count(r => r.Selected)} · contador de Outlook {edgeCount?.ToString() ?? "sin dato"}");
            foreach (var r in view.Item1)
                rows.Items.Add(r, r.Selected);
            status.Text =
                view.Item1.Count == 0
                    ? "Pestaña de Outlook detectada. No hay filas de correo visibles en esta vista."
                : edgeCount > view.Item1.Count(r => r.Selected)
                    ? "Edge indica más elementos seleccionados de los que muestra. Reduce la selección en Outlook y pulsa Leer selección."
                : "Marca los correos. Cada uno tendrá una carpeta con su PDF y sus adjuntos. Durante la lectura, deja Edge en primer plano.";
            if (view.Item3 != null)
                status.Text += " " + view.Item3;
        }
        catch (Exception e)
        {
            diagnostics.Fault("Leer selección", e);
            status.Text = e.Message;
        }
        finally
        {
            refreshing = false;
            Busy(false);
            Counts();
        }
    }

    async Task Export()
    {
        if (operation != null || refreshing)
            return;
        var fromEdge = tabs.SelectedIndex == 0;
        var window = connected;
        var labels = rows.CheckedItems.Cast<BrowserRow>().ToList();
        var paths = files.CheckedItems.Cast<string>().ToArray();
        var dest = target.Text;
        var downloads = incoming.Text;
        var searchOrigin = search.Checked;
        if (!Directory.Exists(dest))
        {
            diagnostics.Event("Validar exportación", "carpeta de destino no disponible");
            status.Text = "Elige una carpeta de destino existente.";
            return;
        }
        if (fromEdge && (window == null || labels.Count == 0) || !fromEdge && paths.Length == 0)
        {
            diagnostics.Event("Validar exportación", "selección vacía o pestaña sin conectar");
            status.Text = "Marca al menos un correo para exportar.";
            return;
        }
        if (fromEdge && edgeCount > rows.Items.Cast<BrowserRow>().Count(r => r.Selected))
        {
            diagnostics.Event("Validar exportación", "selección incompleta en Edge");
            status.Text =
                "La selección de Edge contiene filas ocultas. Reduce la selección en Outlook y vuelve a leerla.";
            return;
        }
        if (fromEdge && !Directory.Exists(downloads))
        {
            diagnostics.Event("Validar exportación", "carpeta de descargas no disponible");
            status.Text = "Indica la carpeta de descargas que usa Edge.";
            return;
        }
        operation = new();
        var ct = operation.Token;
        Busy(true);
        last = null;
        status.Text = "Preparando la exportación…";
        string? work = null;
        diagnostics.Event("Exportar", fromEdge ? $"desde Edge · seleccionados {labels.Count}" : $"archivos locales {paths.Length}");
        IProgress<string> progress = new Progress<string>(s => status.Text = s);
        try
        {
            work = Directory.CreateTempSubdirectory("CorreoDiario-captura-").FullName;
            var result = await Task.Run(
                async () =>
                {
                    var notes = new List<string>();
                    List<Mail> messages;
                    if (fromEdge)
                    {
                        var capt = new Capture(window!, downloads, work, progress, ct, diagnostics.Fault);
                        messages = capt.Collect(labels, searchOrigin);
                        notes.AddRange(capt.Notes);
                        if (messages.Count == 0)
                            throw new InvalidOperationException(
                                notes.Count > 0
                                    ? notes[0]
                                    : "Ningún original comprobable en la selección."
                            );
                    }
                    else
                    {
                        messages = [];
                        foreach (var p in paths)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                messages.Add(
                                    MailReader.Inspect(
                                        p,
                                        new("", "", "", "Archivo original elegido")
                                    )
                                );
                            }
                            catch (Exception e)
                            {
                                diagnostics.Fault("Leer archivo EML/MSG", e);
                                notes.Add(Path.GetFileName(p) + " · " + e.Message);
                            }
                        }
                        if (messages.Count == 0)
                            throw new InvalidDataException(string.Join(" · ", notes));
                    }
                    var printer = new MailPrinter();
                    diagnostics.Event("Generar PDF y guardar adjuntos", $"originales {messages.Count}");
                    return await Archive.WriteAsync(messages, dest, printer, notes, progress, ct, diagnostics.Fault);
                },
                ct
            );
            last = result.Folder;
            diagnostics.Event("Exportación terminada", $"originales {result.Originals} · PDF {result.Pdfs} · hilos {result.Combined} · avisos {result.Warnings}");
            status.Text =
                $"{result.Originals} originales · {result.Pdfs} PDF de correo · {result.Combined} PDF de hilo. "
                + (
                    result.Warnings > 0
                        ? $"{result.Warnings} avisos en Resumen.txt."
                        : "Exportación terminada."
                );
            Activate();
        }
        catch (OperationCanceledException e)
        {
            if (ct.IsCancellationRequested) diagnostics.Event("Exportar", "cancelado por el usuario");
            else diagnostics.Fault("Verificar ventana durante la captura", e);
            status.Text = ct.IsCancellationRequested
                ? "Exportación cancelada. Los archivos ya guardados están en la carpeta de destino."
                : e.Message;
            Activate();
        }
        catch (Exception e)
        {
            diagnostics.Fault("Exportar", e);
            status.Text = e.Message;
            Activate();
        }
        finally
        {
            try
            {
                if (work != null) Directory.Delete(work, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            operation.Dispose();
            operation = null;
            Busy(false);
        }
    }
}
