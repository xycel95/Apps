using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;

// Independent wire reader for the date-search contract. In particular, its
// required Restriction.Item and the polymorphic wire name Constant are checked
// by the framework serializer, rather than by the request-building code.
internal static class OwaSearchContract
{
    private const string Ns = "http://schemas.datacontract.org/2004/07/Exchange";
    public sealed record Range(string Field, DateTimeOffset Start, DateTimeOffset End);
    public static Range Read(JsonElement body)
    {
        var serializer = new DataContractJsonSerializer(typeof(Restriction), [typeof(And), typeof(GreaterOrEqual), typeof(Less), typeof(Field), typeof(Constant)]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body.GetProperty("Restriction").GetRawText()));
        if (serializer.ReadObject(stream) is not Restriction value)
            throw new SerializationException("Restriction debe contener Item, no sustituirse por la expresión.");
        if (value.Item is not And { Items.Length: 2 } conjunction || conjunction.Items[0] is not GreaterOrEqual lower || conjunction.Items[1] is not Less upper ||
            lower.Item.FieldURI != upper.Item.FieldURI || lower.Item.FieldURI is not ("item:DateTimeSent" or "item:DateTimeReceived") ||
            lower.FieldURIOrConstant.Item is not Constant from || upper.FieldURIOrConstant.Item is not Constant until)
            throw new SerializationException("El filtro no representa un intervalo de fecha completo.");
        var start = DateTimeOffset.Parse(from.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var end = DateTimeOffset.Parse(until.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (start >= end) throw new SerializationException("Intervalo de fecha inválido.");
        return new(lower.Item.FieldURI, start, end);
    }
    [DataContract(Namespace = Ns)] private sealed class Restriction
    { [DataMember(IsRequired = true)] public Expression Item { get; set; } = null!; }
    [DataContract(Namespace = Ns)] private abstract class Expression { }
    [DataContract(Name = "And", Namespace = Ns)] private sealed class And : Expression
    { [DataMember(IsRequired = true)] public Expression[] Items { get; set; } = []; }
    [DataContract(Namespace = Ns)] private abstract class Comparison : Expression
    {
        [DataMember(IsRequired = true)] public Field Item { get; set; } = null!;
        [DataMember(IsRequired = true)] public Operand FieldURIOrConstant { get; set; } = null!;
    }
    [DataContract(Name = "IsGreaterThanOrEqualTo", Namespace = Ns)] private sealed class GreaterOrEqual : Comparison { }
    [DataContract(Name = "IsLessThan", Namespace = Ns)] private sealed class Less : Comparison { }
    [DataContract(Name = "PropertyUri", Namespace = Ns)] private sealed class Field
    { [DataMember(IsRequired = true)] public string FieldURI { get; set; } = ""; }
    [DataContract(Namespace = Ns)] private sealed class Operand
    { [DataMember(IsRequired = true)] public object Item { get; set; } = null!; }
    [DataContract(Name = "Constant", Namespace = Ns)] private sealed class Constant
    { [DataMember(IsRequired = true)] public string Value { get; set; } = ""; }
}
