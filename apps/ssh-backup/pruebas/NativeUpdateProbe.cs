using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

[assembly: AssemblyVersion("2.1.0.0")]
namespace SSHBackup
{
    // Host de ensayo: ejecuta el mismo instalador sobre una aplicación 2.1 ficticia.
    // El único paquete aceptado es la entrega real firmada; no contiene ninguna clave.
    public static class Program
    {
        public const string Version = "2.1.0";
        public static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--install-update") return Updates.RunInstaller(args[1], args[2]);
            try { Updates.LaunchInstall(args[1], ""); return 0; }
            catch (Exception ex) { File.WriteAllText(Assembly.GetExecutingAssembly().Location + ".error.txt", ex.ToString()); return 1; }
        }
    }
    public static class XmlFiles
    {
        public static T Read<T>(string path)
        {
            using (var input = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 }))
                return (T)new XmlSerializer(typeof(T)).Deserialize(input);
        }
    }
}
