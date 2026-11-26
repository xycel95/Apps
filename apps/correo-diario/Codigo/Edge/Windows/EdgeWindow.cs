using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using AE = System.Windows.Automation.AutomationElement;
using Ctrl = System.Windows.Automation.ControlType;
using UCondition = System.Windows.Automation.Condition;

namespace CorreoDiario.EdgeApp;

public sealed record BrowserRow(
    string Runtime,
    string AutomationId,
    string Label,
    bool Selected,
    AE Element,
    double? ScrollPosition = null
)
{
    public override string ToString() => Label;
}

public sealed partial class EdgeWindow
{
    public IntPtr Hwnd { get; }
    public string Site { get; }
    public string Title { get; }
    readonly string tabKey;
    readonly string accountPath;

    public EdgeWindow(IntPtr hwnd, string site, string title, string tab, string account)
    {
        Hwnd = hwnd;
        Site = site;
        Title = title;
        tabKey = tab;
        accountPath = account;
    }

    public override string ToString() => Site + "  ·  " + Title;

    internal static string Name(AE x)
    {
        try
        {
            return MailReader.Clean(x.Current.Name);
        }
        catch (ElementNotAvailableException)
        {
            return "";
        }
    }

    internal static bool Visible(AE x)
    {
        try
        {
            return !x.Current.IsOffscreen && !x.Current.BoundingRectangle.IsEmpty;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    internal static string Rid(AE x)
    {
        try
        {
            return string.Join(".", x.GetRuntimeId());
        }
        catch (ElementNotAvailableException)
        {
            return "";
        }
    }

    internal static AE? Parent(AE x)
    {
        try
        {
            return TreeWalker.ControlViewWalker.GetParent(x);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }

    internal static List<AE> By(AE root, params Ctrl[] types)
    {
        var conditions = types
            .Select(t => (UCondition)new PropertyCondition(AE.ControlTypeProperty, t))
            .ToArray();
        return root.FindAll(
                TreeScope.Descendants,
                conditions.Length == 1 ? conditions[0] : new OrCondition(conditions)
            )
            .Cast<AE>()
            .ToList();
    }

    static string TabKey(AE root)
    {
        try
        {
            foreach (var e in By(root, Ctrl.TabItem))
            {
                try
                {
                    if (IsBrowserControl(e, root, out _) && e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p)
                        && ((SelectionItemPattern)p).Current.IsSelected) return Rid(e);
                }
                catch (Exception error) when (ProviderFailure(error)) { }
            }
        }
        catch (Exception error) when (ProviderFailure(error)) { }
        return "";
    }

    internal static IEnumerable<AE> Ancestors(AE e, int max)
    {
        for (var p = Parent(e); p != null && max-- > 0; p = Parent(p))
            yield return p;
    }

    public string ViewLabel()
    {
        foreach (var e in By(Document, Ctrl.TreeItem))
        {
            try
            {
                if (!Visible(e) || ListContext(e) || !Selected(e))
                    continue;
                var label = Name(e);
                if (label.Length > 0 && label.Length < 160)
                    return label;
            }
            catch (ElementNotAvailableException) { }
        }
        return "Vista seleccionada en Outlook";
    }

    public AE Root => AE.FromHandle(Hwnd);

    public void Verify(bool foreground = false)
    {
        if (foreground && GetForegroundWindow() != Hwnd)
            throw new OperationCanceledException(
                "La ventana de Edge ha perdido el foco. La captura se ha detenido."
            );
        var root = Root;
        var location = Location(root, new WindowProbe());
        if (
            location == null || location.IdnHost != Site
            || accountPath.Length > 0 && Account(location) != accountPath
            || tabKey.Length > 0 && TabKey(root) != tabKey
        )
            throw new OperationCanceledException(
                "La pestaña de Outlook ha cambiado. Vuelve a conectar la ventana."
            );
    }

    public void Activate(CancellationToken ct)
    {
        if (IsIconic(Hwnd))
            ShowWindow(Hwnd, 9);
        SetForegroundWindow(Hwnd);
        for (var i = 0; i < 35 && GetForegroundWindow() != Hwnd; i++)
        {
            ct.ThrowIfCancellationRequested();
            Thread.Sleep(120);
        }
        Verify(true);
    }

    public AE Document
    {
        get
        {
            var visible = By(Root, Ctrl.Document).Where(Visible).ToList();
            if (visible.Count == 0)
                throw new InvalidOperationException(
                    "Edge no expone la página. Abre la bandeja de Outlook en la pestaña visible."
                );
            return visible
                .OrderByDescending(d =>
                    d.Current.BoundingRectangle.Width * d.Current.BoundingRectangle.Height
                )
                .First();
        }
    }

    internal static bool ListContext(AE row)
    {
        foreach (var p in Ancestors(row, 12))
        {
            var t = p.Current.ControlType;
            var n = Name(p);
            if (t == Ctrl.Document)
                break;
            if (
                (t == Ctrl.Tree || t == Ctrl.Pane || t == Ctrl.List || t == Ctrl.Group)
                && Regex.IsMatch(
                    n,
                    @"^(Carpetas|Folders|Navigation pane|Panel de navegación)(?:$|\s*[,·])",
                    RegexOptions.IgnoreCase
                )
            )
                return false;
            if (
                t == Ctrl.List
                || t == Ctrl.DataGrid
                || t == Ctrl.Table
                || Regex.IsMatch(
                    n,
                    @"(lista de (mensajes|conversaciones)|message list|conversation list)",
                    RegexOptions.IgnoreCase
                )
            )
                return true;
        }
        return false;
    }

    static bool Selected(AE e)
    {
        if (
            e.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var s)
            && ((SelectionItemPattern)s).Current.IsSelected
        )
            return true;
        return By(e, Ctrl.CheckBox)
            .Any(c =>
                c.TryGetCurrentPattern(TogglePattern.Pattern, out var t)
                && ((TogglePattern)t).Current.ToggleState == ToggleState.On
            );
    }

    public List<BrowserRow> Rows()
    {
        var data = new List<BrowserRow>();
        foreach (var e in By(Document, Ctrl.ListItem, Ctrl.DataItem, Ctrl.TreeItem))
        {
            try
            {
                if (!Visible(e) || !ListContext(e))
                    continue;
                var label = Name(e);
                if (label.Length < 2)
                    label = string.Join(
                        " · ",
                        By(e, Ctrl.Text)
                            .Where(Visible)
                            .Select(Name)
                            .Where(v => v.Length > 0)
                            .Distinct()
                            .Take(16)
                    );
                if (label.Length < 2)
                    continue;
                if (
                    Ancestors(e, 9)
                        .Any(p =>
                            p.Current.ControlType == Ctrl.ListItem
                            || p.Current.ControlType == Ctrl.DataItem
                        )
                )
                    continue;
                data.Add(new(Rid(e), e.Current.AutomationId ?? "", label, Selected(e), e));
            }
            catch (ElementNotAvailableException) { }
        }
        return data;
    }

    static ScrollPattern? ScrollList(IReadOnlyList<BrowserRow> rows)
    {
        if (rows.Count == 0)
            return null;
        foreach (var ancestor in Ancestors(rows[0].Element, 16))
        {
            if (ancestor.Current.ControlType == Ctrl.Document)
                break;
            if (
                ancestor.TryGetCurrentPattern(ScrollPattern.Pattern, out var p)
                && ((ScrollPattern)p).Current.VerticallyScrollable
            )
                return (ScrollPattern)p;
        }
        return null;
    }

    public (List<BrowserRow> Rows, string? Notice) Scan(
        bool selectedOnly,
        int? expected,
        CancellationToken ct
    )
    {
        var first = Rows();
        var list = ScrollList(first);
        if (
            list == null
            || selectedOnly && expected.HasValue && first.Count(r => r.Selected) >= expected.Value
        )
            return (first, null);
        var initial = list.Current.VerticalScrollPercent;
        var result = new Dictionary<(string, string), BrowserRow>();
        string? notice = null;
        var started = DateTime.UtcNow;
        try
        {
            if (initial > 0)
            {
                list.SetScrollPercent(ScrollPattern.NoScroll, 0);
                Thread.Sleep(200);
            }
            int stalled = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                Verify(!selectedOnly);
                var current = list.Current.VerticalScrollPercent;
                foreach (var r in Rows())
                    if (!selectedOnly || r.Selected)
                        result.TryAdd((r.Runtime, r.Label), r with { ScrollPosition = current });
                if (
                    selectedOnly && expected.HasValue && result.Count >= expected.Value
                    || current >= 99.99
                    || !list.Current.VerticallyScrollable
                )
                    break;
                if (DateTime.UtcNow - started > TimeSpan.FromMinutes(3))
                {
                    notice =
                        "La lista siguió cargando al terminar el recorrido. Hay resultados sin comprobar.";
                    break;
                }
                list.ScrollVertical(ScrollAmount.LargeIncrement);
                Thread.Sleep(220);
                if (list.Current.VerticalScrollPercent <= current + 0.001)
                {
                    if (++stalled >= 3)
                    {
                        notice = "Outlook no permitió recorrer toda la lista de resultados.";
                        break;
                    }
                }
                else
                    stalled = 0;
            }
        }
        catch (Exception e)
            when (e
                    is InvalidOperationException
                        or ElementNotAvailableException
                        or System.Runtime.InteropServices.COMException
            )
        {
            notice =
                "Outlook cambió la lista mientras se recorría. Se conservaron las filas identificadas.";
        }
        finally
        {
            try
            {
                if (initial >= 0)
                {
                    list.SetScrollPercent(ScrollPattern.NoScroll, initial);
                    Thread.Sleep(130);
                }
            }
            catch (InvalidOperationException) { }
            catch (ElementNotAvailableException) { }
        }
        foreach (var r in first)
            result.TryAdd((r.Runtime, r.Label), r with { ScrollPosition = initial });
        return (result.Values.ToList(), notice);
    }

