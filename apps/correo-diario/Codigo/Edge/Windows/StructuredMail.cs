using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CorreoDiario.EdgeApp;

// COM's IStorage is used by some mail sources to deliver a compound MSG directly.
// Copy the storage into a memory-backed compound file; never materialize it on disk.
[ComImport, Guid("0000000B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INativeStorage
{
    void CreateStream([MarshalAs(UnmanagedType.LPWStr)] string name, uint mode, uint r1, uint r2, out IStream stream);
    void OpenStream([MarshalAs(UnmanagedType.LPWStr)] string name, nint reserved, uint mode, uint r2, out IStream stream);
    void CreateStorage([MarshalAs(UnmanagedType.LPWStr)] string name, uint mode, uint r1, uint r2, out INativeStorage storage);
    void OpenStorage([MarshalAs(UnmanagedType.LPWStr)] string name, nint priority, uint mode, nint exclude, uint reserved, out INativeStorage storage);
    void CopyTo(uint excludeCount, nint excludedIds, nint excludedNames, INativeStorage destination);
    void MoveElementTo([MarshalAs(UnmanagedType.LPWStr)] string name, INativeStorage destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, uint flags);
    void Commit(uint flags);
}

internal static class StructuredMail
{
    internal static byte[] Read(nint sourcePointer, long limit)
    {
        var source = (INativeStorage)Marshal.GetObjectForIUnknown(sourcePointer);
        try
        {
            Marshal.ThrowExceptionForHR(CreateILockBytesOnHGlobal(0, true, out var backing));
            try
            {
                Marshal.ThrowExceptionForHR(StgCreateDocfileOnILockBytes(backing, 0x1012, 0, out var destination));
                try
                {
                    source.CopyTo(0, 0, 0, destination);
                    destination.Commit(0);
                    Marshal.ThrowExceptionForHR(GetHGlobalFromILockBytes(backing, out var handle));
                    // A reserialized compound file can differ in storage layout and size.
                    return MailTransfer.CopyGlobal(handle, null, limit);
                }
                finally { Marshal.ReleaseComObject(destination); }
            }
            finally { Marshal.Release(backing); }
        }
        finally { if (Marshal.IsComObject(source)) Marshal.ReleaseComObject(source); }
    }

    [DllImport("ole32.dll")] internal static extern int CreateILockBytesOnHGlobal(nint handle, bool releaseHandle, out nint backing);
    [DllImport("ole32.dll")] static extern int GetHGlobalFromILockBytes(nint backing, out nint handle);
    [DllImport("ole32.dll")] static extern int StgCreateDocfileOnILockBytes(nint backing, uint mode, uint reserved, out INativeStorage storage);
}
