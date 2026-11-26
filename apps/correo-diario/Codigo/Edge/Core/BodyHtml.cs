using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CorreoDiario;

public static class BodyHtml
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    static readonly HashSet<string> Drop = new(
        "script style link meta base iframe frame frameset object embed form button input textarea select svg math noscript head title template video audio".Split(
            ' '
        )
    );
    static readonly HashSet<string> Keep = new(
        "p div span b strong i em u s br hr blockquote pre code table thead tbody tfoot tr th td ul ol li h1 h2 h3 h4 h5 h6 dl dt dd small sub sup".Split(
            ' '
        )
    );
    static readonly HashSet<string> Rasters = new(
        "image/png image/jpeg image/gif image/webp image/bmp".Split(' '),
        StringComparer.OrdinalIgnoreCase
    );

    static string Norm(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value).Trim('<', '>', ' ');
        }
        catch
        {
            return value.Trim('<', '>', ' ');
        }
    }

    public static string Create(Mail m, Content c, List<string> notices)
    {
        var sb = new StringBuilder(
            """<!doctype html><html lang="es"><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none';img-src data:;style-src 'unsafe-inline';base-uri 'none';form-action 'none'"><style>@page{size:A4;margin:15mm 16mm}*{box-sizing:border-box}body{font:10.5pt Arial,sans-serif;color:#192434;line-height:1.45;overflow-wrap:anywhere}h1{font-size:18pt;margin:0 0 12px}dl.meta{border-bottom:1px solid #afb8c1;padding-bottom:12px}dl.meta div{margin:3px 0}dt{display:inline;font-weight:bold}dd{display:inline;margin:0 0 0 8px}.body img{max-width:100%;height:auto}.body table{max-width:100%;border-collapse:collapse}.body td,.body th{padding:2px 4px;vertical-align:top}.body pre{white-space:pre-wrap}.body blockquote{margin-left:10px;padding-left:12px;border-left:2px solid #c0c6cd}.note{font-size:9pt;color:#674800}footer{margin-top:13px;color:#4d5864;font-size:8pt}</style></head><body><h1>"""
        );
        sb.Append(E(m.Subject.Length > 0 ? m.Subject : "Sin asunto"))
            .Append("</h1><dl class=meta>");
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.Append("<div><dt>")
                    .Append(E(label))
                    .Append("</dt><dd>")
                    .Append(E(value))
                    .Append("</dd></div>");
        }
        Row("De", m.From.Length == 0 ? "No consta" : m.From);
        if (m.Sender != m.From)
            Row("Enviado por", m.Sender);
        Row("Responder a", m.ReplyTo);
        Row("Para", m.To);
        Row("CC", m.Cc);
        Row(m.DateSource + " · Madrid", m.DisplayDate);
        if (m.Date.HasValue)
            Row(
                "Fecha de la cabecera o registro",
                m.Date.Value.ToString("yyyy-MM-dd HH:mm:ss zzz")
            );
        if (m.Received.HasValue && m.DateSource == "Envío")
            Row(
                "Recepción · Madrid",
                TimeZoneInfo
                    .ConvertTime(m.Received.Value, MailReader.Madrid)
                    .ToString("dd/MM/yyyy HH:mm:ss zzz")
            );
        Row("Message-ID", m.Id);
        Row("En respuesta a", string.Join(", ", m.Parents));
        Row("Adjuntos", string.Join(", ", c.Parts.Where(p => !p.Inline).Select(p => p.Name)));
        sb.Append("</dl><div class=body>");
        if (string.IsNullOrWhiteSpace(c.Html))
            sb.Append("<pre>").Append(E(c.Plain)).Append("</pre>");
        else
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(c.Html);
            var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
            foreach (var n in root.ChildNodes)
                AppendNode(n, 0);
        }
        void AppendNode(HtmlNode n, int depth)
        {
            if (depth > 160)
                throw new InvalidDataException("El cuerpo HTML tiene demasiados niveles.");
            if (n.NodeType == HtmlNodeType.Text)
            {
                sb.Append(E(HtmlEntity.DeEntitize(n.InnerText)));
                return;
            }
            if (n.NodeType != HtmlNodeType.Element)
                return;
            var tag = n.Name.ToLowerInvariant();
            if (Drop.Contains(tag))
                return;
            if (tag == "img")
            {
                string attr(string k) => HtmlEntity.DeEntitize(n.GetAttributeValue(k, ""));
                var src = attr("src");
                var alt = MailReader.Clean(attr("alt"));
                var cid = src.StartsWith("cid:", StringComparison.OrdinalIgnoreCase)
                    ? Norm(src[4..])
                    : "";
                var matches = c
                    .Parts.Where(p =>
                        cid.Length > 0
                            ? Norm(p.Cid).Equals(cid, StringComparison.OrdinalIgnoreCase)
                            : src.Length > 0 && p.Location == src
                    )
                    .ToList();
                string? data = null;
                if (
                    matches.Count == 1
                    && Rasters.Contains(matches[0].Mime)
                    && matches[0].Bytes.Length > 0
                )
                    data =
                        "data:"
                        + matches[0].Mime
                        + ";base64,"
                        + Convert.ToBase64String(matches[0].Bytes);
                else if (
                    Regex.IsMatch(
                        src,
                        @"^data:image/(png|jpeg|gif|webp|bmp);base64,[A-Za-z0-9+/=\s]+$",
                        RegexOptions.IgnoreCase
                    )
                )
                    data = src;
                if (data != null)
                    sb.Append("<img alt=\"")
                        .Append(E(alt))
                        .Append("\" src=\"")
                        .Append(E(data))
                        .Append("\">");
                else
                {
                    var text =
                        cid.Length > 0
                            ? "Imagen incrustada sin resolver (" + cid + ")"
                            : "Imagen externa o sin datos";
                    if (alt.Length > 0)
                        text += " · " + alt;
                    notices.Add(text);
                    sb.Append("<span class=note>[").Append(E(text)).Append("]</span>");
                }
                return;
            }
            var keep = Keep.Contains(tag);
            if (keep)
            {
                sb.Append('<').Append(tag);
                if (tag == "td" || tag == "th")
                    foreach (var a in new[] { "rowspan", "colspan" })
                        if (
                            int.TryParse(n.GetAttributeValue(a, ""), out int span)
                            && span >= 2
                            && span <= 100
                        )
                            sb.Append(' ').Append(a).Append("=\"").Append(span).Append('"');
                sb.Append('>');
            }
            foreach (var child in n.ChildNodes)
                AppendNode(child, depth + 1);
            if (keep && tag is not ("br" or "hr"))
                sb.Append("</").Append(tag).Append('>');
            if (
                tag == "a"
                && Uri.TryCreate(
                    HtmlEntity.DeEntitize(n.GetAttributeValue("href", "")),
                    UriKind.Absolute,
                    out var link
                )
                && link.Scheme is "https" or "http" or "mailto"
                && HtmlEntity.DeEntitize(n.InnerText).Trim() != link.OriginalString
            )
                sb.Append(" (").Append(E(link.OriginalString)).Append(')');
        }
        sb.Append("</div>");
        if (notices.Count > 0)
            sb.Append("<p class=note>")
                .Append(E(string.Join(" · ", notices.Distinct())))
                .Append("</p>");
        return sb.Append("<footer>Original ")
            .Append(m.Key)
            .Append("</footer></body></html>")
            .ToString();
    }
}
