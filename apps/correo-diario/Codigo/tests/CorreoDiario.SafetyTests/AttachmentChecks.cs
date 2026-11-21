using System.Runtime.Serialization;
using System.Text.Json;
using CorreoDiario.Core;

internal static class AttachmentChecks
{
    public static IEnumerable<(string Name, Func<Task> Test)> All()
    {
        yield return ("Adjunto: rechaza el formato antiguo sin BaseShape obligatorio", () =>
        {
            using var old = JsonDocument.Parse("""{"__type":"GetAttachmentRequest:#Exchange","AttachmentShape":{"IncludeMimeContent":true},"AttachmentIds":[{"__type":"AttachmentId:#Exchange","Id":"fixture-id"}]}""");
            try { OwaAttachmentContract.Read(old.RootElement); }
            catch (SerializationException) { return Task.CompletedTask; }
            throw new Exception("El contrato debe rechazar la petición antigua");
        });
        yield return ("Adjunto: acepta la petición completa, conserva el identificador y devuelve sus bytes", async () =>
        {
            const string id = "AA/+opaque==?#";
            var transport = new ContractTransport(id);
            var mail = new MailInfo("fixture-mail", "rev", "inbox", "Prueba", "a@example.invalid", "b@example.invalid", "", DateTimeOffset.UtcNow, false, "", []);
            var bytes = await new OwaSource(transport).AttachmentAsync(mail, new(id, "prueba.bin", 4, "application/octet-stream", false, ""), default);
            if (!bytes.SequenceEqual(new byte[] { 0, 1, 2, 255 }) || transport.Calls != 1) throw new Exception("No coincide el adjunto original");
        });
    }
    private sealed class ContractTransport(string expectedId) : IOutlookTransport
    {
        public int Calls;
        public Task<JsonElement> ActionAsync(string action, object body, CancellationToken ct)
        {
            Calls++;
            if (action != "GetAttachment" || OwaAttachmentContract.Read(JsonSerializer.SerializeToElement(body)) != expectedId)
                throw new Exception("Se ha alterado el identificador solicitado");
            return Task.FromResult(JsonSerializer.SerializeToElement(new { Body = new { Attachments = new[] { new { AttachmentId = new { Id = expectedId }, Content = "AAEC/w==" } } } }));
        }
    }
}
