using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;

// Independent reader: AttachmentShape inherits the required BaseShape member
// from ResponseShape. A shape containing only IncludeMimeContent is invalid.
internal static class OwaAttachmentContract
{
    private const string Ns = "http://schemas.datacontract.org/2004/07/Exchange";
    public static string Read(JsonElement body)
    {
        using var data = new MemoryStream(Encoding.UTF8.GetBytes(body.GetRawText()));
        var serializer = new DataContractJsonSerializer(typeof(Request));
        if (serializer.ReadObject(data) is not Request value || value.AttachmentShape is not { BaseShape: "AllProperties", IncludeMimeContent: true } ||
            value.AttachmentIds is not { Length: 1 } || value.AttachmentIds[0] is null || string.IsNullOrEmpty(value.AttachmentIds[0].Id))
            throw new SerializationException("La petición de adjunto no contiene una forma completa y un identificador.");
        return value.AttachmentIds[0].Id;
    }
    [DataContract(Name = "GetAttachmentRequest", Namespace = Ns)] private sealed class Request
    {
        [DataMember(IsRequired = true)] public Shape AttachmentShape { get; set; } = null!;
        [DataMember(IsRequired = true)] public AttachmentKey[] AttachmentIds { get; set; } = [];
    }
    [DataContract(Name = "AttachmentResponseShapeType", Namespace = Ns)] private sealed class Shape
    {
        [DataMember(IsRequired = true)] public string BaseShape { get; set; } = "";
        [DataMember] public bool IncludeMimeContent { get; set; }
    }
    [DataContract(Name = "AttachmentId", Namespace = Ns)] private sealed class AttachmentKey
    { [DataMember(IsRequired = true)] public string Id { get; set; } = ""; }
}
