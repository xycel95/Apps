using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using OleData = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace CorreoDiario.EdgeApp;

internal sealed class TransferException(string reason, string message) : Exception(message)
{
    public string Reason { get; } = reason;
}

internal sealed record MailDelivery(List<MailInput> Inputs, string Method);

internal static class MailTransfer
{
    const TYMED Memory = TYMED.TYMED_HGLOBAL, Stream = TYMED.TYMED_ISTREAM;
    static short Format(string name) => unchecked((short)DataFormats.GetFormat(name).Id);
    static FORMATETC Request(short format, TYMED medium, int index = -1) =>
        new() { cfFormat = format, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = medium };
    static bool Offers(OleData data, short id, TYMED medium)
    {
        var request = Request(id, medium);
        return data.QueryGetData(ref request) == 0;
    }

    static TransferException Incomplete() => new("entrega incompleta",
        "Outlook anunció uno o más correos, pero no entregó todo su contenido. El lote no se ha añadido.");
    static TransferException Unsupported() => new("elemento no compatible",
        "La selección contiene un elemento distinto de un correo EML o MSG. Selecciona solo los mensajes que quieres exportar.");
    static TransferException TooLarge() => new("memoria insuficiente",
        "Este lote supera la memoria disponible. Entrega menos correos a la vez.");
    static bool IsMail(string name) =>
        Path.GetExtension(name).ToLowerInvariant() is ".eml" or ".msg";

    public static MailDelivery Files(IEnumerable<string> paths)
    {
        var inputs = new List<MailInput>();
        foreach (var path in paths)
        {
            if (!Path.IsPathFullyQualified(path) || !IsMail(path)) throw Unsupported();
            inputs.Add(MailInput.FromFile(path));
        }
        if (inputs.Count == 0) throw Incomplete();
        return new(inputs, "Archivos elegidos");
    }

    public static MailDelivery Read(OleData data)
    {
        foreach (var wide in new[] { true, false })
        {
            var id = Format(wide ? "FileGroupDescriptorW" : "FileGroupDescriptor");
            if (Offers(data, id, Memory)) return VirtualFiles(data, id, wide);
        }
        if (Offers(data, Format("message/rfc822"), Stream | Memory))
        {
            var bytes = Bytes(data, Format("message/rfc822"), -1, null, Budget());
            return new([MailInput.FromMemory(bytes, false)], "Mensaje MIME en memoria");
        }
        if (Offers(data, 15, Memory))
        {
            var request = Request(15, Memory);
            data.GetData(ref request, out var medium);
            try
            {
                if (medium.tymed != Memory) throw Incomplete();
                var handle = medium.unionmember;
                var count = DragQueryFile(handle, uint.MaxValue, null, 0);
                var files = new List<string>();
                for (uint i = 0; i < count; i++)
                {
                    uint length = DragQueryFile(handle, i, null, 0);
                    if (length == 0 || length > 32767) throw Incomplete();
                    var name = new StringBuilder((int)length + 1);
                    if (DragQueryFile(handle, i, name, length + 1) != length) throw Incomplete();
                    files.Add(name.ToString());
                }
                return Files(files);
            }
            finally { ReleaseStgMedium(ref medium); }
        }
        bool labels = Offers(data, 13, Memory) || Offers(data, 1, Memory)
            || Offers(data, Format("UniformResourceLocatorW"), Memory) || Offers(data, Format("HTML Format"), Memory);
        throw labels
            ? new TransferException("solo texto o enlace", "Outlook entregó texto o un enlace. Faltan el cuerpo y los adjuntos del correo. No se ha añadido ningún mensaje.")
            : new TransferException("sin formato de correo", "La aplicación de origen no entregó mensajes completos. Puedes añadir archivos EML o MSG que ya tengas.");
    }

