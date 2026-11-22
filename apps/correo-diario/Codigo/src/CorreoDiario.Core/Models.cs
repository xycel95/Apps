namespace CorreoDiario.Core;

public record MailFolder(string Id, string Name, bool Sent = false);
public sealed record MailDateRange
{
    public DateOnly From { get; }
    public DateOnly Through { get; }
    public MailDateRange(DateOnly from, DateOnly through)
    {
        if (through < from) throw new ArgumentException("La fecha «Hasta» debe ser igual o posterior a «Desde».");
        if (through == DateOnly.MaxValue) throw new ArgumentException("La fecha «Hasta» supera el límite admitido.");
        From = from; Through = through;
    }
    public (DateTime Start, DateTime EndExclusive) UtcBounds(TimeZoneInfo zone) => (
        TimeZoneInfo.ConvertTimeToUtc(From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone),
        TimeZoneInfo.ConvertTimeToUtc(Through.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone));
    public bool Contains(DateTimeOffset timestamp, TimeZoneInfo zone)
    {
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, zone).DateTime);
        return date >= From && date <= Through;
    }
}
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum AttachmentSizeMeaning { ContentBytes, OutlookStoreBytes }
public record AttachmentInfo(string Id, string Name, long? Size, string MimeType, bool Inline, string ContentId,
    AttachmentSizeMeaning SizeMeaning = AttachmentSizeMeaning.ContentBytes);
public record MailInfo(string Id, string Revision, string FolderId, string Subject, string From, string To, string Cc,
    DateTimeOffset Timestamp, bool Sent, string Html, IReadOnlyList<AttachmentInfo> Attachments, DateTimeOffset? SentAt = null, string Bcc = "");
public interface IOutlookTransport
{
    Task<System.Text.Json.JsonElement> ActionAsync(string action, object body, CancellationToken ct);
}
public interface IMailSource
{
    Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct);
    Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, MailDateRange dates, TimeZoneInfo zone, CancellationToken ct);
    Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct);
    Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo attachment, CancellationToken ct);
    // This method must MOVE to Deleted Items. Permanent deletion is not supported.
    Task MoveToDeletedAsync(MailInfo item, CancellationToken ct);
}
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct);
}
public sealed record AttachmentDownload(byte[] Bytes, string ContentId = "", string ContentType = "");
public interface IAttachmentDownloadSource : IMailSource
{
    Task<AttachmentDownload> DownloadAttachmentAsync(MailInfo mail, AttachmentInfo attachment, CancellationToken ct);
}
public sealed record InlineImageIssue(int ReferenceNumber, string Reason, int ReferenceLength, int NormalizedLength, int Candidates);
public sealed record InlineImageFailure(int References, int Attachments, int AttachmentsWithContentId, IReadOnlyList<InlineImageIssue> Issues);
public sealed record PendingMessage(int Number, string Id, string Subject, string Error, InlineImageFailure ImageFailure);
public interface IVerifiedPdfRenderer : IPdfRenderer
{
    Task<PdfRenderOutput> RenderVerifiedAsync(MailInfo item, IReadOnlyDictionary<string, byte[]> attachments, CancellationToken ct);
}
// Counts and isolated Unicode code points, never body fragments/session data.
public sealed record PdfTextCheck(int Pages, int ExpectedWords, int ExtractedWords, int MissingWords,
    int ExpectedCharacters, int ExtractedCharacters, int FirstDifference, bool CompleteWords, bool ExactSequence)
{
    public string? ExpectedCodePoint { get; init; }
    public string? ExtractedCodePoint { get; init; }
    public string? ExpectedUnicodeCategory { get; init; }
    public string? ExtractedUnicodeCategory { get; init; }
    public string DifferenceKind { get; init; } = "identical";
    public int ExpectedPresentationSelectorsIgnored { get; init; }
    public int ExtractedPresentationSelectorsIgnored { get; init; }
}
public sealed record ExportErrorContext(int SelectedMessages, int SavedMessages, int SavedAttachments,
    int ConfirmedMoves, int? FailedMessageNumber, string Phase, bool DeletionRequested,
    int PendingMessages = 0, IReadOnlyList<int>? PendingMessageNumbers = null);
public sealed record PdfRenderVerification(string Mode, PdfTextCheck Text, PdfTextCheck? FormattedFailure = null);
public sealed record PdfRenderOutput(byte[] Bytes, PdfRenderVerification Verification);
public sealed record PdfRenderFailure(PdfTextCheck? Formatted, PdfTextCheck? Simplified);
public record ExportOptions(string Destination, bool MoveToDeleted = false);
public record ExportProgress(string Text, int Completed, int Total);
public record SavedFile(string Name, long Length, string Sha256);
public sealed record SavedAttachment(string Id, string FileName, long? ReportedSize, AttachmentSizeMeaning ReportedSizeMeaning, long ContentLength, string Sha256)
{
    public bool SourceReadbackVerified { get; set; }
}
public record SavedMessage(string Id, string Revision, string Subject, IReadOnlyList<SavedFile> Files)
{
    public List<SavedAttachment> Attachments { get; init; } = [];
    public PdfRenderVerification? Pdf { get; init; }
}
public sealed class ExportReceipt
{
    public string Version { get; set; } = "0.4.7-beta";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public string Status { get; set; } = "EXPORTANDO";
    public bool DeletionRequested { get; set; }
    public List<SavedMessage> Messages { get; set; } = [];
    public List<string> MovedToDeleted { get; set; } = [];
    public string? MoveInProgress { get; set; }
    public string? Error { get; set; }
    public int? FailedMessageNumber { get; set; }
    public PdfRenderFailure? PdfFailure { get; set; }
    public InlineImageFailure? ImageFailure { get; set; }
    public List<PendingMessage> PendingMessages { get; set; } = [];
    public bool SavedCopiesVerified { get; set; }
}
public record ExportResult(string Directory, int Messages, int Attachments, int Moved, string ReceiptPath = "", int SimplifiedPdfs = 0);
