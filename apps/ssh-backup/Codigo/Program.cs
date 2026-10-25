using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("SSH Backup")]
[assembly: AssemblyDescription("Copias de carpetas por SSH/SFTP, organizadas por alias")]
[assembly: AssemblyCompany("SSH Backup")]
[assembly: AssemblyProduct("SSH Backup")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]

namespace SSHBackup
{
    public static class Program
    {
        public const string Version = "2.1.0";
        static bool scheduledLaunch;
        static readonly object resolveLock = new object();
        public static Icon AppIcon
        {
            get
            {
                try { using (var memory = new MemoryStream(ResourceBytes("app.ico"))) using (var icon = new Icon(memory)) return (Icon)icon.Clone(); }
                catch { return SystemIcons.Application; }
            }
        }
        public static void RegisterDependencies()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string name = new AssemblyName(args.Name).Name;
                if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
                lock (resolveLock)
                {
                    foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
                        if (loaded.GetName().Name == name) return loaded;
                    using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("deps." + name + ".dll"))
                    {
                        if (resource == null) return null;
                        using (var memory = new MemoryStream()) { resource.CopyTo(memory); return Assembly.Load(memory.ToArray()); }
                    }
                }
            };
        }
        public static byte[] ResourceBytes(string name)
        {
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("assets." + name))
            {
                if (source == null) throw new FileNotFoundException("No se encuentra el recurso incluido: " + name);
                using (var memory = new MemoryStream()) { source.CopyTo(memory); return memory.ToArray(); }
            }
        }
        public static string ResourceText(string name) { return Encoding.UTF8.GetString(ResourceBytes(name)); }
        [STAThread]
        public static void Main(string[] args)
        {
            RegisterDependencies();
            if (args.Length == 4 && args[0] == "--apply-update")
            {
                try { Updates.ApplyPackage(args[1], args[2], int.Parse(args[3])); }
                catch (Exception e) { ShowFatal(e); }
                return;
            }
            scheduledLaunch = args.Length == 1 && args[0] == "--scheduled";
            Start();
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Start()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, args) => ShowFatal(args.Exception);
            try
            {
                string userKey;
                using (var sha = SHA256.Create()) userKey = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Environment.UserName))).Replace("-", "").Substring(0, 16);
                bool created;
                using (var mutex = new Mutex(true, "SSHBackup-" + userKey, out created))
                {
                    if (!created)
                    {
                        if (scheduledLaunch)
                        { try { using (var signal = EventWaitHandle.OpenExisting(Scheduling.SignalName)) signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
                        else MessageBox.Show("SSH Backup ya está abierto para este usuario.", "SSH Backup");
                        return;
                    }
                    try { Application.Run(new MainForm(new ConfigStore())); }
                    finally { mutex.ReleaseMutex(); }
                }
            }
            catch (Exception error) { ShowFatal(error); }
        }
        static void ShowFatal(Exception error)
        {
            string path = "";
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SSHBackup");
                Directory.CreateDirectory(root); path = Path.Combine(root, "error-aplicacion.log");
                File.AppendAllText(path, DateTime.Now.ToString("s") + "\r\n" + error + "\r\n\r\n", new UTF8Encoding(false));
            }
            catch { }
            MessageBox.Show("No se ha podido completar la operación:\r\n" + error.Message +
                (path.Length == 0 ? "" : "\r\n\r\nDetalles guardados en:\r\n" + path), "SSH Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
