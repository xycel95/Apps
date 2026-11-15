using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sintonia.Core;

public sealed class StreamEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
    public string Group { get; set; } = "General";
    public string Logo { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public string Referrer { get; set; } = "";
    public string OfficialWebsite { get; set; } = "";
    public string Hint { get; set; } = "";
    public override string ToString() => Label + " · " + new Uri(Url).Host;
}

public sealed class Channel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Group { get; init; }
    public List<StreamEntry> Streams { get; init; } = [];
    public override string ToString() => Name;
}

public sealed class PlaylistSource
{
    public const string SpainUrl = "https://iptv-org.github.io/iptv/countries/es.m3u";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Kind { get; set; } = "url";
    public bool Enabled { get; set; } = true;
    public bool OfficialOnly { get; set; }
    public bool BuiltIn { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<StreamEntry> Entries { get; set; } = [];
    public string LastError { get; set; } = "";
    public override string ToString() => $"{(Enabled ? "●" : "○")}  {Name}  ·  {Entries.Count} señales";
}

public sealed class AppState
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<PlaylistSource> Sources { get; set; } = [];
    public HashSet<string> Favorites { get; set; } = [];
    public int Volume { get; set; } = 70;
    public bool Muted { get; set; }
    public string LastChannelId { get; set; } = "";
}

public static partial class Catalog
{
    [GeneratedRegex(@"\s*\((?:\d{3,4}p|\d+[kK])\)\s*|\s*\[[^\]]*\]\s*")]
    private static partial Regex Suffixes();
    public static string CleanName(string value) => Suffixes().Replace(value, " ").Trim();
    public static string SearchKey(string value)
    {
        var text = value.Normalize(NormalizationForm.FormD);
        return string.Concat(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize().ToLowerInvariant();
    }
    public static string StableId(string tvgId, string name)
    {
        if (!string.IsNullOrWhiteSpace(tvgId))
        {
            var parts = tvgId.Split('@', 2);
            if (parts.Length == 2 && new[] { "SD", "HD", "FHD", "UHD", "4K" }.Contains(parts[1], StringComparer.OrdinalIgnoreCase)) return "tvg:" + parts[0].ToLowerInvariant();
            return "tvg:" + tvgId.ToLowerInvariant();
        }
        return "name:" + SearchKey(CleanName(name));
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static List<Channel> Merge(IEnumerable<PlaylistSource> sources) => sources.Where(s => s.Enabled)
        .SelectMany(s => s.Entries.Select(e => { e.SourceName = s.Name; e.SourceId = s.Id; return e; }))
        .GroupBy(e => e.Id)
        .Select(g => new Channel { Id = g.Key, Name = g.First().Name, Group = g.First().Group,
            Streams = g.DistinctBy(e => (e.Url, e.UserAgent, e.Referrer)).ToList() })
        .OrderBy(c => c.Name, StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), true)).ToList();
}
