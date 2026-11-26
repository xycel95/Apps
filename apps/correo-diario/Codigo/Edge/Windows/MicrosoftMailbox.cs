using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Identity.Client;

namespace CorreoDiario.EdgeApp;

internal sealed record M365Settings(Guid ClientId, Guid? TenantId)
{
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "CorreoDiarioEdge");
    static string UserFile => Path.Combine(DirectoryPath, "Microsoft365.json");
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal static M365Settings Read(string path)
    {
        var value = JsonSerializer.Deserialize<M365Settings>(File.ReadAllText(path), Json);
        if (value == null || value.ClientId == Guid.Empty || value.TenantId == Guid.Empty)
            throw new FormatException("La configuración necesita un ID de aplicación válido.");
        return value;
    }
    internal static M365Settings? Load()
    {
        var deployment = Path.Combine(AppContext.BaseDirectory, "CorreoDiario.M365.json");
        return File.Exists(UserFile) ? Read(UserFile) : File.Exists(deployment) ? Read(deployment) : null;
    }
    internal void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = UserFile + "." + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this, Json)); File.Move(temp, UserFile, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class MicrosoftMailbox(GraphMailbox graph, string key, IDisposable lifetime, string displayAccount = "") : IDisposable
{
    internal GraphMailbox Graph { get; } = graph;
    internal string AccountKey { get; } = key;
    internal string DisplayAccount { get; } = displayAccount;
    public void Dispose() => lifetime.Dispose();
    internal const string ITRequest = """
        Para exportar en segundo plano carpetas de mi propio buzón con CorreoDiario, ¿podéis autorizar esta aplicación para mi cuenta y confirmar que el destino local está permitido?
        Configuración en Microsoft Entra: registro para la organización, cliente público, plataforma Aplicaciones móviles y de escritorio, redirección http://localhost. El único permiso de correo es el delegado Microsoft Graph Mail.Read. No se utiliza secreto ni permiso de aplicación. El usuario se identifica en el navegador corporativo.
        Necesito el ID de aplicación (cliente) y el ID de directorio (inquilino). También podéis facilitar CorreoDiario.M365.json con esos dos valores en los campos ClientId y TenantId.
        Guía de Microsoft: https://learn.microsoft.com/entra/identity-platform/scenario-desktop-app-registration
        """;
    static readonly string[] Scopes = ["https://graph.microsoft.com/Mail.Read"];

    internal static async Task<MicrosoftMailbox> Connect(M365Settings settings, CancellationToken ct)
    {
        var app = PublicClientApplicationBuilder.Create(settings.ClientId.ToString())
            .WithAuthority("https://login.microsoftonline.com/" + (settings.TenantId?.ToString() ?? "organizations"))
            .WithRedirectUri("http://localhost").Build();
        try
        {
            var result = await app.AcquireTokenInteractive(Scopes).WithUseEmbeddedWebView(false).WithPrompt(Prompt.SelectAccount).ExecuteAsync(ct).ConfigureAwait(false);
            if (result.Account?.HomeAccountId?.Identifier is not { Length: > 0 } identifier || string.IsNullOrWhiteSpace(result.TenantId))
                throw new MailboxException("cuenta incompleta", "Microsoft no ha identificado el buzón. Vuelve a conectar.");
            var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.TenantId + "/" + identifier)));
            async Task<string> Token(CancellationToken cancel)
            {
                try { return (await app.AcquireTokenSilent(Scopes, result.Account).ExecuteAsync(cancel).ConfigureAwait(false)).AccessToken; }
                catch (MsalException e) { throw Explain(e); }
            }
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3) };
            return new(new GraphMailbox(client, Token, result.UniqueId), key, client, result.Account.Username ?? "");
        }
        catch (MsalException e) { ct.ThrowIfCancellationRequested(); throw Explain(e); }
    }

    static MailboxException Explain(MsalException e)
    {
        var numeric = Regex.Match(e.Message, @"\bAADSTS\d{4,8}\b").Value;
        var code = numeric.Length > 0 ? numeric : Regex.IsMatch(e.ErrorCode ?? "", @"^[A-Za-z_0-9.-]{1,64}$") ? e.ErrorCode! : "identidad";
        var message = numeric switch
        {
            "AADSTS53003" => "La política de acceso de tu organización bloquea la conexión. Informática puede revisar el acceso de esta aplicación.",
            "AADSTS65001" or "AADSTS90094" or "AADSTS65004" => "Falta la autorización de lectura. Copia la solicitud para Informática.",
            "AADSTS700016" or "AADSTS50011" or "AADSTS7000218" => "Informática debe revisar el ID, el directorio y el registro de escritorio con la redirección http://localhost.",
            _ when e is MsalUiRequiredException => "Microsoft necesita que vuelvas a conectar tu cuenta. El lote se puede reanudar.",
            _ => "Microsoft no ha completado la conexión. Comprueba la configuración o copia el diagnóstico para Informática."
        };
        return new(code, message + " (" + code + ")");
    }
}
