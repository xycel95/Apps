using System.Text.RegularExpressions;

namespace Sintonia.Core;

public static partial class M3uParser
{
    [GeneratedRegex("([\\w-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s]+))")]
    private static partial Regex Attributes();
    private static readonly HashSet<string> Schemes = ["https", "http", "rtsp", "rtmp", "udp", "rtp"];
    public static bool IsStreamUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && Schemes.Contains(u.Scheme) && !string.IsNullOrEmpty(u.Host) && !url.Any(char.IsControl);
    public static bool IsListUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http" && !url.Any(char.IsControl);
    public static string NormalizeListUrl(string value)
    {
        value = value.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Host.Equals("iptv-org.github.io", StringComparison.OrdinalIgnoreCase) && u.AbsolutePath is "/" or "") return PlaylistSource.SpainUrl;
        return value;
    }
    public static List<StreamEntry> Parse(string text, string sourceId, Uri? baseUri = null)
    {
        if (text.Contains("#EXT-X-TARGETDURATION", StringComparison.OrdinalIgnoreCase) || text.Contains("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ese enlace es una emisión HLS. Añádelo como «Emisión directa», en lugar de como lista M3U.");
        var result = new List<StreamEntry>();
        Dictionary<string, string> attrs = new(StringComparer.OrdinalIgnoreCase);
        string name = "", userAgent = "", referrer = "", group = "";
        foreach (var raw in text.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = MetadataComma(line);
                attrs = Attributes().Matches(comma < 0 ? line : line[..comma]).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value, StringComparer.OrdinalIgnoreCase);
                name = comma < 0 ? "" : line[(comma + 1)..].Trim();
                userAgent = attrs.GetValueOrDefault("http-user-agent", ""); referrer = attrs.GetValueOrDefault("http-referrer", ""); group = attrs.GetValueOrDefault("group-title", "General");
            }
            else if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase)) group = line[8..];
            else if (line.StartsWith("#EXTVLCOPT:http-user-agent=", StringComparison.OrdinalIgnoreCase)) userAgent = line[(line.IndexOf('=') + 1)..];
            else if (line.StartsWith("#EXTVLCOPT:http-referrer=", StringComparison.OrdinalIgnoreCase)) referrer = line[(line.IndexOf('=') + 1)..];
            else if (line.Length > 0 && !line.StartsWith('#'))
            {
                // Kodi's URL suffix is parsed as header data, never as command-line options.
                var split = line.Split('|', 2); var url = split[0];
                if (split.Length > 1)
                    foreach (var param in split[1].Split('&'))
                    {
                        var p = param.Split('=', 2);
                        if (p.Length != 2) continue;
                        if (p[0].Equals("User-Agent", StringComparison.OrdinalIgnoreCase)) userAgent = Uri.UnescapeDataString(p[1]);
                        if (p[0].Equals("Referer", StringComparison.OrdinalIgnoreCase)) referrer = Uri.UnescapeDataString(p[1]);
                    }
                if (!Uri.TryCreate(url, UriKind.Absolute, out _) && baseUri is not null && Uri.TryCreate(baseUri, url, out var resolved)) url = resolved.AbsoluteUri;
                if (IsStreamUrl(url))
                {
                    var label = name.Length > 0 ? name : new Uri(url).Host;
                    result.Add(new() { Id = Catalog.StableId(attrs.GetValueOrDefault("tvg-id", ""), label), Name = Catalog.CleanName(label), Label = label,
                        Url = url, Group = TranslateGroups(group), Logo = attrs.GetValueOrDefault("tvg-logo", ""), SourceId = sourceId,
                        UserAgent = SafeHeader(userAgent), Referrer = SafeHeader(referrer), Hint = label.Contains("Geo-blocked", StringComparison.OrdinalIgnoreCase) ? "Puede tener restricciones geográficas" : label.Contains("Not 24/7", StringComparison.OrdinalIgnoreCase) ? "Emisión con horario limitado" : "" });
                }
                attrs.Clear(); name = userAgent = referrer = group = "";
            }
            if (result.Count > 30000) throw new InvalidDataException("La lista supera el máximo de 30.000 señales.");
        }
        return result.DistinctBy(e => (e.Id, e.Url, e.UserAgent, e.Referrer)).ToList();
    }
    private static string SafeHeader(string s) => s.Length <= 2048 && !s.Any(char.IsControl) ? s : "";
    private static int MetadataComma(string s)
    {
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] is '\'' or '"') { if (quote == '\0') quote = s[i]; else if (quote == s[i]) quote = '\0'; }
            else if (s[i] == ',' && quote == '\0') return i;
        }
        return -1;
    }
    private static string TranslateGroups(string s)
    {
        var names = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["General"]="General",["Undefined"]="General",["News"]="Noticias",["Sports"]="Deportes",["Public"]="Pública",["Kids"]="Infantil",["Movies"]="Cine",["Music"]="Música",["Entertainment"]="Entretenimiento",["Documentary"]="Documentales",["Weather"]="Tiempo",["Series"]="Series",["Culture"]="Cultura",["Education"]="Educación",["Religious"]="Religión",["Classic"]="Clásicos",["Comedy"]="Comedia",["Legislative"]="Institucional",["Business"]="Economía",["Lifestyle"]="Estilo de vida" };
        return string.Join(" · ", s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => names.GetValueOrDefault(p,p)).DefaultIfEmpty("General"));
    }
}
