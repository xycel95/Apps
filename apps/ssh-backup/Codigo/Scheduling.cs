using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace SSHBackup
{
    public static class Scheduling
    {
        public static readonly string[] DayLabels = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        static readonly string[] TaskDayTags = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public static int SelectedDays(Settings settings)
        {
            int mask = settings.ScheduleDaysMask == -1 ? (settings.ScheduleWeekdaysOnly ? 31 : 127) : settings.ScheduleDaysMask;
            if (mask < 0 || mask > 127) throw new ArgumentException("La selección de días de la semana no es válida.");
            return mask;
        }
        public static string DaysText(int mask)
        {
            if (mask == 127) return "todos los días";
            var names = Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => DayLabels[i].ToLowerInvariant()).ToArray();
            if (names.Length == 0) return "ningún día seleccionado";
            if (names.Length == 1) return names[0];
            return string.Join(", ", names.Take(names.Length - 1)) + " y " + names[names.Length - 1];
        }
        public static string UserKey
        {
            get { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Environment.UserName))).Replace("-", "").Substring(0, 16); }
        }
        public static string TaskName { get { return "SSHBackup-" + UserKey; } }
        public static string SignalName { get { return "Local\\SSHBackupSchedule-" + UserKey; } }
        public static TimeSpan Time(string text)
        {
            TimeSpan result;
            if (!TimeSpan.TryParseExact(text ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out result) || result.TotalHours >= 24)
                throw new ArgumentException("La hora debe tener formato HH:mm, por ejemplo 20:00.");
            return result;
        }
        public static bool Due(Settings settings, DateTime now)
        {
            if (!settings.ScheduleEnabled) return false;
            int day = ((int)now.DayOfWeek + 6) % 7;
            if ((SelectedDays(settings) & (1 << day)) == 0) return false;
            return now.TimeOfDay >= Time(settings.ScheduleTime) && settings.LastScheduleDate != now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        public static string BuildTaskXml(Settings settings, string executable, string userId, DateTime startDate)
        {
            Time(settings.ScheduleTime);
            Func<string, string> esc = s => SecurityElement.Escape(s ?? "");
            int mask = SelectedDays(settings);
            if (mask == 0) throw new ArgumentException("Selecciona al menos un día de la semana para programar las copias.");
            string days = string.Concat(Enumerable.Range(0, 7).Where(i => (mask & (1 << i)) != 0).Select(i => "<" + TaskDayTags[i] + "/>"));
            string calendar = "<ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek>" + days + "</DaysOfWeek></ScheduleByWeek>";
            return "<?xml version=\"1.0\" encoding=\"utf-16\"?><Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>Copia SSH Backup de los servidores marcados. Solo lectura del origen.</Description></RegistrationInfo>" +
                "<Triggers><CalendarTrigger><StartBoundary>" + startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T" + settings.ScheduleTime + ":00</StartBoundary><Enabled>true</Enabled>" + calendar + "</CalendarTrigger></Triggers>" +
                "<Principals><Principal id=\"Author\"><UserId>" + esc(userId) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<StartWhenAvailable>true</StartWhenAvailable><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Enabled>true</Enabled></Settings>" +
                "<Actions Context=\"Author\"><Exec><Command>" + esc(executable) + "</Command><Arguments>--scheduled</Arguments><WorkingDirectory>" +
                esc(Path.GetDirectoryName(executable)) + "</WorkingDirectory></Exec></Actions></Task>";
        }
        static void Run(string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
                Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { try { process.Kill(); } catch { } throw new IOException("Windows no ha respondido al guardar la programación."); }
                if (process.ExitCode != 0) throw new IOException("Windows no ha podido guardar la programación. " + output.Result + " " + errors.Result);
            }
        }
        public static void Apply(Settings settings, string dataDirectory, bool previouslyEnabled)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                if (settings.ScheduleEnabled) throw new PlatformNotSupportedException("La programación utiliza el Programador de tareas de Windows.");
                return;
            }
            if (!settings.ScheduleEnabled)
            {
                if (previouslyEnabled) Run("/Delete /TN \"" + TaskName + "\" /F");
                settings.ScheduledExecutable = ""; return;
            }
            string executable = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string xmlPath = Path.Combine(dataDirectory, "programacion-windows.xml");
            string userId = WindowsIdentity.GetCurrent().User.Value;
            File.WriteAllText(xmlPath, BuildTaskXml(settings, executable, userId, DateTime.Today), Encoding.Unicode);
            Run("/Create /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\" /F");
            settings.ScheduledExecutable = executable;
        }
    }
}
