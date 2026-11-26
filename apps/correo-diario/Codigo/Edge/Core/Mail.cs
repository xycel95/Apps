using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MimeKit.Utils;
using MsgReader.Outlook;

namespace CorreoDiario;

public sealed record Origin(string Site, string Folder, string Group, string Method);

public sealed record FilePart(
    string Name,
    string Mime,
    string Cid,
    string Location,
    bool Inline,
    byte[] Bytes
);

public sealed record Mail(
    string FilePath,
    string Hash,
    Origin Origin,
    string Subject,
    string From,
    string Sender,
    string ReplyTo,
    string ReturnPath,
    string To,
    string Cc,
    string Id,
    string[] References,
    string[] Parents,
    string ThreadIndex,
    string Topic,
    DateTimeOffset? Date,
    string DateSource,
    DateTimeOffset? Received
)
{
    public string Key => Hash[..12];
    public DateTimeOffset? Madrid =>
        Date.HasValue ? TimeZoneInfo.ConvertTime(Date.Value, MailReader.Madrid) : null;
    public string Stamp =>
        Madrid?.ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture) ?? "Sin-fecha";
    public string DisplayDate =>
        Madrid?.ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture)
        ?? "Fecha desconocida";
    public string FolderStem => Stamp + " - " + MailReader.SafeName(Subject, 60);
    public string BaseName => FolderStem + " (" + Key + ")";
}

public sealed record Content(string Html, string Plain, List<FilePart> Parts, List<string> Notices);

