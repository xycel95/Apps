using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace SSHBackup
{
    public static class ServerExporter
    {
        internal const string CsvFormat = "SSHBackupCSV1";
        static readonly string[] Headers = { "alias", "host", "puerto", "usuario", "contrasena",
            "carpeta_origen", "carpeta_destino", "tipo_acceso", "clave_ssh", "huella_ssh", "copiar" };

        public static int Write(string path, IEnumerable<ServerConfig> servers, Settings settings,
            Func<ServerConfig, string> fingerprint, bool includePasswords)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv") throw new InvalidDataException("Elige Excel .xlsx o CSV .csv.");
            var source = servers.Select(s => s.Clone()).ToList();
            if (source.Count == 0) throw new InvalidOperationException("No hay servidores para exportar.");
            if (source.Count > 10000) throw new InvalidOperationException("El listado supera los 10.000 servidores admitidos por la importación.");
            if (includePasswords && source.Any(s => !s.PasswordLoaded))
                throw new InvalidOperationException("Hay contraseñas que no se pueden descifrar. Revisa el acceso de esos servidores o exporta sin contraseñas.");
            var rows = source.Select(s => new[] {
                s.Alias, s.Host, s.Port.ToString(CultureInfo.InvariantCulture), s.User,
                includePasswords ? s.Password : "", s.Source, s.BaseDestination(settings),
                s.Auth == "key" ? "key" : "password", s.KeyFile, fingerprint == null ? s.Fingerprint : fingerprint(s),
                s.Enabled ? "1" : "0"
            }).ToList();
            string full = Path.GetFullPath(path);
            string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool owned = false;
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    owned = true;
                    if (extension == ".csv") WriteCsv(output, rows);
                    else WriteExcel(output, rows);
                }
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
                return source.Count;
            }
            finally { if (owned) File.Delete(temporary); }
        }
        static string ProtectCsv(string text)
        {
            text = text ?? "";
            string trimmed = text.TrimStart();
            if (text.StartsWith("'", StringComparison.Ordinal) ||
                (text.Length > 0 && char.IsControl(text[0])) ||
                (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0))
                return "'" + text;
            return text;
        }
        static string Quote(string text) { return "\"" + (text ?? "").Replace("\"", "\"\"") + "\""; }
        static void WriteCsv(Stream output, List<string[]> rows)
        {
            using (var writer = new StreamWriter(output, new UTF8Encoding(true, true), 4096, true))
            {
                writer.NewLine = "\r\n";
                writer.WriteLine(string.Join(";", Headers.Concat(new[] { "formato_exportacion" })));
                foreach (var row in rows)
                    writer.WriteLine(string.Join(";", row.Select(v => Quote(ProtectCsv(v))).Concat(new[] { Quote(CsvFormat) })));
            }
        }
        static XmlDocument ReadXml(ZipArchiveEntry entry)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var input = entry.Open())
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                doc.Load(reader);
            return doc;
        }
        static string ExcelText(string value)
        {
            value = value ?? "";
            if (value.Length > 32767) throw new InvalidDataException("Un dato supera los 32.767 caracteres que admite una celda de Excel.");
            value = Regex.Replace(value, @"_x[0-9A-Fa-f]{4}_", m => "_x005F_" + m.Value.Substring(1));
            var result = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                { result.Append(c); result.Append(value[++i]); }
                else if (XmlConvert.IsXmlChar(c)) result.Append(c);
                else result.Append("_x" + ((int)c).ToString("X4", CultureInfo.InvariantCulture) + "_");
            }
            return result.ToString();
        }
        internal static string DecodeExcelText(string text)
        {
            return Regex.Replace(text ?? "", @"_x([0-9A-Fa-f]{4})_",
                m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
        }
        static XmlDocument FillSheet(ZipArchiveEntry entry, List<string[]> rows)
        {
            var doc = ReadXml(entry);
            var data = (XmlElement)doc.SelectSingleNode("//*[local-name()='sheetData']");
            var header = data.SelectSingleNode("*[local-name()='row' and @r='1']");
            var model = data.SelectSingleNode("*[local-name()='row' and @r='2']");
            if (header == null || model == null) throw new InvalidDataException("La plantilla de exportación no es válida.");
            var savedHeader = header.CloneNode(true);
            var styles = new Dictionary<string, string>();
            foreach (XmlElement cell in model.SelectNodes("*[local-name()='c']"))
                styles[Regex.Replace(cell.GetAttribute("r"), "[0-9]", "")] = cell.GetAttribute("s");
            data.RemoveAll(); data.AppendChild(savedHeader);
            string ns = doc.DocumentElement.NamespaceURI;
            for (int index = 0; index < rows.Count; index++)
            {
                int number = index + 2;
                var row = doc.CreateElement("row", ns); row.SetAttribute("r", number.ToString(CultureInfo.InvariantCulture));
                row.SetAttribute("ht", "27"); row.SetAttribute("customHeight", "1");
                for (int column = 0; column < Headers.Length; column++)
                {
                    string letter = ((char)('A' + column)).ToString();
                    var cell = doc.CreateElement("c", ns); cell.SetAttribute("r", letter + number);
                    string style;
                    if (styles.TryGetValue(letter, out style) && style.Length > 0) cell.SetAttribute("s", style);
                    if (column == 2 || column == 10)
                    {
                        var value = doc.CreateElement("v", ns); value.InnerText = rows[index][column]; cell.AppendChild(value);
                    }
                    else
                    {
                        cell.SetAttribute("t", "inlineStr");
                        var inline = doc.CreateElement("is", ns); var text = doc.CreateElement("t", ns);
                        text.SetAttribute("space", "http://www.w3.org/XML/1998/namespace", "preserve");
                        text.InnerText = ExcelText(rows[index][column]); inline.AppendChild(text); cell.AppendChild(inline);
                    }
                    row.AppendChild(cell);
                }
                data.AppendChild(row);
            }
            string reference = "A1:K" + (rows.Count + 1);
            foreach (XmlElement dimension in doc.SelectNodes("//*[local-name()='dimension' or local-name()='autoFilter']"))
                dimension.SetAttribute("ref", reference);
            return doc;
        }
        static void WriteExcel(Stream output, List<string[]> rows)
        {
            using (var source = new MemoryStream(Program.ResourceBytes("Plantilla_Exportacion_SSH.xlsx")))
            using (var template = new ZipArchive(source, ZipArchiveMode.Read))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (var original in template.Entries)
                {
                    XmlDocument xml = null;
                    if (original.FullName == "xl/worksheets/sheet1.xml") xml = FillSheet(original, rows);
                    else if (original.FullName.StartsWith("xl/tables/", StringComparison.Ordinal) && original.FullName.EndsWith(".xml", StringComparison.Ordinal))
                    {
                        xml = ReadXml(original); string reference = "A1:K" + (rows.Count + 1);
                        xml.DocumentElement.SetAttribute("ref", reference);
                        foreach (XmlElement filter in xml.SelectNodes("//*[local-name()='autoFilter']")) filter.SetAttribute("ref", reference);
                    }
                    using (var target = archive.CreateEntry(original.FullName, CompressionLevel.Optimal).Open())
                    {
                        if (xml == null) { using (var input = original.Open()) input.CopyTo(target); }
                        else using (var writer = XmlWriter.Create(target, new XmlWriterSettings {
                            Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize, CloseOutput = false }))
                            xml.Save(writer);
                    }
                }
            }
        }
    }
}