    public int? SelectionCount()
    {
        foreach (var e in By(Document, Ctrl.Text, Ctrl.Button))
        {
            var m = Regex.Match(
                Name(e),
                @"^(\d+)\s+(?:(?:mensajes?|elementos?|conversaciones?)\s+seleccionad[oa]s?|(?:items?|messages?|conversations?)\s+selected|seleccionad[oa]s?|selected)\b",
                RegexOptions.IgnoreCase
            );
            if (m.Success && int.TryParse(m.Groups[1].Value, out var count))
                return count;
        }
        return null;
    }

    public AE Locate(BrowserRow row)
    {
        Verify(true);
        var items = Rows();
        if (
            row.ScrollPosition is { } position
            && ScrollList(items) is { } scrolling
            && Math.Abs(scrolling.Current.VerticalScrollPercent - position) > 0.05
        )
        {
            scrolling.SetScrollPercent(ScrollPattern.NoScroll, position);
            Thread.Sleep(240);
            items = Rows();
        }
        var hit = items.FirstOrDefault(x => x.Runtime == row.Runtime && x.Label == row.Label);
        if (hit != null)
            return hit.Element;
        var matches = items
            .Where(x =>
                x.Label == row.Label
                && (row.AutomationId.Length == 0 || x.AutomationId == row.AutomationId)
            )
            .ToList();
        if (matches.Count == 1)
            return matches[0].Element;
        throw new InvalidOperationException(
            "La fila ya no se identifica de forma única en Edge. Actualiza la lista."
        );
    }

