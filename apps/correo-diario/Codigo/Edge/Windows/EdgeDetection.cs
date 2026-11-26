using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using AE = System.Windows.Automation.AutomationElement;
using Ctrl = System.Windows.Automation.ControlType;

namespace CorreoDiario.EdgeApp;

internal sealed class WindowProbe
{
    public int Attempts, AddressControls;
    public int? Documents;
    public bool Minimized;
    public string Version = "sin dato", Method = "ninguno", Result = "sin examinar";
    public string? Error;
    public override string ToString() =>
        $"{Result} | Edge {Version} | minimizada {(Minimized ? "sí" : "no")} | intentos {Attempts} | " +
        $"controles de dirección {AddressControls} | documentos superiores {Documents?.ToString() ?? "sin consultar"} | " +
        $"lectura {Method}" + (Error == null ? "" : " | " + Error);
}

internal sealed record Detection(List<EdgeWindow> Windows, List<WindowProbe> Probes, int Processes);

public sealed partial class EdgeWindow
{
    static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "outlook.office.com", "outlook.office365.com", "outlook.cloud.microsoft",
        "outlook.live.com", "correo.salud.madrid.org",
    };

    internal static bool IsAddressName(string name) => Regex.IsMatch(name,
        @"^(?:barra de (?:direcciones|dirección|búsqueda (?:y|o) (?:de )?direcciones?)|dirección y (?:barra de )?búsqueda|búsqueda (?:o|y) (?:dirección|barra de direcciones)|buscar (?:en (?:la )?web|o (?:escribir|introducir) (?:una )?(?:dirección|URL))|(?:address|search) (?:and |or )?(?:address|search)?\s*bar|search or (?:enter|type) (?:a |an )?(?:web address|URL)|search the web or type)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static Uri? OutlookAddress(string? value, bool allowHiddenScheme)
    {
        var address = value?.Trim().Trim('\u200e', '\u200f', '\u202a', '\u202c', '\u2066', '\u2069').Trim();
        if (string.IsNullOrEmpty(address)) return null;
        if (allowHiddenScheme && !address.Contains("://")) address = "https://" + address;
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
            uri.UserInfo.Length == 0 && uri.IsDefaultPort && Hosts.Contains(uri.IdnHost) ? uri : null;
    }

    static string Account(Uri uri)
    {
        var match = Regex.Match(uri.AbsolutePath, @"^/(?:mail|owa)/([^/]+)(?:/|$)", RegexOptions.IgnoreCase);
        if (!match.Success) return "";
        var part = Uri.UnescapeDataString(match.Groups[1].Value);
        return part.Contains('@') || int.TryParse(part, out _) ? part : "";
    }

    static bool ProviderFailure(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException
        or InvalidOperationException or ArgumentException or COMException or Win32Exception or UnauthorizedAccessException or NotSupportedException or TimeoutException;

    // Only controls attached to the native browser frame can identify its address bar or selected tab.
    static bool IsBrowserControl(AE element, AE root, out bool inLocationBar)
    {
        inLocationBar = false;
        for (AE? p = element; p != null; p = TreeWalker.RawViewWalker.GetParent(p))
        {
            if (Automation.Compare(p, root)) return true;
            var properties = p.Current;
            if (properties.ControlType == Ctrl.Document || properties.ClassName == "Chrome_RenderWidgetHostHWND") return false;
            if (properties.ClassName is "LocationBarView" or "OmniboxViewViews" or "OmniboxView") inLocationBar = true;
        }
        return false;
    }

    static bool IsTopDocument(AE document, AE root)
    {
        for (var p = TreeWalker.RawViewWalker.GetParent(document); p != null; p = TreeWalker.RawViewWalker.GetParent(p))
        {
            if (Automation.Compare(p, root)) return true;
            if (p.Current.ControlType == Ctrl.Document) return false;
        }
        return false;
    }

    static string? AddressValue(AE field, bool textAllowed, out string method)
    {
        method = "ValuePattern";
        if (field.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) &&
            ((ValuePattern)vp).Current.Value is { Length: > 0 } value) return value;
        method = "propiedad Value";
        if (field.GetCurrentPropertyValue(ValuePattern.ValueProperty, true) is string raw && raw.Length > 0) return raw;
        if (textAllowed && field.TryGetCurrentPattern(TextPattern.Pattern, out var tp))
        {
            method = "TextPattern";
            return ((TextPattern)tp).DocumentRange.GetText(8192);
        }
        method = "ninguno";
        return null;
    }

    static readonly OrCondition AddressCandidates = new(
        new PropertyCondition(AE.ControlTypeProperty, Ctrl.Edit),
        new PropertyCondition(AE.ControlTypeProperty, Ctrl.ComboBox),
        new PropertyCondition(AE.AutomationIdProperty, "addressEditBox", PropertyConditionFlags.IgnoreCase),
        new PropertyCondition(AE.ClassNameProperty, "OmniboxViewViews"));

    static Uri? Location(AE root, WindowProbe probe)
    {
        probe.AddressControls = 0;
        probe.Documents = null;
        probe.Method = "ninguno";
        foreach (AE field in root.FindAll(TreeScope.Descendants, AddressCandidates))
        {
            try
            {
                if (!IsBrowserControl(field, root, out var locationBar)) continue;
                var info = field.Current;
                if (!locationBar && !string.Equals(info.AutomationId, "addressEditBox", StringComparison.OrdinalIgnoreCase) && !IsAddressName(Name(field))) continue;
                probe.AddressControls++;
                var value = AddressValue(field, true, out var method);
                if (string.IsNullOrWhiteSpace(value)) continue;
                probe.Method = "barra / " + method;
                if (info.HasKeyboardFocus)
                {
                    // A draft in the omnibox can differ from the loaded page. Only a document may confirm it below.
                    break;
                }
                var uri = OutlookAddress(value, true);
                probe.Result = uri == null ? "dirección ajena a Outlook" : "Outlook reconocido";
                return uri;
            }
            catch (Exception e) when (ProviderFailure(e)) { probe.Error = DiagnosticLog.ErrorCode(e); }
        }

        // The provider's URL on a single outer document is a fallback. Never read the text, name, links or frames of the page.
        var documents = new List<AE>();
        foreach (var doc in By(root, Ctrl.Document))
        {
            try { if (Visible(doc) && IsTopDocument(doc, root)) documents.Add(doc); }
            catch (Exception e) when (ProviderFailure(e)) { probe.Error = DiagnosticLog.ErrorCode(e); }
        }
        probe.Documents = documents.Count;
        if (documents.Count == 1)
        {
            var value = AddressValue(documents[0], false, out var method);
            if (!string.IsNullOrWhiteSpace(value))
            {
                probe.Method = "documento / " + method;
                var uri = OutlookAddress(value, false);
                probe.Result = uri == null ? "dirección ajena a Outlook" : "Outlook reconocido";
                return uri;
            }
        }
        probe.Result = documents.Count > 1 ? "varios documentos y dirección sin confirmar" :
            probe.AddressControls == 0 ? "barra de direcciones no identificada" : "dirección no disponible o en edición";
        return null;
    }

    delegate bool EnumWindowCallback(IntPtr handle, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] static extern bool EnumWindows(EnumWindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);

    internal static Detection Discover()
    {
        var versions = new Dictionary<uint, string>();
        foreach (var process in Process.GetProcessesByName("msedge"))
        {
            using (process) versions[(uint)process.Id] = "sin dato";
        }
        var handles = new List<(IntPtr Handle, uint Process)>();
        if (!EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (versions.ContainsKey(pid) && IsWindowVisible(h) && GetWindow(h, 4) == IntPtr.Zero)
                handles.Add((h, pid));
            return true;
        }, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());

        var result = new Detection([], [], versions.Count);
        foreach (var (handle, pid) in handles)
        {
            var probe = new WindowProbe { Minimized = IsIconic(handle) };
            result.Probes.Add(probe);
            if (versions[pid] == "sin dato")
            {
                try
                {
                    using var process = Process.GetProcessById((int)pid);
                    if (Version.TryParse(process.MainModule?.FileVersionInfo.FileVersion, out var version)) versions[pid] = version.ToString();
                }
                catch (Exception e) when (ProviderFailure(e)) { }
            }
            probe.Version = versions[pid];
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                probe.Attempts = attempt;
                try
                {
                    var root = AE.FromHandle(handle);
                    var uri = Location(root, probe);
                    if (uri != null)
                    {
                        result.Windows.Add(new(handle, uri.IdnHost, Name(root), TabKey(root), Account(uri)));
                        break;
                    }
                    if (probe.Result == "dirección ajena a Outlook" || probe.Minimized) break;
                }
                catch (Exception e) when (ProviderFailure(e))
                {
                    probe.Error = DiagnosticLog.ErrorCode(e);
                    probe.Result = e is UnauthorizedAccessException || e.HResult == unchecked((int)0x80070005)
                        ? "Windows denegó el acceso a la ventana" : "no se pudieron consultar los controles de Edge";
                }
                if (attempt < 3) Thread.Sleep(attempt * 250);
            }
        }
        return result;
    }
}
