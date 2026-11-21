namespace CorreoDiario.Core;

public record MailFolder(string Id, string Name, bool Sent = false);
public record AttachmentInfo(string Id, string Name, long? Size, string MimeType, bool Inline, string ContentId);
public record MailInfo(string Id, string Revision, string FolderId, string Subject, string From, string To, string Cc,
    DateTimeOffset Timestamp, bool Sent, string Html, IReadOnlyList<AttachmentInfo> Attachments, DateTimeOffset? SentAt = null, string Bcc = "");
public interface IOutlookTransport
{
    Task<System.Text.Json.JsonElement> ActionAsync(string action, object body, CancellationToken ct);
}
public interface IMailSource
{
    Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct);
    Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, DateOnly date, TimeZoneInfo zone, CancellationToken ct);
    Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct);
    Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo attachment, CancellationToken ct);
    // This method must MOVE to Deleted Items. Permanent deletion is not supported.
    Task MoveToDeletedAsync(MailInfo item, CancellationToken ct);
}
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct);
}
public record ExportOptions(string Destination, bool MoveToDeleted = false);
public record ExportProgress(string Text, int Completed, int Total);
public record SavedFile(string Name, long Length, string Sha256);
public record SavedMessage(string Id, string Revision, string Subject, IReadOnlyList<SavedFile> Files);
public sealed class ExportReceipt
{
    public string Version { get; set; } = "0.2.0-beta";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public string Status { get; set; } = "EXPORTANDO";
    public bool DeletionRequested { get; set; }
    public List<SavedMessage> Messages { get; set; } = [];
    public List<string> MovedToDeleted { get; set; } = [];
    public string? MoveInProgress { get; set; }
    public string? Error { get; set; }
}
public record ExportResult(string Directory, int Messages, int Attachments, int Moved);