    // FD_FILESIZE applies only when its bit is present. Use the descriptor's byte count,
    // not the HGLOBAL allocation size (which may include alignment/padding).
    static MailDelivery VirtualFiles(OleData data, short id, bool wide)
    {
        var request = Request(id, Memory);
        data.GetData(ref request, out var medium);
        var entries = new List<(bool IsMsg, long? Size)>();
        try
        {
            if (medium.tymed != Memory) throw Incomplete();
            int stride = wide ? 592 : 332;
            var ptr = GlobalLock(medium.unionmember);
            if (ptr == 0) throw Incomplete();
            try
            {
                var capacity = GlobalSize(medium.unionmember).ToUInt64();
                if (capacity < 4) throw Incomplete();
                int count = Marshal.ReadInt32(ptr);
                if (count <= 0 || 4L + (long)count * stride > (long)Math.Min(capacity, int.MaxValue))
                    throw Incomplete();
                for (int i = 0; i < count; i++)
                {
                    var item = ptr + 4 + i * stride;
                    string name = (wide ? Marshal.PtrToStringUni(item + 72, 260)
                        : Marshal.PtrToStringAnsi(item + 72, 260))!.Split('\0')[0];
                    if (!IsMail(name)) throw Unsupported();
                    long? length = null;
                    if ((Marshal.ReadInt32(item) & 0x40) != 0)
                    {
                        uint high = (uint)Marshal.ReadInt32(item, 64);
                        if (high != 0) throw TooLarge();
                        length = (uint)Marshal.ReadInt32(item, 68);
                    }
                    entries.Add((name.EndsWith(".msg", StringComparison.OrdinalIgnoreCase), length));
                }
            }
            finally { GlobalUnlock(medium.unionmember); }
        }
        finally { ReleaseStgMedium(ref medium); }

        var inputs = new List<MailInput>(entries.Count);
        var budget = Budget();
        var contents = Format("FileContents");
        for (int i = 0; i < entries.Count; i++)
        {
            var item = entries[i];
            var bytes = Bytes(data, contents, i, item.Size, budget, item.IsMsg);
            budget -= bytes.Length;
            inputs.Add(MailInput.FromMemory(bytes, item.IsMsg));
        }
        return new(inputs, "Archivos virtuales en memoria");
    }

    static long Budget() => Math.Max(0, (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - GC.GetTotalMemory(false)) / 2);

    static byte[] Bytes(OleData data, short format, int index, long? expected, long available, bool compound = false)
    {
        long limit = Math.Min(Array.MaxLength, available);
        if (expected > limit) throw TooLarge();
        TYMED[] kinds = compound ? [Stream, Memory, TYMED.TYMED_ISTORAGE] : [Stream, Memory];
        foreach (var kind in kinds)
        {
            var request = Request(format, kind, index);
            STGMEDIUM medium;
            try { data.GetData(ref request, out medium); }
            catch (COMException e) when (e.HResult is unchecked((int)0x80040064) or unchecked((int)0x80040069) or unchecked((int)0x80040068) or unchecked((int)0x80004001)) { continue; }
            try
            {
                if (medium.tymed == Memory) return CopyGlobal(medium.unionmember, expected, limit);
                if (compound && medium.tymed == TYMED.TYMED_ISTORAGE)
                    return StructuredMail.Read(medium.unionmember, limit);
                if (medium.tymed == Stream)
                {
                    var source = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                    var countPtr = Marshal.AllocHGlobal(sizeof(long));
                    try
                    {
                        // OLE providers may return the transfer positioned at its end. Some
                        // virtual-file providers instead expose a fresh, forward-only stream.
                        long? length = expected;
                        try
                        {
                            source.Seek(0, 1, countPtr);
                            long position = Marshal.ReadInt64(countPtr);
                            source.Seek(0, 0, IntPtr.Zero);
                            if (length == null && position > 0) length = position;
                        }
                        catch (COMException e) when (e.HResult is unchecked((int)0x80030001)
                            or unchecked((int)0x80004001) or unchecked((int)0x80070032)) { }
                        if (length > limit) throw TooLarge();
                        using var sink = new MemoryStream();
                        var buffer = new byte[65536];
                        while (true)
                        {
                            Marshal.WriteInt32(countPtr, 0);
                            int requested = length is long n ? (int)Math.Min(n - sink.Length, buffer.Length) : buffer.Length;
                            if (requested == 0) break;
                            source.Read(buffer, requested, countPtr);
                            int read = Marshal.ReadInt32(countPtr);
                            if (read == 0) break;
                            if (read < 0 || read > requested) throw Incomplete();
                            if (sink.Length + read > limit) throw TooLarge();
                            sink.Write(buffer, 0, read);
                        }
                        if (sink.Length == 0 || length.HasValue && sink.Length != length) throw Incomplete();
                        return sink.ToArray();
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(countPtr);
                        if (Marshal.IsComObject(source)) Marshal.ReleaseComObject(source);
                    }
                }
            }
            finally { ReleaseStgMedium(ref medium); }
        }
        throw Incomplete();
    }

    internal static byte[] CopyGlobal(IntPtr handle, long? expected, long limit)
    {
        long size = checked((long)GlobalSize(handle).ToUInt64());
        if (expected > size) throw Incomplete();
        var length = expected ?? size;
        if (length < 1) throw Incomplete();
        if (length > limit) throw TooLarge();
        var ptr = GlobalLock(handle);
        if (ptr == 0) throw Incomplete();
        try
        {
            var bytes = new byte[(int)length];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { GlobalUnlock(handle); }
    }

    [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr handle);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW", ExactSpelling = true)]
    static extern uint DragQueryFile(IntPtr handle, uint index, StringBuilder? buffer, uint size);
}