public static class MailReader
{
    public static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid"
    );

    static MailReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string Clean(string? s) => Regex.Replace(s ?? "", @"[\p{C}\s]+", " ").Trim();

    public static string SafeName(string? s, int max)
    {
        var name = Regex.Replace(Clean(s), """[<>:"/\\|?*]""", "_").Trim('.', ' ');
        if (name.Length > max)
            name = name[..max].TrimEnd('.', ' ');
        if (name.Length > 0 && char.IsHighSurrogate(name[^1]))
            name = name[..^1];
        if (
            Regex.IsMatch(
                name,
                @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",
                RegexOptions.IgnoreCase
            )
        )
            name = "_" + name;
        return name.Length > 0 ? name : "Sin asunto";
    }

    public static string ShortSubject(string? s) =>
        Regex.Replace(
            Clean(s),
            @"^(?:(?:RE|RV|FW|FWD|AW|WG|TR|RES)\s*(?:\[\d+\])?\s*:\s*)+",
            "",
            RegexOptions.IgnoreCase
        );

    public static string FileHash(string path)
    {
        using var f = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(f));
    }

    public static string NormalizeId(string? id)
    {
        var s = Clean(id).Trim('<', '>', ' ');
        if (s.Length == 0 || s.Any(char.IsWhiteSpace) || s.IndexOfAny(['<', '>']) >= 0)
            return "";
        var at = s.LastIndexOf('@');
        return at > 0 ? s[..(at + 1)] + s[(at + 1)..].ToLowerInvariant() : s;
    }

    private static string[] Ids(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        var matches = Regex
            .Matches(raw, @"<([^<>\s]+)>")
            .Select(m => NormalizeId(m.Groups[1].Value))
            .ToList();
        if (matches.Count == 0 && raw.Contains('@'))
            matches.AddRange(
                raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(NormalizeId)
            );
        return matches.Where(s => s.Length > 0).Distinct().ToArray();
    }

    public static byte[]? IndexBytes(string? raw)
    {
        try
        {
            var b = Convert.FromBase64String(raw ?? "");
            return b.Length >= 22 && (b.Length - 22) % 5 == 0 && b[0] == 1 ? b : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    static DateTimeOffset? DateValue(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateUtils.TryParse(s, out var d) ? d : null;

    static DateTimeOffset? Received(HeaderList h)
    {
        foreach (var line in h.Where(v => v.Id == HeaderId.Received))
        {
            var idx = line.Value.LastIndexOf(';');
            if (idx >= 0 && DateValue(line.Value[(idx + 1)..]) is { } d)
                return d;
        }
        return null;
    }

    static string H(HeaderList h, string name) => h[name] ?? "";

    static void ValidateEml(MimeMessage m)
    {
        if (
            !m.Headers.Any(h =>
                h.Id
                    is HeaderId.From
                        or HeaderId.Sender
                        or HeaderId.To
                        or HeaderId.MessageId
                        or HeaderId.Received
                        or HeaderId.Date
            )
        )
            throw new InvalidDataException(
                "El archivo no contiene cabeceras de un mensaje de correo."
            );
    }

    static bool IsMsg(string path) =>
        Path.GetExtension(path).Equals(".msg", StringComparison.OrdinalIgnoreCase);

    public static Mail Inspect(string path, Origin origin)
    {
        var hash = FileHash(path);
        if (IsMsg(path))
        {
            using var f = File.OpenRead(path);
            using var m = new Storage.Message(f, FileAccess.Read, true);
            var raw = m.TransportMessageHeaders ?? "";
            using var headerStream = new MemoryStream(
                Encoding.UTF8.GetBytes(raw.TrimEnd() + "\r\n\r\n")
            );
            var h = raw.Length == 0 ? new HeaderList() : HeaderList.Load(headerStream);
            string Address(string? name, string? email) =>
                Clean((name ?? "") + (string.IsNullOrEmpty(email) ? "" : " <" + email + ">"));
            string Recipients(string type) =>
                string.Join(
                    ", ",
                    m.Recipients.Where(r =>
                            string.Equals(
                                r.Type.ToString(),
                                type,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .Select(r => Clean(r.DisplayName + " <" + r.Email + ">"))
                );
            var idx = m.ConversationIndex;
            var sent = DateValue(H(h, "Date")) ?? m.SentOn;
            var received = Received(h) ?? m.ReceivedOn;
            var mi = NormalizeId(Ids(H(h, "Message-ID")).FirstOrDefault() ?? m.Id);
            return new(
                path,
                hash,
                origin,
                Clean(m.Subject),
                Clean(
                    h["From"]
                        ?? Address(
                            m.SenderRepresenting?.DisplayName ?? m.Sender?.DisplayName,
                            m.SenderRepresenting?.Email ?? m.Sender?.Email
                        )
                ),
                Clean(h["Sender"] ?? Address(m.Sender?.DisplayName, m.Sender?.Email)),
                Clean(H(h, "Reply-To")),
                Clean(H(h, "Return-Path")),
                Clean(h["To"] ?? Recipients("To")),
                Clean(h["Cc"] ?? Recipients("Cc")),
                mi,
                Ids(H(h, "References")),
                Ids(H(h, "In-Reply-To")),
                H(h, "Thread-Index") is { Length: > 0 } ix ? ix : idx ?? "",
                Clean(h["Thread-Topic"] ?? m.ConversationTopic ?? m.Subject),
                sent ?? received,
                sent.HasValue ? "Envío" : "Recepción",
                received
            );
        }
        using var stream = File.OpenRead(path);
        var eml = MimeMessage.Load(stream);
        ValidateEml(eml);
        var headers = eml.Headers;
        var date = DateValue(headers["Date"]);
        var recv = Received(headers);
        return new(
            path,
            hash,
            origin,
            Clean(eml.Subject),
            Clean(eml.From.ToString()),
            Clean(eml.Sender?.ToString()),
            Clean(eml.ReplyTo.ToString()),
            Clean(headers["Return-Path"]),
            Clean(eml.To.ToString()),
            Clean(eml.Cc.ToString()),
            NormalizeId(eml.MessageId),
            Ids(H(headers, "References")),
            Ids(H(headers, "In-Reply-To")),
            Clean(headers["Thread-Index"]),
            Clean(headers["Thread-Topic"] ?? eml.Subject),
            date ?? recv,
            date.HasValue ? "Envío" : "Recepción",
            recv
        );
    }

    public static Content ReadContent(string path)
    {
        var parts = new List<FilePart>();
        var notices = new List<string>();
        if (IsMsg(path))
        {
            using var fs = File.OpenRead(path);
            using var msg = new Storage.Message(fs, FileAccess.Read, true);
            foreach (var entry in msg.Attachments)
            {
                if (entry is Storage.Attachment a)
                {
                    if (a.IsExternalReference)
                    {
                        notices.Add("Adjunto enlazado sin contenido local: " + Clean(a.FileName));
                        continue;
                    }
                    if (a.Data is not { } bytes)
                    {
                        notices.Add("Adjunto sin datos: " + Clean(a.FileName));
                        continue;
                    }
                    parts.Add(
                        new(
                            Clean(a.FileName),
                            a.MimeType ?? "",
                            Clean(a.ContentId),
                            "",
                            a.IsInline,
                            bytes
                        )
                    );
                }
                else if (entry is Storage.Message child)
                {
                    using var dst = new MemoryStream();
                    child.Save(dst);
                    parts.Add(
                        new(
                            SafeName(child.Subject, 50) + ".msg",
                            "application/vnd.ms-outlook",
                            "",
                            "",
                            false,
                            dst.ToArray()
                        )
                    );
                }
                else
                    notices.Add("Outlook incluyó un elemento adjunto de tipo desconocido.");
            }
            return new(msg.BodyHtml ?? "", msg.BodyText ?? "", parts, notices);
        }
        using var stream = File.OpenRead(path);
        var m = MimeMessage.Load(stream);
        ValidateEml(m);
        // Include only the alternatives that the MIME tree selected as message bodies.
        var htmlBody = m.GetTextBody(MimeKit.Text.TextFormat.Html);
        var plainBody = m.GetTextBody(MimeKit.Text.TextFormat.Plain);
        void Visit(MimeEntity e, int depth, bool bodyBranch)
        {
            if (depth > 80)
                throw new InvalidDataException("Estructura de mensaje demasiado profunda.");
            if (e is Multipart multipart)
            {
                if (
                    multipart.ContentType.MimeType.Equals(
                        "multipart/encrypted",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    notices.Add("Contenido cifrado conservado en el original.");
                var related = multipart as MultipartRelated;
                for (var i = 0; i < multipart.Count; i++)
                    Visit(
                        multipart[i],
                        depth + 1,
                        bodyBranch
                            && (related == null || ReferenceEquals(related.Root, multipart[i]))
                    );
                return;
            }
            if (e is MessagePart nested)
            {
                using var sink = new MemoryStream();
                if (nested.Message != null)
                {
                    nested.Message.WriteTo(sink);
                    parts.Add(
                        new(
                            e.ContentDisposition?.FileName
                                ?? SafeName(nested.Message.Subject, 42) + ".eml",
                            "message/rfc822",
                            e.ContentId ?? "",
                            "",
                            false,
                            sink.ToArray()
                        )
                    );
                }
                else
                    notices.Add("Mensaje adjunto sin contenido.");
                return;
            }
            if (e is not MimePart p)
                return;
            if (p.ContentType.MimeType.Contains("pkcs7-mime", StringComparison.OrdinalIgnoreCase))
                notices.Add("Contenido S/MIME conservado en el original.");
            if (
                p is TextPart t
                && bodyBranch
                && !p.IsAttachment
                && string.IsNullOrEmpty(p.FileName)
                && (t.IsHtml && t.Text == htmlBody || t.IsPlain && t.Text == plainBody)
            )
                return;
            if (p.Content == null)
            {
                notices.Add("Parte MIME sin datos: " + Clean(p.FileName));
                return;
            }
            using var output = new MemoryStream();
            p.Content.DecodeTo(output);
            var filename = p.FileName ?? p.ContentType.Name;
            if (string.IsNullOrWhiteSpace(filename))
                filename =
                    "parte-"
                    + (parts.Count + 1)
                    + (
                        MimeTypes.TryGetExtension(p.ContentType.MimeType, out var ex)
                            ? "." + ex
                            : ".bin"
                    );
            parts.Add(
                new(
                    Clean(filename),
                    p.ContentType.MimeType,
                    Clean(p.ContentId),
                    p.ContentLocation?.OriginalString ?? "",
                    p.ContentDisposition?.Disposition == "inline"
                        || (!p.IsAttachment && !string.IsNullOrEmpty(p.ContentId)),
                    output.ToArray()
                )
            );
        }
        if (m.Body != null)
            Visit(m.Body, 0, true);
        return new(htmlBody ?? "", plainBody ?? "", parts, notices);
    }
}
