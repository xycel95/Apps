using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace CorreoDiario.EdgeApp;

internal sealed record Update(Version Version, Uri Url, long Bytes, string Hash);

internal static class Updates
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };
    const string Manifest = "https://raw.githubusercontent.com/xycel95/Apps/correo-diario/apps/correo-diario/app.json";
    internal static async Task<Update?> Check(CancellationToken ct)
    {
        using var json = JsonDocument.Parse(await Client.GetStringAsync(Manifest, ct));
        var root = json.RootElement;
        if (root.GetProperty("id").GetString() != "correo-diario" || root.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("El canal de actualización no identifica una versión estable.");
        var version = Version.Parse(root.GetProperty("version").GetString()!);
        if (version <= typeof(Updates).Assembly.GetName().Version!) return null;
        var asset = root.GetProperty("executable");
        var url = $"https://github.com/xycel95/Apps/releases/download/correo-diario-v{version}/CorreoDiario-Edge.exe";
        var size = asset.GetProperty("size_bytes").GetInt64();
        var hash = asset.GetProperty("sha256").GetString();
        if (!asset.GetProperty("available").GetBoolean() || asset.GetProperty("url").GetString() != url ||
            hash?.Length != 64 || !hash.All(Uri.IsHexDigit) || size <= 0 || size > 512L*1024*1024)
            throw new InvalidDataException("La descarga publicada tiene datos de verificación incompletos.");
        return new(version, new Uri(url), size, hash);
    }

    internal static async Task Download(Update update, string path, CancellationToken ct)
    {
        using var response = await Client.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            total += count;
            if (total > update.Bytes) throw new InvalidDataException("La descarga supera el tamaño publicado.");
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0,count), ct);
        }
        if (total != update.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Hash,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La descarga no coincide con la versión publicada.");
        output.Flush(true);
    }

    internal static Process Install(string target, string staged)
    {
        string backup = target + ".anterior";
        // ReplaceFile preserves the old image until the new image can be started.
        File.Replace(staged, target, backup);
        try { return Process.Start(new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)! })!; }
        catch { File.Replace(backup, target, null); throw; }
    }
}
