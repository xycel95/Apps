using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace SSHBackup
{
    public static class Tests2
    {
        static int checks;
        static void Assert(bool condition, string text)
        { if (!condition) throw new Exception("FALLO: " + text); checks++; Console.WriteLine("OK " + text); }
        [STAThread]
        public static int Main(string[] args)
        {
            Program.RegisterDependencies();
            try
            {
                if (args[0] == "unit2") Unit(args[1], args[2]);
                else if (args[0] == "bandwidth") Bandwidth();
                else if (args[0] == "schedule") Schedule(args[1]);
                else if (args[0] == "ssh2") Ssh(args);
                else if (args[0] == "gui2") Gui(args[1]);
                else throw new ArgumentException("Modo desconocido");
                Console.WriteLine("COMPROBACIONES NUEVAS SUPERADAS: " + checks); return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        static void Unit(string root, string packages)
        {
            Directory.CreateDirectory(root);
            var settings = new Settings();
            Assert(!settings.OnlyChanges && !settings.ScheduleEnabled && !settings.BandwidthEnabled, "Novedades, programación y límite de red desactivados por defecto");
            settings.OnlyChanges = true; settings.ScheduleEnabled = true; settings.ScheduleTime = "14:30";
            Assert(settings.ExecutionCopy().OnlyChanges && settings.ExecutionCopy().ScheduleTime == "14:30", "Opciones conservadas en la ejecución");
            var monday = new DateTime(2026, 10, 26, 14, 29, 0);
            Assert(!Scheduling.Due(settings, monday) && Scheduling.Due(settings, monday.AddMinutes(1)), "Programación inicia al llegar la hora");
            settings.LastScheduleDate = "2026-10-26";
            Assert(!Scheduling.Due(settings, monday.AddHours(2)), "No se duplica una ejecución programada en el mismo día");
            settings.LastScheduleDate = ""; settings.ScheduleWeekdaysOnly = true;
            Assert(!Scheduling.Due(settings, new DateTime(2026, 10, 24, 18, 0, 0)), "Programación de lunes a viernes omite el sábado");
            var xml = new XmlDocument(); xml.LoadXml(Scheduling.BuildTaskXml(settings, @"C:\Copias & Datos\SSHBackup.exe", "S-1-5-21-ejemplo", monday));
            var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");
            Assert(xml.SelectSingleNode("//t:Command", ns).InnerText == @"C:\Copias & Datos\SSHBackup.exe" &&
                xml.SelectSingleNode("//t:LogonType", ns).InnerText == "InteractiveToken" && xml.SelectSingleNode("//t:DaysOfWeek/t:Monday", ns) != null,
                "Tarea Windows válida como XML: ruta con espacios, usuario interactivo y días elegidos");
            bool rejected = false; try { Scheduling.Time("25:99"); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Hora programada fuera de rango rechazada");
            var store = new ConfigStore(Path.Combine(root, "config")); store.Settings.OnlyChanges = true; store.Settings.ScheduleEnabled = true;
            store.Settings.ScheduleTime = "04:10"; store.Settings.BandwidthEnabled = true; store.Settings.BandwidthValue = 12.5m; store.Settings.BandwidthUnit = "Mbps";
            store.Save(); var loaded = new ConfigStore(store.DirectoryPath);
            Assert(loaded.Settings.OnlyChanges && loaded.Settings.ScheduleEnabled && loaded.Settings.ScheduleTime == "04:10", "Opciones persisten al reiniciar");
            Assert(loaded.Settings.BandwidthEnabled && loaded.Settings.BandwidthValue == 12.5m && loaded.Settings.BandwidthUnit == "Mbps", "El límite y la unidad persisten al reiniciar");
            var valid = Updates.ReadPackage(Path.Combine(packages, "valid.sshupdate"));
            Assert(valid.Version == Program.Version, "Paquete de actualización con firma RSA y SHA-256 aceptado");
            foreach (string name in new[] { "tampered.sshupdate", "bad-signature.sshupdate", "traversal.sshupdate" })
            {
                rejected = false; try { Updates.ReadPackage(Path.Combine(packages, name)); } catch (Exception) { rejected = true; }
                Assert(rejected, "Actualización no válida rechazada: " + name);
            }
            rejected = false; try { Updates.RequireHttps("http://example.com/release.xml"); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Canales sin HTTPS rechazados");
            valid.Notes += " alteradas"; rejected = false; try { Updates.Validate(valid); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Cambiar notas del parche invalida la firma");
            string target = Path.Combine(root, "actualizar.exe");
            File.Copy(Path.Combine(packages, "old.exe"), target, true);
            string oldHash = Updates.FileHash(target);
            bool launched = false;
            Updates.ApplyPackage(Path.Combine(packages, "valid.sshupdate"), target, int.MaxValue, path => launched = Updates.FileHash(path) == Updates.ReadPackage(Path.Combine(packages, "valid.sshupdate")).Sha256);
            Assert(launched && Updates.FileHash(target + ".previous") == oldHash, "Actualizador reemplaza el EXE y conserva la versión anterior");
            File.Copy(Path.Combine(packages, "old.exe"), target, true); rejected = false;
            try { Updates.ApplyPackage(Path.Combine(packages, "valid.sshupdate"), target, int.MaxValue, path => { throw new IOException("Error de arranque simulado"); }); }
            catch (IOException) { rejected = true; }
            Assert(rejected && Updates.FileHash(target) == oldHash, "Si falla el arranque, restaura el ejecutable anterior");
            var before = new BackupCatalog { Alias = "A", Host = "host", Source = "/datos", Path = "antes.zip" };
            before.Files.Add("informe.txt", new CatalogFile { Relative = "informe.txt", Size = 3, Hash = "a", ModifiedUtc = DateTime.UtcNow });
            before.Files.Add("borrado.txt", new CatalogFile { Relative = "borrado.txt", Size = 5, Hash = "b", ModifiedUtc = DateTime.UtcNow });
            var after = new BackupCatalog { Alias = "A", Host = "host", Source = "/datos", Path = "despues.zip" };
            after.Files.Add("informe.txt", new CatalogFile { Relative = "informe.txt", Size = 3, Hash = "c", ModifiedUtc = before.Files["informe.txt"].ModifiedUtc });
            after.Files.Add("carpeta/NUEVO.txt", new CatalogFile { Relative = "carpeta/NUEVO.txt", Size = 6, Hash = "d", ModifiedUtc = DateTime.UtcNow });
            after.Directories.Add("vacía/");
            var changes = ArchiveCatalog.Compare(before, after);
            Assert(changes.Count == 4 && changes.Any(c => c.Change == "Modificado" && c.Relative == "informe.txt") && changes.Any(c => c.Change == "Eliminado") &&
                changes.Any(c => c.Change == "Añadido"), "Comparador detecta contenido distinto con el mismo tamaño y fecha, altas, bajas y carpetas");
            var hits = ArchiveCatalog.Search(new[] { before, after }, "INFORME");
            Assert(hits.Count == 2 && ArchiveCatalog.Search(new[] { before, after }, "carpeta/").Count == 1, "Búsqueda entre copias por nombre y ruta, sin distinguir mayúsculas");
        }
        static void Schedule(string root)
        {
            Directory.CreateDirectory(root);
            var monday = new DateTime(2026, 10, 26, 20, 1, 0);
            string[] tags = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            foreach (int mask in new[] { 1, 2, 4, 8, 16, 32, 64, 5, 127 })
            {
                var settings = new Settings { ScheduleEnabled = true, ScheduleTime = "20:00", ScheduleDaysMask = mask };
                Assert(Enumerable.Range(0, 7).All(i => Scheduling.Due(settings, monday.AddDays(i)) == ((mask & (1 << i)) != 0)),
                    "Solo se ejecuta en los días seleccionados: " + Scheduling.DaysText(mask));
                var xml = new XmlDocument(); xml.LoadXml(Scheduling.BuildTaskXml(settings, @"C:\Copias\SSHBackup.exe", "S-1-5-21-prueba", monday));
                var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");
                Assert(Enumerable.Range(0, 7).All(i => (xml.SelectSingleNode("//t:DaysOfWeek/t:" + tags[i], ns) != null) == ((mask & (1 << i)) != 0)),
                    "La tarea de Windows contiene exactamente esos días: " + Scheduling.DaysText(mask));
                Assert(!Scheduling.Due(settings, monday.Date.AddHours(19)), "Se respeta la hora en la selección " + mask);
            }
            var custom = new Settings { ScheduleEnabled = true, ScheduleTime = "20:00", ScheduleDaysMask = 5, LastScheduleDate = "2026-10-26" };
            Assert(!Scheduling.Due(custom, monday), "La programación personalizada no duplica la ejecución del día");
            Assert(custom.ExecutionCopy().ScheduleDaysMask == 5, "La selección se conserva al crear los ajustes de ejecución");
            var store = new ConfigStore(Path.Combine(root, "config")); store.Settings.ScheduleDaysMask = 5; store.Save();
            Assert(new ConfigStore(store.DirectoryPath).Settings.ScheduleDaysMask == 5, "Lunes y miércoles se guardan y recuperan al reiniciar");
            string persisted = File.ReadAllText(store.ConfigPath).Replace("<ScheduleDaysMask>5</ScheduleDaysMask>", "");
            File.WriteAllText(store.ConfigPath, persisted.Replace("<ScheduleWeekdaysOnly>false</ScheduleWeekdaysOnly>", "<ScheduleWeekdaysOnly>true</ScheduleWeekdaysOnly>"));
            Assert(Scheduling.SelectedDays(new ConfigStore(store.DirectoryPath).Settings) == 31, "Configuración 2.0 de lunes a viernes conserva los cinco días");
            File.WriteAllText(store.ConfigPath, persisted);
            Assert(Scheduling.SelectedDays(new ConfigStore(store.DirectoryPath).Settings) == 127, "Configuración 2.0 diaria conserva los siete días");
            var none = new Settings { ScheduleEnabled = true, ScheduleTime = "20:00", ScheduleDaysMask = 0 };
            Assert(!Scheduling.Due(none, monday), "Sin días seleccionados no se ejecuta ninguna copia");
            bool rejected = false; try { Scheduling.BuildTaskXml(none, @"C:\SSHBackup.exe", "usuario", monday); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "No se permite crear una tarea activa sin días");
        }
        static JobInfo Copy(ConfigStore store, ServerConfig server)
        {
            using (var runner = new BackupRunner(store, new[] { server }, (s, a, f) => true))
            {
                runner.RunAsync().GetAwaiter().GetResult(); var result = runner.Jobs[0].Snapshot();
                Assert(result.State == JobState.Success, "Copia SSH terminada: " + result.Phase + " " + result.Error);
                return result;
            }
        }
        static void SameContents(string path, string source)
        {
            using (var file = File.OpenRead(path))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                foreach (var local in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string relative = local.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.GetEntry(relative); Assert(entry != null, "Archivo presente en copia: " + relative);
                    using (var input = entry.Open())
                    using (var memory = new MemoryStream()) { input.CopyTo(memory); Assert(memory.ToArray().SequenceEqual(File.ReadAllBytes(local)), "Bytes exactos: " + relative); }
                }
            }
        }
        static void Interrupt(ConfigStore store, ServerConfig server)
        {
            using (var runner = new BackupRunner(store, new[] { server }, (s, a, f) => true))
            {
                var task = runner.RunAsync(); var watch = Stopwatch.StartNew();
                while (!task.IsCompleted && runner.Jobs[0].Snapshot().DownloadedBytes < 32768 && watch.ElapsedMilliseconds < 30000) Thread.Sleep(5);
                Assert(!task.IsCompleted, "Transferencia activa antes de interrumpir"); runner.Cancel(); task.GetAwaiter().GetResult();
                Assert(runner.Jobs[0].Snapshot().State == JobState.Canceled && runner.Jobs[0].Snapshot().FinalPath == "", "Cancelación no publica un ZIP incompleto");
                Assert(Directory.GetFiles(server.Folder(store.Settings), "*.part", SearchOption.AllDirectories).Any(p => new FileInfo(p).Length > 0), "Se conserva la descarga parcial para reanudar");
            }
        }
        static void Ssh(string[] args)
        {
            string source = args[5], destination = args[6]; Directory.CreateDirectory(destination);
            byte[] big = new byte[512 * 1024]; new Random(42).NextBytes(big); File.WriteAllBytes(Path.Combine(source, "grande.bin"), big);
            var store = new ConfigStore(Path.Combine(destination, "config2")); store.Settings.Destination = destination; store.Settings.Retries = 0;
            var server = new ServerConfig { Alias = "Sede_Pruebas", Host = args[1], Port = int.Parse(args[2]), User = args[3], Password = args[4], Source = source };
            store.Settings.Servers.Add(server); store.Save();
            Interrupt(store, server);
            var resumed = Copy(store, server);
            Assert(resumed.ResumedBytes > 0 && resumed.DownloadedBytes < resumed.TotalBytes, "Reanuda desde el byte conservado y reduce los datos descargados");
            SameContents(resumed.FinalPath, source);
            Assert(!Directory.GetFiles(server.Folder(store.Settings), "*.part", SearchOption.AllDirectories).Any(), "Caché limpiada después de una copia verificada");
            store.Settings.OnlyChanges = true; var unchanged = Copy(store, server);
            Assert(unchanged.DownloadedBytes == 0 && unchanged.ReusedBytes == unchanged.TotalBytes, "Solo novedades: cero bytes descargados si no hay cambios");
            Assert(ArchiveCatalog.Compare(ArchiveCatalog.Read(resumed.FinalPath), ArchiveCatalog.Read(unchanged.FinalPath)).Count == 0, "Comparador de copias SSH iguales sin falsos cambios");
            string changed = Path.Combine(source, "niño.txt"); File.WriteAllText(changed, "Contenido nuevo con más información"); File.SetLastWriteTimeUtc(changed, DateTime.UtcNow.AddSeconds(10));
            File.WriteAllText(Path.Combine(source, "añadido.txt"), "Nuevo archivo"); File.Delete(Path.Combine(source, "vacio.txt")); Directory.CreateDirectory(Path.Combine(source, "Nueva vacía"));
            var updated = Copy(store, server);
            long expected = new FileInfo(changed).Length + new FileInfo(Path.Combine(source, "añadido.txt")).Length;
            Assert(updated.DownloadedBytes == expected && updated.ReusedBytes > 512 * 1024, "Solo descarga los archivos nuevos y modificados");
            SameContents(updated.FinalPath, source);
            var differences = ArchiveCatalog.Compare(ArchiveCatalog.Read(unchanged.FinalPath), ArchiveCatalog.Read(updated.FinalPath));
            Assert(differences.Any(d => d.Change == "Modificado" && d.Relative == "niño.txt") && differences.Any(d => d.Change == "Eliminado" && d.Relative == "vacio.txt") &&
                differences.Any(d => d.Change == "Añadido" && d.Relative == "añadido.txt"), "Comparador detecta cambios reales tras copia incremental");
            Assert(ArchiveCatalog.Search(new[] { ArchiveCatalog.Read(resumed.FinalPath), ArchiveCatalog.Read(updated.FinalPath) }, "niño").Count == 2, "Buscador encuentra versiones históricas de un archivo Unicode");
            store.Settings.OnlyChanges = false; var full = Copy(store, server);
            Assert(full.ReusedBytes == 0 && full.DownloadedBytes == full.TotalBytes, "Desmarcar solo novedades vuelve a descargar todo");
            var changedServer = server.Clone(); changedServer.Id = Guid.NewGuid().ToString("N"); changedServer.Alias = "Origen_modificado";
            Interrupt(store, changedServer);
            string largePath = Path.Combine(source, "grande.bin"); big[0] ^= 255; File.WriteAllBytes(largePath, big); File.SetLastWriteTimeUtc(largePath, DateTime.UtcNow.AddSeconds(20));
            var changedResume = Copy(store, changedServer); SameContents(changedResume.FinalPath, source);
            Assert(changedResume.DownloadedBytes >= big.Length, "Si cambia el origen, el archivo parcial se descarta y se descarga otra vez");
            Assert(!Directory.GetFiles(destination, "*.partial", SearchOption.AllDirectories).Any(), "Nunca quedan ZIP incompletos publicados");
            store.Settings.BandwidthEnabled = true; store.Settings.BandwidthValue = 8; store.Settings.BandwidthUnit = "Mbps"; store.Settings.Parallel = 2;
            var other = server.Clone(); other.Alias = "Limitado_B"; other.Id = Guid.NewGuid().ToString("N");
            var first = server.Clone(); first.Alias = "Limitado_A"; first.Id = Guid.NewGuid().ToString("N");
            using (var runner = new BackupRunner(store, new[] { first, other }, (s, a, f) => true))
            {
                var clock = Stopwatch.StartNew(); runner.RunAsync().GetAwaiter().GetResult();
                Assert(runner.Jobs.All(j => j.Snapshot().State == JobState.Success), "Dos conexiones SSH reales funcionan con el limitador compartido: " + string.Join("; ", runner.Jobs.Select(j => j.Snapshot().Error)));
                Assert(clock.Elapsed.TotalSeconds >= runner.Jobs.Sum(j => j.Snapshot().DownloadedBytes) / 1000000.0 * .9,
                    "El límite global no se multiplica al descargar de dos servidores");
            }
        }
        static void Bandwidth()
        {
            Assert(BandwidthLimiter.Rate(8, "Mbps") == 1000000 && BandwidthLimiter.Rate(1, "MB/s") == 1000000,
                "Conversión correcta de megabits y megabytes por segundo");
            var limiter = new BandwidthLimiter(1000000); var clock = Stopwatch.StartNew();
            var workers = Enumerable.Range(0, 4).Select(async n => { for (int i = 0; i < 4; i++) await limiter.WaitAsync(62500, CancellationToken.None); }).ToArray();
            Task.WhenAll(workers).GetAwaiter().GetResult();
            Assert(clock.Elapsed.TotalSeconds >= .98 && clock.Elapsed.TotalSeconds < 3,
                "Cuatro consumidores comparten 1 MB/s total (" + clock.Elapsed.TotalSeconds.ToString("0.00") + " s para 1 MB)");
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(35); clock.Restart(); bool canceled = false;
                try { new BandwidthLimiter(1).WaitAsync(1000000, cts.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
                Assert(canceled && clock.ElapsedMilliseconds < 500, "La espera del limitador se cancela sin bloquear el cierre");
            }
            var settings = new Settings { BandwidthEnabled = true, BandwidthValue = 4, BandwidthUnit = "MB/s" };
            Assert(settings.ExecutionCopy().BandwidthEnabled && settings.ExecutionCopy().BandwidthValue == 4 && settings.ExecutionCopy().BandwidthUnit == "MB/s",
                "El límite se conserva también en la configuración de una copia programada");
            TransportLimit().GetAwaiter().GetResult();
        }
        static async Task ReadExactly(Stream input, int length)
        {
            var buffer = new byte[length]; int total = 0;
            while (total < length) { int read = await input.ReadAsync(buffer, total, length - total); if (read == 0) throw new EndOfStreamException(); total += read; }
        }
        static async Task<long> ProxyRead(int proxyPort, int targetPort)
        {
            using (var client = new System.Net.Sockets.TcpClient())
            {
                client.NoDelay = true; await client.ConnectAsync("127.0.0.1", proxyPort);
                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(new byte[] { 5, 1, 0 }, 0, 3); await ReadExactly(stream, 2);
                    var request = new byte[] { 5, 1, 0, 1, 127, 0, 0, 1, (byte)(targetPort / 256), (byte)(targetPort % 256) };
                    await stream.WriteAsync(request, 0, request.Length); await ReadExactly(stream, 10);
                    var buffer = new byte[32768]; long total = 0; int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) total += read;
                    return total;
                }
            }
        }
        static async Task TransportLimit()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var sending = Task.Run(async () => {
                var tasks = new List<Task>();
                for (int i = 0; i < 2; i++)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    tasks.Add(Task.Run(async () => { using (client) using (var stream = client.GetStream()) { byte[] data = new byte[1000000]; await stream.WriteAsync(data, 0, data.Length); } }));
                }
                await Task.WhenAll(tasks);
            });
            try
            {
                using (var proxy = new LimitedSshTransport(new[] { new ServerConfig { Host = "127.0.0.1", Port = port } }, 1000000))
                {
                    var watch = Stopwatch.StartNew(); var results = await Task.WhenAll(ProxyRead(proxy.Port, port), ProxyRead(proxy.Port, port));
                    await sending;
                    Assert(results.Sum() == 2000000 && proxy.DeliveredBytes == 2000000, "Transporte limitado conserva todos los bytes de dos conexiones reales");
                    Assert(watch.Elapsed.TotalSeconds >= 1.98 && watch.Elapsed.TotalSeconds < 6, "Dos conexiones TCP comparten 1 MB/s agregado (" + watch.Elapsed.TotalSeconds.ToString("0.00") + " s para 2 MB)");
                }
            }
            finally { listener.Stop(); }
        }
        static object Field(MainForm form, string name) { return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }
        static void Gui(string root)
        {
            Directory.CreateDirectory(root); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var store = new ConfigStore(Path.Combine(root, "config")); store.Settings.Destination = Path.Combine(root, "copias");
            var form = new MainForm(store); int step = 0;
            var timer = new System.Windows.Forms.Timer { Interval = 350 };
            Action<string> capture = name => { Application.DoEvents(); System.Threading.Thread.Sleep(80); Application.DoEvents(); using (var bitmap = new Bitmap(form.Width, form.Height)) { using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.Left, form.Top, 0, 0, bitmap.Size); bitmap.Save(Path.Combine(root, name + ".png")); } };
            timer.Tick += async (s, e) => {
                timer.Stop();
                try
                {
                    if (step == 0)
                    {
                        Assert(!((CheckBox)Field(form, "onlyChanges")).Checked, "Opción solo novedades visible y desmarcada");
                        ((CheckBox)Field(form, "onlyChanges")).Checked = true;
                        Assert(new ConfigStore(store.DirectoryPath).Settings.OnlyChanges, "Cambio de solo novedades guardado desde la interfaz");
                        typeof(MainForm).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { "tools" });
                        capture("herramientas-claro");
                    }
                    else if (step == 1)
                    {
                        ((CheckBox)Field(form, "darkMode")).Checked = true;
                        form.Size = form.MinimumSize; capture("herramientas-oscuro-minimo");
                        var pages = (Dictionary<string, Panel>)Field(form, "pages"); var tabs = pages["tools"].Controls.OfType<ModernTabs>().Single(); tabs.SelectedIndex = 1;
                    }
                    else if (step == 2)
                    {
                        Assert(!((CheckBox)Field(form, "scheduleEnabled")).Checked, "Programación visible y desactivada");
                        var days = (CheckBox[])Field(form, "scheduleDays");
                        Assert(days.Length == 7 && days.Select(c => c.Text).SequenceEqual(Scheduling.DayLabels), "Una casilla independiente por cada día de la semana");
                        ((Button)Field(form, "scheduleNoDays")).PerformClick();
                        Assert(days.All(c => !c.Checked), "Ninguno desmarca los siete días");
                        ((Button)Field(form, "scheduleAllDays")).PerformClick();
                        Assert(days.All(c => c.Checked), "Todos los días marca las siete casillas");
                        ((Button)Field(form, "scheduleNoDays")).PerformClick(); days[0].Checked = true; days[2].Checked = true;
                        ((TextBox)Field(form, "scheduleTime")).Text = "20:00";
                        Assert(((Label)Field(form, "scheduleSummary")).Text.Contains("lunes y miércoles") && days.Count(c => c.Checked) == 2,
                            "Selección libre de lunes y miércoles con resumen visible");
                        capture("programacion-oscuro");
                        ((CheckBox)Field(form, "darkMode")).Checked = false; capture("programacion-claro");
                        ((CheckBox)Field(form, "darkMode")).Checked = true;
                        var pages = (Dictionary<string, Panel>)Field(form, "pages"); pages["tools"].Controls.OfType<ModernTabs>().Single().SelectedIndex = 2;
                    }
                    else if (step == 3) { capture("actualizador-oscuro"); Assert(!((Button)Field(form, "installUpdate")).Enabled, "Actualizar deshabilitado sin un paquete nuevo válido"); }
                    else if (step == 4)
                    {
                        typeof(MainForm).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { "settings" });
                        Assert(!((CheckBox)Field(form, "settingBandwidthEnabled")).Checked, "Limitador opcional desactivado en la interfaz inicial");
                        ((CheckBox)Field(form, "settingBandwidthEnabled")).Checked = true;
                        ((NumericUpDown)Field(form, "settingBandwidthValue")).Value = 8;
                        ((ComboBox)Field(form, "settingBandwidthUnit")).SelectedItem = "Mbps";
                        typeof(MainForm).GetMethod("SaveSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                        var loaded = new ConfigStore(store.DirectoryPath);
                        Assert(loaded.Settings.BandwidthEnabled && loaded.Settings.BandwidthValue == 8, "Interfaz guarda el máximo global elegido");
                        capture("limitador-oscuro");

                    }
                    else if (step == 5)
                    {
                        await (Task)typeof(MainForm).GetMethod("LoadCatalogs", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                        Assert(((Label)Field(form, "catalogStatus")).Text.Contains("0 copias"), "Catálogo vacío se maneja sin errores");
                    }
                    else if (step == 6)
                    {
                        var navigation = (Dictionary<string, Button>)Field(form, "navigation");
                        foreach (string key in new[] { "copies", "servers", "history", "settings" })
                        {
                            navigation[key].PerformClick(); Application.DoEvents();
                            var page = ((Dictionary<string, Panel>)Field(form, "pages"))[key];
                            Assert(page.Visible && page.Top == 0 && page.Left == 0, "Navegación lateral abre " + key + " en tamaño mínimo");
                            capture(key + "-oscuro-minimo");
                        }
                        var settingsPage = ((Dictionary<string, Panel>)Field(form, "pages"))["settings"];
                        var scroll = settingsPage.Controls.OfType<Panel>().First(p => p.AutoScroll);
                        Assert(scroll.VerticalScroll.Visible, "Los ajustes extensos permiten desplazarse en la ventana mínima");
                        scroll.AutoScrollPosition = new Point(0, 2000); capture("ajustes-final-minimo");
                        form.Size = new Size(1420, 960); navigation["tools"].PerformClick();
                        var tabs = ((Dictionary<string, Panel>)Field(form, "pages"))["tools"].Controls.OfType<ModernTabs>().Single();
                        tabs.SelectedIndex = 1; capture("programacion-oscuro-amplio");
                        var days = (CheckBox[])Field(form, "scheduleDays");
                        Assert(days.All(c => c.Width >= 100 && c.Height >= 44), "Las siete casillas tienen un área de pulsación legible");
                    }
                    else if (step == 7)
                    {
                        var navigation = (Dictionary<string, Button>)Field(form, "navigation");
                        foreach (string key in new[] { "copies", "servers", "history", "settings" })
                        { navigation[key].PerformClick(); capture(key + "-oscuro-amplio"); }
                        ((CheckBox)Field(form, "darkMode")).Checked = false;
                        navigation["tools"].PerformClick(); capture("programacion-claro-amplio");
                        form.Close();
                    }
                    step++; if (step < 8) timer.Start();
                }
                catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
            };
            form.Shown += (s, e) => timer.Start(); Application.Run(form); timer.Dispose();
            Assert(step == 8, "Interfaz de herramientas y limitador comprobada en claro, oscuro y tamaño mínimo");
        }
    }
}
