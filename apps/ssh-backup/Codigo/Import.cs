using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace SSHBackup
{
    public sealed class ImportedRow
    {
        public int RowNumber;
        public ServerConfig Server;
        public bool HasEnabled, HasAuth;
        public List<string> ParseErrors = new List<string>();
        public List<string> Errors = new List<string>();
    }

    public static class ServerImporter
    {
        static readonly string[] Headers = { "alias", "host", "puerto", "usuario", "contrasena", "carpeta_origen", "carpeta_destino" };
        public static List<ImportedRow> Read(string path)
        {
            if (new FileInfo(path).Length > 32L * 1024 * 1024) throw new InvalidDataException("El archivo de importación supera 32 MB.");
            List<Tuple<int, List<string>>> records;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".xlsx") records = ReadExcel(path);
            else if (extension == ".csv" || extension == ".tsv" || extension == ".txt")
            {
                string text = Decode(File.ReadAllBytes(path));
                records = ParseCsv(text, GuessSeparator(text));
            }
            else throw new InvalidDataException("Selecciona un archivo CSV o Excel .xlsx. Para un Excel antiguo, guárdalo primero como .xlsx.");
            records = records.Where(r => r.Item2.Any(v => !string.IsNullOrWhiteSpace(v))).ToList();
            if (records.Count < 2) throw new InvalidDataException("El archivo debe incluir una fila de encabezados y al menos un servidor.");
            if (records.Count > 10001) throw new InvalidDataException("El archivo contiene más de 10.000 servidores.");
            var keys = records[0].Item2.Select(NormalizeHeader).ToList();
            if (keys.Where(k => k.Length > 0).GroupBy(k => k).Any(g => g.Count() > 1))
                throw new InvalidDataException("Hay encabezados repetidos.");
            foreach (string required in new[] { "alias", "host", "usuario", "carpeta_origen" })
                if (!keys.Contains(required)) throw new InvalidDataException("Falta la columna " + required + ".");
            var result = new List<ImportedRow>();
            foreach (var record in records.Skip(1))
            {
                var cells = record.Item2;
                Func<string, string> raw = key => {
                    int index = keys.IndexOf(key);
                    return index >= 0 && index < cells.Count ? cells[index] ?? "" : "";
                };
                bool protectedCsv = raw("formato_exportacion") == ServerExporter.CsvFormat;
                Func<string, string> get = key => {
                    string value = raw(key);
                    return protectedCsv && value.StartsWith("'", StringComparison.Ordinal) ? value.Substring(1) : value;
                };
                var server = new ServerConfig {
                    Alias = get("alias").Trim(), Host = get("host").Trim().Trim('[', ']'), User = get("usuario").Trim(),
                    Source = get("carpeta_origen").Trim(), Destination = get("carpeta_destino").Trim(),
                    Password = get("contrasena"), KeyFile = get("clave_ssh").Trim(), Fingerprint = get("huella_ssh").Trim()
                };
                if (!string.IsNullOrWhiteSpace(server.KeyFile)) server.Auth = "key";
                string auth = get("tipo_acceso").Trim().ToLowerInvariant(), enabled = get("copiar").Trim().ToLowerInvariant();
                var extraErrors = new List<string>();
                if (auth == "password" || auth == "contrasena" || auth == "contraseña") server.Auth = "password";
                else if (auth == "key" || auth == "clave" || auth == "clave_ssh") server.Auth = "key";
                else if (auth.Length > 0) extraErrors.Add("tipo_acceso debe ser password o key.");
                if (enabled == "1" || enabled == "true" || enabled == "si" || enabled == "sí") server.Enabled = true;
                else if (enabled == "0" || enabled == "false" || enabled == "no") server.Enabled = false;
                else if (enabled.Length > 0) extraErrors.Add("copiar debe ser 1 o 0.");
                decimal port;
                string portText = get("puerto").Trim();
                if (portText.Length > 0 && decimal.TryParse(portText, NumberStyles.Number, CultureInfo.InvariantCulture, out port) &&
                    decimal.Truncate(port) == port && port >= 1 && port <= 65535) server.Port = (int)port;
                else if (portText.Length > 0) server.Port = 0;
                var row = new ImportedRow { RowNumber = record.Item1, Server = server,
                    HasEnabled = enabled.Length > 0, HasAuth = auth.Length > 0 };
                row.Errors.AddRange(Validation.ServerErrors(server));
                row.ParseErrors.AddRange(extraErrors);
                row.Errors.AddRange(extraErrors);
                result.Add(row);
            }
            return result;
        }
        public static void ValidateRows(List<ImportedRow> rows, List<ServerConfig> existing, bool update)
        {
            var repeated = rows.GroupBy(r => Validation.AliasKey(r.Server.Alias)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSetCompat();
            var aliases = existing.Select(s => Validation.AliasKey(s.Alias)).ToHashSetCompat();
            foreach (var row in rows)
            {
                row.Errors = Validation.ServerErrors(row.Server);
                row.Errors.AddRange(row.ParseErrors);
                string key = Validation.AliasKey(row.Server.Alias);
                if (repeated.Contains(key)) row.Errors.Add("El alias se repite dentro del archivo.");
                if (!update && aliases.Contains(key)) row.Errors.Add("Este alias ya existe. Activa «Actualizar alias existentes» para modificarlo.");
            }
        }
        public static List<ServerConfig> Apply(List<ImportedRow> rows, List<ServerConfig> existing, bool update)
        {
            ValidateRows(rows, existing, update);
            if (rows.Any(r => r.Errors.Count > 0)) throw new InvalidDataException("Corrige los errores de importación antes de continuar.");
            var next = existing.Select(s => s.Clone()).ToList();
            foreach (var row in rows)
            {
                var server = row.Server.Clone();
                var old = next.FirstOrDefault(s => Validation.AliasKey(s.Alias) == Validation.AliasKey(server.Alias));
                if (old != null)
                {
                    server.Id = old.Id;
                    if (!row.HasEnabled) server.Enabled = old.Enabled;
                    if (string.IsNullOrEmpty(server.Password) && (!row.HasAuth || server.Auth == old.Auth))
                    { server.Password = old.Password; server.PasswordLoaded = old.PasswordLoaded; server.EncryptedPassword = old.EncryptedPassword; }
                    if (!row.HasAuth && string.IsNullOrEmpty(server.KeyFile) && old.Auth == "key")
                    { server.Auth = old.Auth; server.KeyFile = old.KeyFile; }
                    if (server.AddressKey == old.AddressKey && string.IsNullOrEmpty(server.Fingerprint)) server.Fingerprint = old.Fingerprint;
                    next[next.IndexOf(old)] = server;
                }
                else next.Add(server);
            }
            return next;
        }
        static string NormalizeHeader(string value)
        {
            string normalized = (value ?? "").Trim().TrimStart('\uFEFF').ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var b = new StringBuilder();
            foreach (char c in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) b.Append(c);
            switch (b.ToString())
            {
                case "alias": case "nombre": return "alias";
                case "host": case "servidor": case "ip": case "direccion": return "host";
                case "puerto": case "port": return "puerto";
                case "usuario": case "user": case "username": return "usuario";
                case "contrasena": case "password": case "clave": return "contrasena";
                case "carpeta": case "carpetaorigen": case "origen": case "carpetaremota": case "source": return "carpeta_origen";
                case "carpetadestino": case "destino": case "destination": return "carpeta_destino";
                case "clavessh": case "claveprivada": case "keyfile": return "clave_ssh";
                case "huellassh": case "huella": case "fingerprint": return "huella_ssh";
                case "tipoacceso": case "autenticacion": case "auth": return "tipo_acceso";
                case "copiar": case "activo": case "enabled": return "copiar";
                case "formatoexportacion": return "formato_exportacion";
                default: return b.ToString();
            }
        }
        static string Decode(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xff && data[1] == 0xfe) return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            if (data.Length >= 2 && data[0] == 0xfe && data[1] == 0xff) return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(data); }
        }
        public static char GuessSeparator(string text)
        {
            var counts = new Dictionary<char, int> { { ';', 0 }, { ',', 0 }, { '\t', 0 } };
            bool quoted = false;
            foreach (char c in text)
            {
                if (c == '"') quoted = !quoted;
                if (!quoted && (c == '\r' || c == '\n')) break;
                if (!quoted && counts.ContainsKey(c)) counts[c]++;
            }
            return counts.OrderByDescending(x => x.Value).First().Key;
        }
        public static List<Tuple<int, List<string>>> ParseCsv(string text, char separator)
        {
            var records = new List<Tuple<int, List<string>>>();
            var row = new List<string>(); var field = new StringBuilder();
            bool quoted = false, closed = false; int line = 1, rowLine = 1;
            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                if (index == 0 && c == '\uFEFF') continue;
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index++; }
                        else { quoted = false; closed = true; }
                    }
                    else { field.Append(c); if (c == '\n') line++; }
                    continue;
                }
                if (c == '"' && field.Length == 0 && !closed) { quoted = true; continue; }
                if (c == separator)
                { row.Add(field.ToString()); field.Clear(); closed = false; continue; }
                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    row.Add(field.ToString()); records.Add(Tuple.Create(rowLine, row));
                    row = new List<string>(); field.Clear(); closed = false; line++; rowLine = line;
                    continue;
                }
                if (closed && c != ' ' && c != '\t') throw new InvalidDataException("CSV incorrecto cerca de la línea " + line + ".");
                if (!closed) field.Append(c);
            }
            if (quoted) throw new InvalidDataException("Hay un campo CSV con comillas sin cerrar, cerca de la línea " + rowLine + ".");
            if (field.Length > 0 || row.Count > 0 || closed)
            { row.Add(field.ToString()); records.Add(Tuple.Create(rowLine, row)); }
            return records;
        }
        static XmlDocument ReadXml(ZipArchive archive, string entryPath)
        {
            var entry = archive.GetEntry(entryPath);
            if (entry == null) throw new InvalidDataException("Excel incompleto: falta " + entryPath + ".");
            if (entry.Length > 64L * 1024 * 1024) throw new InvalidDataException("La hoja de Excel es demasiado grande para importarla.");
            var document = new XmlDocument { XmlResolver = null };
            using (var stream = entry.Open())
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64L * 1024 * 1024 }))
                document.Load(reader);
            return document;
        }
        static List<Tuple<int, List<string>>> ReadExcel(string path)
        {
            var result = new List<Tuple<int, List<string>>>();
            using (var stream = File.OpenRead(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var workbook = ReadXml(zip, "xl/workbook.xml");
                var sheets = workbook.SelectNodes("//*[local-name()='sheet']").Cast<XmlElement>().ToList();
                var selected = sheets.FirstOrDefault(s => string.Equals(s.GetAttribute("name"), "Servidores", StringComparison.OrdinalIgnoreCase))
                    ?? sheets.FirstOrDefault(s => s.GetAttribute("state") != "hidden" && s.GetAttribute("state") != "veryHidden");
                if (selected == null) throw new InvalidDataException("El Excel no contiene ninguna hoja visible.");
                string relationId = selected.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                var relations = ReadXml(zip, "xl/_rels/workbook.xml.rels");
                var relation = relations.SelectNodes("//*[local-name()='Relationship']").Cast<XmlElement>().FirstOrDefault(e => e.GetAttribute("Id") == relationId);
                if (relation == null || relation.GetAttribute("TargetMode") == "External") throw new InvalidDataException("La hoja de Excel no es válida.");
                string target = relation.GetAttribute("Target").Replace('\\', '/');
                if (target.StartsWith("/")) target = target.TrimStart('/');
                else target = "xl/" + target;
                var parts = new List<string>();
                foreach (string part in target.Split('/'))
                {
                    if (part == "..") { if (parts.Count == 0) throw new InvalidDataException("Ruta de hoja inválida."); parts.RemoveAt(parts.Count - 1); }
                    else if (part != "." && part.Length > 0) parts.Add(part);
                }
                target = string.Join("/", parts);
                var strings = new List<string>();
                if (zip.GetEntry("xl/sharedStrings.xml") != null)
                {
                    var shared = ReadXml(zip, "xl/sharedStrings.xml");
                    foreach (XmlNode value in shared.SelectNodes("//*[local-name()='si']"))
                        strings.Add(string.Concat(value.SelectNodes(".//*[local-name()='t']").Cast<XmlNode>().Select(n => n.InnerText)));
                }
                var sheet = ReadXml(zip, target);
                foreach (XmlElement row in sheet.SelectNodes("//*[local-name()='sheetData']/*[local-name()='row']"))
                {
                    int number; if (!int.TryParse(row.GetAttribute("r"), out number)) number = result.Count + 1;
                    var values = new List<string>();
                    foreach (XmlElement cell in row.SelectNodes("*[local-name()='c']"))
                    {
                        string address = cell.GetAttribute("r"); int column = 0;
                        foreach (char c in address) { if (!char.IsLetter(c)) break; column = column * 26 + char.ToUpperInvariant(c) - 'A' + 1; }
                        if (column == 0) column = values.Count + 1;
                        if (column > 256) continue;
                        if (cell.SelectSingleNode("*[local-name()='f']") != null)
                            throw new InvalidDataException("La fila " + number + " contiene una fórmula. Pega sus valores antes de importar.");
                        var raw = cell.SelectSingleNode("*[local-name()='v']");
                        string value = raw == null ? "" : raw.InnerText;
                        if (cell.GetAttribute("t") == "s")
                        {
                            int index;
                            if (!int.TryParse(value, out index) || index < 0 || index >= strings.Count) throw new InvalidDataException("Texto de Excel no válido en " + address + ".");
                            value = ServerExporter.DecodeExcelText(strings[index]);
                        }
                        else if (cell.GetAttribute("t") == "inlineStr")
                            value = ServerExporter.DecodeExcelText(string.Concat(cell.SelectNodes(".//*[local-name()='t']").Cast<XmlNode>().Select(n => n.InnerText)));
                        else if (cell.GetAttribute("t") == "str") value = ServerExporter.DecodeExcelText(value);
                        else if (cell.GetAttribute("t") == "e") throw new InvalidDataException("La celda " + address + " contiene un error de Excel.");
                        while (values.Count < column) values.Add("");
                        values[column - 1] = value;
                    }
                    result.Add(Tuple.Create(number, values));
                }
            }
            return result;
        }
        public static string CsvCell(string text)
        {
            text = text ?? "";
            if (text.Length > 0 && "=+-@\t\r".IndexOf(text[0]) >= 0) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        public static void WriteTemplateCsv(string path)
        {
            var lines = new[] { string.Join(";", Headers), "Sede_Madrid;192.0.2.11;22;backup;;/srv/datos;D:\\Copias_SSH" };
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
        }
        static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> values) { return new HashSet<T>(values); }
    }
}
