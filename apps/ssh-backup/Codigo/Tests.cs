using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SSHBackup
{
    public static class Tests
    {
        static int checks;
        static void Assert(bool condition, string message)
        { if (!condition) throw new Exception("FALLO: " + message); checks++; Console.WriteLine("OK " + message); }
        [STAThread]
        public static int Main(string[] args)
        {
            Program.RegisterDependencies();
            try { return Run(args); }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Run(string[] args)
        {
            if (args.Length == 0 || args[0] == "unit") Unit(args.Length > 1 ? args[1] : Path.GetTempPath());
            else if (args[0] == "ssh") Integration(args);
            else if (args[0] == "contents") Contents(args);
            else if (args[0] == "export") ExportRoundTrip(args[1]);
            else if (args[0] == "gui") Gui(args[1]);
            else throw new Exception("Modo de comprobación desconocido");
            Console.WriteLine("COMPROBACIONES SUPERADAS: " + checks); return 0;
        }
        static void Unit(string root)
        {
            Directory.CreateDirectory(root);
            var csv = Path.Combine(root, "importacion.csv");
            string secret = " Secreto;con \"comillas\"\ny salto ";
            string escaped = "\"" + secret.Replace("\"", "\"\"") + "\"";
            File.WriteAllText(csv, "alias;host;puerto;usuario;contrasena;carpeta_origen;carpeta_destino\r\n" +
                "Madrid;192.0.2.1;22;backup;" + escaped + ";/srv/datos;D:\\Copias\r\n;;;;;;\r\n", new UTF8Encoding(true));
            var rows = ServerImporter.Read(csv);
            Assert(rows.Count == 1 && rows[0].Server.Password == secret, "CSV conserva contraseñas con separadores, comillas, espacios y saltos");
            Assert(rows[0].Server.Source == "/srv/datos" && rows[0].Server.Destination == "D:\\Copias", "Importación separa origen remoto y destino local");
            Assert(rows[0].Errors.Count == 0, "Fila CSV válida y filas vacías ignoradas");
            var xlsx = Path.Combine(root, "plantilla.xlsx"); File.WriteAllBytes(xlsx, Program.ResourceBytes("Plantilla_Servidores_SSH.xlsx"));
            var excel = ServerImporter.Read(xlsx);
            Assert(excel.Count == 2 && excel[0].Server.Alias == "Sede_Madrid" && excel[0].Server.Port == 22, "Excel .xlsx directo con hoja Servidores y puerto numérico");
            Assert(excel[0].Server.Source == "/srv/datos" && excel[0].Server.Destination == "D:\\Copias_SSH", "Columnas de origen y destino en Excel");
            File.WriteAllText(csv, "alias,host,puerto,usuario,carpeta\nAnterior,192.0.2.2,,backup,/srv/origen\n");
            var older = ServerImporter.Read(csv);
            Assert(older[0].Server.Source == "/srv/origen" && older[0].Server.Port == 22, "CSV anterior, coma y puerto 22 por defecto");
            var duplicate = new List<ImportedRow> { new ImportedRow { Server = rows[0].Server.Clone() }, new ImportedRow { Server = rows[0].Server.Clone() } };
            duplicate[1].Server.Alias = "madrid";
            ServerImporter.ValidateRows(duplicate, new List<ServerConfig>(), false);
            Assert(duplicate.All(r => r.Errors.Count > 0), "Alias duplicados sin distinguir mayúsculas");
            Assert(Validation.AliasError("../escape").Length > 0 && Validation.AliasError("CON").Length > 0, "Alias que atraviesan carpetas y nombres reservados rechazados");
            Assert(Validation.ServerErrors(new ServerConfig { Alias = "Valido", Host = "host", User = "u", Source = "/x", Port = 65536 }).Count > 0, "Puerto fuera de rango rechazado");
            var existing = rows[0].Server.Clone(); existing.Password = "Anterior-123";
            var incoming = existing.Clone(); incoming.Password = ""; incoming.Source = "/nueva";
            var apply = ServerImporter.Apply(new List<ImportedRow> { new ImportedRow { Server = incoming } }, new List<ServerConfig> { existing }, true);
            Assert(apply.Count == 1 && apply[0].Id == existing.Id && apply[0].Password == "Anterior-123" && apply[0].Source == "/nueva",
                "Actualización por alias conserva identidad y contraseña vacía");
            var store = new ConfigStore(Path.Combine(root, "configuracion"));
            store.Settings.Servers.Add(rows[0].Server.Clone()); store.Save();
            string encrypted = File.ReadAllText(store.ConfigPath);
            Assert(!encrypted.Contains(secret) && !encrypted.Contains("Secreto;"), "La configuración no guarda la contraseña en texto visible");
            var loaded = new ConfigStore(store.DirectoryPath);
            Assert(loaded.Settings.Servers[0].Password == secret, "Credenciales cifradas se recuperan con el mismo usuario");
            File.WriteAllText(store.ConfigPath, encrypted.Replace("<DarkMode>false</DarkMode>", ""));
            var legacy = new ConfigStore(store.DirectoryPath);
            Assert(!legacy.Settings.DarkMode && legacy.Settings.Servers[0].Id == rows[0].Server.Id,
                "Configuración 1.0 sin tema carga en modo claro y conserva los servidores");
            legacy.Settings.DarkMode = true; legacy.Save();
            var themed = new ConfigStore(store.DirectoryPath);
            Assert(themed.Settings.DarkMode && themed.Settings.Servers[0].Password == secret &&
                themed.Settings.Servers[0].Source == "/srv/datos", "El tema persiste sin perder acceso ni rutas");
            store.Settings.Parallel = 4; store.Save(); File.WriteAllText(store.ConfigPath, "<incompleto>");
            var recovered = new ConfigStore(store.DirectoryPath);
            Assert(recovered.RecoveryNotice.Length > 0 && recovered.Settings.Servers.Count == 1, "Recuperación de configuración dañada desde copia anterior");
            Assert(ServerImporter.CsvCell("=SUM(1,2)").StartsWith("\"'="), "Resumen CSV neutraliza fórmulas");
            File.WriteAllText(csv, "alias;host;puerto;usuario;carpeta\nA;host;22;u;\"sin cerrar");
            bool invalid = false; try { ServerImporter.Read(csv); } catch (InvalidDataException) { invalid = true; }
            Assert(invalid, "CSV con comillas sin cerrar rechazado");
        }
        static void Integration(string[] args)
        {
            string host = args[1], user = args[3], password = args[4], source = args[5], destination = args[6], key = args[7];
            int port = int.Parse(args[2]);
            var store = new ConfigStore(Path.Combine(destination, "config"));
            store.Settings.Destination = destination; store.Settings.Retries = 0; store.Settings.TimeoutSeconds = 10;
            store.Settings.Exclusions = "*.tmp"; store.Settings.Parallel = 2;
            var server = new ServerConfig { Alias = "Principal", Host = host, Port = port, User = user, Password = password, Source = source };
            store.Settings.Servers.Add(server); store.Save();
            int trusts = 0;
            using (var runner = new BackupRunner(store, new[] { server }, (s, a, f) => { trusts++; return true; }))
            {
                runner.RunAsync().GetAwaiter().GetResult();
                var job = runner.Jobs[0].Snapshot();
                Assert(job.State == JobState.Success || job.State == JobState.Warning, "Copia real por SSH/SFTP: " + job.StateText + " " + job.Error);
                Assert(File.Exists(job.FinalPath) && new FileInfo(job.FinalPath).Length > 0, "ZIP final guardado por alias");
                Assert(Path.GetDirectoryName(job.FinalPath) == Path.Combine(destination, "Principal"), "Destino organizado por alias");
                using (var stream = File.OpenRead(job.FinalPath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    Assert(zip.GetEntry("omitir.tmp") == null && zip.GetEntry("carpeta vacía/") != null, "Exclusiones y directorios vacíos");
                    Assert(zip.Entries.Any(e => e.FullName.StartsWith(".ssh-backup-") && e.FullName.EndsWith(".xml")), "Manifiesto incluido");
                    var unicode = zip.GetEntry("niño.txt");
                    Assert(unicode != null, "Nombres Unicode conservados");
                    foreach (var entry in zip.Entries.Where(e => !e.FullName.EndsWith("/") && !e.FullName.StartsWith(".ssh-backup-")))
                    {
                        using (var memory = new MemoryStream())
                        using (var input = entry.Open())
                        {
                            input.CopyTo(memory);
                            Assert(memory.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(source, entry.FullName.Replace('/', Path.DirectorySeparatorChar)))),
                                "Contenido exacto: " + entry.FullName);
                        }
                    }
                }
                Assert(!File.ReadAllText(runner.Journal.LogPath).Contains(password), "Registro sin contraseña");
                Assert(store.ReadHistory()[0].Archive == job.FinalPath, "Historial conserva la ruta real");
            }
            Assert(trusts == 1, "Primera huella SSH guardada una vez");
            var byKey = server.Clone(); byKey.Alias = "Acceso_Clave"; byKey.Auth = "key"; byKey.KeyFile = key; byKey.Password = "";
            using (var runner = new BackupRunner(store, new[] { byKey }, (s, a, f) => { trusts++; return true; }, true))
            {
                runner.RunAsync().GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Success, "Acceso con clave privada SSH y comprobación sin crear ZIP: "+runner.Jobs[0].Snapshot().Error);
            }
            Assert(trusts == 1, "Huella conocida reutilizada sin nuevas confirmaciones");
            var wrong = server.Clone(); wrong.Alias = "Huella_Incorrecta";
            wrong.Fingerprint = "SHA256:" + Convert.ToBase64String(new byte[32]).TrimEnd('=');
            using (var runner = new BackupRunner(store, new[] { wrong }, (s, a, f) => false))
            {
                runner.RunAsync().GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Error && runner.Jobs[0].Snapshot().FinalPath.Length == 0, "Huella incorrecta bloquea la copia");
            }
            var bad = server.Clone(); bad.Alias = "Fallo_Controlado"; bad.Source = source + "/no-existe";
            var good = server.Clone(); good.Alias = "Continua";
            using (var runner = new BackupRunner(store, new[] { bad, good }, (s, a, f) => true))
            {
                runner.RunAsync().GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Error && (runner.Jobs[1].Snapshot().State == JobState.Success || runner.Jobs[1].Snapshot().State == JobState.Warning),
                    "Un servidor fallido permite que el siguiente termine");
            }
            var badPassword = server.Clone(); badPassword.Alias = "Clave_Incorrecta"; badPassword.Password = "incorrecta-" + Guid.NewGuid().ToString("N");
            using (var runner = new BackupRunner(store, new[] { badPassword }, (s, a, f) => true, true))
            {
                runner.RunAsync().GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Error, "Contraseña incorrecta detectada sin dar éxito");
            }
            string cancelSource = Path.Combine(source, "cancelacion");
            Directory.CreateDirectory(cancelSource);
            using (var file = File.Create(Path.Combine(cancelSource, "grande.bin")))
            {
                var buffer = new byte[1048576]; new Random(44).NextBytes(buffer);
                for (int i = 0; i < 80; i++) file.Write(buffer, 0, buffer.Length);
            }
            var canceled = server.Clone(); canceled.Alias = "Detenida"; canceled.Source = cancelSource;
            using (var runner = new BackupRunner(store, new[] { canceled }, (s, a, f) => true))
            {
                var task = runner.RunAsync(); DateTime deadline = DateTime.UtcNow.AddSeconds(20);
                while (!task.IsCompleted && runner.Jobs[0].Snapshot().Stage < 3 && DateTime.UtcNow < deadline) Thread.Sleep(15);
                runner.Cancel(); task.GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Canceled, "Cancelación durante la transferencia");
                Assert(!Directory.Exists(Path.Combine(destination, "Detenida")) || Directory.GetFiles(Path.Combine(destination, "Detenida")).Length == 0, "Copia incompleta eliminada");
            }
            var partials=Directory.GetFiles(destination, "*.partial", SearchOption.AllDirectories);
            Assert(partials.Length == 0, "No quedan ZIP incompletos"+(partials.Length>0?": "+string.Join(", ",partials):""));
        }
        static void Contents(string[] args)
        {
            string host = args[1], user = args[3], secret = args[4], source = args[5], destination = args[6], local = args[8];
            int port = int.Parse(args[2]);
            var store = new ConfigStore(Path.Combine(destination, "config"));
            store.Settings.Destination = destination; store.Settings.Retries = 0; store.Settings.TimeoutSeconds = 10;
            var paths = new[] { source, source.Replace('\\', '/'), "/" + source.Replace('\\', '/'), "\"" + source + "\"" };
            int index = 0;
            foreach (string path in paths)
            {
                index++;
                var server = new ServerConfig { Alias = "Formato_" + index, Host = host, Port = port, User = user, Password = secret, Source = path };
                using (var check = new BackupRunner(store, new[] { server }, (s, a, f) => true, true))
                {
                    check.RunAsync().GetAwaiter().GetResult();
                    var result = check.Jobs[0].Snapshot();
                    Assert(result.State == JobState.Success, "Acceso a carpeta Windows " + path + ": " + result.Error);
                    Assert(result.TotalFiles == Directory.GetFiles(local, "*", SearchOption.AllDirectories).Length,
                        "Comprobar SSH inventaría todos los archivos y subcarpetas");
                }
                using (var copy = new BackupRunner(store, new[] { server }, (s, a, f) => true))
                {
                    copy.RunAsync().GetAwaiter().GetResult();
                    var result = copy.Jobs[0].Snapshot();
                    Assert(result.State == JobState.Success, "Copia completa de origen Windows: " + result.Error);
                    using (var zip = ZipFileForTest(result.FinalPath))
                    {
                        var files = Directory.GetFiles(local, "*", SearchOption.AllDirectories);
                        Assert(zip.Entries.Count(e => e.FullName.IndexOf('/') < 0 &&
                            e.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) == 5, "Los cinco ZIP originales se incluyen sin filtrar por extensión");
                        foreach (string file in files)
                        {
                            string relative = file.Substring(local.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                            var entry = zip.GetEntry(relative);
                            Assert(entry != null, "Incluido: " + relative);
                            using (var input = entry.Open())
                            using (var buffer = new MemoryStream())
                            {
                                input.CopyTo(buffer);
                                Assert(buffer.ToArray().SequenceEqual(File.ReadAllBytes(file)), "Descarga idéntica, sin descomprimir: " + relative);
                            }
                        }
                        foreach (string directory in Directory.GetDirectories(local, "*", SearchOption.AllDirectories))
                        {
                            string relative = directory.Substring(local.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/') + "/";
                            Assert(zip.GetEntry(relative) != null, "Conservada la carpeta: " + relative);
                        }
                        Assert(result.CompletedFiles == files.Length, "Todos los archivos descargados y verificados");
                    }
                    Assert(!File.ReadAllText(copy.Journal.LogPath).Contains(secret), "Registro de copia sin contraseña");
                }
            }
            var missing = new ServerConfig { Alias = "Origen_inexistente", Host = host, Port = port, User = user,
                Password = secret, Source = source + @"\No existe" };
            using (var check = new BackupRunner(store, new[] { missing }, (s, a, f) => true, true))
            {
                check.RunAsync().GetAwaiter().GetResult();
                var result = check.Jobs[0].Snapshot();
                Assert(result.State == JobState.Error && result.FinalPath.Length == 0, "Una carpeta inexistente no se presenta como comprobación correcta");
                Assert(result.Error.Contains("/D:/Copias de prueba/No existe") && result.Error.Contains("Operación:"),
                    "El error identifica la operación y la ruta remota exacta");
            }
            missing.Source = source + @"\Copia 01.zip"; missing.Alias = "Archivo_como_origen";
            using (var check = new BackupRunner(store, new[] { missing }, (s, a, f) => true, true))
            {
                check.RunAsync().GetAwaiter().GetResult();
                Assert(check.Jobs[0].Snapshot().State == JobState.Error &&
                    check.Jobs[0].Snapshot().Error.Contains("carpeta, no un archivo"),
                    "Seleccionar un ZIP como carpeta de origen muestra una explicación concreta");
            }
        }
        static ZipArchive ZipFileForTest(string path)
        { return new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read); }

        static void ExportRoundTrip(string folder)
        {
            Directory.CreateDirectory(folder);
            string pin = "SHA256:" + Convert.ToBase64String(new byte[32]).TrimEnd('=');
            var passwords = new[] { "Ejemplo-privado-123", "=SUM(1,2)", "+Simbolo", "-Simbolo", "@Simbolo",
                "'Comilla", " \t=Espacios", "con;separador\"comillas\r\nsaltos\tfin ", "_x0041_ _x005F_ \u0001", "Córdoba \ud83d\udd11" };
            var settings = new Settings { Destination = @"D:\Copias generales" };
            var servers = Enumerable.Range(0, 50).Select(i => new ServerConfig {
                Alias = i == 0 ? "=Sede_Madrid" : i == 1 ? "'Sede_Córdoba" : "Sede_" + i.ToString("00"),
                Host = "192.0.2." + (i + 1), Port = i % 2 == 0 ? 22 : 2222, User = i == 0 ? "+backup" : "backup",
                Password = passwords[i % passwords.Length], EncryptedPassword = "CIFRADO-QUE-NO-SE-EXPORTA",
                Source = i % 2 == 0 ? @"D:\Copias remotas" : "/srv/datos",
                Destination = i % 3 == 0 ? "" : @"E:\Copias por sede", Auth = i % 4 == 0 ? "key" : "password",
                KeyFile = @"C:\Claves\servidor.pem", Enabled = i % 3 != 0
            }).ToList();
            foreach (string extension in new[] { ".xlsx", ".csv" })
            {
                string file = Path.Combine(folder, "Servidores_con_acceso" + extension);
                Assert(ServerExporter.Write(file, servers, settings, s => pin, true) == 50, "Exportación completa de 50 servidores " + extension);
                var rows = ServerImporter.Read(file);
                ServerImporter.ValidateRows(rows, new List<ServerConfig>(), false);
                Assert(rows.Count == 50 && rows.All(r => r.Errors.Count == 0), "Listado exportado se puede importar " + extension);
                var imported = ServerImporter.Apply(rows, new List<ServerConfig>(), false);
                Assert(imported.Select(s => s.Alias).SequenceEqual(servers.Select(s => s.Alias)), "Alias y orden completos " + extension);
                Assert(imported.Select(s => s.Password).SequenceEqual(servers.Select(s => s.Password)), "Contraseñas exactas con símbolos, saltos, Unicode y texto OOXML " + extension);
                Assert(imported.Select(s => s.Enabled).SequenceEqual(servers.Select(s => s.Enabled)), "Incluye servidores no marcados y conserva Copiar " + extension);
                Assert(imported.Select(s => s.Auth + "|" + s.KeyFile).SequenceEqual(servers.Select(s => s.Auth + "|" + s.KeyFile)), "Tipo de acceso y ruta de clave conservados " + extension);
                Assert(imported.Select(s => s.Host + "|" + s.Port + "|" + s.User + "|" + s.Source)
                    .SequenceEqual(servers.Select(s => s.Host + "|" + s.Port + "|" + s.User + "|" + s.Source)), "Conexión y origen conservados " + extension);
                Assert(imported.Select(s => s.Destination).SequenceEqual(servers.Select(s => s.BaseDestination(settings))) &&
                    imported.All(s => s.Fingerprint == pin), "Destino efectivo y huella conocida portables " + extension);
                string noSecrets = Path.Combine(folder, "Servidores_sin_acceso" + extension);
                ServerExporter.Write(noSecrets, servers, settings, s => pin, false);
                var omitted = ServerImporter.Read(noSecrets);
                Assert(omitted.All(r => r.Server.Password == ""), "Exportación sin contraseñas " + extension);
                var existing = servers.Select(s => s.Clone()).ToList();
                foreach (var s in existing) s.Enabled = !s.Enabled;
                var updated = ServerImporter.Apply(omitted, existing, true);
                Assert(updated.Select(s => s.Password).SequenceEqual(servers.Select(s => s.Password)) &&
                    updated.Select(s => s.Enabled).SequenceEqual(servers.Select(s => s.Enabled)), "Actualizar por alias conserva contraseña omitida y restaura Copiar " + extension);
                byte[] original = File.ReadAllBytes(file); servers[0].PasswordLoaded = false;
                bool denied = false;
                try { ServerExporter.Write(file, servers, settings, s => pin, true); } catch (InvalidOperationException) { denied = true; }
                servers[0].PasswordLoaded = true;
                Assert(denied && File.ReadAllBytes(file).SequenceEqual(original), "Un acceso sin descifrar no sobrescribe un archivo válido " + extension);
                ServerExporter.Write(file, servers, settings, s => pin, true);
                Assert(ServerImporter.Read(file).Count == 50, "Sustitución completa de un archivo existente " + extension);
                if (extension == ".csv")
                {
                    var parsed = ServerImporter.ParseCsv(File.ReadAllText(file), ';');
                    Assert(parsed[1].Item2[0].StartsWith("'=") && parsed[1].Item2[3].StartsWith("'+") &&
                        parsed[2].Item2[4].StartsWith("'="), "CSV neutraliza fórmulas y restaura el valor al importar");
                    Assert(File.ReadAllBytes(file).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }), "CSV UTF-8 con BOM para Windows");
                }
                else
                {
                    using (var zip = ZipFileForTest(file))
                    using (var input = zip.GetEntry("xl/worksheets/sheet1.xml").Open())
                    {
                        var xml = new System.Xml.XmlDocument(); xml.Load(input);
                        Assert(xml.SelectNodes("//*[local-name()='f']").Count == 0 &&
                            xml.SelectSingleNode("//*[local-name()='c' and @r='E3']").Attributes["t"].Value == "inlineStr",
                            "Excel guarda los textos especiales como texto, sin fórmulas");
                    }
                }
            }
            string invalid = Path.Combine(folder, "Tipo_invalido.csv");
            File.WriteAllText(invalid, "alias;host;usuario;carpeta_origen;tipo_acceso;copiar\r\nSede;host;backup;/srv;invalido;otro\r\n");
            var invalidRows = ServerImporter.Read(invalid); ServerImporter.ValidateRows(invalidRows, new List<ServerConfig>(), false);
            Assert(invalidRows[0].Errors.Count >= 2, "La vista previa conserva los errores de tipo de acceso y Copiar");
            Assert(servers.All(s => s.EncryptedPassword == "CIFRADO-QUE-NO-SE-EXPORTA") && servers[0].Destination == "",
                "Exportar no modifica credenciales ni destinos guardados");
            Assert(Directory.GetFiles(folder, "*.tmp").Length == 0, "Archivos temporales de exportación eliminados");
        }

        static void Gui(string folder)
        {
            Directory.CreateDirectory(folder);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var store = new ConfigStore(Path.Combine(folder, "config-gui")); store.Settings.Destination = Path.Combine(folder, "copias");
            store.Settings.DarkMode = false;
            store.Settings.Servers = Enumerable.Range(1, 6).Select(n => new ServerConfig {
                Alias = n == 1 ? "Sede_Madrid" : n == 2 ? "Sede_Cordoba" : "SERVIDOR-" + n.ToString("00"),
                Host = "192.0.2." + (10 + n), User = "backup", Password = "ejemplo", Source = "/srv/datos"
            }).ToList();
            store.History.Entries.Clear();
            store.AddHistory(new HistoryEntry { Alias = "Sede_Madrid", Host = "192.0.2.11", Status = "Correcto",
                StartedUtc = DateTime.UtcNow.AddMinutes(-4), FinishedUtc = DateTime.UtcNow.AddMinutes(-2),
                Files = 60, Bytes = 800000000, Archive = Path.Combine(store.Settings.Destination, "Sede_Madrid/copia.zip") });
            store.AddHistory(new HistoryEntry { Alias = "SERVIDOR-05", Host = "192.0.2.15", Status = "Error",
                StartedUtc = DateTime.UtcNow.AddMinutes(-3), FinishedUtc = DateTime.UtcNow.AddMinutes(-2),
                Error = "Conexión rechazada en el puerto 22." });
            var form = new MainForm(store);
            var runner = new BackupRunner(store, store.Settings.Servers, (s, a, f) => false);
            for (int i = 0; i < runner.Jobs.Count; i++)
            {
                int index = i;
                runner.Jobs[i].Change(j => { j.State = index == 0 ? JobState.Success : index < 4 ? JobState.Running : index == 4 ? JobState.Error : JobState.Pending;
                    j.Stage = index == 0 ? 5 : index < 4 ? 3 : 1; j.Progress = index == 0 ? 100 : index < 4 ? 64 - index * 8 : 0;
                    j.TotalFiles = 60; j.CompletedFiles = 32; j.TotalBytes = 800000000; j.CopiedBytes = 430000000; j.Phase = index < 4 ? "Copiando" : "En cola";
                    j.CurrentFile = "documentos/informe.pdf"; if (index == 4) j.Error = "Conexión rechazada en el puerto 22."; });
            }
            typeof(MainForm).GetField("runner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, runner);
            runner.Journal.Write("INFO", "Sede_Madrid", "Leyendo /srv/datos por SFTP.");
            runner.Journal.Write("OK", "Sede_Cordoba", "ZIP guardado y verificado.");
            runner.Journal.Write("ERROR", "SERVIDOR-05", "Conexión rechazada. El lote continúa.");
            runner.Journal.Write("AVISO", "Sede_Madrid", "Un enlace simbólico se ha omitido.");
            var pending = new System.Threading.Tasks.TaskCompletionSource<bool>();
            typeof(MainForm).GetField("currentTask", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, pending.Task);
            var toggle = (CheckBox)typeof(MainForm).GetField("darkMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            var destination = (TextBox)typeof(MainForm).GetField("settingsDestination", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Action<string> page = name => typeof(MainForm).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { name });
            int stage = 0; Form dialog = null;
            var timer = new System.Windows.Forms.Timer { Interval = 800 };
            Action<Form, string> capture = (window, name) => {
                using (var bitmap = new Bitmap(window.Width, window.Height))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                        graphics.CopyFromScreen(window.Left, window.Top, 0, 0, bitmap.Size);
                    bitmap.Save(Path.Combine(folder, name + ".png"));
                }
                Console.WriteLine("Captura: " + name);
            };
            timer.Tick += (s, e) => {
                try
                {
                    if (stage == 0)
                    {
                        capture(form, "copias-claro"); toggle.Checked = true;
                        Assert(!pending.Task.IsCompleted && runner.Jobs[1].Snapshot().Progress == 56 &&
                            ReferenceEquals(typeof(MainForm).GetField("runner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form), runner),
                            "Cambiar el tema durante una ejecución mantiene su tarea, cola y avance");
                    }
                    else if (stage == 1) { capture(form, "copias-oscuro"); page("servers"); }
                    else if (stage == 2) { capture(form, "servidores-oscuro"); dialog = new ServerDialog(store.Settings.Servers[0], store.Settings); dialog.Show(form); }
                    else if (stage == 3) { capture(dialog, "editor-oscuro"); dialog.Close(); pending.SetResult(true); page("settings"); }
                    else if (stage == 4)
                    {
                        capture(form, "ajustes-oscuro"); destination.Text = @"D:\PendienteDeGuardar";
                        toggle.Checked = false;
                        Assert(destination.Text == @"D:\PendienteDeGuardar" && store.Settings.Destination != destination.Text,
                            "Cambiar de tema mantiene los ajustes pendientes sin aplicarlos");
                    }
                    else if (stage == 5) { capture(form, "ajustes-claro"); toggle.Checked = true; page("history"); }
                    else if (stage == 6)
                    {
                        capture(form, "historial-oscuro");
                        dialog = new HostTrustDialog(store.Settings.Servers[0], "ssh-ed25519", "SHA256:" + Convert.ToBase64String(new byte[32]).TrimEnd('='));
                        dialog.Show(form);
                    }
                    else if (stage == 7) { capture(dialog, "huella-oscuro"); dialog.Close(); dialog = new AccessDialog(6); dialog.Show(form); }
                    else if (stage == 8) { capture(dialog, "acceso-oscuro"); dialog.Close();
                        string template = Path.Combine(folder, "plantilla.xlsx");
                        File.WriteAllBytes(template, Program.ResourceBytes("Plantilla_Servidores_SSH.xlsx"));
                        dialog = new ImportDialog(template, new List<ServerConfig>()); dialog.Show(form); }
                    else if (stage == 9) { capture(dialog, "importacion-oscuro"); dialog.Close(); dialog = new HelpDialog(Program.ResourceText("GUIA.txt")); dialog.Show(form); }
                    else if (stage == 10) { capture(dialog, "ayuda-oscuro"); dialog.Close(); form.Size = form.MinimumSize; page("copies"); }
                    else if (stage == 11) { capture(form, "copias-oscuro-minimo"); page("settings"); }
                    else if (stage == 12) { capture(form, "ajustes-oscuro-minimo"); toggle.Checked = false; page("copies"); }
                    else if (stage == 13) { capture(form, "copias-claro-minimo"); toggle.Checked = true; page("servers"); }
                    else if (stage == 14)
                    {
                        capture(form, "servidores-oscuro-minimo");
                        var exportButton = (Button)typeof(MainForm).GetField("exportServers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                        var actions = exportButton.Parent;
                        Assert(actions.Controls.Cast<Control>().All(c => c.Bounds.Right <= actions.ClientSize.Width && c.Bounds.Bottom <= actions.ClientSize.Height),
                            "Todos los botones de Servidores caben al tamaño mínimo");
                        dialog = new ExportDialog(6); dialog.Show(form);
                    }
                    else if (stage == 15)
                    {
                        capture(dialog, "exportar-excel-oscuro");
                        var options = (ExportDialog)dialog;
                        Assert(options.Extension == ".xlsx" && !options.IncludePasswords, "Exportar ofrece Excel y contraseñas opcionales sin marcar");
                        var choice = (ComboBox)typeof(ExportDialog).GetField("format", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(options);
                        choice.SelectedIndex = 1;
                        Assert(options.Extension == ".csv", "El selector permite elegir CSV");
                    }
                    else if (stage == 16) { capture(dialog, "exportar-csv-oscuro"); dialog.Close(); toggle.Checked = false; dialog = new ExportDialog(6); dialog.Show(form); }
                    else if (stage == 17) { capture(dialog, "exportar-excel-claro"); dialog.Close(); toggle.Checked = true; timer.Stop(); form.Close(); }
                    stage++;
                }
                catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
            };
            form.Shown += (s, e) => timer.Start();
            Application.Run(form); timer.Dispose(); Assert(stage >= 18, "Interfaz y exportación en ambos temas y tamaño mínimo");
            var reloaded = new ConfigStore(store.DirectoryPath);
            using (var reopened = new MainForm(reloaded))
            {
                reopened.Show(); Application.DoEvents();
                var savedToggle = (CheckBox)typeof(MainForm).GetField("darkMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reopened);
                Assert(savedToggle.Checked && reloaded.Settings.Servers.Count == 6 && reloaded.ReadHistory().Count == 2,
                    "Reabrir recupera el modo oscuro, los seis servidores y el historial");
                capture(reopened, "reinicio-oscuro"); reopened.Close();
            }
        }
    }
}
