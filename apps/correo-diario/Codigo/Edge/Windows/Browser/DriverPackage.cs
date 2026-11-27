using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;

namespace CorreoDiario.EdgeApp;

internal sealed class WebExportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class DriverPackage
{
    private const string Distribution = "https://msedgedriver.microsoft.com/";
    private static readonly HttpClient Downloads = new() { Timeout = TimeSpan.FromMinutes(2) };

    internal static string EdgePath() =>
        new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.LocalApplicationData }
        .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "Microsoft", "Edge", "Application", "msedge.exe"))
        .FirstOrDefault(File.Exists) ?? throw new WebExportException("edge-ausente", "Windows no encuentra Microsoft Edge.");

    private static Version VersionOf(string file)
    {
        var v = FileVersionInfo.GetVersionInfo(file);
        return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart, v.FilePrivatePart);
    }

    internal static void CheckPolicy()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Edge", false);
            foreach (var (name, blocked) in new[] { ("DeveloperToolsAvailability", 2), ("RemoteDebuggingAllowed", 0), ("PrintingEnabled", 0) })
                if (key?.GetValue(name) is int value && value == blocked)
                    throw new WebExportException("directiva-" + name, "La configuración de tu organización bloquea este modo de Edge (" + name + ").");
        }
    }

    internal static async Task<string> Find(string edge, string temporary, Action<string> progress, CancellationToken ct)
    {
        CheckPolicy();
        var version = VersionOf(edge);
        progress("Comprobando el controlador de Edge " + version + "…");
        var locations = new[] { AppContext.BaseDirectory, Environment.GetEnvironmentVariable("EDGEWEBDRIVER") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var directory in locations)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) continue;
            var candidate = Path.Combine(directory, "msedgedriver.exe");
            if (File.Exists(candidate) && VersionOf(candidate).ToString(3) == version.ToString(3) && MicrosoftSignature(candidate)) return candidate;
        }
        progress("Obteniendo el controlador firmado desde Microsoft…");
        var target = Path.Combine(temporary, "msedgedriver.exe");
        async Task<bool> Download(Version match)
        {
            using var reply = await Downloads.GetAsync(Distribution + match + "/edgedriver_win64.zip", HttpCompletionOption.ResponseHeadersRead, ct);
            if (reply.StatusCode == HttpStatusCode.NotFound) return false;
            if (!reply.IsSuccessStatusCode) throw new WebExportException("descarga-controlador-" + (int)reply.StatusCode, "Microsoft o el proxy ha rechazado la descarga del controlador.");
            var zipPath = Path.Combine(temporary, "controlador.zip");
            await using (var stream = await reply.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(zipPath))
            {
                byte[] block = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(block, ct)) > 0)
                {
                    if (output.Position + read > 96L * 1024 * 1024) throw new InvalidDataException("El paquete supera el tamaño previsto.");
                    await output.WriteAsync(block.AsMemory(0, read), ct);
                }
            }
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.GetEntry("msedgedriver.exe");
                if (entry == null || entry.Length > 160L * 1024 * 1024) throw new InvalidDataException("El paquete recibido no tiene el formato previsto.");
                await using var source = entry.Open();
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(output, ct);
            }
            if (VersionOf(target).ToString(3) != version.ToString(3) || !MicrosoftSignature(target))
                throw new WebExportException("firma-controlador", "Windows no reconoce una versión compatible firmada por Microsoft.");
            return true;
        }
        if (await Download(version)) return target;
        using var metadata = await Downloads.GetAsync(Distribution + "LATEST_RELEASE_" + version.Major + "_WINDOWS", ct);
        if (metadata.IsSuccessStatusCode)
        {
            var bytes = await metadata.Content.ReadAsByteArrayAsync(ct);
            var label = (bytes.Contains((byte)0) ? Encoding.Unicode : Encoding.UTF8).GetString(bytes).Trim().Trim('\uFEFF').Trim();
            if (Version.TryParse(label, out var other) && other.Build >= 0 && other.ToString(3) == version.ToString(3) && other != version && await Download(other)) return target;
        }
        throw new WebExportException("version-controlador", "Microsoft no ofrece un controlador compatible con esta compilación de Edge.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileTrust { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Choice; public IntPtr File;
        public uint Action; public IntPtr State, Url; public uint Flags, Context; public IntPtr Settings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    private static bool MicrosoftSignature(string file)
    {
        var info = new FileTrust { Size = (uint)Marshal.SizeOf<FileTrust>(), Path = file };
        var ptr = Marshal.AllocHGlobal((int)info.Size);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Choice = 1, File = ptr };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
#pragma warning disable SYSLIB0057 // Read the Authenticode signer from the verified PE, not from a certificate file.
            using var signer = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(file);
#pragma warning restore SYSLIB0057
            using var cert = X509CertificateLoader.LoadCertificate(signer.GetRawCertData());
            return string.Equals(cert.GetNameInfo(X509NameType.SimpleName, false), "Microsoft Corporation", StringComparison.OrdinalIgnoreCase);
        }
        finally { Marshal.DestroyStructure<FileTrust>(ptr); Marshal.FreeHGlobal(ptr); }
    }
}
