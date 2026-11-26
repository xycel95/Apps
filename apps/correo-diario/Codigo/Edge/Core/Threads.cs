using System.Text.RegularExpressions;

namespace CorreoDiario;

public sealed record Conversation(
    IReadOnlyList<Mail> Messages,
    IReadOnlyList<string> Missing,
    IReadOnlyList<Mail> Roots,
    string OriginText
);

public static class Threads
{
    static IEnumerable<string> Keys(Mail m)
    {
        foreach (var id in m.References.Concat(m.Parents).Append(m.Id).Where(s => s.Length > 0))
            yield return "id/" + id;
        if (MailReader.IndexBytes(m.ThreadIndex) is { } index)
            yield return "idx/" + Convert.ToHexString(index[..22]);
        if (m.Origin.Group.Length > 0)
            yield return "vista/" + m.Origin.Group;
    }

    public static List<Conversation> Find(IEnumerable<Mail> source)
    {
        var all = source.DistinctBy(m => m.Hash).ToList();
        var sets = Enumerable.Range(0, all.Count).ToArray();
        int Root(int i)
        {
            while (sets[i] != i)
            {
                sets[i] = sets[sets[i]];
                i = sets[i];
            }
            return i;
        }
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < all.Count; i++)
            foreach (var key in Keys(all[i]))
                if (owners.TryGetValue(key, out var owner))
                    sets[Root(i)] = Root(owner);
                else
                    owners.Add(key, i);
        return Enumerable
            .Range(0, all.Count)
            .GroupBy(Root)
            .Select(g => Describe(g.Select(i => all[i])))
            .ToList();
    }

    static Conversation Describe(IEnumerable<Mail> members)
    {
        var ordered = new List<Mail>();
        foreach (
            var time in members.GroupBy(m => m.Date?.UtcTicks ?? long.MaxValue).OrderBy(g => g.Key)
        )
        {
            var left = time.OrderBy(m => m.Hash, StringComparer.Ordinal).ToList();
            while (left.Count > 0)
            {
                var next = left.FirstOrDefault(m =>
                    !left.Any(p =>
                        p != m
                        && p.Id.Length > 0
                        && (m.Parents.Contains(p.Id) || m.References.Contains(p.Id))
                    )
                ) ?? left[0];
                ordered.Add(next);
                left.Remove(next);
            }
        }
        var ids = ordered
            .Select(m => m.Id)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var missing = ordered
            .SelectMany(m => m.References.Concat(m.Parents))
            .Where(id => !ids.Contains(id))
            .Distinct()
            .ToList();
        var roots = ordered
            .Where(m =>
                m.Parents.Length == 0
                && m.References.Length == 0
                && !(MailReader.IndexBytes(m.ThreadIndex) is { Length: > 22 })
            )
            .ToList();
        var firstIds = ordered
            .Where(m => m.References.Length > 0)
            .Select(m => m.References[0])
            .ToHashSet(StringComparer.Ordinal);
        var parentIds = ordered.SelectMany(m => m.Parents).ToHashSet(StringComparer.Ordinal);
        var supported = roots
            .Where(m =>
                m.Id.Length > 0 && (firstIds.Contains(m.Id) || parentIds.Contains(m.Id))
                || MailReader.IndexBytes(m.ThreadIndex) is { Length: 22 }
            )
            .ToList();
        string state;
        if (ordered.GroupBy(m => m.Id).Any(g => g.Key.Length > 0 && g.Count() > 1))
            state =
                "Hay originales distintos con el mismo Message-ID. Se conservan las dos versiones y el origen requiere revisión.";
        else if (supported.Count == 1)
            state =
                "Origen identificado por cabeceras: "
                + supported[0].From
                + " · "
                + supported[0].DisplayDate
                + ".";
        else if (
            roots.Count == 1
            && ordered.Count == 1
            && missing.Count == 0
            && MailReader.ShortSubject(roots[0].Subject) == roots[0].Subject
        )
            state = "Mensaje sin referencia a otro anterior.";
        else if (missing.Count > 0 || roots.Count == 0)
            state =
                "Origen no confirmado. Hay respuestas o referencias a mensajes que no se han recuperado.";
        else
            state =
                "Origen no confirmado. Las cabeceras disponibles no identifican un único primer mensaje.";
        return new(ordered, missing, roots, state);
    }
}
