using System;
using System.IO;

namespace Guarda
{
    // Only compiled into the temporary old-version test launcher, never the release.
    internal static class NativeUpdateProbe
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--guarda-apply-update") return Updates.RunInstaller(args[1], args[2]);
            SignedUpdate release = Updates.ReadSigned(File.ReadAllBytes(args[0]), Convert.FromBase64String(File.ReadAllText(args[0] + ".sig")));
            string stage = Path.Combine(Updates.CacheRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string payload = Path.Combine(stage, "Guarda.Nueva.exe"); File.Copy(args[1], payload, false);
            Updates.PrepareInstall(release, payload);
            return 0;
        }
    }
}
