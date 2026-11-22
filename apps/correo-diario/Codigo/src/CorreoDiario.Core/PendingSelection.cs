using System.Text.Json;

namespace CorreoDiario.Core;

public static class PendingSelection
{
    public static IReadOnlyList<string> FromReceipt(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var r = parsed.RootElement;
        if (r.ValueKind != JsonValueKind.Object ||
            !r.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(version.GetString()) ||
            !r.TryGetProperty("SavedCopiesVerified", out var verified) || verified.ValueKind != JsonValueKind.True ||
            !r.TryGetProperty("PendingMessages", out var pending) || pending.ValueKind != JsonValueKind.Array || pending.GetArrayLength() == 0 ||
            !r.TryGetProperty("MovedToDeleted", out var moved) || moved.ValueKind != JsonValueKind.Array || moved.GetArrayLength() != 0 ||
            r.TryGetProperty("MoveInProgress", out var active) && active.ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("El registro no confirma un lote con copias verificadas y correos pendientes sin movimientos.");
        var ids = new List<string>();
        foreach (var message in pending.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("Id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                throw new InvalidDataException("El registro contiene un pendiente sin identificador.");
            ids.Add(id.GetString()!);
        }
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new InvalidDataException("El registro contiene pendientes repetidos.");
        return ids;
    }

    // Validate the whole match before changing any checkboxes. Never apply a
    // partial match from another mailbox or an incomplete/currently filtered list.
    public static IReadOnlySet<string> Match(IReadOnlyList<MailInfo> loaded, IReadOnlyList<string> pending)
    {
        if (pending.Count == 0 || pending.Any(string.IsNullOrWhiteSpace) || pending.Distinct(StringComparer.Ordinal).Count() != pending.Count)
            throw new InvalidDataException("La selección de pendientes está vacía o repetida.");
        var available = loaded.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var missing = pending.Count(id => !available.Contains(id));
        if (missing > 0) throw new InvalidDataException($"Faltan {missing} correos pendientes en la lista actual. Busca con las mismas carpetas y fechas antes de marcar los pendientes.");
        return pending.ToHashSet(StringComparer.Ordinal);
    }
}
