using System.Net;
using System.Text;
using System.Text.Json;

namespace Sintonia.Core;

public sealed class StateStore(string directory)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string DirectoryPath => directory;
    public string? RecoveryNotice { get; private set; }
    public AppState Load()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        if (!File.Exists(path)) return new();
        try { return Validate(JsonSerializer.Deserialize<AppState>(File.ReadAllText(path)) ?? new()); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        {
            try { File.Copy(path, path + ".recovered-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch (IOException) { }
            RecoveryNotice = "No se han podido leer las preferencias anteriores. Se ha conservado una copia y se han cargado los valores iniciales.";
            return new();
        }
    }
    public void Save(AppState state)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state, Json), new UTF8Encoding(false));
        File.Move(path + ".tmp", path, true);
    }
    private static AppState Validate(AppState state)
    {
        state.Sources ??= []; state.Favorites ??= [];
        // Retire only the automatic source shipped in 1.0.0. User imports and
        // favorite identities are retained, even when they use the same URL.
        state.Sources.RemoveAll(source => source.BuiltIn || source.Id == "spain-official");
        foreach (var source in state.Sources)
        {
            source.Entries ??= [];
            source.Entries = source.Entries.Where(e => M3uParser.IsStreamUrl(e.Url) && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name)).ToList();
        }
        state.SchemaVersion = Math.Max(state.SchemaVersion, AppState.CurrentSchemaVersion);
        state.Volume = Math.Clamp(state.Volume, 0, 100); return state;
    }
}

public sealed class PlaylistService : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(25) };
    public PlaylistService() => _http.DefaultRequestHeaders.UserAgent.ParseAdd("Sintonia/1.0.1 (+https://github.com/xycel95/Apps)");
    public async Task<int> RefreshAsync(PlaylistSource source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var operationToken = timeout.Token;
        try
        {
            List<StreamEntry> entries;
            if (source.Kind == "stream")
            {
                if (!M3uParser.IsStreamUrl(source.Location)) throw new InvalidDataException("La dirección de la emisión no es válida.");
                entries = [new() { Id = Catalog.StableId("",source.Name), Name = source.Name, Label = source.Name, Url = source.Location, SourceId = source.Id }];
            }
            else
            {
                string text; Uri? baseUri = null;
                if (source.Kind == "file")
                {
                    if (new FileInfo(source.Location).Length > 15 * 1024 * 1024) throw new InvalidDataException("El archivo supera los 15 MB.");
                    text = await File.ReadAllTextAsync(source.Location, operationToken);
                }
                else
                {
                    var url = M3uParser.NormalizeListUrl(source.Location);
                    if (!M3uParser.IsListUrl(url)) throw new InvalidDataException("Usa un enlace HTTP o HTTPS a una lista M3U.");
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, operationToken);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 15 * 1024 * 1024) throw new InvalidDataException("La lista supera los 15 MB.");
                    baseUri = response.RequestMessage?.RequestUri;
                    await using var stream = await response.Content.ReadAsStreamAsync(operationToken);
                    using var memory = new MemoryStream();
                    var buffer = new byte[32768]; int n;
                    while ((n = await stream.ReadAsync(buffer, operationToken)) > 0)
                    {
                        if (memory.Length + n > 15 * 1024 * 1024) throw new InvalidDataException("La lista supera los 15 MB.");
                        memory.Write(buffer,0,n);
                    }
                    text = Encoding.UTF8.GetString(memory.ToArray());
                }
                entries = M3uParser.Parse(text, source.Id, baseUri);
                if (source.OfficialOnly) entries = OfficialSources.Filter(entries);
                if (entries.Count == 0) throw new InvalidDataException(source.OfficialOnly ? "No se han encontrado señales de los emisores identificados. Se mantiene la última lista disponible." : "No hay emisiones compatibles en esa lista. Comprueba que es un archivo M3U y no una página web.");
            }
            source.Entries = entries; source.UpdatedAt = DateTimeOffset.Now; source.LastError = "";
            return entries.Count;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or OperationCanceledException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            source.LastError = ex is OperationCanceledException ? "La conexión ha tardado demasiado." : ex.Message;
            throw new InvalidDataException(source.LastError, ex);
        }
    }
    public void Dispose() => _http.Dispose();
}
