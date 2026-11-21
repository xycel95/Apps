using System.Globalization;
using System.Runtime.InteropServices;
using CorreoDiario.Core;

namespace CorreoDiario.Windows;

// Uses the user's existing classic Outlook profile. No add-in is installed.
internal sealed class OutlookClassicSource : IMailSource, IDisposable
{
    private dynamic? application;
    private dynamic? outlook;
    private readonly Dictionary<string, (MailFolder Folder, string Store)> folderMap = new(StringComparer.Ordinal);
    public string? Notice { get; private set; }

    public OutlookClassicSource()
    {
        var type = Type.GetTypeFromProgID("Outlook.Application", false)
            ?? throw new InvalidOperationException("No se encuentra Outlook clásico instalado. Si utilizas el nuevo Outlook, elige una de las conexiones web.");
        try
        {
            application = Activator.CreateInstance(type) ?? throw new InvalidOperationException("No se ha podido abrir Outlook clásico.");
            outlook = application.GetNamespace("MAPI");
            if ((int)outlook.Stores.Count == 0) throw new InvalidOperationException("Configura primero tu cuenta en Outlook clásico y vuelve a conectar.");
        }
        catch (COMException)
        {
            Dispose();
            throw new InvalidOperationException("No se ha podido acceder al perfil de Outlook clásico. Abre Outlook con tu cuenta configurada y vuelve a conectar.");
        }
    }

    public async Task<IReadOnlyList<MailFolder>> FoldersAsync(CancellationToken ct)
    {
        folderMap.Clear();
        var result = new List<MailFolder>();
        dynamic stores = outlook!.Stores;
        try
        {
            var count = (int)stores.Count;
            for (var i = 1; i <= count; i++)
            {
                ct.ThrowIfCancellationRequested();
                dynamic store = stores.Item(i);
                try
                {
                    string storeId = store.StoreID;
                    string name = store.DisplayName;
                    foreach (var type in new[] { 6, 5 }) // Inbox, Sent Items.
                    {
                        dynamic folder = store.GetDefaultFolder(type);
                        try { Add(folder, storeId, name, type == 5); } finally { Release(folder); }
                    }
                    dynamic root = store.GetRootFolder();
                    try { Walk(root, storeId, name, 0); } finally { Release(root); }
                }
                finally { Release(store); }
                await Task.Yield();
            }
        }
        finally { Release(stores); }
        if (result.Count == 0) throw new InvalidOperationException("Outlook no ha devuelto carpetas de correo accesibles.");
        return result;

        void Add(dynamic folder, string storeId, string name, bool sent)
        {
            string id = folder.EntryID;
            if (folderMap.ContainsKey(id)) return;
            string path = folder.FolderPath;
            var item = new MailFolder(id, path.Length == 0 ? name : path.TrimStart('\\'), sent);
            folderMap[id] = (item, storeId);
            result.Add(item);
        }
        void Walk(dynamic parent, string storeId, string name, int depth)
        {
            ct.ThrowIfCancellationRequested();
            if (depth > 20) { Notice = "Hay carpetas profundas que no se han podido mostrar."; return; }
            dynamic children = parent.Folders;
            try
            {
                for (var j = 1; j <= (int)children.Count; j++)
                {
                    dynamic child = children.Item(j);
                    try
                    {
                        if ((int)child.DefaultItemType == 0) Add(child, storeId, name, false);
                        Walk(child, storeId, name, depth + 1);
                    }
                    catch (COMException) { Notice = "Algunas carpetas del perfil no están accesibles. Solo se muestran las que Outlook permite leer."; }
                    finally { Release(child); }
                }
            }
            finally { Release(children); }
        }
    }