    public void Press(AE e)
    {
        Verify(true);
        if (e.TryGetCurrentPattern(InvokePattern.Pattern, out var inv))
        {
            ((InvokePattern)inv).Invoke();
            return;
        }
        if (
            e.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var exp)
            && ((ExpandCollapsePattern)exp).Current.ExpandCollapseState
                == ExpandCollapseState.Collapsed
        )
        {
            ((ExpandCollapsePattern)exp).Expand();
            return;
        }
        Click(e);
    }

    public void Click(AE e, bool right = false)
    {
        Verify(true);
        if (!Visible(e))
            throw new InvalidOperationException("El control solicitado no está visible.");
        var r = e.Current.BoundingRectangle;
        var p = new PointI
        {
            X = (int)(r.Left + r.Width * 0.48),
            Y = (int)(r.Top + r.Height * 0.5),
        };
        if (GetAncestor(WindowFromPoint(p), 2) != Hwnd)
            throw new OperationCanceledException(
                "Hay otra ventana encima de Outlook. Deja su pestaña visible durante la captura."
            );
        SetCursorPos(p.X, p.Y);
        mouse_event(right ? 8u : 2u, 0, 0, 0, UIntPtr.Zero);
        mouse_event(right ? 16u : 4u, 0, 0, 0, UIntPtr.Zero);
    }

    public void Key(byte key, bool control = false)
    {
        Verify(true);
        if (control)
            keybd_event(17, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 2, UIntPtr.Zero);
        if (control)
            keybd_event(17, 0, 2, UIntPtr.Zero);
    }

    public bool Search(string subject, CancellationToken ct)
    {
        Verify(true);
        bool IsSearch(AE e) =>
            Regex.IsMatch(Name(e), @"^(Buscar|Búsqueda|Search)(?:$|\b)", RegexOptions.IgnoreCase)
            && Visible(e);
        var search = By(Document, Ctrl.Edit, Ctrl.ComboBox).FirstOrDefault(IsSearch);
        if (search == null)
        {
            var button = By(Document, Ctrl.Button)
                .FirstOrDefault(e =>
                    IsSearch(e)
                    && Regex.IsMatch(
                        Name(e),
                        @"^(Buscar|Búsqueda|Search)$",
                        RegexOptions.IgnoreCase
                    )
                );
            if (button == null)
                return false;
            Press(button);
            Thread.Sleep(280);
            search = By(Document, Ctrl.Edit, Ctrl.ComboBox).FirstOrDefault(IsSearch);
        }
        if (search == null)
            return false;
        Click(search);
        search.SetFocus();
        var focus = AE.FocusedElement;
        if (
            focus == null
            || !(Rid(focus) == Rid(search) || Ancestors(focus, 5).Any(a => Rid(a) == Rid(search)))
        )
            return false;
        if (
            !focus.TryGetCurrentPattern(ValuePattern.Pattern, out var vp)
            || ((ValuePattern)vp).Current.IsReadOnly
        )
            return false;
        var normalized = MailReader.Clean(subject).Replace("\"", " ").Trim();
        if (normalized.Length == 0)
            return false;
        var query = "\"" + (normalized.Length > 180 ? normalized[..180] : normalized) + "\"";
        ((ValuePattern)vp).SetValue(query);
        if (((ValuePattern)vp).Current.Value != query)
            return false;
        Key(13);
        var last = "";
        int stable = 0;
        for (int i = 0; i < 21; i++)
        {
            Thread.Sleep(300);
            ct.ThrowIfCancellationRequested();
            Verify(true);
            var signature = string.Join("|", Rows().Select(r => r.Label));
            stable = signature == last ? stable + 1 : 0;
            last = signature;
            if (i >= 4 && stable >= 2 && signature.Length > 0)
                break;
        }
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PointI
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr h);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr h, int cmd);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr h);

    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(PointI p);

    [DllImport("user32.dll")]
    static extern IntPtr GetAncestor(IntPtr h, uint flags);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
