using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using AE = System.Windows.Automation.AutomationElement;
using Ctrl = System.Windows.Automation.ControlType;

namespace CorreoDiario.EdgeApp;

internal sealed class Capture(
    EdgeWindow tab,
    string downloads,
    string cache,
    IProgress<string> progress,
    CancellationToken token
)
{
    public List<string> Notes { get; } = [];

    sealed class OriginalUnavailable(string message) : Exception(message);

    static readonly Regex Direct = new(
        @"^(?:Descargar|Download|Guardar como (?:archivo )?\.?EML|Descargar (?:como |en (?:formato )?)?\.?EML|Save as \.?EML|Download as \.?EML|Download message|Descargar (?:el )?mensaje|Guardar como \.?MSG|Save as \.?MSG)(?=$|\s*\()",
        RegexOptions.IgnoreCase
    );
    static readonly Regex Submenu = new(
        @"^(?:Guardar como|Save as|Descargar como|Download as)(?:\s*[.…›>]+)?$",
        RegexOptions.IgnoreCase
    );
    static readonly Regex More = new(
        @"^(?:Más acciones(?: del mensaje| de mensaje)?|More actions(?: for (?:this )?message)?|More message actions|Más opciones del mensaje)(?:\s*\([^)]*\))?$",
        RegexOptions.IgnoreCase
    );
    static readonly Regex ExpandAll = new(
        @"^(?:Expand all(?: messages| in conversation)?|Expandir (?:todo|todos(?: los mensajes)?)|Mostrar todos los mensajes|Show all messages|Expandir (?:la )?conversación|Expand conversation)$",
        RegexOptions.IgnoreCase
    );
    static readonly Regex ExpandMessage = new(
        @"^(?:Expand message|Expandir (?:el )?mensaje)(?:$|[, :])",
        RegexOptions.IgnoreCase
    );

    static string N(string s) =>
        Regex.Replace(MailReader.Clean(s), "[\u200e\u200f]", "").ToLowerInvariant();

    static bool MatchSubject(string label, Mail m)
    {
        var s = N(MailReader.ShortSubject(m.Subject));
        var text = N(label);
        return s.Length > 0
            ? text.Contains(s, StringComparison.Ordinal)
            : text.Contains("sin asunto") || text.Contains("no subject");
    }

    static bool IsOriginalFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".eml" or ".msg";

    IEnumerable<string> Files() => Directory.EnumerateFiles(downloads).Where(IsOriginalFile);

    static (long, long) Stamp(string s)
    {
        var f = new FileInfo(s);
        return (f.Length, f.LastWriteTimeUtc.Ticks);
    }

    Dictionary<string, (long, long)> Snapshot() =>
        Files().ToDictionary(p => p, Stamp, StringComparer.OrdinalIgnoreCase);

    static string ZoneSite(string path)
    {
        try
        {
            var trusted = new List<string>();
            foreach (var line in File.ReadAllLines(path + ":Zone.Identifier"))
            {
                if (
                    !(
                        line.StartsWith("ReferrerUrl=", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("HostUrl=", StringComparison.OrdinalIgnoreCase)
                    )
                )
                    continue;
                var val = line[(line.IndexOf('=') + 1)..];
                if (val.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                    val = val[5..];
                if (Uri.TryCreate(val, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                    trusted.Add(uri.IdnHost);
            }
            return string.Join("|", trusted);
        }
        catch (IOException)
        {
            return "";
        }
        catch (UnauthorizedAccessException)
        {
            return "";
        }
        catch (NotSupportedException)
        {
            return "";
        }
    }

    Mail WaitOriginal(Dictionary<string, (long, long)> before, string label, Origin origin)
    {
        var end = DateTime.UtcNow.AddSeconds(55);
        string? seen = null;
        int stable = 0;
        (long, long) last = default;
        while (DateTime.UtcNow < end)
        {
            token.ThrowIfCancellationRequested();
            Thread.Sleep(240);
            var found = Files()
                .Where(p => !before.TryGetValue(p, out var old) || old != Stamp(p))
                .ToList();
            if (found.Count > 1)
                throw new OriginalUnavailable(
                    "Ha aparecido más de un EML/MSG en Descargas a la vez. No se asignaron a esta fila."
                );
            if (found.Count == 0)
                continue;
            var path = found[0];
            var current = Stamp(path);
            if (current.Item1 <= 0)
                continue;
            if (path != seen || current != last)
            {
                seen = path;
                last = current;
                stable = 0;
                continue;
            }
            if (++stable < 2)
                continue;
            var copy = Path.Combine(
                cache,
                Guid.NewGuid().ToString("N") + Path.GetExtension(path).ToLowerInvariant()
            );
            try
            {
                using (
                    var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                )
                using (var dst = new FileStream(copy, FileMode.CreateNew))
                    src.CopyTo(dst);
            }
            catch (IOException)
            {
                if (File.Exists(copy))
                    File.Delete(copy);
                continue;
            }
            var zones = ZoneSite(path);
            if (
                zones.Length > 0
                && !zones.Split('|').Contains(tab.Site, StringComparer.OrdinalIgnoreCase)
            )
                throw new OriginalUnavailable(
                    "El dominio de la descarga no coincide con la pestaña de Outlook."
                );
            var item = MailReader.Inspect(copy, origin);
            if (!MatchSubject(label, item))
                throw new OriginalUnavailable(
                    "El asunto de la descarga no coincide con la fila solicitada. Importa el original manualmente para revisarlo."
                );
            tab.Verify();
            return item;
        }
        throw new OriginalUnavailable(
            "Outlook no entregó un EML/MSG en la carpeta de descargas indicada. Si Edge pregunta dónde guardarlo, selecciona esa carpeta."
        );
    }

    AE? MenuCommand(Regex text)
    {
        return EdgeWindow
            .By(tab.Root, Ctrl.MenuItem, Ctrl.Button, Ctrl.ListItem)
            .FirstOrDefault(e =>
                EdgeWindow.Visible(e)
                && text.IsMatch(EdgeWindow.Name(e))
                && (
                    e.Current.ControlType == Ctrl.MenuItem
                    || EdgeWindow.Ancestors(e, 7).Any(p => p.Current.ControlType == Ctrl.Menu)
                )
            );
    }

    (AE? Command, bool SaveInvoked) FindCommand()
    {
        bool expanded = false;
        for (int i = 0; i < 11; i++)
        {
            token.ThrowIfCancellationRequested();
            var direct = MenuCommand(Direct);
            if (direct != null)
                return (direct, expanded);
            if (!expanded && MenuCommand(Submenu) is { } sub)
            {
                tab.Press(sub);
                expanded = true;
            }
            Thread.Sleep(140);
        }
        return (null, expanded);
    }

    Mail? Download(Action open, string label, Origin origin)
    {
        token.ThrowIfCancellationRequested();
        var before = Snapshot();
        open();
        var (command, saveInvoked) = FindCommand();
        if (command == null && !saveInvoked)
        {
            tab.Key(27);
            return null;
        }
        if (command != null)
            tab.Press(command);
        return WaitOriginal(before, label, origin);
    }

    static bool InPane(AE e)
    {
        foreach (var a in EdgeWindow.Ancestors(e, 12))
        {
            if (
                EdgeWindow.ListContext(a)
                && a.Current.ControlType is var t
                && (t == Ctrl.ListItem || t == Ctrl.DataItem || t == Ctrl.TreeItem)
            )
                return false;
            if (a.Current.ControlType == Ctrl.Document)
                break;
        }
        return true;
    }

    List<AE> Buttons(Regex names) =>
        EdgeWindow
            .By(tab.Document, Ctrl.Button, Ctrl.Hyperlink)
            .Where(e => EdgeWindow.Visible(e) && names.IsMatch(EdgeWindow.Name(e)) && InPane(e))
            .ToList();

    void Expand()
    {
        var all = Buttons(ExpandAll);
        if (all.Count == 1)
        {
            tab.Press(all[0]);
            Thread.Sleep(310);
        }
        for (int i = 0; i < 80; i++)
        {
            token.ThrowIfCancellationRequested();
            var closed = Buttons(ExpandMessage);
            if (closed.Count == 0)
                return;
            var key = EdgeWindow.Rid(closed[0]);
            tab.Press(closed[0]);
            Thread.Sleep(150);
            if (Buttons(ExpandMessage).Any(b => EdgeWindow.Rid(b) == key))
            {
                Notes.Add("Alguna tarjeta del hilo siguió contraída en Outlook.");
                return;
            }
        }
        Notes.Add("Quedaron tarjetas por expandir en la vista de Outlook.");
    }

    List<Mail> Row(BrowserRow row, string folder, bool discover)
    {
        var result = new List<Mail>();
        var origin = new Origin(
            tab.Site,
            folder,
            Guid.NewGuid().ToString("N"),
            "Descarga del mensaje en Outlook Web"
        );
        var e = tab.Locate(row);
        // A normal click selects only this row, including when several messages were initially ticked.
        tab.Click(e);
        Thread.Sleep(400);
        token.ThrowIfCancellationRequested();
        var direct = Download(() => tab.Click(tab.Locate(row), true), row.Label, origin);
        if (direct != null)
            result.Add(direct);
        if (!discover && direct != null)
            return result;
        try
        {
            Expand();
            var buttons = Buttons(More);
            if (buttons.Count > 0 && (result.Count == 0 || buttons.Count > 1))
            {
                var keys = buttons.Select(EdgeWindow.Rid).Distinct().ToArray();
                foreach (var key in keys)
                {
                    token.ThrowIfCancellationRequested();
                    var current = Buttons(More).FirstOrDefault(b => EdgeWindow.Rid(b) == key);
                    if (current == null)
                    {
                        Notes.Add("Outlook cambió una tarjeta del hilo durante la lectura.");
                        continue;
                    }
                    var member = Download(() => tab.Press(current), row.Label, origin);
                    if (member != null)
                        result.Add(member);
                }
            }
        }
        catch (Exception ex) when (result.Count > 0 && ex is not OperationCanceledException)
        {
            Notes.Add("Algunas tarjetas no se pudieron recuperar: " + ex.Message);
        }
        if (result.Count == 0)
            throw new OriginalUnavailable(
                "Outlook no ofrece «Descargar» para esta fila o para sus tarjetas."
            );
        return result.DistinctBy(m => m.Hash).ToList();
    }

    public List<Mail> Collect(IReadOnlyList<BrowserRow> selected, bool search)
    {
        Directory.CreateDirectory(cache);
        tab.Activate(token);
        var primary = new List<Mail>();
        int pos = 0;
        var view = tab.ViewLabel();
        try
        {
            foreach (var row in selected)
            {
                token.ThrowIfCancellationRequested();
                progress.Report($"Leyendo selección {++pos}/{selected.Count} en Edge");
                try
                {
                    primary.AddRange(Row(row, view, true));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Notes.Add("Fila pendiente «" + row.Label + "»: " + e.Message);
                }
            }
            primary = primary.DistinctBy(m => m.Hash).ToList();
            if (!search || primary.Count == 0)
                return primary;
            var found = new List<Mail>(primary);
            var seeds = primary.Select(m => m.Hash).ToHashSet();
            var topics = primary
                .Select(m => MailReader.ShortSubject(m.Topic.Length > 0 ? m.Topic : m.Subject))
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var terms = new HashSet<string>(StringComparer.Ordinal);
            void SearchMatches(string term, bool byId)
            {
                if (!terms.Add(term))
                    return;
                token.ThrowIfCancellationRequested();
                progress.Report(
                    byId
                        ? "Comprobando una referencia al mensaje original…"
                        : "Buscando el origen y las respuestas · " + term
                );
                try
                {
                    if (!tab.Search(term, token))
                    {
                        Notes.Add(
                            "El buscador de Outlook no estuvo disponible para «" + term + "»."
                        );
                        return;
                    }
                    var scan = tab.Scan(false, null, token);
                    if (scan.Notice != null)
                        Notes.Add(term + " · " + scan.Notice);
                    var candidates = scan
                        .Rows.Where(r => byId || N(r.Label).Contains(N(term)))
                        .ToList();
                    foreach (var row in candidates)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            var hits = Row(row, "Resultados de búsqueda de Outlook", false);
                            if (byId)
                                hits = hits.Where(h => h.Id == term).ToList();
                            found.AddRange(hits);
                        }
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            Notes.Add(
                                "Una coincidencia de «" + term + "» quedó pendiente: " + e.Message
                            );
                        }
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Notes.Add("No se completó la búsqueda de «" + term + "»: " + e.Message);
                }
            }
            foreach (var topic in topics)
                SearchMatches(topic, false);
            var parents = Threads
                .Find(found)
                .Where(h => h.Messages.Any(m => seeds.Contains(m.Hash)))
                .SelectMany(h => h.Missing)
                .Distinct()
                .ToArray();
            foreach (var parent in parents)
                SearchMatches(parent, true);
            // Search hits only enter the archive if an identifier links them to an explicitly selected original.
            return Threads
                .Find(found)
                .Where(t => t.Messages.Any(m => seeds.Contains(m.Hash)))
                .SelectMany(t => t.Messages)
                .ToList();
        }
        catch (OperationCanceledException e)
            when (!token.IsCancellationRequested && primary.Count > 0)
        {
            Notes.Add(
                e.Message
                    + " Se conservan los originales de la selección leídos antes de la interrupción."
            );
            return primary.DistinctBy(m => m.Hash).ToList();
        }
    }
}
