using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using AE = System.Windows.Automation.AutomationElement;
using Ctrl = System.Windows.Automation.ControlType;
using UCondition = System.Windows.Automation.Condition;

namespace CorreoDiario.EdgeApp;

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

    public AE Root => AE.FromHandle(Hwnd);

    public void Verify()
    {
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

    public AE Document
    {
        get
        {
            var root = Root;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var visible = By(root, Ctrl.Document).Where(d => Visible(d) && IsTopDocument(d, root));
                var document = visible.OrderByDescending(d =>
                    d.Current.BoundingRectangle.Width * d.Current.BoundingRectangle.Height).FirstOrDefault();
                if (document != null) return document;
                if (attempt < 2) Thread.Sleep(250 * (attempt + 1));
            }
            throw new InvalidOperationException(
                "Edge no expone el contenido de esta vista. Pulsa Detectar Edge para reintentar o Copiar diagnóstico."
            );
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

    public (int Visible, int Marked) VisibleSelection()
    {
        int visible = 0, marked = 0;
        foreach (var row in By(Document, Ctrl.ListItem, Ctrl.DataItem, Ctrl.TreeItem))
        {
            try
            {
                if (!Visible(row) || !ListContext(row)) continue;
                if (Ancestors(row, 9).Any(p => p.Current.ControlType == Ctrl.ListItem
                    || p.Current.ControlType == Ctrl.DataItem)) continue;
                bool selected = Selected(row);
                visible++;
                if (selected) marked++;
            }
            catch (ElementNotAvailableException) { }
        }
        return (visible, marked);
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

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr window);
}
