using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace CorreoDiario;

public static class MessageFolders
{
    public static string Name(DateTime date, string sender, string subject)
    {
        if (!MailAddress.TryCreate(sender, out var address) || !address.Address.Contains('@'))
            throw new InvalidDataException("Falta la dirección de correo del remitente.");
        string prefix = date.ToString("yyyy-MM-dd_HH.mm", CultureInfo.InvariantCulture) + "_" + MailReader.SafeName(address.Address, 110) + "_";
        return prefix + MailReader.SafeName(subject, Math.Min(110, 220-prefix.Length));
    }

    public static string Name(Mail mail) => Name(mail.Madrid?.DateTime ??
        throw new InvalidDataException("Falta la fecha y hora del mensaje."),mail.From,mail.Subject);

    public static string StateDirectory(string destination)
    {
        string path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if(OperatingSystem.IsWindows()) path=path.ToUpperInvariant();
        string key=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"XyceL","CorreoDiarioEdge","Archivo",key);
    }

    public static string CreateStage(string destination)
    {
        string full=Directory.CreateDirectory(destination).FullName;
        string systemTemp=Path.GetTempPath();
        if(string.Equals(Path.GetPathRoot(systemTemp),Path.GetPathRoot(full),StringComparison.OrdinalIgnoreCase))
            return Directory.CreateTempSubdirectory("CorreoDiario-").FullName;
        // Reservar en el volumen de destino para poder publicar la carpeta terminada de una vez.
        string leaf=".CorreoDiario-"+Guid.NewGuid().ToString("N");
        var parent=Directory.GetParent(full);
        if(parent!=null)
        {
            try { return Directory.CreateDirectory(Path.Combine(parent.FullName,leaf)).FullName; }
            catch(UnauthorizedAccessException) { }
            catch(IOException) { }
        }
        var stage=Directory.CreateDirectory(Path.Combine(full,leaf));
        if(OperatingSystem.IsWindows()) stage.Attributes |= FileAttributes.Hidden;
        return stage.FullName;
    }

    public static string Publish(string stage,string destination,string stem,CancellationToken ct,Action<string>? beforeMove=null)
    {
        var directory=new DirectoryInfo(stage);
        if(OperatingSystem.IsWindows() && directory.Attributes.HasFlag(FileAttributes.Hidden))
            directory.Attributes &= ~FileAttributes.Hidden;
        for(int n=1;;n++)
        {
            ct.ThrowIfCancellationRequested();
            string name=n==1?stem:stem+" ("+n+")";
            string target=Path.Combine(destination,name);
            if(Directory.Exists(target) || File.Exists(target)) continue;
            beforeMove?.Invoke(name);
            try { Directory.Move(stage,target); return name; }
            catch(IOException) when(Directory.Exists(target) || File.Exists(target)) { }
        }
    }
}
