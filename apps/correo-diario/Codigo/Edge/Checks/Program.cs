using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CorreoDiario;
using MimeKit;
using OpenMcdf;
using UglyToad.PdfPig;

var dir = Directory.CreateTempSubdirectory("cd-edge-check-").FullName;
var origin = new Origin("outlook.cloud.microsoft", "Entrada", "", "Prueba");
var assertions = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception(name);
    assertions++;
    Console.WriteLine("OK " + name);
}
string Make(
    string name,
    string subject,
    string id,
    string date,
    string from,
    string? parent = null,
    string? refs = null,
    bool attach = false
)
{
    var file = Path.Combine(dir, name + ".eml");
    var m = new MimeMessage();
    m.From.Add(MailboxAddress.Parse(from));
    m.To.Add(MailboxAddress.Parse("equipo@example.test"));
    m.Subject = subject;
    if (id.Length > 0)
        m.MessageId = id;
    if (date.Length > 0)
        m.Date = DateTimeOffset.Parse(date);
    if (parent != null)
        m.InReplyTo = parent;
    if (refs != null)
        foreach (var v in refs.Split(' '))
            m.References.Add(v);
    var builder = new BodyBuilder();
    builder.TextBody = "Texto de " + name + ".";
    builder.HtmlBody = "<p>Texto de " + name + ".</p>";
    if (attach)
    {
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAAAYCAIAAADF1mwTAAAAO0lEQVR4nO3PQQ3AIADAQOCBOISheyJ4XJb0FLRznzv+bOmAVw1oDWgNaA1oDWgNaA1oDWgNaA1oDWgf4ywBBv6iL2gAAAAASUVORK5CYII="
        );
        var img = builder.LinkedResources.Add("firma.png", png, new ContentType("image", "png"));
        img.ContentId = "firma@example.test";
        builder.HtmlBody +=
            "<img src='cid:firma@example.test' alt='Firma'><img src='cid:noexiste@example.test'><img src='https://example.invalid/externa.png'><script>SCRIPT_AJENO</script>";
        builder.Attachments.Add("solicitud.pdf", new byte[] { 4, 2, 0, 8 });
        builder.Attachments.Add("SOLICITUD.pdf", new byte[] { 1, 2, 3 });
        builder.Attachments.Add("Correo.pdf", new byte[] { 9, 1 });
        builder.Attachments.Add("Original.eml", new byte[] { 9, 2 });
        builder.Attachments.Add("../../prueba.txt", new byte[] { 44 });
    }
    m.Body = builder.ToMessageBody();
    m.WriteTo(file);
    return file;
}
try
{
    string when = "2026-11-26T08:35:22+01:00";
    var a = MailReader.Inspect(
        Make(
            "principal",
            "Permiso",
            "raiz@example.test",
            when,
            "Rosa <rosa@example.test>",
            attach: true
        ),
        origin
    );
    var b = MailReader.Inspect(
        Make(
            "respuesta",
            "RE: Permiso",
            "hija@example.test",
            when,
            "Luis <luis@example.test>",
            a.Id,
            a.Id
        ),
        origin
    );
    var other = MailReader.Inspect(
        Make("independiente", "Permiso", "distinto@example.test", when, "Rosa <rosa@example.test>"),
        origin
    );
    var reply2 = MailReader.Inspect(
        Make(
            "otra",
            "Distinto asunto",
            "nieta@example.test",
            "2026-11-27T00:05:07+01:00",
            "Luz <luz@example.test>",
            b.Id,
            a.Id + " " + b.Id
        ),
        origin
    );
    Check(a.Stamp == "2026-11-26_08.35.22", "nombre usa hora real con segundos");
    Check(a.BaseName != other.BaseName, "misma hora y asunto en carpetas distintas");
    var threads = Threads.Find([reply2, b, other, a, a]);
    Check(threads.Count == 2, "asuntos iguales sin referencias independientes");
    var t = threads.Single(t => t.Messages.Count == 3);
    Check(
        t.Missing.Count == 0 && t.OriginText.Contains("rosa@example.test"),
        "origen determinado con cabeceras aunque cambie el asunto"
    );
    Check(t.Messages.First() == a, "empate de segundos ordena el original antes de la respuesta");
    Check(t.Messages.Last() == reply2, "orden cronológico de mensajes del día siguiente");
    Check(
        Threads.Find([b]).Single().Missing.SequenceEqual([a.Id]),
        "se identifica el antecesor ausente"
    );
    Check(
        !Threads.Find([b]).Single().OriginText.Contains("Origen identificado"),
        "no se afirma haber encontrado el origen ausente"
    );
    var conflict = a with { Hash = "0123456789ab" + a.Hash[12..] };
    Check(
        Threads.Find([a, conflict]).Single().OriginText.Contains("revisión"),
        "message-id repetido no sobrescribe originales distintos"
    );
    var rootIndex = new byte[22];
    rootIndex[0] = 1;
    rootIndex[5] = 3;
    var ia = a with { ThreadIndex = Convert.ToBase64String(rootIndex) };
    var ib = other with
    {
        ThreadIndex = Convert.ToBase64String(rootIndex.Concat(new byte[5]).ToArray()),
    };
    Check(
        Threads.Find([ia, ib]).Single().Messages.Count == 2,
        "recuperación de hilo mediante Thread-Index de Exchange"
    );
    Check(Threads.Find([a, a]).Single().Messages.Count == 1, "deduplicación por bytes");
    var dt1 = a with { Date = DateTimeOffset.Parse("2026-10-25T00:30:00Z") };
    var dt2 = b with { Date = DateTimeOffset.Parse("2026-10-25T01:30:00Z") };
    Check(dt1.Stamp == dt2.Stamp && dt1.DisplayDate != dt2.DisplayDate, "cambio de hora en Madrid");
    var missingDate = Path.Combine(dir, "sinfecha.eml");
    File.WriteAllText(
        missingDate,
        "From: A <a@example.test>\r\nTo: b@example.test\r\nSubject: Sin fecha\r\n\r\nTexto"
    );
    var undated = MailReader.Inspect(missingDate, origin);
    Check(
        undated.Date == null && undated.Stamp == "Sin-fecha",
        "fecha ausente nunca se sustituye por hoy"
    );
    var received = Path.Combine(dir, "recibido.eml");
    File.WriteAllText(
        received,
        "Received: from origen by destino; Thu, 26 Nov 2026 12:01:02 +0000\r\n"
            + File.ReadAllText(missingDate)
    );
    Check(
        MailReader.Inspect(received, origin).Stamp == "2026-11-26_13.01.02",
        "alternativa de fecha de recepción"
    );
    var c = MailReader.ReadContent(a.FilePath);
    Check(c.Parts.Count == 6, "adjuntos e imagen MIME recuperados");
    var warnings = new List<string>();
    var html = BodyHtml.Create(a, c, warnings);
    Check(html.Contains("data:image/png;base64,"), "imagen incrustada resuelta por Content-ID");
    Check(
        !html.Contains("SCRIPT_AJENO") && !html.Contains("https://example.invalid"),
        "HTML de terceros no se ejecuta ni descarga imágenes externas"
    );
    Check(
        warnings.Any(s => s.Contains("noexiste")) && warnings.Any(s => s.Contains("externa")),
        "avisos de imágenes no disponibles"
    );
    var msgfile = Path.Combine(dir, "prueba.msg");
    using (var root = RootStorage.Create(msgfile))
    {
        void Add(string id, string value)
        {
            using var node = root.CreateStream("__substg1.0_" + id + "001F");
            node.Write(Encoding.Unicode.GetBytes(value));
        }
        Add("001A", "IPM.Note");
        Add("0037", "Petición MSG");
        Add("1000", "Este es el cuerpo del MSG de prueba.");
        Add("0C1A", "Rosa");
        Add("0C1F", "rosa@example.test");
        Add("1035", "<mapi@example.test>");
        Add(
            "007D",
            "Date: Thu, 26 Nov 2026 09:00:02 +0100\r\nFrom: Rosa <rosa@example.test>\r\nTo: equipo@example.test\r\nSubject: Petición MSG\r\nMessage-ID: <mapi@example.test>\r\n"
        );
        using var properties = root.CreateStream("__properties_version1.0");
        properties.Write(new byte[32]);
    }
    var msg = MailReader.Inspect(msgfile, origin);
    Check(
        msg.Id == "mapi@example.test"
            && msg.From.Contains("rosa@example.test")
            && msg.Stamp.EndsWith("09.00.02"),
        "lectura de un MSG binario con cabeceras originales"
    );
    Check(
        MailReader.ReadContent(msgfile).Plain.Contains("cuerpo del MSG"),
        "cuerpo MAPI recuperado"
    );
    var invalid = Path.Combine(dir, "invalido.eml");
    File.WriteAllText(invalid, "texto sin cabeceras");
    bool refused = false;
    try
    {
        MailReader.Inspect(invalid, origin);
    }
    catch
    {
        refused = true;
    }
    Check(refused, "se rechaza un archivo que no es un correo");
    var printer = new MailPrinter();
    var output = await Archive.WriteAsync(
        [other, a, b, reply2, a, msg],
        dir,
        printer,
        [],
        null,
        default
    );
    Check(
        output.Originals == 5 && output.Pdfs == 5 && output.Combined == 1,
        "cinco carpetas de correos y un PDF de hilo"
    );
    var dirs = Directory
        .GetDirectories(output.Folder)
        .Where(p => Path.GetFileName(p) != "Hilos")
        .ToArray();
    Check(
        dirs.Length == 5 && dirs.All(p => File.Exists(Path.Combine(p, "Correo.pdf"))),
        "una carpeta por correo con Correo.pdf en su interior"
    );
    using var archiveIndex = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(output.Folder, "Origen.json"))
    );
    var stored = archiveIndex
        .RootElement.GetProperty("mensajes")
        .EnumerateArray()
        .ToDictionary(
            m => m.GetProperty("sha256").GetString()!,
            m => m.GetProperty("carpeta").GetString()!
        );
    string MailDir(Mail item) => Path.Combine(output.Folder, stored[item.Hash]);
    Check(
        stored[msg.Hash] == msg.FolderStem && stored[a.Hash] != stored[other.Hash],
        "nombres sencillos y sufijo solo si coinciden fecha, hora y asunto"
    );
    var originalFolder = MailDir(a);
    Check(
        File.ReadAllBytes(Path.Combine(originalFolder, "solicitud.pdf"))
            .SequenceEqual(new byte[] { 4, 2, 0, 8 }),
        "adjunto en su carpeta con bytes y nombre originales"
    );
    Check(
        File.ReadAllBytes(Path.Combine(originalFolder, "SOLICITUD (2).pdf"))
            .SequenceEqual(new byte[] { 1, 2, 3 }),
        "adjuntos homónimos conservados sin sobrescribir"
    );
    Check(
        File.Exists(Path.Combine(originalFolder, "Correo (2).pdf"))
            && File.Exists(Path.Combine(originalFolder, "Original (2).eml")),
        "colisiones con los nombres reservados del correo"
    );
    Check(
        File.Exists(Path.Combine(originalFolder, "firma.png"))
            && Directory.GetDirectories(originalFolder).Length == 0,
        "adjuntos e imágenes junto al PDF sin subcarpetas"
    );
    Check(
        MailReader.FileHash(Path.Combine(originalFolder, "Original.eml")) == a.Hash,
        "original íntegro por SHA-256"
    );
    var combined = Directory.GetFiles(Path.Combine(output.Folder, "Hilos")).Single();
    Check(
        MailPrinter.VerifyPdf(combined)
            == t.Messages.Sum(m => MailPrinter.VerifyPdf(Path.Combine(MailDir(m), "Correo.pdf"))),
        "hilo contiene todas las páginas de las copias individuales"
    );
    using (var pp = PdfDocument.Open(combined))
        Check(
            pp.GetPages().Last().Text.Contains(reply2.Key),
            "último mensaje del hilo en última página"
        );
    using (var pp = PdfDocument.Open(Path.Combine(originalFolder, "Correo.pdf")))
        Check(
            pp.GetPages().Any(p => p.GetImages().Any()),
            "la imagen realmente aparece como objeto PDF"
        );
    using (
        var audit = JsonDocument.Parse(File.ReadAllText(Path.Combine(output.Folder, "Origen.json")))
    )
        Check(
            audit.RootElement.GetProperty("mensajes").GetArrayLength() == 5,
            "registro de procedencia por carpeta"
        );
    var again = await Archive.WriteAsync([b], dir, printer, [], null, default);
    Check(
        again.Folder != output.Folder && File.Exists(Path.Combine(originalFolder, "Correo.pdf")),
        "repetición en lote independiente sin sobrescritura"
    );
    Check(again.Warnings >= 1, "aviso de hilo parcial en el resumen de la exportación");
    var paragraphs = string.Join(
        "",
        Enumerable
            .Range(1, 130)
            .Select(i =>
                "<p>Solicitud número " + i + ". Cada apartado ocupa su posición en el archivo.</p>"
            )
    );
    var rich = new Content(
        "<table><tr><th>Fecha</th><th>Turno</th></tr><tr><td>26 de noviembre</td><td>Mañana</td></tr></table>"
            + paragraphs
            + "<p>Fin del documento</p>",
        "",
        [],
        []
    );
    var longPdf = Path.Combine(dir, "largo.pdf");
    var bodyNotes = new List<string>();
    var printNotes = await printer.PrintAsync(
        BodyHtml.Create(a, rich, bodyNotes),
        longPdf,
        a.Key,
        default
    );
    using (var multi = PdfDocument.Open(longPdf))
        Check(
            multi.NumberOfPages > 1
                && Regex
                    .Replace(multi.GetPages().Last().Text, @"\s+", "")
                    .Contains("Findeldocumento"),
            "tablas y texto largo llegan hasta la última página"
        );
    Check(!printNotes.Any(w => w.Contains("cotejar")), "texto multipágina cotejado con el cuerpo");
    Console.WriteLine($"{assertions} comprobaciones superadas.");
    if (args.Contains("--retain"))
        Console.WriteLine("Archivos de prueba: " + dir);
    else
        Directory.Delete(dir, true);
}
catch
{
    Console.WriteLine("Directorio de pruebas: " + dir);
    throw;
}
