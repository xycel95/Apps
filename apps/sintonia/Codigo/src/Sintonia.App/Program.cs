using System.IO.Compression;
using System.Reflection;
using LibVLCSharp.Shared;
using Sintonia.Core;

namespace Sintonia.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        try
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XyceL", "Sintonia");
            Directory.CreateDirectory(data);
            var store = new StateStore(data); var state = store.Load();
            foreach (var source in state.Sources.Where(s => s.BuiltIn && s.Entries.Count == 0))
                source.Entries = OfficialSources.Filter(M3uParser.Parse(ResourceText("spain.m3u"), source.Id));
            var native = NativeRuntime.Prepare(data);
            LibVLCSharp.Shared.Core.Initialize(native);
            using var app = new MainForm(store,state);
            if (args.Contains("--smoke-test"))
            {
                // Real WinForms + real native VLC self-check, used on Windows/Wine.
                app.Shown += (_,_) => { File.WriteAllText(Path.Combine(data,"smoke-test.txt"), "Window initialized; VLC initialized"); app.Close(); };
            }
            Application.Run(app);
        }
        catch (Exception ex) { ShowError(ex); }
    }
    internal static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream("Sintonia.App.Assets." + name) ?? throw new FileNotFoundException("Falta el recurso " + name);
    internal static string ResourceText(string name) { using var r = new StreamReader(Resource(name)); return r.ReadToEnd(); }
    private static void ShowError(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XyceL","Sintonia");
            Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder,"errors.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
        } catch { }
        MessageBox.Show("No se ha podido completar la operación.\n\n" + ex.Message + "\n\nPuedes consultar el registro en %LOCALAPPDATA%\\XyceL\\Sintonia\\errors.log.", "Sintonía", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal static class NativeRuntime
{
    internal static string Prepare(string data)
    {
        // Keeping VLC outside the .NET bundle allows LGPL library replacement.
        // Native dependencies are extracted automatically, only on the first run.
        var destination = Path.Combine(data,"runtime","vlc-3.0.24-x64");
        using var gate = new Mutex(false,"Local\\XyceL.Sintonia.VLC3024");
        try { gate.WaitOne(); } catch (AbandonedMutexException) { }
        try
        {
            if (!File.Exists(Path.Combine(destination,".complete")))
            {
                var staging = destination + ".staging";
                if (Directory.Exists(staging)) Directory.Delete(staging,true);
                Directory.CreateDirectory(staging);
                using var archive = new ZipArchive(Program.Resource("vlc-win-x64.zip"));
                archive.ExtractToDirectory(staging);
                if (!File.Exists(Path.Combine(staging,"libvlc.dll")) || !File.Exists(Path.Combine(staging,"libvlccore.dll"))) throw new InvalidDataException("El reproductor incluido está incompleto.");
                File.WriteAllText(Path.Combine(staging,".complete"),"3.0.24");
                if (Directory.Exists(destination)) Directory.Delete(destination,true);
                Directory.Move(staging,destination);
            }
        }
        finally { gate.ReleaseMutex(); }
        Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH",Path.Combine(destination,"plugins"));
        return destination;
    }
}
