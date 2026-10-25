using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace SSHBackup
{
    internal static class RemotePaths
    {
        public static string Input(string path)
        {
            path = (path ?? "").Trim();
            if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"' &&
                Regex.IsMatch(path.Substring(1, path.Length - 2), @"^/?[A-Za-z]:[\\/]"))
                path = path.Substring(1, path.Length - 2);
            return Normalize(path);
        }
        public static string Normalize(string path)
        {
            // SFTP absolute paths use '/', including Windows drive letters.
            // Preserve spaces and literal backslashes in Unix names.
            if (Regex.IsMatch(path ?? "", @"^/?[A-Za-z]:[\\/]"))
            {
                path = path.Replace('\\', '/');
                if (path[0] != '/') path = "/" + path;
            }
            return path;
        }
        public static string Child(string directory, string name)
        { return Normalize(directory).TrimEnd('/') + "/" + name; }
    }

    internal sealed class RemoteSourceException : IOException
    {
        public RemoteSourceException(string operation, string path, Exception inner)
            : base((inner is SftpPermissionDeniedException ? "El servidor deniega el permiso de lectura." :
                "No se encuentra la carpeta o el archivo solicitado en el servidor.") +
                " Operación: " + operation + ". Ruta SFTP: " + path +
                ". Revisa la ruta y el acceso del usuario SSH.", inner) { }
    }

    // Deliberately exposes only remote reads. No upload/delete/rename/shell operations.
    internal sealed class RemoteFolder
    {
        readonly SftpClient client;
        public string Root { get; private set; }
        public string Requested { get; private set; }
        public RemoteFolder(SftpClient connection, string source)
        {
            client = connection; Requested = RemotePaths.Input(source);
            var attributes = Attributes(Requested);
            if (!attributes.IsDirectory)
                throw new InvalidDataException("El origen debe ser una carpeta, no un archivo. Ruta SFTP: " + Requested);
            Read(Requested, "Abrir carpeta de origen", path => { client.ChangeDirectory(path); return true; });
            Root = RemotePaths.Normalize(client.WorkingDirectory);
            if (string.IsNullOrEmpty(Root) || !Root.StartsWith("/", StringComparison.Ordinal))
                throw new InvalidDataException("El servidor no devolvió una ruta SFTP absoluta válida: " + Root);
        }
        T Read<T>(string path, string operation, Func<string, T> action)
        {
            path = RemotePaths.Normalize(path);
            try { return action(path); }
            catch (SftpPathNotFoundException error) { throw new RemoteSourceException(operation, path, error); }
            catch (SftpPermissionDeniedException error) { throw new RemoteSourceException(operation, path, error); }
        }
        public SftpFileAttributes Attributes(string path)
        { return Read(path, "Consultar archivo o carpeta", p => client.GetAttributes(p)); }
        public List<ISftpFile> List(string path)
        { return Read(path, "Listar carpeta", p => client.ListDirectory(p).ToList()); }
        public Stream OpenRead(string path)
        { return Read<Stream>(path, "Abrir archivo para descargar", p => client.OpenRead(p)); }
        public int Read(Stream stream, byte[] buffer, string path)
        { return Read(path, "Descargar archivo", p => stream.Read(buffer, 0, buffer.Length)); }
    }
}