    public async Task<IReadOnlyList<MailInfo>> ListAsync(IReadOnlyList<MailFolder> folders, MailDateRange dates, TimeZoneInfo zone, CancellationToken ct)
    {
        var (start, end) = dates.UtcBounds(zone);
        var result = new Dictionary<string, MailInfo>(StringComparer.Ordinal);
        foreach (var selected in folders)
        {
            ct.ThrowIfCancellationRequested();
            var folderInfo = folderMap[selected.Id];
            dynamic folder = outlook!.GetFolderFromID(selected.Id, folderInfo.Store);
            dynamic? all = null, filtered = null;
            try
            {
                var property = selected.Sent ? "http://schemas.microsoft.com/mapi/proptag/0x00390040" : "http://schemas.microsoft.com/mapi/proptag/0x0E060040";
                var filter = $"@SQL=\"{property}\" >= '{start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}' AND \"{property}\" < '{end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}'";
                all = folder.Items;
                filtered = all.Restrict(filter);
                var count = (int)filtered.Count;
                for (var i = 1; i <= count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    dynamic mail = filtered.Item(i);
                    try
                    {
                        if ((int)mail.Class != 43 || !(bool)mail.Sent) continue;
                        var item = Snapshot(mail, selected, false);
                        if (dates.Contains(item.Timestamp, zone)) result[item.Id] = item;
                    }
                    finally { Release(mail); }
                    if (i % 20 == 0) await Task.Yield();
                }
                if ((int)filtered.Count != count) throw new InvalidDataException("La carpeta ha cambiado durante la búsqueda. Vuelve a buscar antes de exportar.");
            }
            finally { Release(filtered); Release(all); Release(folder); }
        }
        return result.Values.OrderBy(m => m.Timestamp).ToList();
    }

    public Task<MailInfo> ReadAsync(MailInfo item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var location = folderMap[item.FolderId];
        dynamic mail = outlook!.GetItemFromID(item.Id, location.Store);
        try
        {
            if ((int)mail.Class != 43) throw new InvalidDataException("El elemento no es un correo.");
            string messageClass = mail.MessageClass;
            if (messageClass.Contains("SMIME", StringComparison.OrdinalIgnoreCase) || messageClass.Contains("rpmsg", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Este correo está cifrado o protegido y no se borra automáticamente.");
            return Task.FromResult(Snapshot(mail, location.Folder, true));
        }
        finally { Release(mail); }
    }

    public async Task<byte[]> AttachmentAsync(MailInfo item, AttachmentInfo attachment, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var location = folderMap[item.FolderId];
        dynamic mail = outlook!.GetItemFromID(item.Id, location.Store);
        dynamic? attachments = null, file = null;
        var temp = Path.Combine(Path.GetTempPath(), "CorreoDiario-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            attachments = mail.Attachments;
            file = attachments.Item(int.Parse(attachment.Id, CultureInfo.InvariantCulture));
            file.SaveAsFile(temp);
            return await File.ReadAllBytesAsync(temp, ct);
        }
        finally { Release(file); Release(attachments); Release(mail); try { File.Delete(temp); } catch (IOException) { } }
    }

    public Task MoveToDeletedAsync(MailInfo item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var location = folderMap[item.FolderId];
        dynamic mail = outlook!.GetItemFromID(item.Id, location.Store);
        dynamic? store = null, deleted = null, moved = null, parent = null;
        try
        {
            // Check the original revision once more immediately before moving it.
            if (Revision(mail) != item.Revision) throw new InvalidDataException("El correo ha cambiado. Se conserva en su carpeta.");
            store = outlook.GetStoreFromID(location.Store);
            deleted = store.GetDefaultFolder(3);
            moved = mail.Move(deleted);
            parent = moved.Parent;
            if ((string)parent.EntryID != (string)deleted.EntryID)
                throw new InvalidOperationException("Outlook no ha confirmado el movimiento a Eliminados. Comprueba el buzón antes de repetirlo.");
            return Task.CompletedTask;
        }
        finally { Release(parent); Release(moved); Release(deleted); Release(store); Release(mail); }
    }

    private static MailInfo Snapshot(dynamic mail, MailFolder folder, bool full)
    {
        string from = mail.SenderEmailAddress ?? "";
        if ((string)mail.SenderEmailType == "EX")
        {
            dynamic? sender = null;
            try { sender = mail.Sender; from = Smtp(sender, from); } finally { Release(sender); }
        }
        var to = new List<string>(); var cc = new List<string>(); var bcc = new List<string>();
        dynamic recipients = mail.Recipients;
        try
        {
            for (var i = 1; i <= (int)recipients.Count; i++)
            {
                dynamic r = recipients.Item(i); dynamic? entry = null;
                try
                {
                    entry = r.AddressEntry;
                    string address = Smtp(entry, (string)r.Address ?? (string)r.Name ?? "");
                    if ((int)r.Type == 1) to.Add(address); else if ((int)r.Type == 2) cc.Add(address); else if ((int)r.Type == 3) bcc.Add(address);
                }
                finally { Release(entry); Release(r); }
            }
        }
        finally { Release(recipients); }
        var attachments = new List<AttachmentInfo>();
        if (full)
        {
            dynamic collection = mail.Attachments;
            try
            {
                for (var i = 1; i <= (int)collection.Count; i++)
                {
                    dynamic a = collection.Item(i);
                    try
                    {
                        var type = (int)a.Type;
                        if (type is not (1 or 5)) throw new InvalidDataException("El correo contiene un adjunto enlazado o un objeto que esta versión no puede verificar.");
                        string name = a.FileName;
                        if (type == 5 && !name.EndsWith(".msg", StringComparison.OrdinalIgnoreCase)) name += ".msg";
                        string cid = Property(a, "http://schemas.microsoft.com/mapi/proptag/0x3712001F", "http://schemas.microsoft.com/mapi/proptag/0x3712001E");
                        string mime = Property(a, "http://schemas.microsoft.com/mapi/proptag/0x370E001F", "http://schemas.microsoft.com/mapi/proptag/0x370E001E");
                        attachments.Add(new(i.ToString(CultureInfo.InvariantCulture), name, type == 5 ? null : Convert.ToInt64(a.Size), mime, cid.Length > 0, cid));
                    }
                    finally { Release(a); }
                }
            }
            finally { Release(collection); }
        }
        DateTimeOffset sentAt = AsLocal((DateTime)mail.SentOn);
        var timestamp = folder.Sent ? sentAt : AsLocal((DateTime)mail.ReceivedTime);
        string html = full ? (string)mail.HTMLBody : "";
        if (full && string.IsNullOrEmpty(html)) html = "<pre>" + System.Net.WebUtility.HtmlEncode((string)mail.Body ?? "") + "</pre>";
        dynamic parent = mail.Parent;
        try
        {
            return new((string)mail.EntryID, Revision(mail), (string)parent.EntryID, (string)mail.Subject ?? "Sin asunto", from,
                string.Join("; ", to), string.Join("; ", cc), timestamp, folder.Sent, html, attachments, sentAt, string.Join("; ", bcc));
        }
        finally { Release(parent); }
    }
    private static DateTimeOffset AsLocal(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Local));
    private static string Revision(dynamic mail) => AsLocal((DateTime)mail.LastModificationTime).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static string Smtp(dynamic? entry, string fallback)
    {
        if (entry is null) return fallback;
        var property = Property(entry, "http://schemas.microsoft.com/mapi/proptag/0x39FE001F", "http://schemas.microsoft.com/mapi/proptag/0x39FE001E");
        return property.Length > 0 ? property : fallback;
    }
    private static string Property(dynamic target, params string[] names)
    {
        dynamic accessor = target.PropertyAccessor;
        try
        {
            foreach (var name in names) try { return Convert.ToString(accessor.GetProperty(name), CultureInfo.InvariantCulture) ?? ""; } catch (COMException) { }
            return "";
        }
        finally { Release(accessor); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    public void Dispose() { Release(outlook); outlook = null; Release(application); application = null; }
}
